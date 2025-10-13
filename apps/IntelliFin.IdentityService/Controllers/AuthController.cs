using IntelliFin.IdentityService.Models;
using IntelliFin.IdentityService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.Net;

namespace IntelliFin.IdentityService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IPasswordService _passwordService;
    private readonly ISessionService _sessionService;
    private readonly IAccountLockoutService _lockoutService;
    private readonly IUserService _userService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IJwtTokenService jwtTokenService,
        IPasswordService passwordService,
        ISessionService sessionService,
        IAccountLockoutService lockoutService,
        IUserService userService,
        ILogger<AuthController> logger)
    {
        _jwtTokenService = jwtTokenService;
        _passwordService = passwordService;
        _sessionService = sessionService;
        _lockoutService = lockoutService;
        _userService = userService;
        _logger = logger;
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(TokenResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.Locked)]
    public async Task<IActionResult> LoginAsync([FromBody] LoginRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

            // Check account lockout
            if (await _lockoutService.IsAccountLockedAsync(request.Username, cancellationToken))
            {
                var remainingTime = await _lockoutService.GetRemainingLockoutTimeAsync(request.Username, cancellationToken);
                
                _logger.LogWarning("Login attempt for locked account {Username} from IP {IpAddress}", 
                    request.Username, ipAddress);

                return Problem(
                    title: "Account Locked",
                    detail: $"Account is locked. Try again in {remainingTime?.Minutes ?? 0} minutes.",
                    statusCode: (int)HttpStatusCode.Locked);
            }

            // Validate user credentials using the real user service
            var isValidUser = await _userService.ValidateUserCredentialsAsync(request.Username, request.Password, cancellationToken);

            if (!isValidUser)
            {
                await _lockoutService.RecordFailedAttemptAsync(request.Username, ipAddress ?? string.Empty, cancellationToken);
                
                _logger.LogWarning("Invalid login attempt for user {Username} from IP {IpAddress}", 
                    request.Username, ipAddress);

                return Unauthorized(new ProblemDetails
                {
                    Title = "Invalid Credentials",
                    Detail = "Username or password is incorrect",
                    Status = (int)HttpStatusCode.Unauthorized
                });
            }

            // Reset failed attempts on successful login
            await _lockoutService.ResetFailedAttemptsAsync(request.Username, cancellationToken);

            // Get user information
            var user = await _userService.GetUserByUsernameOrEmailAsync(request.Username, cancellationToken);
            if (user == null)
            {
                _logger.LogError("User not found after successful validation: {Username}", request.Username);
                return Problem(
                    title: "Authentication Error",
                    detail: "User information could not be retrieved",
                    statusCode: (int)HttpStatusCode.InternalServerError);
            }

            // Update last login time
            await _userService.UpdateLastLoginAsync(user.Id, DateTime.UtcNow, cancellationToken);

            // Get user roles and permissions
            var userRoles = await _userService.GetUserRolesAsync(user.Id, cancellationToken);
            var userPermissions = await _userService.GetUserPermissionsAsync(user.Id, cancellationToken);

            // Create session
            var session = await _sessionService.CreateSessionAsync(
                userId: user.Id,
                username: user.Username,
                deviceId: request.DeviceId,
                ipAddress: ipAddress,
                userAgent: request.UserAgent,
                cancellationToken: cancellationToken);

            // Generate tokens
            var userClaims = new UserClaims
            {
                UserId = user.Id,
                Username = user.Username,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Roles = userRoles.ToArray(),
                Permissions = userPermissions.ToArray(),
                BranchId = user.BranchId,
                BranchName = user.BranchName,
                BranchRegion = user.BranchRegion,
                SessionId = session.SessionId,
                DeviceId = request.DeviceId,
                AuthenticatedAt = DateTime.UtcNow,
                IpAddress = ipAddress
            };

            var accessToken = await _jwtTokenService.GenerateAccessTokenAsync(userClaims, cancellationToken);
            var refreshTokenResult = await _jwtTokenService.GenerateRefreshTokenAsync(userClaims.UserId, request.DeviceId ?? string.Empty, cancellationToken: cancellationToken);

            var response = new TokenResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshTokenResult.Token,
                ExpiresIn = 3600, // 1 hour in seconds
                IssuedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                RefreshTokenFamilyId = refreshTokenResult.FamilyId,
                RefreshTokenExpiresAt = refreshTokenResult.ExpiresAt,
                User = new UserInfo
                {
                    Id = userClaims.UserId,
                    Username = userClaims.Username,
                    Email = userClaims.Email,
                    FirstName = userClaims.FirstName,
                    LastName = userClaims.LastName,
                    Roles = userClaims.Roles,
                    Permissions = userClaims.Permissions,
                    BranchId = userClaims.BranchId,
                    BranchName = userClaims.BranchName,
                    BranchRegion = userClaims.BranchRegion,
                    RequiresTwoFactor = user.TwoFactorEnabled,
                    IsActive = user.IsActive
                }
            };

            _logger.LogInformation("User {Username} logged in successfully from IP {IpAddress}", request.Username, ipAddress);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for user {Username}", request.Username);
            return Problem(
                title: "Login Error",
                detail: "An error occurred during login",
                statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    [HttpPost("refresh")]
    [ProducesResponseType(typeof(RefreshTokenResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.Unauthorized)]
    public async Task<IActionResult> RefreshTokenAsync([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            request.IpAddress ??= HttpContext.Connection.RemoteIpAddress?.ToString();
            request.UserAgent ??= HttpContext.Request.Headers.UserAgent.ToString();

            var rotationResult = await _jwtTokenService.RefreshTokensAsync(request, cancellationToken);

            // Get user information to generate new tokens
            var user = await _userService.GetUserByIdAsync(rotationResult.UserId, cancellationToken);
            if (user == null)
            {
                return Unauthorized(new ProblemDetails
                {
                    Title = "Invalid Token",
                    Detail = "User not found",
                    Status = (int)HttpStatusCode.Unauthorized
                });
            }

            // Get user roles and permissions
            var userRoles = await _userService.GetUserRolesAsync(user.Id, cancellationToken);
            var userPermissions = await _userService.GetUserPermissionsAsync(user.Id, cancellationToken);

            var deviceId = string.IsNullOrEmpty(request.DeviceId) ? rotationResult.DeviceId : request.DeviceId;
            request.DeviceId = deviceId;

            // Create new session
            var session = await _sessionService.CreateSessionAsync(
                userId: user.Id,
                username: user.Username,
                deviceId: deviceId,
                ipAddress: request.IpAddress,
                userAgent: request.UserAgent,
                cancellationToken: cancellationToken);

            // Generate new tokens
            var userClaims = new UserClaims
            {
                UserId = user.Id,
                Username = user.Username,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Roles = userRoles.ToArray(),
                Permissions = userPermissions.ToArray(),
                BranchId = user.BranchId,
                BranchName = user.BranchName,
                BranchRegion = user.BranchRegion,
                SessionId = session.SessionId,
                DeviceId = deviceId,
                AuthenticatedAt = DateTime.UtcNow,
                IpAddress = request.IpAddress
            };

            var newAccessToken = await _jwtTokenService.GenerateAccessTokenAsync(userClaims, cancellationToken);

            var response = new RefreshTokenResponse
            {
                AccessToken = newAccessToken,
                RefreshToken = rotationResult.RefreshToken,
                ExpiresIn = 3600, // 1 hour in seconds
                IssuedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                RefreshTokenFamilyId = rotationResult.FamilyId,
                RefreshTokenExpiresAt = rotationResult.RefreshTokenExpiresAt,
                TokensRotated = true
            };

            _logger.LogInformation("Tokens refreshed successfully for user {UserId}", user.Id);

            return Ok(response);
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogWarning(ex, "Refresh token rotation rejected: {Message}", ex.Message);
            return Unauthorized(new ProblemDetails
            {
                Title = "Invalid Refresh Token",
                Detail = ex.Message,
                Status = (int)HttpStatusCode.Unauthorized
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token refresh");
            return Problem(
                title: "Refresh Error",
                detail: "An error occurred during token refresh",
                statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    [HttpPost("revoke")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> RevokeRefreshTokenFamilyAsync([FromBody] RevokeTokenRequest request, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var result = await _jwtTokenService.RevokeRefreshTokenFamilyAsync(request.RefreshToken, cancellationToken);

            if (result == null)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Refresh token not found",
                    Detail = "No refresh token family matched the supplied token",
                    Status = (int)HttpStatusCode.NotFound
                });
            }

            _logger.LogInformation("Refresh token family {FamilyId} revoked with {Count} tokens", result.FamilyId, result.RevokedTokens.Count);

            return Ok(new
            {
                message = "Refresh token family revoked",
                familyId = result.FamilyId,
                revokedTokens = result.RevokedTokens.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking refresh token family");
            return Problem(
                title: "Revocation Error",
                detail: "An error occurred while revoking the refresh token family",
                statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.BadRequest)]
    public async Task<IActionResult> LogoutAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var sessionId = User.FindFirst("session_id")?.Value;
            var tokenId = User.FindFirst("jti")?.Value;

            if (!string.IsNullOrEmpty(sessionId))
            {
                await _sessionService.InvalidateSessionAsync(sessionId, cancellationToken);
            }

            if (!string.IsNullOrEmpty(tokenId))
            {
                await _jwtTokenService.RevokeTokenAsync(tokenId, cancellationToken);
            }

            _logger.LogInformation("User {Username} logged out successfully", User.Identity?.Name);

            return Ok(new { message = "Logged out successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout for user {Username}", User.Identity?.Name);
            return Problem(
                title: "Logout Error",
                detail: "An error occurred during logout",
                statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserInfo), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.Unauthorized)]
    public IActionResult GetCurrentUser()
    {
        try
        {
            var userInfo = new UserInfo
            {
                Id = User.FindFirst("sub")?.Value ?? string.Empty,
                Username = User.FindFirst("name")?.Value ?? string.Empty,
                Email = User.FindFirst("email")?.Value ?? string.Empty,
                FirstName = User.FindFirst("given_name")?.Value ?? string.Empty,
                LastName = User.FindFirst("family_name")?.Value ?? string.Empty,
                Roles = User.FindAll("role").Select(c => c.Value).ToArray(),
                BranchId = User.FindFirst("branch_id")?.Value,
                IsActive = true
            };

            return Ok(userInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current user info for {Username}", User.Identity?.Name);
            return Problem(
                title: "User Info Error",
                detail: "An error occurred while getting user information",
                statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    [HttpPost("validate-token")]
    [ProducesResponseType(typeof(object), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.BadRequest)]
    public async Task<IActionResult> ValidateTokenAsync([FromBody] string token, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return BadRequest(new { message = "Token is required" });
            }

            var isValid = await _jwtTokenService.ValidateTokenAsync(token, cancellationToken);
            var claims = isValid ? await _jwtTokenService.GetClaimsFromTokenAsync(token, cancellationToken) : null;

            return Ok(new { 
                isValid, 
                claims = claims != null ? new {
                    userId = claims.UserId,
                    username = claims.Username,
                    roles = claims.Roles,
                    sessionId = claims.SessionId
                } : null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating token");
            return Problem(
                title: "Token Validation Error",
                detail: "An error occurred while validating the token",
                statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }


}