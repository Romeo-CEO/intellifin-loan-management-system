using IntelliFin.ClientManagement.Domain.Enums;
using IntelliFin.ClientManagement.Infrastructure.Persistence;
using IntelliFin.ClientManagement.Models.Mobile;
using IntelliFin.ClientManagement.Models.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IntelliFin.ClientManagement.Controllers;

/// <summary>
/// Mobile-optimized API endpoints for tablet/mobile clients
/// Features pagination, lightweight DTOs, and compression
/// Story 1.17: Mobile Optimization
/// </summary>
[ApiController]
[Route("api/mobile")]
[Authorize]
public class MobileController : ControllerBase
{
    private readonly ClientManagementDbContext _context;
    private readonly ILogger<MobileController> _logger;

    public MobileController(
        ClientManagementDbContext context,
        ILogger<MobileController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Gets mobile dashboard summary for current officer
    /// </summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(MobileDashboardSummary), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard()
    {
        var userId = User.Identity?.Name ?? "unknown";
        var branchId = GetBranchIdFromClaims();

        _logger.LogInformation(
            "Mobile dashboard requested by {User}",
            userId);

        var today = DateTime.UtcNow.Date;

        // Get client counts
        var clientQuery = _context.Clients.Where(c => c.BranchId == branchId);
        var totalClients = await clientQuery.CountAsync();

        // KYC statuses
        var kycStatuses = await _context.KycStatuses
            .Include(k => k.Client)
            .Where(k => k.Client.BranchId == branchId)
            .ToListAsync();

        var pendingKyc = kycStatuses.Count(k =>
            k.CurrentState == KycState.Pending ||
            k.CurrentState == KycState.InProgress);

        var completedToday = kycStatuses.Count(k =>
            k.CurrentState == KycState.Completed &&
            k.KycCompletedAt.HasValue &&
            k.KycCompletedAt.Value.Date == today);

        var requiringAttention = kycStatuses.Count(k =>
            k.CurrentState == KycState.EDD_Required ||
            k.CurrentState == KycState.Rejected);

        // Pending documents
        var pendingDocuments = await _context.ClientDocuments
            .Include(d => d.Client)
            .Where(d => d.Client.BranchId == branchId &&
                       d.UploadStatus == UploadStatus.Uploaded)
            .CountAsync();

        // Recent clients
        var recentClients = await _context.Clients
            .Where(c => c.BranchId == branchId)
            .OrderByDescending(c => c.CreatedAt)
            .Take(5)
            .Select(c => new MobileClientSummary
            {
                Id = c.Id,
                FullName = $"{c.FirstName} {c.LastName}",
                NrcMasked = MaskNrc(c.Nrc),
                Phone = c.PrimaryPhone,
                BranchId = c.BranchId,
                CreatedAt = c.CreatedAt,
                // KYC and risk info loaded separately for performance
                KycStatus = "Pending",
                DocumentCompletionPercent = 0
            })
            .ToListAsync();

        var dashboard = new MobileDashboardSummary
        {
            TotalClients = totalClients,
            PendingKyc = pendingKyc,
            PendingDocuments = pendingDocuments,
            CompletedToday = completedToday,
            RequiringAttention = requiringAttention,
            RecentClients = recentClients
        };

        return Ok(dashboard);
    }

    /// <summary>
    /// Gets paginated list of clients (mobile-optimized)
    /// </summary>
    [HttpGet("clients")]
    [ProducesResponseType(typeof(PagedResult<MobileClientSummary>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetClients(
        [FromQuery] PaginationParams pagination,
        [FromQuery] string? search = null,
        [FromQuery] string? kycStatus = null)
    {
        var branchId = GetBranchIdFromClaims();

        _logger.LogInformation(
            "Mobile clients list requested: Page={Page}, Size={Size}, Search={Search}",
            pagination.PageNumber, pagination.PageSize, search);

        // Base query
        var query = _context.Clients
            .Where(c => c.BranchId == branchId);

        // Search filter
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(c =>
                c.FirstName.ToLower().Contains(searchLower) ||
                c.LastName.ToLower().Contains(searchLower) ||
                c.Nrc.Contains(search) ||
                c.PrimaryPhone.Contains(search));
        }

        // Total count
        var totalCount = await query.CountAsync();

        // Paginated results
        var clients = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .Select(c => new MobileClientSummary
            {
                Id = c.Id,
                FullName = $"{c.FirstName} {c.LastName}",
                NrcMasked = MaskNrc(c.Nrc),
                Phone = c.PrimaryPhone,
                BranchId = c.BranchId,
                CreatedAt = c.CreatedAt,
                KycStatus = "Pending", // Simplified for mobile
                DocumentCompletionPercent = 0
            })
            .ToListAsync();

        var result = PagedResult<MobileClientSummary>.Create(
            clients,
            pagination.PageNumber,
            pagination.PageSize,
            totalCount);

        return Ok(result);
    }

    /// <summary>
    /// Gets paginated documents for a client (mobile-optimized)
    /// </summary>
    [HttpGet("clients/{clientId}/documents")]
    [ProducesResponseType(typeof(PagedResult<MobileDocumentSummary>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetClientDocuments(
        Guid clientId,
        [FromQuery] PaginationParams pagination)
    {
        var totalCount = await _context.ClientDocuments
            .Where(d => d.ClientId == clientId)
            .CountAsync();

        var documents = await _context.ClientDocuments
            .Where(d => d.ClientId == clientId)
            .OrderByDescending(d => d.UploadedAt)
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .Select(d => new MobileDocumentSummary
            {
                Id = d.Id,
                Type = d.DocumentType,
                Status = d.UploadStatus.ToString(),
                UploadedAt = d.UploadedAt,
                FileSize = FormatFileSize(d.FileSizeBytes),
                HasThumbnail = false, // Future: generate thumbnails
                ThumbnailUrl = null
            })
            .ToListAsync();

        var result = PagedResult<MobileDocumentSummary>.Create(
            documents,
            pagination.PageNumber,
            pagination.PageSize,
            totalCount);

        return Ok(result);
    }

    /// <summary>
    /// Gets client details (lightweight for mobile)
    /// </summary>
    [HttpGet("clients/{clientId}")]
    [ProducesResponseType(typeof(MobileClientSummary), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetClient(Guid clientId)
    {
        var client = await _context.Clients
            .Where(c => c.Id == clientId)
            .Select(c => new MobileClientSummary
            {
                Id = c.Id,
                FullName = $"{c.FirstName} {c.LastName}",
                NrcMasked = MaskNrc(c.Nrc),
                Phone = c.PrimaryPhone,
                BranchId = c.BranchId,
                CreatedAt = c.CreatedAt,
                KycStatus = "Pending",
                DocumentCompletionPercent = 0
            })
            .FirstOrDefaultAsync();

        if (client == null)
        {
            return NotFound(new { error = "Client not found" });
        }

        return Ok(client);
    }

    #region Helper Methods

    private Guid GetBranchIdFromClaims()
    {
        // In production, extract from JWT claims
        // For now, return a default branch ID
        var branchClaim = User.Claims.FirstOrDefault(c => c.Type == "BranchId");
        return branchClaim != null && Guid.TryParse(branchClaim.Value, out var branchId)
            ? branchId
            : Guid.Empty;
    }

    private static string MaskNrc(string nrc)
    {
        // Mask last digit: 111111/11/1 → 111111/11/*
        if (string.IsNullOrEmpty(nrc) || nrc.Length < 3)
            return nrc;

        var parts = nrc.Split('/');
        if (parts.Length == 3)
        {
            return $"{parts[0]}/{parts[1]}/*";
        }

        return nrc.Substring(0, nrc.Length - 1) + "*";
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        return $"{len:0.#} {sizes[order]}";
    }

    #endregion
}
