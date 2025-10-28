# Vault Setup Guide for Loan Origination Service

**Purpose**: Local development and testing of Vault-based product configuration  
**Last Updated**: 2025-10-27

## Quick Start

### Option 1: Use In-Memory Fallback (No Vault Required)

The service gracefully falls back to in-memory configuration when Vault is not configured.

**Configuration**: Leave `Vault` section empty or remove it from `appsettings.Development.json`

```json
{
  "Vault": {
    "Address": "",
    "Token": ""
  }
}
```

**Behavior**:
- Service logs: `"Vault not configured - LoanProductService will use in-memory configuration"`
- Uses hardcoded product definitions (PL001, BL001, HL001)
- No EAR validation from Vault
- Suitable for: Initial development, unit testing

### Option 2: Local Vault Instance

Run Vault locally in development mode for testing Vault integration.

## Local Vault Setup

### Step 1: Install Vault

**Windows (via Chocolatey)**:
```powershell
choco install vault
```

**Windows (Manual)**:
1. Download from https://www.vaultproject.io/downloads
2. Extract `vault.exe` to `C:\HashiCorp\Vault\`
3. Add to PATH: `$env:PATH += ";C:\HashiCorp\Vault"`

**Verify Installation**:
```powershell
vault --version
# Output: Vault v1.15.x or later
```

### Step 2: Start Vault in Dev Mode

```powershell
# Start Vault server (dev mode - NOT for production)
vault server -dev

# Output:
# ==> Vault server configuration:
# 
#              Api Address: http://127.0.0.1:8200
#                      Cgo: disabled
#          Cluster Address: https://127.0.0.1:8201
#  ...
# 
# WARNING! dev mode is enabled!
# 
# Root Token: hvs.XXXXXXXXXXXXXXXXXXXX  ← COPY THIS TOKEN
```

**IMPORTANT**: 
- Keep this terminal open (Vault runs in foreground)
- Copy the Root Token shown in output
- Data is ephemeral - will be lost when stopped

### Step 3: Configure Environment

In a **new terminal**:

```powershell
# Set Vault address
$env:VAULT_ADDR = "http://127.0.0.1:8200"

# Set Vault token (use token from Step 2)
$env:VAULT_TOKEN = "hvs.XXXXXXXXXXXXXXXXXXXX"

# Verify connectivity
vault status
```

### Step 4: Enable KV Secrets Engine

```powershell
# Enable KV v2 secrets engine at "kv" mount point
vault secrets enable -version=2 -path=kv kv

# Verify
vault secrets list
# Should show: kv/ with type=kv version=2
```

### Step 5: Seed Sample Product Configurations

#### GEPL-001 (Government Employee Payroll Loan)

```powershell
vault kv put kv/intellifin/loan-products/GEPL-001/rules `
  productName="Government Employee Payroll Loan" `
  minAmount=1000 `
  maxAmount=50000 `
  minTermMonths=1 `
  maxTermMonths=60 `
  baseInterestRate=0.12 `
  adminFee=0.02 `
  managementFee=0.01 `
  calculatedEAR=0.152 `
  earCapCompliance=true `
  earLimit=0.48 `
  eligibilityRules='{"requiredKycStatus":"Approved","minMonthlyIncome":5000,"maxDtiRatio":0.40,"pmecRegistrationRequired":true}'
```

#### SMEABL-001 (SME Asset-Backed Loan)

```powershell
vault kv put kv/intellifin/loan-products/SMEABL-001/rules `
  productName="SME Asset-Backed Loan" `
  minAmount=10000 `
  maxAmount=500000 `
  minTermMonths=12 `
  maxTermMonths=84 `
  baseInterestRate=0.18 `
  adminFee=0.025 `
  managementFee=0.015 `
  calculatedEAR=0.224 `
  earCapCompliance=true `
  earLimit=0.48 `
  eligibilityRules='{"requiredKycStatus":"Approved","minMonthlyIncome":15000,"maxDtiRatio":0.35,"pmecRegistrationRequired":false}'
```

#### Test Product (Non-Compliant EAR - for testing)

```powershell
# This will be rejected by the service (EAR > 48%)
vault kv put kv/intellifin/loan-products/TEST-NONCOMPLIANT/rules `
  productName="Test Non-Compliant Product" `
  minAmount=1000 `
  maxAmount=10000 `
  minTermMonths=1 `
  maxTermMonths=12 `
  baseInterestRate=0.40 `
  adminFee=0.05 `
  managementFee=0.05 `
  calculatedEAR=0.55 `
  earCapCompliance=false `
  earLimit=0.48 `
  eligibilityRules='{"requiredKycStatus":"Approved","minMonthlyIncome":1000,"maxDtiRatio":0.50,"pmecRegistrationRequired":false}'
```

### Step 6: Verify Configurations

```powershell
# List all product configurations
vault kv list kv/intellifin/loan-products/

# Read specific configuration
vault kv get kv/intellifin/loan-products/GEPL-001/rules

# Read as JSON
vault kv get -format=json kv/intellifin/loan-products/GEPL-001/rules
```

### Step 7: Configure Application

Update `appsettings.Development.json`:

```json
{
  "Vault": {
    "Address": "http://localhost:8200",
    "Token": "hvs.XXXXXXXXXXXXXXXXXXXX"
  }
}
```

**Security Note**: Never commit actual tokens to source control. Use environment variables in production.

### Step 8: Start Application

```powershell
cd "D:\Projects\Intellifin Loan Management System\apps\IntelliFin.LoanOriginationService"
dotnet run
```

**Expected Logs**:
```
info: IntelliFin.LoanOriginationService.Services.LoanProductService[0]
      LoanProductService initialized with Vault configuration

info: IntelliFin.LoanOriginationService.Services.VaultProductConfigService[0]
      Loading product config for GEPL-001 from Vault

info: IntelliFin.LoanOriginationService.Services.VaultProductConfigService[0]
      Product config for GEPL-001 loaded successfully. EAR=15.20%, Limit=48.00%, Compliant=true
```

## Testing Vault Integration

### Test 1: Load Compliant Configuration

```powershell
# Call API to retrieve product
curl http://localhost:5000/api/products/GEPL-001
```

**Expected**: Product loaded successfully with EAR validation passed

### Test 2: Load Non-Compliant Configuration

```powershell
# Attempt to load non-compliant product
curl http://localhost:5000/api/products/TEST-NONCOMPLIANT
```

**Expected**: `ComplianceException` with error message:
```
"Product TEST-NONCOMPLIANT EAR 55.00% exceeds Bank of Zambia limit 48.00%"
```

### Test 3: Cache Behavior

```powershell
# First call - reads from Vault (logs show "Loading from Vault")
curl http://localhost:5000/api/products/GEPL-001

# Second call within 5 minutes - reads from cache (logs show "retrieved from cache")
curl http://localhost:5000/api/products/GEPL-001

# Wait 5 minutes, then call again - cache expired, reads from Vault again
```

### Test 4: Configuration Update

```powershell
# Update configuration in Vault
vault kv put kv/intellifin/loan-products/GEPL-001/rules `
  productName="Government Employee Payroll Loan" `
  minAmount=1000 `
  maxAmount=60000 `
  minTermMonths=1 `
  maxTermMonths=60 `
  baseInterestRate=0.10 `
  adminFee=0.02 `
  managementFee=0.01 `
  calculatedEAR=0.132 `
  earCapCompliance=true `
  earLimit=0.48 `
  eligibilityRules='{"requiredKycStatus":"Approved","minMonthlyIncome":5000,"maxDtiRatio":0.40,"pmecRegistrationRequired":true}'

# Wait 5 minutes for cache to expire
Start-Sleep -Seconds 300

# Verify new config loaded
curl http://localhost:5000/api/products/GEPL-001
# Should show maxAmount=60000, baseInterestRate=0.10
```

### Test 5: Vault Unavailable (Fallback)

```powershell
# Stop Vault server (Ctrl+C in Vault terminal)

# Restart application - should fall back to in-memory
dotnet run
```

**Expected Log**:
```
warn: Vault not configured - LoanProductService will use in-memory configuration
warn: LoanProductService initialized with legacy in-memory configuration
```

## Troubleshooting

### Issue: "Connection refused" error

**Cause**: Vault server not running or wrong address

**Solution**:
```powershell
# Check Vault is running
vault status

# Check address matches appsettings
$env:VAULT_ADDR
# Should be: http://localhost:8200
```

### Issue: "Permission denied" error

**Cause**: Invalid or expired token

**Solution**:
```powershell
# Check token validity
vault token lookup

# If expired, restart Vault in dev mode and get new token
vault server -dev
```

### Issue: "Path not found" error

**Cause**: KV secrets engine not enabled or wrong path

**Solution**:
```powershell
# Check secrets engines
vault secrets list

# Enable KV v2 if missing
vault secrets enable -version=2 -path=kv kv
```

### Issue: "Failed to deserialize" error

**Cause**: Invalid JSON in eligibilityRules field

**Solution**:
```powershell
# Verify JSON syntax
vault kv get -format=json kv/intellifin/loan-products/GEPL-001/rules | ConvertFrom-Json

# Ensure eligibilityRules is valid JSON string
```

## Docker Vault Setup (Alternative)

For a more production-like environment:

```powershell
# Run Vault in Docker
docker run --cap-add=IPC_LOCK `
  -p 8200:8200 `
  -e 'VAULT_DEV_ROOT_TOKEN_ID=dev-token' `
  -e 'VAULT_DEV_LISTEN_ADDRESS=0.0.0.0:8200' `
  hashicorp/vault:1.15

# Use same setup steps as above, with token: dev-token
```

## Production Considerations

### Authentication

**Development**: Token authentication (simple)  
**Production**: Kubernetes ServiceAccount or AppRole (secure)

```hcl
# Example: Kubernetes auth
vault auth enable kubernetes

vault write auth/kubernetes/role/loan-origination \
  bound_service_account_names=loan-origination-service \
  bound_service_account_namespaces=intellifin \
  policies=loan-products-read \
  ttl=1h
```

### Access Policies

```hcl
# loan-products-read.hcl
path "kv/data/intellifin/loan-products/*" {
  capabilities = ["read", "list"]
}

path "kv/metadata/intellifin/loan-products/*" {
  capabilities = ["read", "list"]
}
```

Apply policy:
```bash
vault policy write loan-products-read loan-products-read.hcl
```

### TLS/HTTPS

**Production**: Always use HTTPS with valid certificates

```json
{
  "Vault": {
    "Address": "https://vault.intellifin.local:8200",
    "Token": "<from-k8s-secret>"
  }
}
```

### High Availability

- Use Vault cluster (3+ nodes)
- Configure load balancer
- Enable audit logging
- Regular backups of Vault data

## Cleanup

To stop and clean up local Vault:

```powershell
# Stop Vault server (Ctrl+C)

# Remove environment variables
Remove-Item Env:\VAULT_ADDR
Remove-Item Env:\VAULT_TOKEN

# Dev mode data is automatically cleaned up (ephemeral)
```

## Next Steps

1. Test all product configurations locally
2. Write unit tests with mocked IVaultClient
3. Write integration tests with Testcontainers Vault
4. Deploy Vault to dev/staging environment
5. Configure proper authentication (ServiceAccount/AppRole)
6. Enable audit logging
7. Document production runbook

## References

- [Vault Product Configuration Schema](vault-product-config-schema.md)
- [Configuration Change Workflow](vault-config-change-workflow.md)
- [HashiCorp Vault Documentation](https://www.vaultproject.io/docs)
- [VaultSharp Library](https://github.com/rajanadar/VaultSharp)
