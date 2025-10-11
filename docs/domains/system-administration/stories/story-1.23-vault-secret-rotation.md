# Story 1.23: Vault Secret Rotation Automation

## Story Metadata

| Field | Value |
|-------|-------|
| **Story ID** | 1.23 |
| **Epic** | System Administration Control Plane Enhancement |
| **Phase** | Phase 4: Governance & Workflows |
| **Sprint** | Sprint 7-8 |
| **Story Points** | 13 |
| **Estimated Effort** | 8-12 days |
| **Priority** | P0 (Critical - Security) |
| **Status** | 📋 Backlog |
| **Assigned To** | TBD |
| **Dependencies** | HashiCorp Vault deployment, Database instances, Story 1.22 (Config management) |
| **Blocks** | All services requiring database credentials |

---

## User Story

**As a** Security Engineer,  
**I want** database credentials and secrets to rotate automatically using Vault,  
**so that** we eliminate long-lived credentials and reduce the blast radius of credential compromise.

---

## Business Value

Automated secret rotation with Vault provides critical security improvements:

- **Credential Lifecycle Management**: Eliminates static, long-lived database credentials
- **Reduced Blast Radius**: Compromised credentials have limited validity period (24-hour leases)
- **Zero-Downtime Rotation**: Applications transparently receive new credentials without restarts
- **Audit Trail**: Complete visibility into credential issuance, renewal, and revocation
- **Compliance**: Meets BoZ requirements for credential rotation and access control
- **Emergency Response**: Rapid credential revocation capability for security incidents

This story is **critical** for production security posture and regulatory compliance.

---

## Acceptance Criteria

### AC1: Vault Database Secrets Engine Configured
**Given** Vault is deployed and accessible  
**When** configuring database secrets engine  
**Then**:
- Vault database secrets engine enabled at path `/database`
- Database connection configurations created for:
  - Identity Service database (`identity-db`)
  - Loan Service database (`loan-db`)
  - Admin Service database (`admin-db`)
  - Audit database (`audit-db`)
- Connection configurations include:
  - Connection strings with admin credentials (from Vault KV)
  - Plugin name (e.g., `postgresql-database-plugin`, `mssql-database-plugin`)
  - Max connection lifetime (1 hour)
  - Max idle connections (5)
- Database roles defined for each service:
  - Role name (e.g., `identity-service-role`)
  - Creation statements (SQL for creating dynamic users)
  - Default TTL (24 hours)
  - Max TTL (72 hours)
  - Revocation statements (SQL for cleanup)

### AC2: Dynamic Credential Generation and Validation
**Given** Vault database roles are configured  
**When** service requests database credentials  
**Then**:
- Service authenticates to Vault using Kubernetes service account token
- Vault returns dynamic credentials:
  - Username (e.g., `v-k8s-identity-service-abc123`)
  - Password (32-character random string)
  - Lease ID (unique identifier)
  - Lease duration (24 hours)
- Credentials are valid immediately in target database
- Database user has appropriate permissions (read, write, execute stored procedures)
- Lease stored in Vault with metadata (service name, namespace, requested by)

### AC3: Lease Renewal and Extension
**Given** Service has active database credentials  
**When** lease approaches expiration (1 hour remaining)  
**Then**:
- Service automatically sends lease renewal request to Vault
- Vault extends lease by 24 hours (up to max TTL of 72 hours)
- Service continues using same credentials (no rotation)
- Renewal logged in Vault audit log
- If renewal fails:
  - Service requests new credentials
  - Old credentials remain valid until original expiration
  - Service transitions to new credentials gracefully

### AC4: Zero-Downtime Secret Rotation
**Given** Service is running with database credentials  
**When** credentials need rotation (lease expires or manual rotation)  
**Then**:
- Service requests new credentials from Vault **before** old credentials expire
- Service maintains connection pool with both old and new credentials
- New connections use new credentials
- Old connections drain gracefully (max 5 minutes)
- Once all old connections closed, old credentials revoked
- No database errors or service disruptions
- Rotation event logged with correlation ID

### AC5: Vault Secret Cache Service (Sidecar Pattern)
**Given** Microservices need Vault integration  
**When** deploying services to Kubernetes  
**Then**:
- Vault Agent sidecar injected into each service pod
- Vault Agent configuration includes:
  - Kubernetes auth method (service account token)
  - Database credential template
  - Lease renewal configuration
  - Credential rotation trigger
- Vault Agent writes credentials to shared volume:
  - File: `/vault/secrets/database-credentials.json`
  - Format: `{ "username": "...", "password": "...", "lease_id": "..." }`
- Service reads credentials from file on startup and on file change (inotify)
- File permissions: 0400 (read-only for service user)

### AC6: Emergency Credential Revocation
**Given** Security incident detected (e.g., compromised credentials)  
**When** Security Engineer triggers emergency revocation  
**Then**:
- POST `/api/admin/vault/revoke-lease` endpoint accepts lease ID or service name
- Vault revokes specified lease immediately
- Database user dropped from database
- All active connections using revoked credentials terminated
- Service automatically requests new credentials
- Revocation logged with incident correlation ID
- Alert sent to security team
- Incident response workflow initiated in Camunda (if applicable)

### AC7: Vault Audit Integration
**Given** Vault operations occur  
**When** audit events are logged  
**Then**:
- Vault audit device enabled (file-based audit log)
- Audit events forwarded to Elasticsearch/Splunk
- Audit events include:
  - `vault/credential-generated`: New database credentials issued
  - `vault/lease-renewed`: Credential lease extended
  - `vault/lease-revoked`: Credentials revoked (manual or automatic)
  - `vault/authentication-success`: Service authenticated to Vault
  - `vault/authentication-failure`: Failed authentication attempt
- All audit events include: timestamp, service identity, database, lease ID, TTL
- Admin UI displays Vault audit events with filtering and search

### AC8: Vault Health Monitoring and Alerting
**Given** Vault is critical infrastructure  
**When** monitoring Vault health  
**Then**:
- Prometheus metrics exposed by Vault:
  - `vault_core_unsealed`: Vault seal status (0=sealed, 1=unsealed)
  - `vault_token_count`: Number of active tokens
  - `vault_database_credentials_issued_total`: Counter of credentials issued
  - `vault_lease_renewal_errors_total`: Counter of failed lease renewals
- Grafana dashboard displays Vault metrics
- Alerts configured for:
  - Vault sealed (P0 - Critical)
  - Lease renewal failure rate >5% (P1 - High)
  - Database connection errors from Vault (P1 - High)
- Alerts route to PagerDuty and Slack

---

## Technical Implementation Details

### Architecture Reference

**PRD Sections**: Lines 1185-1208 (Story 1.23), Phase 4 Overview  
**Architecture Sections**: Section 10 (Vault Integration), Section 4 (Service Architecture), Section 9 (Kubernetes)  
**Requirements**: NFR8 (Secrets rotation <1 minute), NFR9 (Zero downtime during rotation)

### Technology Stack

- **Secret Management**: HashiCorp Vault 1.15+
- **Database Plugins**: PostgreSQL, MS SQL Server
- **Sidecar Injection**: Vault Agent Injector (Kubernetes)
- **Authentication**: Kubernetes Auth Method
- **Monitoring**: Prometheus, Grafana
- **Audit**: Vault audit device → Elasticsearch

### Vault Configuration

#### Vault Database Secrets Engine Setup

```hcl
# vault-db-config.hcl

# Enable database secrets engine
path "sys/mounts/database" {
  capabilities = ["create", "read", "update", "delete", "list"]
}

# Configure database connection for Identity Service
vault write database/config/identity-db \
  plugin_name=mssql-database-plugin \
  connection_url="sqlserver://{{username}}:{{password}}@identity-db.database.windows.net:1433/IdentityDb?database=IdentityDb" \
  allowed_roles="identity-service-role" \
  username="vault-admin" \
  password="$VAULT_DB_ADMIN_PASSWORD" \
  max_open_connections=5 \
  max_connection_lifetime="1h"

# Create role for Identity Service
vault write database/roles/identity-service-role \
  db_name=identity-db \
  creation_statements="CREATE LOGIN [{{name}}] WITH PASSWORD = '{{password}}'; \
    USE IdentityDb; \
    CREATE USER [{{name}}] FOR LOGIN [{{name}}]; \
    ALTER ROLE db_datareader ADD MEMBER [{{name}}]; \
    ALTER ROLE db_datawriter ADD MEMBER [{{name}}]; \
    GRANT EXECUTE TO [{{name}}];" \
  revocation_statements="USE IdentityDb; DROP USER IF EXISTS [{{name}}]; DROP LOGIN IF EXISTS [{{name}}];" \
  default_ttl="24h" \
  max_ttl="72h"

# Configure database connection for Loan Service
vault write database/config/loan-db \
  plugin_name=mssql-database-plugin \
  connection_url="sqlserver://{{username}}:{{password}}@loan-db.database.windows.net:1433/LoanDb?database=LoanDb" \
  allowed_roles="loan-service-role" \
  username="vault-admin" \
  password="$VAULT_DB_ADMIN_PASSWORD" \
  max_open_connections=5 \
  max_connection_lifetime="1h"

# Create role for Loan Service
vault write database/roles/loan-service-role \
  db_name=loan-db \
  creation_statements="CREATE LOGIN [{{name}}] WITH PASSWORD = '{{password}}'; \
    USE LoanDb; \
    CREATE USER [{{name}}] FOR LOGIN [{{name}}]; \
    ALTER ROLE db_datareader ADD MEMBER [{{name}}]; \
    ALTER ROLE db_datawriter ADD MEMBER [{{name}}]; \
    GRANT EXECUTE TO [{{name}}];" \
  revocation_statements="USE LoanDb; DROP USER IF EXISTS [{{name}}]; DROP LOGIN IF EXISTS [{{name}}];" \
  default_ttl="24h" \
  max_ttl="72h"

# Enable Kubernetes auth method
vault auth enable kubernetes

# Configure Kubernetes auth
vault write auth/kubernetes/config \
  kubernetes_host="https://kubernetes.default.svc:443" \
  kubernetes_ca_cert=@/var/run/secrets/kubernetes.io/serviceaccount/ca.crt \
  token_reviewer_jwt=@/var/run/secrets/kubernetes.io/serviceaccount/token

# Create policy for Identity Service
vault policy write identity-service-policy - <<EOF
path "database/creds/identity-service-role" {
  capabilities = ["read"]
}

path "sys/leases/renew" {
  capabilities = ["update"]
}

path "sys/leases/revoke" {
  capabilities = ["update"]
}
EOF

# Bind Kubernetes service account to Vault policy
vault write auth/kubernetes/role/identity-service \
  bound_service_account_names=identity-service \
  bound_service_account_namespaces=default \
  policies=identity-service-policy \
  ttl=24h
```

### Kubernetes Deployment with Vault Agent

```yaml
# k8s/identity-service-deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: identity-service
  namespace: default
spec:
  replicas: 3
  selector:
    matchLabels:
      app: identity-service
  template:
    metadata:
      labels:
        app: identity-service
      annotations:
        # Vault Agent Injector annotations
        vault.hashicorp.com/agent-inject: "true"
        vault.hashicorp.com/role: "identity-service"
        vault.hashicorp.com/agent-inject-secret-database-credentials: "database/creds/identity-service-role"
        vault.hashicorp.com/agent-inject-template-database-credentials: |
          {{- with secret "database/creds/identity-service-role" -}}
          {
            "username": "{{ .Data.username }}",
            "password": "{{ .Data.password }}",
            "lease_id": "{{ .LeaseID }}",
            "lease_duration": {{ .LeaseDuration }},
            "renewable": {{ .Renewable }}
          }
          {{- end }}
        vault.hashicorp.com/secret-volume-path: "/vault/secrets"
        vault.hashicorp.com/agent-limits-cpu: "100m"
        vault.hashicorp.com/agent-limits-mem: "128Mi"
        vault.hashicorp.com/agent-requests-cpu: "50m"
        vault.hashicorp.com/agent-requests-mem: "64Mi"
    spec:
      serviceAccountName: identity-service
      containers:
      - name: identity-service
        image: intellifin/identity-service:1.0.0
        ports:
        - containerPort: 8080
        env:
        - name: DATABASE_CREDENTIALS_PATH
          value: "/vault/secrets/database-credentials"
        - name: VAULT_ADDR
          value: "http://vault.vault.svc.cluster.local:8200"
        volumeMounts:
        - name: vault-secrets
          mountPath: /vault/secrets
          readOnly: true
        resources:
          requests:
            memory: "512Mi"
            cpu: "250m"
          limits:
            memory: "1Gi"
            cpu: "500m"
      volumes:
      - name: vault-secrets
        emptyDir:
          medium: Memory
---
apiVersion: v1
kind: ServiceAccount
metadata:
  name: identity-service
  namespace: default
```

### Service Implementation - Dynamic Credential Loading

```csharp
// Services/VaultDatabaseCredentialService.cs
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace IntelliFin.Identity.Services
{
    public interface IVaultDatabaseCredentialService
    {
        DatabaseCredential GetCurrentCredentials();
        event EventHandler<DatabaseCredential> CredentialsRotated;
    }

    public class DatabaseCredential
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string LeaseId { get; set; }
        public int LeaseDuration { get; set; }
        public bool Renewable { get; set; }
        public DateTime LoadedAt { get; set; }
    }

    public class VaultDatabaseCredentialService : BackgroundService, IVaultDatabaseCredentialService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<VaultDatabaseCredentialService> _logger;
        private DatabaseCredential _currentCredentials;
        private readonly SemaphoreSlim _credentialLock = new SemaphoreSlim(1, 1);
        private FileSystemWatcher _fileWatcher;

        public event EventHandler<DatabaseCredential> CredentialsRotated;

        private const string CREDENTIALS_FILE = "/vault/secrets/database-credentials";

        public VaultDatabaseCredentialService(
            IConfiguration configuration,
            ILogger<VaultDatabaseCredentialService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Vault Database Credential Service starting...");

            // Initial credential load
            await LoadCredentialsAsync(stoppingToken);

            // Set up file watcher for credential rotation
            SetupFileWatcher();

            // Keep service running
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        private async Task LoadCredentialsAsync(CancellationToken cancellationToken)
        {
            await _credentialLock.WaitAsync(cancellationToken);
            try
            {
                var credentialsPath = _configuration["DATABASE_CREDENTIALS_PATH"] ?? CREDENTIALS_FILE;
                
                if (!File.Exists(credentialsPath))
                {
                    _logger.LogWarning("Credentials file not found at {Path}. Waiting for Vault Agent...", credentialsPath);
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                    return;
                }

                var credentialsJson = await File.ReadAllTextAsync(credentialsPath, cancellationToken);
                var newCredentials = JsonSerializer.Deserialize<DatabaseCredential>(credentialsJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (newCredentials == null)
                {
                    _logger.LogError("Failed to deserialize database credentials");
                    return;
                }

                newCredentials.LoadedAt = DateTime.UtcNow;

                var oldCredentials = _currentCredentials;
                _currentCredentials = newCredentials;

                _logger.LogInformation(
                    "Database credentials loaded: Username={Username}, LeaseId={LeaseId}, LeaseDuration={Duration}s",
                    newCredentials.Username,
                    newCredentials.LeaseId,
                    newCredentials.LeaseDuration);

                // Notify listeners of credential rotation
                if (oldCredentials != null && oldCredentials.Username != newCredentials.Username)
                {
                    _logger.LogInformation(
                        "Database credentials rotated: {OldUsername} -> {NewUsername}",
                        oldCredentials.Username,
                        newCredentials.Username);
                    
                    CredentialsRotated?.Invoke(this, newCredentials);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading database credentials from Vault");
            }
            finally
            {
                _credentialLock.Release();
            }
        }

        private void SetupFileWatcher()
        {
            var credentialsPath = _configuration["DATABASE_CREDENTIALS_PATH"] ?? CREDENTIALS_FILE;
            var directory = Path.GetDirectoryName(credentialsPath);
            var fileName = Path.GetFileName(credentialsPath);

            _fileWatcher = new FileSystemWatcher(directory, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
            };

            _fileWatcher.Changed += async (sender, e) =>
            {
                _logger.LogInformation("Credentials file changed. Reloading...");
                await Task.Delay(TimeSpan.FromMilliseconds(500)); // Debounce
                await LoadCredentialsAsync(CancellationToken.None);
            };

            _fileWatcher.EnableRaisingEvents = true;
            _logger.LogInformation("File watcher enabled for credentials at {Path}", credentialsPath);
        }

        public DatabaseCredential GetCurrentCredentials()
        {
            if (_currentCredentials == null)
            {
                throw new InvalidOperationException("Database credentials not yet loaded from Vault");
            }

            return _currentCredentials;
        }

        public override void Dispose()
        {
            _fileWatcher?.Dispose();
            _credentialLock?.Dispose();
            base.Dispose();
        }
    }
}
```

### Database Context with Dynamic Credentials

```csharp
// Data/DynamicConnectionDbContext.cs
using Microsoft.EntityFrameworkCore;
using IntelliFin.Identity.Services;

namespace IntelliFin.Identity.Data
{
    public class IdentityDbContext : DbContext
    {
        private readonly IVaultDatabaseCredentialService _credentialService;
        private readonly IConfiguration _configuration;

        public IdentityDbContext(
            DbContextOptions<IdentityDbContext> options,
            IVaultDatabaseCredentialService credentialService,
            IConfiguration configuration)
            : base(options)
        {
            _credentialService = credentialService;
            _configuration = configuration;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var connectionString = BuildDynamicConnectionString();
                optionsBuilder.UseSqlServer(connectionString);
            }
        }

        private string BuildDynamicConnectionString()
        {
            var credentials = _credentialService.GetCurrentCredentials();
            var baseConnectionString = _configuration.GetConnectionString("IdentityDb");

            // Replace username and password in connection string
            var builder = new SqlConnectionStringBuilder(baseConnectionString)
            {
                UserID = credentials.Username,
                Password = credentials.Password,
                ConnectTimeout = 30,
                MinPoolSize = 5,
                MaxPoolSize = 100
            };

            return builder.ConnectionString;
        }

        // DbSet properties
        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }
    }
}
```

### Connection Pool Management with Rotation

```csharp
// Services/DatabaseConnectionPoolManager.cs
using Microsoft.Data.SqlClient;
using IntelliFin.Identity.Services;

namespace IntelliFin.Identity.Data
{
    public interface IDatabaseConnectionPoolManager
    {
        Task DrainOldConnectionsAsync(string oldUsername, CancellationToken cancellationToken);
    }

    public class DatabaseConnectionPoolManager : IDatabaseConnectionPoolManager
    {
        private readonly IVaultDatabaseCredentialService _credentialService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<DatabaseConnectionPoolManager> _logger;

        public DatabaseConnectionPoolManager(
            IVaultDatabaseCredentialService credentialService,
            IConfiguration configuration,
            ILogger<DatabaseConnectionPoolManager> logger)
        {
            _credentialService = credentialService;
            _configuration = configuration;
            _logger = logger;

            // Subscribe to credential rotation events
            _credentialService.CredentialsRotated += OnCredentialsRotated;
        }

        private async void OnCredentialsRotated(object sender, DatabaseCredential newCredentials)
        {
            _logger.LogInformation("Starting connection pool drain for credential rotation");

            try
            {
                // Clear all connection pools
                SqlConnection.ClearAllPools();

                _logger.LogInformation("Connection pools cleared successfully");

                // Wait for existing connections to drain (max 5 minutes)
                await Task.Delay(TimeSpan.FromMinutes(5));

                _logger.LogInformation("Connection pool drain completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during connection pool drain");
            }
        }

        public async Task DrainOldConnectionsAsync(string oldUsername, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Draining connections for old user: {Username}", oldUsername);

            // Clear SQL Server connection pools
            SqlConnection.ClearAllPools();

            // Wait for graceful drain
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);

            _logger.LogInformation("Old connections drained for user: {Username}", oldUsername);
        }
    }
}
```

### Admin API - Emergency Revocation

```csharp
// Controllers/VaultManagementController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using VaultSharp;
using VaultSharp.V1.AuthMethods.Kubernetes;

namespace IntelliFin.Admin.Controllers
{
    [ApiController]
    [Route("api/admin/vault")]
    [Authorize(Roles = "System Administrator,Security Engineer")]
    public class VaultManagementController : ControllerBase
    {
        private readonly IVaultClient _vaultClient;
        private readonly ILogger<VaultManagementController> _logger;
        private readonly IAuditService _auditService;

        public VaultManagementController(
            IVaultClient vaultClient,
            ILogger<VaultManagementController> logger,
            IAuditService auditService)
        {
            _vaultClient = vaultClient;
            _logger = logger;
            _auditService = auditService;
        }

        /// <summary>
        /// Revoke a specific lease (emergency credential revocation)
        /// </summary>
        [HttpPost("revoke-lease")]
        [RequiresMfa(TimeoutMinutes = 15)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RevokeLeaseAsync(
            [FromBody] VaultLeaseRevocationRequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.LeaseId))
                return BadRequest(new { error = "LeaseId is required" });

            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var correlationId = Guid.NewGuid().ToString("N");

            _logger.LogWarning(
                "Emergency lease revocation requested: LeaseId={LeaseId}, Admin={AdminId}, Reason={Reason}",
                request.LeaseId, adminId, request.Reason);

            try
            {
                // Revoke lease in Vault
                await _vaultClient.V1.System.RevokeLease(request.LeaseId);

                _logger.LogInformation(
                    "Lease revoked successfully: LeaseId={LeaseId}",
                    request.LeaseId);

                // Audit log
                await _auditService.LogAsync(new AuditEvent
                {
                    Actor = adminId,
                    Action = "VaultLeaseRevoked",
                    EntityType = "VaultLease",
                    EntityId = request.LeaseId,
                    CorrelationId = correlationId,
                    Severity = "Critical",
                    EventData = JsonSerializer.Serialize(new
                    {
                        leaseId = request.LeaseId,
                        reason = request.Reason,
                        incidentId = request.IncidentId
                    })
                }, cancellationToken);

                return Ok(new
                {
                    message = "Lease revoked successfully",
                    leaseId = request.LeaseId,
                    correlationId = correlationId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to revoke lease: LeaseId={LeaseId}", request.LeaseId);
                return StatusCode(500, new { error = "Failed to revoke lease", details = ex.Message });
            }
        }

        /// <summary>
        /// List active database credentials for a service
        /// </summary>
        [HttpGet("leases")]
        [ProducesResponseType(typeof(List<VaultLeaseDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> ListLeasesAsync(
            [FromQuery] string? serviceName = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Query Vault for active leases
                // Note: This is a simplified example; actual implementation depends on Vault API
                var leases = await GetActiveLeasesFromVaultAsync(serviceName, cancellationToken);

                return Ok(leases);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to list leases");
                return StatusCode(500, new { error = "Failed to list leases" });
            }
        }

        private async Task<List<VaultLeaseDto>> GetActiveLeasesFromVaultAsync(
            string serviceName,
            CancellationToken cancellationToken)
        {
            // Implementation depends on Vault lease lookup API
            // This is a placeholder
            return new List<VaultLeaseDto>();
        }
    }

    public class VaultLeaseRevocationRequest
    {
        public string LeaseId { get; set; }
        public string Reason { get; set; }
        public string IncidentId { get; set; }
    }

    public class VaultLeaseDto
    {
        public string LeaseId { get; set; }
        public string ServiceName { get; set; }
        public string Database { get; set; }
        public string Username { get; set; }
        public DateTime IssuedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public int RemainingSeconds { get; set; }
    }
}
```

### Vault Health Monitoring

```yaml
# prometheus-vault-servicemonitor.yaml
apiVersion: monitoring.coreos.com/v1
kind: ServiceMonitor
metadata:
  name: vault
  namespace: vault
spec:
  selector:
    matchLabels:
      app: vault
  endpoints:
  - port: metrics
    path: /v1/sys/metrics
    params:
      format: ['prometheus']
    interval: 30s
---
# prometheus-vault-alerts.yaml
apiVersion: monitoring.coreos.com/v1
kind: PrometheusRule
metadata:
  name: vault-alerts
  namespace: vault
spec:
  groups:
  - name: vault.rules
    interval: 30s
    rules:
    - alert: VaultSealed
      expr: vault_core_unsealed == 0
      for: 1m
      labels:
        severity: critical
        component: vault
      annotations:
        summary: "Vault is sealed"
        description: "Vault instance {{ $labels.instance }} is sealed and unavailable"
        
    - alert: VaultLeaseRenewalErrors
      expr: rate(vault_lease_renewal_errors_total[5m]) > 0.05
      for: 5m
      labels:
        severity: high
        component: vault
      annotations:
        summary: "High Vault lease renewal error rate"
        description: "Vault lease renewal error rate is {{ $value | humanizePercentage }} (threshold: 5%)"
        
    - alert: VaultDatabaseConnectionErrors
      expr: rate(vault_database_connection_errors_total[5m]) > 0
      for: 2m
      labels:
        severity: high
        component: vault
      annotations:
        summary: "Vault database connection errors detected"
        description: "Vault is experiencing database connection errors: {{ $value }} errors/sec"
```

### Configuration

```json
// appsettings.json - Identity Service
{
  "ConnectionStrings": {
    "IdentityDb": "Server=identity-db.database.windows.net;Database=IdentityDb;TrustServerCertificate=true"
  },
  "Vault": {
    "Address": "http://vault.vault.svc.cluster.local:8200",
    "Role": "identity-service",
    "DatabaseRole": "identity-service-role",
    "CredentialsPath": "/vault/secrets/database-credentials"
  }
}
```

---

## Integration Verification

### IV1: Dynamic Credential Generation and Database Access
**Verification Steps**:
1. Deploy Identity Service with Vault Agent sidecar
2. Verify Vault Agent writes credentials to `/vault/secrets/database-credentials`
3. Service reads credentials and establishes database connection
4. Query database to confirm dynamic user exists: `SELECT name FROM sys.database_principals WHERE name LIKE 'v-k8s-%'`
5. Verify service can read/write data using dynamic credentials
6. Check Vault audit log for credential generation event

**Success Criteria**:
- Dynamic credentials generated within 30 seconds of pod start
- Database connection successful
- Dynamic user has correct permissions
- Audit log contains credential generation event

### IV2: Lease Renewal Without Service Restart
**Verification Steps**:
1. Service running with database credentials (24-hour lease)
2. Wait 23 hours (simulate lease approaching expiration)
3. Trigger lease renewal manually: `vault lease renew <lease-id>`
4. Verify service continues operating without restart
5. Check Vault audit log for lease renewal event
6. Verify lease expiration extended by 24 hours

**Success Criteria**:
- Lease renewed successfully
- Service continues operating (no errors)
- No database connection interruptions
- Lease expiration extended

### IV3: Zero-Downtime Credential Rotation
**Verification Steps**:
1. Service running with active database connections
2. Generate load: 100 req/sec database queries
3. Rotate credentials manually (delete and regenerate lease)
4. Vault Agent detects rotation and writes new credentials
5. Service detects file change and loads new credentials
6. Verify no database errors during rotation
7. Old connections drain within 5 minutes
8. All new connections use new credentials

**Success Criteria**:
- Zero database errors during rotation
- No service downtime (HTTP 500 errors)
- Rotation completes within 1 minute (NFR8)
- Old credentials revoked after drain period

---

## Testing Strategy

### Unit Tests

#### Test: Credential Loading from File
```csharp
[Fact]
public async Task LoadCredentials_ValidFile_Success()
{
    // Arrange
    var credentialsJson = @"{
        ""username"": ""v-k8s-identity-abc123"",
        ""password"": ""test-password-32-chars-long"",
        ""lease_id"": ""database/creds/identity-service-role/abc123"",
        ""lease_duration"": 86400,
        ""renewable"": true
    }";
    
    var tempFile = Path.GetTempFileName();
    await File.WriteAllTextAsync(tempFile, credentialsJson);

    var configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string>
        {
            { "DATABASE_CREDENTIALS_PATH", tempFile }
        })
        .Build();

    var service = new VaultDatabaseCredentialService(configuration, Mock.Of<ILogger<VaultDatabaseCredentialService>>());

    // Act
    await service.StartAsync(CancellationToken.None);
    await Task.Delay(TimeSpan.FromSeconds(2)); // Allow time for file load
    var credentials = service.GetCurrentCredentials();

    // Assert
    Assert.Equal("v-k8s-identity-abc123", credentials.Username);
    Assert.Equal("test-password-32-chars-long", credentials.Password);
    Assert.True(credentials.Renewable);

    // Cleanup
    File.Delete(tempFile);
}
```

#### Test: Credential Rotation Detection
```csharp
[Fact]
public async Task CredentialRotation_FileChanged_EventRaised()
{
    // Arrange
    var tempFile = Path.GetTempFileName();
    var initialCreds = @"{""username"": ""user1"", ""password"": ""pass1"", ""lease_id"": ""lease1"", ""lease_duration"": 86400}";
    await File.WriteAllTextAsync(tempFile, initialCreds);

    var service = new VaultDatabaseCredentialService(
        CreateConfig(tempFile),
        Mock.Of<ILogger<VaultDatabaseCredentialService>>());

    var rotationDetected = false;
    service.CredentialsRotated += (sender, creds) => rotationDetected = true;

    await service.StartAsync(CancellationToken.None);
    await Task.Delay(TimeSpan.FromSeconds(1));

    // Act: Simulate credential rotation
    var newCreds = @"{""username"": ""user2"", ""password"": ""pass2"", ""lease_id"": ""lease2"", ""lease_duration"": 86400}";
    await File.WriteAllTextAsync(tempFile, newCreds);
    await Task.Delay(TimeSpan.FromSeconds(2)); // Allow file watcher to trigger

    // Assert
    Assert.True(rotationDetected);
    Assert.Equal("user2", service.GetCurrentCredentials().Username);

    // Cleanup
    File.Delete(tempFile);
}
```

### Integration Tests

#### Test: End-to-End Vault Credential Lifecycle
```csharp
[Fact]
public async Task VaultCredentialLifecycle_GenerateRenewRevoke_Success()
{
    // Arrange
    var vaultClient = CreateVaultClient();

    // Act 1: Generate credentials
    var credentials = await vaultClient.V1.Secrets.Database.GetCredentialsAsync(
        "identity-service-role",
        "database");
    Assert.NotNull(credentials.Data.Username);
    Assert.NotNull(credentials.Data.Password);

    // Act 2: Renew lease
    await Task.Delay(TimeSpan.FromSeconds(30));
    await vaultClient.V1.System.RenewLeaseAsync(credentials.LeaseId);

    // Act 3: Verify lease renewed
    var leaseInfo = await vaultClient.V1.System.ReadLeaseAsync(credentials.LeaseId);
    Assert.True(leaseInfo.Data.ExpireTime > DateTime.UtcNow.AddHours(23));

    // Act 4: Revoke lease
    await vaultClient.V1.System.RevokeLease(credentials.LeaseId);

    // Assert: Credentials no longer valid
    await Assert.ThrowsAsync<Exception>(async () =>
    {
        await TestDatabaseConnectionAsync(credentials.Data.Username, credentials.Data.Password);
    });
}
```

### Performance Tests

#### Test: Credential Rotation Latency
```csharp
[Fact]
public async Task CredentialRotation_Latency_UnderOneMinute()
{
    // Arrange
    var service = CreateIdentityService();
    var stopwatch = Stopwatch.StartNew();

    // Act: Trigger credential rotation
    await RotateCredentialsAsync();

    // Wait for service to detect rotation and update connection
    while (!IsNewCredentialsActive() && stopwatch.Elapsed < TimeSpan.FromMinutes(2))
    {
        await Task.Delay(TimeSpan.FromSeconds(1));
    }

    stopwatch.Stop();

    // Assert
    Assert.True(stopwatch.Elapsed < TimeSpan.FromMinutes(1), 
        $"Credential rotation took {stopwatch.Elapsed.TotalSeconds}s (threshold: 60s per NFR8)");
}
```

---

## Risks and Mitigation

| Risk | Impact | Probability | Mitigation |
|------|---------|-------------|------------|
| Vault unavailable | Services cannot obtain credentials | Medium | Implement credential caching (last valid credentials). Vault HA deployment. Fallback to static credentials (emergency only). |
| Lease renewal fails | Credentials expire, service loses database access | Low | Implement automatic retry with exponential backoff. Request new credentials if renewal fails repeatedly. Alert ops team. |
| Credential rotation during high load | Database connection errors | Medium | Implement graceful connection drain (5-minute window). Monitor error rates during rotation. Pause rotation if error rate >1%. |
| Dynamic user quota exceeded | Cannot create new database users | Low | Monitor active dynamic users. Set database user quota alerts. Implement automatic cleanup of expired users. |
| Vault Agent sidecar crashes | Service loses ability to rotate credentials | Low | Kubernetes restarts Vault Agent automatically. Service uses cached credentials until Agent recovers. |

---

## Definition of Done

- [ ] Vault database secrets engine configured for all services
- [ ] Kubernetes auth method enabled and configured
- [ ] Vault policies created for each service
- [ ] Vault Agent Injector deployed to Kubernetes
- [ ] Service deployments updated with Vault Agent annotations
- [ ] Credential loading service implemented in all microservices
- [ ] Connection pool management with rotation support
- [ ] Admin API emergency revocation endpoint implemented
- [ ] Vault audit device enabled and forwarding to Elasticsearch
- [ ] Prometheus metrics and Grafana dashboards configured
- [ ] Alerts configured for Vault health and lease renewal errors
- [ ] Unit tests: >85% code coverage
- [ ] Integration tests: Credential lifecycle verified
- [ ] Performance test: Rotation <1 minute (NFR8)
- [ ] Load test: Zero errors during rotation (NFR9)
- [ ] Documentation: Vault setup guide, emergency procedures
- [ ] Runbook: Vault unseal, credential revocation, backup/restore

---

## Related Documentation

### PRD References
- **Lines 1185-1208**: Story 1.23 detailed requirements
- **Lines 1079-1243**: Phase 4 (Governance & Workflows) overview
- **NFR8**: Secrets rotation <1 minute
- **NFR9**: Zero downtime during rotation

### Architecture References
- **Section 10**: Vault Integration Architecture
- **Section 4**: Microservices Architecture
- **Section 9**: Kubernetes Infrastructure

### External Documentation
- [HashiCorp Vault Database Secrets Engine](https://developer.hashicorp.com/vault/docs/secrets/databases)
- [Vault Agent Injector](https://developer.hashicorp.com/vault/docs/platform/k8s/injector)
- [Vault Kubernetes Auth Method](https://developer.hashicorp.com/vault/docs/auth/kubernetes)
- [MS SQL Server Dynamic Credentials](https://developer.hashicorp.com/vault/docs/secrets/databases/mssql)

---

## Notes for Development Team

### Pre-Implementation Checklist
- [ ] Deploy Vault cluster (3 nodes for HA)
- [ ] Initialize and unseal Vault
- [ ] Enable audit device
- [ ] Install Vault Agent Injector in Kubernetes
- [ ] Create database admin accounts for Vault
- [ ] Test dynamic credential generation manually
- [ ] Document Vault unseal procedure
- [ ] Create emergency credential revocation runbook

### Post-Implementation Handoff
- [ ] Train DevOps team on Vault operations
- [ ] Demo credential rotation to development teams
- [ ] Create troubleshooting guide for common issues
- [ ] Set up monitoring dashboards for Vault health
- [ ] Schedule quarterly Vault security audit
- [ ] Document disaster recovery procedure for Vault
- [ ] Create incident response plan for credential compromise

### Technical Debt / Future Enhancements
- [ ] Implement Vault auto-unseal with cloud KMS
- [ ] Add support for PostgreSQL dynamic credentials
- [ ] Implement certificate-based database authentication
- [ ] Add Vault namespace support for multi-tenancy
- [ ] Create automated Vault backup and restore
- [ ] Implement Vault replication for disaster recovery
- [ ] Add credential usage analytics dashboard
- [ ] Implement predictive lease renewal (renew before 1 hour remaining)

---

**Story Created**: 2025-10-11  
**Last Updated**: 2025-10-11  
**Next Story**: [Story 1.24: Quarterly Access Recertification Workflows](./story-1.24-access-recertification.md)
