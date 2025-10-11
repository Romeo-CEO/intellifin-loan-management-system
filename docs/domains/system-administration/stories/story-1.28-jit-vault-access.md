# Story 1.28: JIT Infrastructure Access with Vault

## Story Metadata

| Field | Value |
|-------|-------|
| **Story ID** | 1.28 |
| **Epic** | System Administration Control Plane Enhancement |
| **Phase** | Phase 5: Observability & Infrastructure |
| **Sprint** | Sprint 9-10 |
| **Story Points** | 13 |
| **Estimated Effort** | 8-12 days |
| **Priority** | P0 (Critical - Security) |
| **Status** | 📋 Backlog |
| **Assigned To** | TBD |
| **Dependencies** | Vault (Story 1.23), Bastion (Story 1.27) |
| **Blocks** | Production database access, Cloud infrastructure management |

---

## User Story

**As a** DevOps Engineer,  
**I want** Just-In-Time (JIT) access to infrastructure resources with automatically expiring credentials,  
**so that** I can perform administrative tasks securely without long-lived credentials or standing privileges.

---

## Business Value

JIT access with Vault provides critical security and operational benefits:

- **Zero Standing Privileges**: No permanent credentials; all access is temporary and on-demand
- **Reduced Attack Surface**: Compromised credentials expire automatically, limiting breach impact
- **Automated Credential Rotation**: Database passwords, cloud keys rotate continuously
- **Audit Trail**: Complete record of who accessed what, when, and why
- **Compliance**: Meets BoZ requirements for privileged access management
- **Operational Efficiency**: Self-service access reduces dependency on manual provisioning
- **Principle of Least Privilege**: Users receive only the permissions needed, for only the time needed

This story is **critical** for production security posture and regulatory compliance.

---

## Acceptance Criteria

### AC1: Dynamic Database Credentials with Vault
**Given** Users need temporary database access  
**When** requesting database credentials  
**Then**:
- Vault database secrets engine configured for:
  - PostgreSQL (Identity, Loan, Payment databases)
  - SQL Server (Admin, Reporting databases)
  - MongoDB (Analytics database)
  - Redis (Cache clusters)
- Database roles defined:
  - `readonly`: SELECT only, 2-hour TTL
  - `developer`: SELECT, INSERT, UPDATE on non-production, 4-hour TTL
  - `dba`: Full privileges (production only), 1-hour TTL
- Dynamic credential generation:
  - Username format: `v-{role}-{uuid}-{timestamp}`
  - Password: Random 64-character string
  - Credentials automatically revoked after TTL
- Database user cleanup on revocation
- Connection string provided to Admin UI

### AC2: JIT Cloud Provider Access (Azure/AWS)
**Given** Infrastructure changes require cloud provider access  
**When** requesting cloud credentials  
**Then**:
- Azure AD integration via Vault:
  - Dynamic service principal creation
  - Role assignments: Reader, Contributor, Owner
  - Scoped to specific resource groups
  - 4-hour TTL (configurable)
  - Automatic cleanup on expiration
- AWS IAM integration via Vault:
  - STS AssumeRole for temporary credentials
  - IAM policies: ReadOnly, PowerUser, Administrator
  - Cross-account role assumption supported
  - 1-hour TTL (AWS maximum for session credentials)
  - MFA required for production account access
- Credentials delivered via:
  - Admin UI download (JSON/ENV format)
  - CLI tool (`intellifin-vault creds azure`)
  - Environment variable injection

### AC3: Temporary Kubernetes Access
**Given** Engineers need temporary cluster access  
**When** requesting Kubernetes credentials  
**Then**:
- Vault Kubernetes auth method configured
- Dynamic kubeconfig generation with:
  - Temporary service account token
  - Namespace-scoped RBAC
  - Cluster roles: view, edit, admin
  - 8-hour TTL
- Access request workflow:
  - Specify cluster (dev, staging, production)
  - Specify namespace (optional, default: all)
  - Justification required for production
  - Manager approval for production admin access
- Kubeconfig automatically expires
- Audit log of all kubectl commands (via bastion)

### AC4: JIT SSH Access with Vault SSH OTP
**Given** Legacy servers require SSH access (non-certificate)  
**When** requesting SSH OTP  
**Then**:
- Vault SSH OTP secrets engine configured
- One-time password generation for:
  - Target host
  - Target user
  - 5-minute validity
- OTP delivered via:
  - Admin UI (copy to clipboard)
  - SMS (optional, for emergency access)
- Server-side Vault agent validates OTP
- OTP invalidated after first use
- Failed OTP attempts logged and alerted

### AC5: Access Request Workflow with Approval
**Given** Production access requires approval  
**When** user requests JIT credentials  
**Then**:
- Admin UI access request form:
  - Resource type (database, cloud, kubernetes, SSH)
  - Target resource (specific server, database, cluster)
  - Access level (readonly, developer, admin)
  - Duration (1-8 hours)
  - Justification (min 50 characters)
- Approval workflow:
  - **Non-production**: Auto-approved for authorized roles
  - **Production readonly**: Manager approval (async)
  - **Production admin**: Manager + Security Engineer approval
  - Approval timeout: 4 hours (auto-deny after)
- Notification:
  - Requester notified on approval/denial (email, Slack)
  - Approvers notified of pending requests
- Camunda orchestration for workflow state

### AC6: Credential Leasing and Renewal
**Given** Long-running tasks need extended access  
**When** credentials near expiration  
**Then**:
- Lease renewal available via API:
  - Max 2 renewals per lease
  - Each renewal extends TTL by 50% of original
  - Example: 4-hour initial → renew to 6 hours → renew to 7 hours
- Renewal requires re-justification
- Auto-renewal for background jobs (with approval)
- Lease revocation:
  - Manual revocation via Admin UI
  - Automatic revocation on task completion
  - Forced revocation by Security team
- Revocation propagates within 30 seconds

### AC7: Access Analytics and Compliance Reporting
**Given** Security needs visibility into access patterns  
**When** generating compliance reports  
**Then**:
- Admin UI dashboard displays:
  - Active leases by resource type
  - Top users by access frequency
  - Access requests by approval status
  - Expired/revoked credentials (last 30 days)
- Grafana dashboards:
  - Credential generation rate
  - Average lease duration
  - Approval wait time
  - Revocation reasons
- Compliance reports:
  - Monthly access summary (PDF export)
  - Per-user access history
  - Privileged access audit trail
  - Anomaly detection (unusual access patterns)
- Prometheus alerts:
  - Credential generation spike (>100/hour)
  - Long-lived credentials (>8 hours)
  - Failed approval rate high (>30%)

### AC8: Break-Glass Emergency Access
**Given** Critical P0 incident requires immediate access  
**When** normal approval process too slow  
**Then**:
- Emergency break-glass endpoint available
- Requires:
  - Incident ticket ID
  - Two-factor authentication
  - On-call engineer role
- Immediate credential generation (no approval)
- Credentials limited to 1 hour
- All actions logged with `EMERGENCY` severity
- PagerDuty alert triggered
- Post-incident review required within 24 hours
- Break-glass usage tracked separately

### AC9: Vault Audit Logging and SIEM Integration
**Given** All access must be auditable  
**When** credentials are issued or revoked  
**Then**:
- Vault audit log captures:
  - Requester identity
  - Resource accessed
  - Credentials issued
  - TTL duration
  - Approval chain
  - Revocation timestamp
- Logs forwarded to Elasticsearch
- Real-time log streaming to Splunk/SIEM
- Log retention: 3 years
- Tamper-proof log storage (immutable)
- Automated log analysis for suspicious patterns

---

## Technical Implementation Details

### Architecture Reference

**PRD Sections**: Lines 1308-1332 (Story 1.28), Phase 5 Overview  
**Architecture Sections**: Section 10 (Vault Integration), Section 11 (IAM), Section 6 (Security)  
**Requirements**: NFR15 (All credentials temporary), NFR16 (Credential TTL ≤8 hours)

### Technology Stack

- **Secrets Management**: HashiCorp Vault
- **Database Engines**: PostgreSQL, SQL Server, MongoDB, Redis
- **Cloud Providers**: Azure (Service Principals), AWS (STS)
- **Kubernetes**: Service Account Token API
- **Workflow**: Camunda (approval workflows)
- **Monitoring**: Prometheus, Grafana, Elasticsearch
- **Notifications**: Slack, Email (via Admin Service)

### Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                        Admin UI / CLI                            │
│                   (Access Request Interface)                     │
└────────────────────────────┬────────────────────────────────────┘
                             │
                             ▼
              ┌──────────────────────────┐
              │    Admin Service API     │
              │   (Access Management)    │
              └─────────┬────────────────┘
                        │
        ┌───────────────┼───────────────┬────────────────┐
        │               │               │                │
        ▼               ▼               ▼                ▼
┌──────────────┐  ┌──────────┐  ┌──────────┐    ┌──────────────┐
│   Camunda    │  │  Vault   │  │  Keycloak│    │ Notification │
│  (Approval)  │  │ (Secrets)│  │  (AuthZ) │    │   Service    │
└──────────────┘  └────┬─────┘  └──────────┘    └──────────────┘
                       │
        ┌──────────────┼──────────────┬──────────────┐
        │              │              │              │
        ▼              ▼              ▼              ▼
┌──────────────┐ ┌──────────┐ ┌──────────┐  ┌─────────────┐
│  PostgreSQL  │ │  Azure   │ │   AWS    │  │ Kubernetes  │
│   Dynamic    │ │   AD     │ │   STS    │  │   Clusters  │
│ Credentials  │ │ (SP Gen) │ │(AssumeRole)│ │   (SA)      │
└──────────────┘ └──────────┘ └──────────┘  └─────────────┘
```

### Vault Database Secrets Engine Configuration

```hcl
# vault-db-config.hcl

# Enable database secrets engine
vault secrets enable -path=database database

# Configure PostgreSQL connection (Identity Database)
vault write database/config/identity-db \
    plugin_name=postgresql-database-plugin \
    allowed_roles="readonly,developer,dba" \
    connection_url="postgresql://{{username}}:{{password}}@identity-db.intellifin.local:5432/identitydb?sslmode=require" \
    username="vault_admin" \
    password="vault_admin_password"

# Create readonly role (2-hour TTL)
vault write database/roles/readonly \
    db_name=identity-db \
    creation_statements="CREATE ROLE \"{{name}}\" WITH LOGIN PASSWORD '{{password}}' VALID UNTIL '{{expiration}}' IN ROLE readonly_role;" \
    revocation_statements="DROP ROLE IF EXISTS \"{{name}}\";" \
    default_ttl="2h" \
    max_ttl="4h"

# Create developer role (4-hour TTL)
vault write database/roles/developer \
    db_name=identity-db \
    creation_statements="CREATE ROLE \"{{name}}\" WITH LOGIN PASSWORD '{{password}}' VALID UNTIL '{{expiration}}' IN ROLE developer_role;" \
    revocation_statements="DROP ROLE IF EXISTS \"{{name}}\";" \
    default_ttl="4h" \
    max_ttl="8h"

# Create DBA role (1-hour TTL, production only)
vault write database/roles/dba \
    db_name=identity-db \
    creation_statements="CREATE ROLE \"{{name}}\" WITH LOGIN PASSWORD '{{password}}' VALID UNTIL '{{expiration}}' IN ROLE pg_dba;" \
    revocation_statements="REVOKE ALL PRIVILEGES ON DATABASE identitydb FROM \"{{name}}\"; DROP ROLE IF EXISTS \"{{name}}\";" \
    default_ttl="1h" \
    max_ttl="2h"

# Configure SQL Server connection (Admin Database)
vault write database/config/admin-db \
    plugin_name=mssql-database-plugin \
    allowed_roles="readonly,developer,dba" \
    connection_url="sqlserver://{{username}}:{{password}}@admin-db.intellifin.local:1433?database=AdminDB" \
    username="vault_admin" \
    password="vault_admin_password"

# SQL Server roles
vault write database/roles/sqlserver-readonly \
    db_name=admin-db \
    creation_statements="CREATE LOGIN [{{name}}] WITH PASSWORD = '{{password}}'; USE [AdminDB]; CREATE USER [{{name}}] FOR LOGIN [{{name}}]; EXEC sp_addrolemember 'db_datareader', '{{name}}';" \
    revocation_statements="USE [AdminDB]; DROP USER IF EXISTS [{{name}}]; DROP LOGIN IF EXISTS [{{name}}];" \
    default_ttl="2h" \
    max_ttl="4h"

# Configure MongoDB connection (Analytics Database)
vault write database/config/analytics-db \
    plugin_name=mongodb-database-plugin \
    allowed_roles="readonly,developer" \
    connection_url="mongodb://{{username}}:{{password}}@analytics-db.intellifin.local:27017/admin?ssl=true" \
    username="vault_admin" \
    password="vault_admin_password"

# MongoDB role
vault write database/roles/mongo-readonly \
    db_name=analytics-db \
    creation_statements='{ "db": "analytics", "roles": [{ "role": "read" }] }' \
    revocation_statements='{ "db": "analytics" }' \
    default_ttl="2h" \
    max_ttl="4h"
```

### Vault Azure Secrets Engine Configuration

```hcl
# vault-azure-config.hcl

# Enable Azure secrets engine
vault secrets enable -path=azure azure

# Configure Azure backend
vault write azure/config \
    subscription_id="${AZURE_SUBSCRIPTION_ID}" \
    tenant_id="${AZURE_TENANT_ID}" \
    client_id="${AZURE_CLIENT_ID}" \
    client_secret="${AZURE_CLIENT_SECRET}"

# Create role for Reader access
vault write azure/roles/reader \
    ttl=4h \
    max_ttl=8h \
    azure_roles=- <<EOF
    [
      {
        "role_name": "Reader",
        "scope": "/subscriptions/${AZURE_SUBSCRIPTION_ID}/resourceGroups/rg-intellifin-production"
      }
    ]
EOF

# Create role for Contributor access
vault write azure/roles/contributor \
    ttl=4h \
    max_ttl=8h \
    azure_roles=- <<EOF
    [
      {
        "role_name": "Contributor",
        "scope": "/subscriptions/${AZURE_SUBSCRIPTION_ID}/resourceGroups/rg-intellifin-production"
      }
    ]
EOF

# Create role for Owner access (emergency only)
vault write azure/roles/owner \
    ttl=1h \
    max_ttl=2h \
    azure_roles=- <<EOF
    [
      {
        "role_name": "Owner",
        "scope": "/subscriptions/${AZURE_SUBSCRIPTION_ID}/resourceGroups/rg-intellifin-production"
      }
    ]
EOF
```

### Vault AWS Secrets Engine Configuration

```hcl
# vault-aws-config.hcl

# Enable AWS secrets engine
vault secrets enable -path=aws aws

# Configure AWS backend
vault write aws/config/root \
    access_key="${AWS_ACCESS_KEY_ID}" \
    secret_key="${AWS_SECRET_ACCESS_KEY}" \
    region="us-east-1"

# Configure lease settings
vault write aws/config/lease \
    lease=1h \
    lease_max=2h

# Create role for ReadOnly access
vault write aws/roles/readonly \
    credential_type=assumed_role \
    role_arns=arn:aws:iam::${AWS_ACCOUNT_ID}:role/ReadOnlyRole \
    default_sts_ttl=1h \
    max_sts_ttl=2h

# Create role for PowerUser access
vault write aws/roles/poweruser \
    credential_type=assumed_role \
    role_arns=arn:aws:iam::${AWS_ACCOUNT_ID}:role/PowerUserRole \
    default_sts_ttl=1h \
    max_sts_ttl=2h

# Create role for Administrator access
vault write aws/roles/administrator \
    credential_type=assumed_role \
    role_arns=arn:aws:iam::${AWS_ACCOUNT_ID}:role/AdministratorRole \
    default_sts_ttl=1h \
    max_sts_ttl=1h \
    mfa_required=true
```

### Admin Service API - JIT Access Controller

```csharp
// Controllers/JitAccessController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using IntelliFin.Admin.Services;
using IntelliFin.Admin.Models;

namespace IntelliFin.Admin.Controllers
{
    [ApiController]
    [Route("api/admin/jit-access")]
    [Authorize]
    public class JitAccessController : ControllerBase
    {
        private readonly IJitAccessService _jitService;
        private readonly IVaultService _vaultService;
        private readonly ILogger<JitAccessController> _logger;

        public JitAccessController(
            IJitAccessService jitService,
            IVaultService vaultService,
            ILogger<JitAccessController> logger)
        {
            _jitService = jitService;
            _vaultService = vaultService;
            _logger = logger;
        }

        /// <summary>
        /// Request JIT database credentials
        /// </summary>
        [HttpPost("database")]
        [ProducesResponseType(typeof(DatabaseCredentialsDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        public async Task<IActionResult> RequestDatabaseAccess(
            [FromBody] DatabaseAccessRequest request,
            CancellationToken cancellationToken)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userName = User.FindFirstValue(ClaimTypes.Name);

            _logger.LogInformation(
                "Database access requested: User={UserId}, Database={Database}, Role={Role}",
                userId, request.DatabaseName, request.Role);

            var accessRequest = await _jitService.RequestDatabaseAccessAsync(
                request,
                userId,
                userName,
                cancellationToken);

            if (accessRequest.RequiresApproval)
            {
                return AcceptedAtAction(
                    nameof(GetAccessRequestStatus),
                    new { requestId = accessRequest.RequestId },
                    accessRequest);
            }

            // Auto-approved, generate credentials immediately
            var credentials = await _vaultService.GenerateDatabaseCredentialsAsync(
                request.DatabaseName,
                request.Role,
                request.TtlHours,
                cancellationToken);

            // Track lease
            await _jitService.TrackLeaseAsync(
                accessRequest.RequestId,
                credentials.LeaseId,
                credentials.LeaseDuration,
                cancellationToken);

            return Ok(new DatabaseCredentialsDto
            {
                RequestId = accessRequest.RequestId,
                Username = credentials.Username,
                Password = credentials.Password,
                ConnectionString = credentials.ConnectionString,
                LeaseId = credentials.LeaseId,
                ExpiresAt = credentials.ExpiresAt,
                RenewalAvailable = true
            });
        }

        /// <summary>
        /// Request JIT cloud provider credentials
        /// </summary>
        [HttpPost("cloud/{provider}")]
        [ProducesResponseType(typeof(CloudCredentialsDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> RequestCloudAccess(
            string provider,
            [FromBody] CloudAccessRequest request,
            CancellationToken cancellationToken)
        {
            if (!new[] { "azure", "aws" }.Contains(provider.ToLower()))
                return BadRequest(new { error = "Unsupported cloud provider" });

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            _logger.LogInformation(
                "Cloud access requested: User={UserId}, Provider={Provider}, Role={Role}",
                userId, provider, request.Role);

            var accessRequest = await _jitService.RequestCloudAccessAsync(
                provider,
                request,
                userId,
                cancellationToken);

            if (accessRequest.RequiresApproval)
            {
                return AcceptedAtAction(
                    nameof(GetAccessRequestStatus),
                    new { requestId = accessRequest.RequestId },
                    accessRequest);
            }

            // Generate credentials
            var credentials = provider.ToLower() switch
            {
                "azure" => await _vaultService.GenerateAzureCredentialsAsync(
                    request.Role,
                    request.TtlHours,
                    cancellationToken),
                "aws" => await _vaultService.GenerateAwsCredentialsAsync(
                    request.Role,
                    request.TtlHours,
                    request.MfaToken,
                    cancellationToken),
                _ => throw new InvalidOperationException($"Unsupported provider: {provider}")
            };

            return Ok(credentials);
        }

        /// <summary>
        /// Request JIT Kubernetes access
        /// </summary>
        [HttpPost("kubernetes")]
        [ProducesResponseType(typeof(KubernetesCredentialsDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> RequestKubernetesAccess(
            [FromBody] KubernetesAccessRequest request,
            CancellationToken cancellationToken)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            _logger.LogInformation(
                "Kubernetes access requested: User={UserId}, Cluster={Cluster}, Role={Role}",
                userId, request.ClusterName, request.Role);

            var accessRequest = await _jitService.RequestKubernetesAccessAsync(
                request,
                userId,
                cancellationToken);

            if (accessRequest.RequiresApproval)
            {
                return AcceptedAtAction(
                    nameof(GetAccessRequestStatus),
                    new { requestId = accessRequest.RequestId },
                    accessRequest);
            }

            // Generate kubeconfig
            var kubeconfig = await _vaultService.GenerateKubeconfigAsync(
                request.ClusterName,
                request.Role,
                request.Namespace,
                request.TtlHours,
                cancellationToken);

            return Ok(kubeconfig);
        }

        /// <summary>
        /// Request SSH OTP
        /// </summary>
        [HttpPost("ssh/otp")]
        [ProducesResponseType(typeof(SshOtpDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> RequestSshOtp(
            [FromBody] SshOtpRequest request,
            CancellationToken cancellationToken)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            _logger.LogInformation(
                "SSH OTP requested: User={UserId}, Host={Host}",
                userId, request.TargetHost);

            var otp = await _vaultService.GenerateSshOtpAsync(
                request.TargetHost,
                request.TargetUser,
                cancellationToken);

            // Track OTP usage
            await _jitService.TrackSshOtpAsync(
                userId,
                request.TargetHost,
                otp.OtpCode,
                cancellationToken);

            return Ok(otp);
        }

        /// <summary>
        /// Renew lease
        /// </summary>
        [HttpPost("leases/{leaseId}/renew")]
        [ProducesResponseType(typeof(LeaseRenewalDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> RenewLease(
            string leaseId,
            [FromBody] LeaseRenewalRequest request,
            CancellationToken cancellationToken)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            _logger.LogInformation(
                "Lease renewal requested: User={UserId}, LeaseId={LeaseId}",
                userId, leaseId);

            var renewal = await _vaultService.RenewLeaseAsync(
                leaseId,
                request.IncrementSeconds,
                cancellationToken);

            return Ok(renewal);
        }

        /// <summary>
        /// Revoke lease
        /// </summary>
        [HttpDelete("leases/{leaseId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> RevokeLease(
            string leaseId,
            CancellationToken cancellationToken)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            _logger.LogInformation(
                "Lease revocation requested: User={UserId}, LeaseId={LeaseId}",
                userId, leaseId);

            await _vaultService.RevokeLeaseAsync(leaseId, cancellationToken);

            return NoContent();
        }

        /// <summary>
        /// Get access request status
        /// </summary>
        [HttpGet("requests/{requestId}")]
        [ProducesResponseType(typeof(AccessRequestStatusDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAccessRequestStatus(
            Guid requestId,
            CancellationToken cancellationToken)
        {
            var status = await _jitService.GetAccessRequestStatusAsync(requestId, cancellationToken);

            if (status == null)
                return NotFound();

            return Ok(status);
        }

        /// <summary>
        /// Get active leases
        /// </summary>
        [HttpGet("leases")]
        [ProducesResponseType(typeof(List<LeaseDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetActiveLeases(
            CancellationToken cancellationToken)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var leases = await _jitService.GetActiveLeasesAsync(userId, cancellationToken);

            return Ok(leases);
        }

        /// <summary>
        /// Emergency break-glass access
        /// </summary>
        [HttpPost("emergency")]
        [Authorize(Roles = "System Administrator,On-Call Engineer")]
        [RequiresMfa(TimeoutMinutes = 15)]
        [ProducesResponseType(typeof(EmergencyAccessDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> RequestEmergencyAccess(
            [FromBody] EmergencyAccessRequest request,
            CancellationToken cancellationToken)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            _logger.LogCritical(
                "Emergency access requested: User={UserId}, Incident={IncidentId}, Resource={Resource}",
                userId, request.IncidentTicketId, request.ResourceType);

            var emergencyAccess = await _jitService.RequestEmergencyAccessAsync(
                request,
                userId,
                cancellationToken);

            return Ok(emergencyAccess);
        }
    }
}
```

### Vault Service Implementation

```csharp
// Services/VaultService.cs
using VaultSharp;
using VaultSharp.V1.AuthMethods.Token;
using VaultSharp.V1.Commons;

namespace IntelliFin.Admin.Services
{
    public interface IVaultService
    {
        Task<DatabaseCredentials> GenerateDatabaseCredentialsAsync(
            string databaseName, 
            string role, 
            int ttlHours, 
            CancellationToken cancellationToken);
        
        Task<CloudCredentials> GenerateAzureCredentialsAsync(
            string role, 
            int ttlHours, 
            CancellationToken cancellationToken);
        
        Task<CloudCredentials> GenerateAwsCredentialsAsync(
            string role, 
            int ttlHours, 
            string mfaToken, 
            CancellationToken cancellationToken);
        
        Task<KubernetesCredentials> GenerateKubeconfigAsync(
            string clusterName, 
            string role, 
            string namespace, 
            int ttlHours, 
            CancellationToken cancellationToken);
        
        Task<SshOtp> GenerateSshOtpAsync(
            string targetHost, 
            string targetUser, 
            CancellationToken cancellationToken);
        
        Task<LeaseRenewal> RenewLeaseAsync(
            string leaseId, 
            int incrementSeconds, 
            CancellationToken cancellationToken);
        
        Task RevokeLeaseAsync(
            string leaseId, 
            CancellationToken cancellationToken);
    }

    public class VaultService : IVaultService
    {
        private readonly IVaultClient _vaultClient;
        private readonly ILogger<VaultService> _logger;
        private readonly IConfiguration _config;

        public VaultService(
            IVaultClient vaultClient,
            ILogger<VaultService> logger,
            IConfiguration config)
        {
            _vaultClient = vaultClient;
            _logger = logger;
            _config = config;
        }

        public async Task<DatabaseCredentials> GenerateDatabaseCredentialsAsync(
            string databaseName,
            string role,
            int ttlHours,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Generating database credentials: Database={Database}, Role={Role}, TTL={TtlHours}h",
                databaseName, role, ttlHours);

            try
            {
                // Read credentials from Vault
                var credentials = await _vaultClient.V1.Secrets.Database.GetCredentialsAsync(
                    $"{databaseName}-{role}",
                    mountPoint: "database");

                var username = credentials.Data.Username;
                var password = credentials.Data.Password;
                var leaseId = credentials.LeaseId;
                var leaseDuration = credentials.LeaseDurationSeconds;

                // Build connection string
                var connectionString = BuildConnectionString(
                    databaseName,
                    username,
                    password);

                return new DatabaseCredentials
                {
                    Username = username,
                    Password = password,
                    ConnectionString = connectionString,
                    LeaseId = leaseId,
                    LeaseDuration = TimeSpan.FromSeconds(leaseDuration),
                    ExpiresAt = DateTime.UtcNow.AddSeconds(leaseDuration)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate database credentials");
                throw;
            }
        }

        public async Task<CloudCredentials> GenerateAzureCredentialsAsync(
            string role,
            int ttlHours,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Generating Azure credentials: Role={Role}, TTL={TtlHours}h",
                role, ttlHours);

            var credentials = await _vaultClient.V1.Secrets.Azure.GetCredentialsAsync(
                role,
                mountPoint: "azure");

            return new CloudCredentials
            {
                Provider = "Azure",
                ClientId = credentials.Data.ClientId,
                ClientSecret = credentials.Data.ClientSecret,
                TenantId = _config["Azure:TenantId"],
                SubscriptionId = _config["Azure:SubscriptionId"],
                LeaseId = credentials.LeaseId,
                ExpiresAt = DateTime.UtcNow.AddSeconds(credentials.LeaseDurationSeconds)
            };
        }

        public async Task<CloudCredentials> GenerateAwsCredentialsAsync(
            string role,
            int ttlHours,
            string mfaToken,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Generating AWS credentials: Role={Role}, TTL={TtlHours}h",
                role, ttlHours);

            var credentials = await _vaultClient.V1.Secrets.AWS.GetCredentialsAsync(
                role,
                mountPoint: "aws");

            return new CloudCredentials
            {
                Provider = "AWS",
                AccessKeyId = credentials.Data.AccessKey,
                SecretAccessKey = credentials.Data.SecretKey,
                SessionToken = credentials.Data.SecurityToken,
                LeaseId = credentials.LeaseId,
                ExpiresAt = DateTime.UtcNow.AddSeconds(credentials.LeaseDurationSeconds)
            };
        }

        public async Task<LeaseRenewal> RenewLeaseAsync(
            string leaseId,
            int incrementSeconds,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Renewing lease: LeaseId={LeaseId}, Increment={IncrementSeconds}s",
                leaseId, incrementSeconds);

            var renewal = await _vaultClient.V1.System.RenewLeaseAsync(
                leaseId,
                incrementSeconds);

            return new LeaseRenewal
            {
                LeaseId = renewal.LeaseId,
                Renewable = renewal.Renewable,
                LeaseDuration = TimeSpan.FromSeconds(renewal.LeaseDurationSeconds),
                NewExpiresAt = DateTime.UtcNow.AddSeconds(renewal.LeaseDurationSeconds)
            };
        }

        public async Task RevokeLeaseAsync(
            string leaseId,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Revoking lease: LeaseId={LeaseId}", leaseId);

            await _vaultClient.V1.System.RevokeLeaseAsync(leaseId);
        }

        private string BuildConnectionString(
            string databaseName,
            string username,
            string password)
        {
            var host = _config[$"Databases:{databaseName}:Host"];
            var port = _config[$"Databases:{databaseName}:Port"];
            var dbName = _config[$"Databases:{databaseName}:DatabaseName"];

            return $"Host={host};Port={port};Database={dbName};Username={username};Password={password};SSL Mode=Require";
        }
    }
}
```

### Database Schema

```sql
-- Admin Service Database

CREATE TABLE JitAccessRequests (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    RequestId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() UNIQUE,
    
    UserId NVARCHAR(100) NOT NULL,
    UserName NVARCHAR(200) NOT NULL,
    UserEmail NVARCHAR(200) NOT NULL,
    
    ResourceType NVARCHAR(50) NOT NULL,  -- database, azure, aws, kubernetes, ssh
    ResourceName NVARCHAR(200) NOT NULL,  -- Specific resource (db name, cluster, etc.)
    AccessRole NVARCHAR(100) NOT NULL,    -- readonly, developer, admin, etc.
    TtlHours INT NOT NULL,
    Justification NVARCHAR(1000) NOT NULL,
    
    Status NVARCHAR(50) NOT NULL,  -- Pending, Approved, Denied, Active, Expired, Revoked
    RequiresApproval BIT NOT NULL,
    
    RequestedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ApprovedBy NVARCHAR(100),
    ApprovedAt DATETIME2,
    DeniedBy NVARCHAR(100),
    DeniedAt DATETIME2,
    DenialReason NVARCHAR(500),
    
    LeaseId NVARCHAR(200),  -- Vault lease ID
    LeaseDuration INT,      -- Seconds
    ExpiresAt DATETIME2,
    RevokedAt DATETIME2,
    RevocationReason NVARCHAR(500),
    
    CamundaProcessInstanceId NVARCHAR(100),
    
    INDEX IX_RequestId (RequestId),
    INDEX IX_UserId (UserId),
    INDEX IX_Status (Status),
    INDEX IX_ResourceType (ResourceType),
    INDEX IX_RequestedAt (RequestedAt DESC),
    INDEX IX_ExpiresAt (ExpiresAt)
);

CREATE TABLE JitLeases (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    LeaseId NVARCHAR(200) NOT NULL UNIQUE,
    AccessRequestId UNIQUEIDENTIFIER NOT NULL,
    
    ResourceType NVARCHAR(50) NOT NULL,
    ResourceName NVARCHAR(200) NOT NULL,
    
    UserId NVARCHAR(100) NOT NULL,
    
    IssuedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ExpiresAt DATETIME2 NOT NULL,
    RenewedCount INT NOT NULL DEFAULT 0,
    MaxRenewals INT NOT NULL DEFAULT 2,
    
    Status NVARCHAR(50) NOT NULL,  -- Active, Expired, Revoked
    RevokedAt DATETIME2,
    RevocationReason NVARCHAR(500),
    
    CredentialsJson NVARCHAR(MAX),  -- Encrypted credentials payload
    
    FOREIGN KEY (AccessRequestId) REFERENCES JitAccessRequests(RequestId),
    INDEX IX_LeaseId (LeaseId),
    INDEX IX_UserId (UserId),
    INDEX IX_Status (Status),
    INDEX IX_ExpiresAt (ExpiresAt)
);

CREATE TABLE JitAuditLogs (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    EventId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    
    EventType NVARCHAR(100) NOT NULL,  -- CredentialIssued, LeaseRenewed, LeaseRevoked, EmergencyAccess
    ResourceType NVARCHAR(50) NOT NULL,
    ResourceName NVARCHAR(200) NOT NULL,
    
    UserId NVARCHAR(100) NOT NULL,
    UserName NVARCHAR(200) NOT NULL,
    UserIpAddress NVARCHAR(50),
    
    LeaseId NVARCHAR(200),
    AccessRequestId UNIQUEIDENTIFIER,
    
    EventTimestamp DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    EventSeverity NVARCHAR(20) NOT NULL,  -- INFO, WARNING, CRITICAL, EMERGENCY
    
    AdditionalData NVARCHAR(MAX),  -- JSON
    
    INDEX IX_UserId (UserId),
    INDEX IX_EventType (EventType),
    INDEX IX_EventTimestamp (EventTimestamp DESC),
    INDEX IX_EventSeverity (EventSeverity)
);

CREATE TABLE EmergencyAccessLogs (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    EmergencyId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() UNIQUE,
    
    UserId NVARCHAR(100) NOT NULL,
    UserName NVARCHAR(200) NOT NULL,
    
    IncidentTicketId NVARCHAR(100) NOT NULL,
    Justification NVARCHAR(1000) NOT NULL,
    
    ResourceType NVARCHAR(50) NOT NULL,
    ResourceName NVARCHAR(200) NOT NULL,
    
    RequestedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    GrantedAt DATETIME2 NOT NULL,
    ExpiresAt DATETIME2 NOT NULL,
    
    LeaseId NVARCHAR(200),
    
    PostIncidentReviewCompleted BIT NOT NULL DEFAULT 0,
    ReviewCompletedAt DATETIME2,
    ReviewNotes NVARCHAR(MAX),
    
    INDEX IX_EmergencyId (EmergencyId),
    INDEX IX_UserId (UserId),
    INDEX IX_IncidentTicketId (IncidentTicketId),
    INDEX IX_RequestedAt (RequestedAt DESC)
);

-- View for active leases
CREATE VIEW vw_ActiveJitLeases AS
SELECT 
    l.LeaseId,
    l.UserId,
    ar.UserName,
    l.ResourceType,
    l.ResourceName,
    ar.AccessRole,
    l.IssuedAt,
    l.ExpiresAt,
    DATEDIFF(MINUTE, GETUTCDATE(), l.ExpiresAt) AS MinutesRemaining,
    l.RenewedCount,
    l.MaxRenewals,
    CASE WHEN l.RenewedCount < l.MaxRenewals THEN 1 ELSE 0 END AS CanRenew
FROM JitLeases l
INNER JOIN JitAccessRequests ar ON l.AccessRequestId = ar.RequestId
WHERE l.Status = 'Active' 
  AND l.ExpiresAt > GETUTCDATE()
ORDER BY l.ExpiresAt;
GO
```

### Camunda Approval Workflow

```xml
<?xml version="1.0" encoding="UTF-8"?>
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL"
                  xmlns:bpmndi="http://www.omg.org/spec/BPMN/20100524/DI"
                  xmlns:camunda="http://camunda.org/schema/1.0/bpmn"
                  id="jit-access-approval">
  
  <bpmn:process id="JitAccessApproval" name="JIT Access Approval" isExecutable="true">
    
    <bpmn:startEvent id="StartEvent" name="Access Requested">
      <bpmn:outgoing>Flow_1</bpmn:outgoing>
    </bpmn:startEvent>
    
    <bpmn:exclusiveGateway id="Gateway_Environment" name="Production?">
      <bpmn:incoming>Flow_1</bpmn:incoming>
      <bpmn:outgoing>Flow_Production</bpmn:outgoing>
      <bpmn:outgoing>Flow_NonProduction</bpmn:outgoing>
    </bpmn:exclusiveGateway>
    
    <!-- Non-production: Auto-approve -->
    <bpmn:serviceTask id="Task_AutoApprove" name="Auto-Approve" 
                      camunda:delegateExpression="${autoApproveDelegate}">
      <bpmn:incoming>Flow_NonProduction</bpmn:incoming>
      <bpmn:outgoing>Flow_IssueCredentials</bpmn:outgoing>
    </bpmn:serviceTask>
    
    <!-- Production: Requires approval -->
    <bpmn:exclusiveGateway id="Gateway_AccessLevel" name="Admin Access?">
      <bpmn:incoming>Flow_Production</bpmn:incoming>
      <bpmn:outgoing>Flow_AdminAccess</bpmn:outgoing>
      <bpmn:outgoing>Flow_ReadonlyAccess</bpmn:outgoing>
    </bpmn:exclusiveGateway>
    
    <!-- Readonly: Manager approval only -->
    <bpmn:userTask id="Task_ManagerApproval" name="Manager Approval" 
                   camunda:candidateGroups="managers">
      <bpmn:incoming>Flow_ReadonlyAccess</bpmn:incoming>
      <bpmn:outgoing>Flow_ManagerDecision</bpmn:outgoing>
    </bpmn:userTask>
    
    <!-- Admin: Manager + Security approval -->
    <bpmn:userTask id="Task_ManagerApproval_Admin" name="Manager Approval" 
                   camunda:candidateGroups="managers">
      <bpmn:incoming>Flow_AdminAccess</bpmn:incoming>
      <bpmn:outgoing>Flow_SecurityApproval</bpmn:outgoing>
    </bpmn:userTask>
    
    <bpmn:userTask id="Task_SecurityApproval" name="Security Engineer Approval" 
                   camunda:candidateGroups="security-engineers">
      <bpmn:incoming>Flow_SecurityApproval</bpmn:incoming>
      <bpmn:outgoing>Flow_AdminDecision</bpmn:outgoing>
    </bpmn:userTask>
    
    <!-- Approval decision gateway -->
    <bpmn:exclusiveGateway id="Gateway_Approved" name="Approved?">
      <bpmn:incoming>Flow_ManagerDecision</bpmn:incoming>
      <bpmn:incoming>Flow_AdminDecision</bpmn:incoming>
      <bpmn:outgoing>Flow_Approved</bpmn:outgoing>
      <bpmn:outgoing>Flow_Denied</bpmn:outgoing>
    </bpmn:exclusiveGateway>
    
    <!-- Issue credentials -->
    <bpmn:serviceTask id="Task_IssueCredentials" name="Issue Credentials via Vault" 
                      camunda:delegateExpression="${issueCredentialsDelegate}">
      <bpmn:incoming>Flow_Approved</bpmn:incoming>
      <bpmn:incoming>Flow_IssueCredentials</bpmn:incoming>
      <bpmn:outgoing>Flow_NotifyUser</bpmn:outgoing>
    </bpmn:serviceTask>
    
    <!-- Notify user -->
    <bpmn:serviceTask id="Task_NotifyApproved" name="Notify User (Approved)" 
                      camunda:delegateExpression="${notificationDelegate}">
      <bpmn:incoming>Flow_NotifyUser</bpmn:incoming>
      <bpmn:outgoing>Flow_End</bpmn:outgoing>
    </bpmn:serviceTask>
    
    <bpmn:serviceTask id="Task_NotifyDenied" name="Notify User (Denied)" 
                      camunda:delegateExpression="${notificationDelegate}">
      <bpmn:incoming>Flow_Denied</bpmn:incoming>
      <bpmn:outgoing>Flow_End</bpmn:outgoing>
    </bpmn:serviceTask>
    
    <bpmn:endEvent id="EndEvent" name="Process Complete">
      <bpmn:incoming>Flow_End</bpmn:incoming>
    </bpmn:endEvent>
    
    <!-- Sequence Flows -->
    <bpmn:sequenceFlow id="Flow_1" sourceRef="StartEvent" targetRef="Gateway_Environment" />
    <bpmn:sequenceFlow id="Flow_Production" sourceRef="Gateway_Environment" targetRef="Gateway_AccessLevel">
      <bpmn:conditionExpression>${environment == 'production'}</bpmn:conditionExpression>
    </bpmn:sequenceFlow>
    <bpmn:sequenceFlow id="Flow_NonProduction" sourceRef="Gateway_Environment" targetRef="Task_AutoApprove">
      <bpmn:conditionExpression>${environment != 'production'}</bpmn:conditionExpression>
    </bpmn:sequenceFlow>
    <bpmn:sequenceFlow id="Flow_IssueCredentials" sourceRef="Task_AutoApprove" targetRef="Task_IssueCredentials" />
    <bpmn:sequenceFlow id="Flow_AdminAccess" sourceRef="Gateway_AccessLevel" targetRef="Task_ManagerApproval_Admin">
      <bpmn:conditionExpression>${accessRole == 'admin' || accessRole == 'dba'}</bpmn:conditionExpression>
    </bpmn:sequenceFlow>
    <bpmn:sequenceFlow id="Flow_ReadonlyAccess" sourceRef="Gateway_AccessLevel" targetRef="Task_ManagerApproval">
      <bpmn:conditionExpression>${accessRole == 'readonly' || accessRole == 'developer'}</bpmn:conditionExpression>
    </bpmn:sequenceFlow>
    <bpmn:sequenceFlow id="Flow_SecurityApproval" sourceRef="Task_ManagerApproval_Admin" targetRef="Task_SecurityApproval" />
    <bpmn:sequenceFlow id="Flow_ManagerDecision" sourceRef="Task_ManagerApproval" targetRef="Gateway_Approved" />
    <bpmn:sequenceFlow id="Flow_AdminDecision" sourceRef="Task_SecurityApproval" targetRef="Gateway_Approved" />
    <bpmn:sequenceFlow id="Flow_Approved" sourceRef="Gateway_Approved" targetRef="Task_IssueCredentials">
      <bpmn:conditionExpression>${approved == true}</bpmn:conditionExpression>
    </bpmn:sequenceFlow>
    <bpmn:sequenceFlow id="Flow_Denied" sourceRef="Gateway_Approved" targetRef="Task_NotifyDenied">
      <bpmn:conditionExpression>${approved == false}</bpmn:conditionExpression>
    </bpmn:sequenceFlow>
    <bpmn:sequenceFlow id="Flow_NotifyUser" sourceRef="Task_IssueCredentials" targetRef="Task_NotifyApproved" />
    <bpmn:sequenceFlow id="Flow_End" sourceRef="Task_NotifyApproved" targetRef="EndEvent" />
    <bpmn:sequenceFlow id="Flow_End" sourceRef="Task_NotifyDenied" targetRef="EndEvent" />
    
  </bpmn:process>
</bpmn:definitions>
```

---

## Integration Verification

### IV1: Dynamic Database Credentials
**Verification Steps**:
1. Request database access via Admin UI
2. Verify dynamic username created in database
3. Test database connection with provided credentials
4. Wait for TTL expiration
5. Verify credentials no longer work
6. Check database user removed

**Success Criteria**:
- Credentials generated successfully
- Database connection works
- Credentials expire at correct time
- Database cleanup occurs automatically

### IV2: Cloud Provider JIT Access
**Verification Steps**:
1. Request Azure credentials via Admin UI
2. Download credentials JSON
3. Configure Azure CLI with credentials
4. Execute Azure commands
5. Verify service principal created in Azure AD
6. Wait for expiration, verify SP deleted

**Success Criteria**:
- Service principal created dynamically
- Azure commands execute successfully
- SP deleted after expiration
- Audit log complete

### IV3: Approval Workflow
**Verification Steps**:
1. Request production admin access
2. Verify approval task created in Camunda
3. Manager approves request
4. Security engineer approves request
5. Credentials issued automatically
6. User notified via email/Slack

**Success Criteria**:
- Approval workflow triggered correctly
- Notifications sent at each stage
- Credentials issued only after full approval
- Audit trail complete

---

## Testing Strategy

### Unit Tests

```csharp
[Fact]
public async Task RequestDatabaseAccess_NonProduction_AutoApproved()
{
    // Arrange
    var service = CreateJitAccessService();
    var request = new DatabaseAccessRequest
    {
        DatabaseName = "identity-db",
        Role = "developer",
        Environment = "staging",
        TtlHours = 4,
        Justification = "Testing feature branch database migration scripts"
    };

    // Act
    var result = await service.RequestDatabaseAccessAsync(
        request, "USER001", "John Doe", CancellationToken.None);

    // Assert
    Assert.False(result.RequiresApproval);
    Assert.Equal("Approved", result.Status);
}

[Fact]
public async Task RequestDatabaseAccess_ProductionAdmin_RequiresApproval()
{
    // Arrange
    var service = CreateJitAccessService();
    var request = new DatabaseAccessRequest
    {
        DatabaseName = "identity-db",
        Role = "dba",
        Environment = "production",
        TtlHours = 1,
        Justification = "Need to investigate slow query performance in production"
    };

    // Act
    var result = await service.RequestDatabaseAccessAsync(
        request, "USER001", "John Doe", CancellationToken.None);

    // Assert
    Assert.True(result.RequiresApproval);
    Assert.Equal("Pending", result.Status);
}
```

### Integration Tests

```bash
#!/bin/bash
# test-jit-access.sh

echo "Testing JIT access workflow..."

# Test 1: Request database credentials
echo "Test 1: Request database credentials"
REQUEST_ID=$(curl -s -X POST "$ADMIN_API/jit-access/database" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "databaseName": "identity-db",
    "role": "readonly",
    "environment": "staging",
    "ttlHours": 2,
    "justification": "Testing JIT database access for integration test suite"
  }' | jq -r '.requestId')

echo "Request ID: $REQUEST_ID"

# Extract credentials
CREDS=$(curl -s -X GET "$ADMIN_API/jit-access/requests/$REQUEST_ID" \
  -H "Authorization: Bearer $TOKEN")

DB_USERNAME=$(echo "$CREDS" | jq -r '.username')
DB_PASSWORD=$(echo "$CREDS" | jq -r '.password')
LEASE_ID=$(echo "$CREDS" | jq -r '.leaseId')

echo "Generated username: $DB_USERNAME"
echo "Lease ID: $LEASE_ID"

# Test 2: Test database connection
echo "Test 2: Test database connection"
PGPASSWORD="$DB_PASSWORD" psql -h identity-db.intellifin.local -U "$DB_USERNAME" -d identitydb -c "SELECT version();"

if [ $? -eq 0 ]; then
  echo "✅ Database connection successful"
else
  echo "❌ Database connection failed"
  exit 1
fi

# Test 3: Renew lease
echo "Test 3: Renew lease"
RENEWED=$(curl -s -X POST "$ADMIN_API/jit-access/leases/$LEASE_ID/renew" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"incrementSeconds": 3600, "justification": "Task taking longer than expected"}' \
  | jq -r '.newExpiresAt')

echo "Lease renewed until: $RENEWED"

# Test 4: Revoke lease
echo "Test 4: Revoke lease"
curl -s -X DELETE "$ADMIN_API/jit-access/leases/$LEASE_ID" \
  -H "Authorization: Bearer $TOKEN"

echo "Lease revoked"

# Test 5: Verify credentials no longer work
echo "Test 5: Verify credentials revoked"
PGPASSWORD="$DB_PASSWORD" psql -h identity-db.intellifin.local -U "$DB_USERNAME" -d identitydb -c "SELECT 1;" 2>/dev/null

if [ $? -ne 0 ]; then
  echo "✅ Credentials correctly revoked"
else
  echo "❌ Credentials still active after revocation"
  exit 1
fi

echo "All tests passed! ✅"
```

---

## Risks and Mitigation

| Risk | Impact | Probability | Mitigation |
|------|---------|-------------|------------|
| Vault service outage | No JIT access available | Low | HA Vault cluster with auto-failover. Break-glass static credentials (encrypted, audited). |
| Credentials leaked | Unauthorized access | Medium | Short TTLs limit exposure. Automatic rotation. Anomaly detection. Immediate revocation capability. |
| Approval bottleneck | Delayed production access | Medium | Auto-approval for non-production. SLA for approvals (4 hours). Escalation to managers' managers. |
| Lease cleanup failure | Orphaned credentials | Low | Background job checks for expired leases. Automatic revocation. Monitoring alerts. |
| Emergency access abuse | Unauthorized privileged access | Low | Two-factor authentication. PagerDuty alerts. Post-incident review mandatory. Complete audit trail. |

---

## Definition of Done

- [ ] Vault database secrets engine configured for all databases
- [ ] Vault Azure/AWS secrets engines configured
- [ ] Vault Kubernetes auth method configured
- [ ] Admin Service API endpoints implemented (9 endpoints)
- [ ] Database schema created (4 tables)
- [ ] Camunda approval workflow deployed
- [ ] Admin UI JIT access request forms
- [ ] Credential renewal and revocation tested
- [ ] Integration tests: Database, Azure, AWS, Kubernetes
- [ ] Grafana dashboards for JIT access metrics
- [ ] Prometheus alerts configured
- [ ] Audit logging to Elasticsearch
- [ ] Emergency break-glass procedure tested
- [ ] Documentation: User guide, API docs, runbooks
- [ ] Security review completed

---

## Related Documentation

### PRD References
- **Lines 1308-1332**: Story 1.28 detailed requirements
- **Lines 1244-1408**: Phase 5 (Observability & Infrastructure) overview
- **NFR15**: All credentials temporary
- **NFR16**: Credential TTL ≤8 hours

### Architecture References
- **Section 10**: Vault Integration
- **Section 11**: IAM
- **Section 6**: Security

### External Documentation
- [Vault Database Secrets Engine](https://developer.hashicorp.com/vault/docs/secrets/databases)
- [Vault Azure Secrets Engine](https://developer.hashicorp.com/vault/docs/secrets/azure)
- [Vault AWS Secrets Engine](https://developer.hashicorp.com/vault/docs/secrets/aws)
- [Vault Kubernetes Auth](https://developer.hashicorp.com/vault/docs/auth/kubernetes)

---

## Notes for Development Team

### Pre-Implementation Checklist
- [ ] Vault cluster deployed and configured
- [ ] Database admin accounts for Vault created
- [ ] Azure service principal with subscription access
- [ ] AWS IAM role for Vault with sts:AssumeRole
- [ ] Kubernetes clusters configured with RBAC
- [ ] Camunda workflow engine deployed
- [ ] Notification service (email, Slack) configured
- [ ] Test databases with sample data

### Post-Implementation Handoff
- [ ] Train developers on JIT access workflow
- [ ] Create video tutorial for requesting access
- [ ] Document emergency access procedures
- [ ] Set up monitoring dashboards
- [ ] Schedule monthly access audits
- [ ] Establish SLA for access requests
- [ ] Create incident response plan
- [ ] Document disaster recovery procedures

### Technical Debt / Future Enhancements
- [ ] Implement risk-based adaptive TTLs
- [ ] Add ML-based anomaly detection for access patterns
- [ ] Support for additional cloud providers (GCP)
- [ ] Mobile app for access requests
- [ ] Chatbot integration (Slack commands)
- [ ] Automated credential rotation for long-running jobs
- [ ] Integration with SIEM for advanced threat detection
- [ ] Session recording for database access

---

**Story Created**: 2025-10-11  
**Last Updated**: 2025-10-11  
**Next Story**: [Story 1.29: Distributed Tracing with Jaeger](./story-1.29-jaeger-tracing.md)
