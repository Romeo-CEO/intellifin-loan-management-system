# Story 1.26: Container Image Signing and SBOM Generation

## Story Metadata

| Field | Value |
|-------|-------|
| **Story ID** | 1.26 |
| **Epic** | System Administration Control Plane Enhancement |
| **Phase** | Phase 5: Observability & Infrastructure |
| **Sprint** | Sprint 9 |
| **Story Points** | 13 |
| **Estimated Effort** | 8-12 days |
| **Priority** | P0 (Critical - Security) |
| **Status** | 📋 Backlog |
| **Assigned To** | TBD |
| **Dependencies** | Container registry, Kubernetes cluster, CI/CD pipeline |
| **Blocks** | Production deployments, Supply chain security compliance |

---

## User Story

**As a** Security Engineer,  
**I want** container images cryptographically signed with SBOMs generated and validated,  
**so that** we ensure image integrity, detect vulnerabilities, and meet supply chain security requirements.

---

## Business Value

Container image signing and SBOM generation provide critical security benefits:

- **Supply Chain Security**: Cryptographic verification ensures images haven't been tampered with
- **Vulnerability Management**: SBOM enables tracking of all dependencies and their vulnerabilities
- **Compliance**: Meets regulatory requirements for software supply chain transparency (SLSA, NIST SSDF)
- **Risk Mitigation**: Prevents deployment of unsigned or vulnerable images to production
- **Audit Trail**: Complete provenance tracking from source code to deployed container
- **Incident Response**: Rapid identification of affected services when vulnerabilities are discovered

This story is **critical** for production security and regulatory compliance.

---

## Acceptance Criteria

### AC1: Cosign Installation and Key Management
**Given** Container images need cryptographic signing  
**When** setting up Cosign infrastructure  
**Then**:
- Cosign CLI installed in CI/CD pipeline runners
- Key pair generated for image signing:
  - Private key stored in Vault (path: `secret/cosign/private-key`)
  - Public key stored in Kubernetes ConfigMap
  - Key rotation procedure documented (annual rotation)
- Cosign configured to use keyless signing (optional, Sigstore Fulcio)
- Signing key protected with password/PIN stored in Vault
- Key backup stored in secure offline location

### AC2: CI/CD Pipeline Image Signing
**Given** Container images built in CI/CD pipeline  
**When** image is built and pushed to registry  
**Then**:
- CI/CD pipeline stages:
  1. Build Docker image
  2. Run security scan (Trivy)
  3. Generate SBOM (Syft)
  4. Sign image with Cosign
  5. Attach SBOM to image
  6. Push image to registry
- Signing occurs after successful vulnerability scan
- Signature stored in OCI registry alongside image
- Signature format: `<registry>/<image>:<tag>.sig`
- Pipeline fails if signing fails
- Signing timestamp recorded
- Git commit SHA included in signature metadata

### AC3: SBOM Generation with Syft
**Given** Container images contain dependencies  
**When** generating Software Bill of Materials  
**Then**:
- Syft installed in CI/CD pipeline
- SBOM generated in multiple formats:
  - SPDX JSON (primary format)
  - CycloneDX JSON
  - Syft JSON (detailed format)
- SBOM includes:
  - All OS packages (apt, yum, apk)
  - Application dependencies (NuGet packages for C#)
  - File metadata (paths, hashes)
  - License information
  - Supplier information
- SBOM attached to container image as attestation
- SBOM stored in artifact repository (e.g., MinIO)
- SBOM indexed for vulnerability tracking

### AC4: Vulnerability Scanning with Trivy
**Given** Container images may contain vulnerabilities  
**When** scanning images in CI/CD pipeline  
**Then**:
- Trivy installed in CI/CD pipeline
- Scan types performed:
  - OS package vulnerabilities
  - Application dependency vulnerabilities (NuGet)
  - Misconfigurations (Dockerfile best practices)
  - Secrets scanning (detect leaked credentials)
- Vulnerability severity levels: `CRITICAL`, `HIGH`, `MEDIUM`, `LOW`, `UNKNOWN`
- Pipeline gates:
  - **CRITICAL**: Pipeline fails immediately
  - **HIGH**: Pipeline fails (configurable threshold)
  - **MEDIUM/LOW**: Warning logged, pipeline continues
- Scan results exported to JSON format
- Scan results integrated with vulnerability management system
- Scan results visible in CI/CD pipeline UI

### AC5: Admission Controller for Image Verification
**Given** Unsigned images should not deploy to production  
**When** pod creation is requested in Kubernetes  
**Then**:
- Policy Controller (Kyverno or OPA Gatekeeper) installed
- Admission policy enforces:
  - All images must be signed by trusted key
  - Signature verification performed before pod creation
  - Images from approved registries only
  - Images with CRITICAL vulnerabilities blocked
- Policy exemptions for system namespaces (`kube-system`, `argocd`)
- Policy enforcement mode:
  - **Dev/Staging**: `Audit` mode (log violations, allow deployment)
  - **Production**: `Enforce` mode (block unsigned images)
- Policy violations logged and alerted
- Metrics collected for compliance reporting

### AC6: SBOM Storage and Retrieval API
**Given** SBOMs need to be accessible for audit  
**When** storing and retrieving SBOMs  
**Then**:
- SBOMs stored in MinIO object storage:
  - Bucket: `intellifin-sboms`
  - Path: `<service>/<version>/sbom.spdx.json`
- Admin Service API endpoints:
  - `GET /api/admin/sboms` - List all SBOMs
  - `GET /api/admin/sboms/{service}/{version}` - Retrieve specific SBOM
  - `GET /api/admin/sboms/{service}/{version}/vulnerabilities` - Get vulnerabilities for SBOM
- SBOM metadata stored in database:
  - Service name, version, image digest
  - Generation timestamp, build number
  - Vulnerability counts by severity
- SBOM retention: 3 years (regulatory requirement)
- SBOM access logged for audit

### AC7: Vulnerability Dashboard Integration
**Given** Vulnerabilities need visibility  
**When** displaying vulnerability metrics  
**Then**:
- Admin UI dashboard displays:
  - Total vulnerabilities by severity (pie chart)
  - Vulnerabilities by service (bar chart)
  - Vulnerability trends over time (line chart)
  - Top 10 vulnerable packages (table)
- Vulnerability data refreshed daily
- Critical vulnerability alerts sent to security team
- Vulnerability remediation status tracking
- Integration with Grafana for detailed dashboards

### AC8: Signature Verification in ArgoCD
**Given** ArgoCD deploys signed images  
**When** ArgoCD syncs application  
**Then**:
- ArgoCD webhook configured to verify signatures before sync
- Signature verification uses public key from ConfigMap
- Unsigned images rejected with clear error message
- Signature verification logged in ArgoCD audit trail
- ArgoCD notification sent if signature verification fails
- Manual override available for emergencies (requires approval)

### AC9: Supply Chain Security Compliance Reporting
**Given** Regulatory compliance requires evidence  
**When** generating compliance reports  
**Then**:
- Compliance report includes:
  - All deployed images with signature status
  - SBOM availability for all images
  - Vulnerability scan results
  - Signature verification audit trail
  - Key rotation history
- Report generated monthly (automated)
- Report format: PDF and JSON
- Report stored in document management system
- Report available for auditors
- Compliance metrics tracked: % signed images, % scanned images

---

## Technical Implementation Details

### Architecture Reference

**PRD Sections**: Lines 1258-1282 (Story 1.26), Phase 5 Overview  
**Architecture Sections**: Section 11 (Supply Chain Security), Section 6 (CI/CD Pipeline), Section 9 (Kubernetes)  
**Requirements**: NFR15 (100% signed images in production), NFR16 (Vulnerability scan <5 minutes)

### Technology Stack

- **Image Signing**: Cosign (Sigstore)
- **SBOM Generation**: Syft (Anchore)
- **Vulnerability Scanning**: Trivy (Aqua Security)
- **Admission Control**: Kyverno or OPA Gatekeeper
- **SBOM Storage**: MinIO (S3-compatible)
- **Key Management**: HashiCorp Vault
- **CI/CD**: GitHub Actions, Azure DevOps, GitLab CI
- **Monitoring**: Prometheus, Grafana

### Cosign Setup

```bash
# Install Cosign
VERSION="v2.2.2"
curl -LO "https://github.com/sigstore/cosign/releases/download/${VERSION}/cosign-linux-amd64"
chmod +x cosign-linux-amd64
sudo mv cosign-linux-amd64 /usr/local/bin/cosign

# Generate key pair
cosign generate-key-pair

# Store private key in Vault
vault kv put secret/cosign/private-key \
  key="$(cat cosign.key)" \
  password="<secure-password>"

# Store public key in Kubernetes ConfigMap
kubectl create configmap cosign-public-key \
  --from-file=cosign.pub \
  -n kube-system

# Verify Cosign installation
cosign version
```

### CI/CD Pipeline Integration (GitHub Actions Example)

```yaml
# .github/workflows/build-and-sign.yml
name: Build, Sign, and Push Container Image

on:
  push:
    branches: [main]
    paths:
      - 'src/**'
      - 'Dockerfile'

env:
  REGISTRY: ghcr.io
  IMAGE_NAME: ${{ github.repository }}/identity-service

jobs:
  build-sign-push:
    runs-on: ubuntu-latest
    permissions:
      contents: read
      packages: write
      id-token: write  # For keyless signing

    steps:
      - name: Checkout code
        uses: actions/checkout@v4

      - name: Set up Docker Buildx
        uses: docker/setup-buildx-action@v3

      - name: Log in to Container Registry
        uses: docker/login-action@v3
        with:
          registry: ${{ env.REGISTRY }}
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}

      - name: Extract metadata
        id: meta
        uses: docker/metadata-action@v5
        with:
          images: ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}
          tags: |
            type=ref,event=branch
            type=sha,prefix={{branch}}-
            type=semver,pattern={{version}}

      - name: Build container image
        uses: docker/build-push-action@v5
        with:
          context: .
          push: false
          tags: ${{ steps.meta.outputs.tags }}
          labels: ${{ steps.meta.outputs.labels }}
          cache-from: type=gha
          cache-to: type=gha,mode=max
          load: true

      - name: Install Trivy
        run: |
          curl -sfL https://raw.githubusercontent.com/aquasecurity/trivy/main/contrib/install.sh | sh -s -- -b /usr/local/bin v0.48.3

      - name: Run Trivy vulnerability scan
        id: trivy
        run: |
          trivy image \
            --severity CRITICAL,HIGH \
            --exit-code 1 \
            --format json \
            --output trivy-results.json \
            ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}:${{ steps.meta.outputs.version }}

      - name: Upload Trivy scan results
        uses: actions/upload-artifact@v4
        if: always()
        with:
          name: trivy-results
          path: trivy-results.json

      - name: Install Syft
        run: |
          curl -sSfL https://raw.githubusercontent.com/anchore/syft/main/install.sh | sh -s -- -b /usr/local/bin

      - name: Generate SBOM with Syft
        run: |
          syft packages \
            ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}:${{ steps.meta.outputs.version }} \
            --output spdx-json=sbom.spdx.json \
            --output cyclonedx-json=sbom.cyclonedx.json

      - name: Upload SBOM
        uses: actions/upload-artifact@v4
        with:
          name: sbom
          path: |
            sbom.spdx.json
            sbom.cyclonedx.json

      - name: Push image to registry
        uses: docker/build-push-action@v5
        with:
          context: .
          push: true
          tags: ${{ steps.meta.outputs.tags }}
          labels: ${{ steps.meta.outputs.labels }}

      - name: Install Cosign
        uses: sigstore/cosign-installer@v3
        with:
          cosign-release: 'v2.2.2'

      - name: Retrieve Cosign private key from Vault
        run: |
          # Authenticate to Vault (using GitHub OIDC)
          export VAULT_TOKEN=$(vault write -field=token auth/jwt/login \
            role=github-actions \
            jwt=${{ secrets.GITHUB_TOKEN }})
          
          # Retrieve private key
          vault kv get -field=key secret/cosign/private-key > cosign.key
          vault kv get -field=password secret/cosign/private-key > cosign-password.txt

      - name: Sign container image with Cosign
        env:
          COSIGN_PASSWORD: ${{ secrets.COSIGN_PASSWORD }}
        run: |
          cosign sign --key cosign.key \
            --annotations git_sha=${{ github.sha }} \
            --annotations build_date=$(date -u +%Y-%m-%dT%H:%M:%SZ) \
            --annotations pipeline_run=${{ github.run_id }} \
            ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}@${{ steps.meta.outputs.digest }}

      - name: Attach SBOM to image
        run: |
          cosign attach sbom \
            --sbom sbom.spdx.json \
            ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}@${{ steps.meta.outputs.digest }}

      - name: Verify signature
        run: |
          cosign verify --key cosign.pub \
            ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}@${{ steps.meta.outputs.digest }}

      - name: Store SBOM in MinIO
        env:
          MINIO_ENDPOINT: ${{ secrets.MINIO_ENDPOINT }}
          MINIO_ACCESS_KEY: ${{ secrets.MINIO_ACCESS_KEY }}
          MINIO_SECRET_KEY: ${{ secrets.MINIO_SECRET_KEY }}
        run: |
          aws s3 cp sbom.spdx.json \
            s3://intellifin-sboms/identity-service/${{ steps.meta.outputs.version }}/sbom.spdx.json \
            --endpoint-url $MINIO_ENDPOINT

      - name: Notify security team
        if: failure()
        run: |
          curl -X POST ${{ secrets.SLACK_WEBHOOK_URL }} \
            -H 'Content-Type: application/json' \
            -d '{
              "text": "🚨 Image signing failed for ${{ env.IMAGE_NAME }}:${{ steps.meta.outputs.version }}",
              "blocks": [{
                "type": "section",
                "text": {
                  "type": "mrkdwn",
                  "text": "*Image*: `${{ env.IMAGE_NAME }}:${{ steps.meta.outputs.version }}`\n*Build*: <${{ github.server_url }}/${{ github.repository }}/actions/runs/${{ github.run_id }}|View Pipeline>\n*Status*: ❌ Failed"
                }
              }]
            }'
```

### Kyverno Policy for Image Verification

```yaml
# kyverno-policies/verify-image-signature.yaml
apiVersion: kyverno.io/v1
kind: ClusterPolicy
metadata:
  name: verify-image-signature
  annotations:
    policies.kyverno.io/title: Verify Image Signatures
    policies.kyverno.io/category: Security
    policies.kyverno.io/severity: high
    policies.kyverno.io/description: >-
      Verifies that all container images are signed with Cosign
      before allowing deployment to production namespaces.
spec:
  validationFailureAction: Enforce  # Enforce in production, Audit in dev/staging
  background: true
  failurePolicy: Fail
  
  rules:
    - name: verify-signature
      match:
        any:
          - resources:
              kinds:
                - Pod
              namespaces:
                - production
                - default
      
      exclude:
        any:
          - resources:
              namespaces:
                - kube-system
                - argocd
                - vault
                - prometheus
      
      verifyImages:
        - imageReferences:
            - "ghcr.io/intellifin/*"
            - "intellifin/*"
          
          attestors:
            - count: 1
              entries:
                - keys:
                    publicKeys: |-
                      -----BEGIN PUBLIC KEY-----
                      MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAE...
                      -----END PUBLIC KEY-----
          
          attestations:
            - predicateType: https://spdx.dev/Document
              conditions:
                - all:
                    - key: "{{ components[].name }}"
                      operator: AnyNotIn
                      value: ["log4j-core", "spring-core"]  # Known vulnerable packages
    
    - name: require-sbom
      match:
        any:
          - resources:
              kinds:
                - Pod
              namespaces:
                - production
      
      verifyImages:
        - imageReferences:
            - "ghcr.io/intellifin/*"
          
          attestations:
            - predicateType: https://spdx.dev/Document
              conditions:
                - all:
                    - key: "{{ spdxVersion }}"
                      operator: Equals
                      value: "SPDX-2.3"
---
# kyverno-policies/block-high-severity-vulnerabilities.yaml
apiVersion: kyverno.io/v1
kind: ClusterPolicy
metadata:
  name: block-high-severity-vulnerabilities
spec:
  validationFailureAction: Enforce
  background: false
  
  rules:
    - name: check-vulnerability-scan
      match:
        any:
          - resources:
              kinds:
                - Pod
              namespaces:
                - production
      
      preconditions:
        all:
          - key: "{{request.operation}}"
            operator: Equals
            value: CREATE
      
      validate:
        message: >-
          Container image must not have CRITICAL or HIGH severity vulnerabilities.
          Please remediate vulnerabilities before deployment.
        
        deny:
          conditions:
            any:
              - key: "{{ images.*.annotations.\"trivy.severity.CRITICAL\" }}"
                operator: GreaterThan
                value: 0
              - key: "{{ images.*.annotations.\"trivy.severity.HIGH\" }}"
                operator: GreaterThan
                value: 5  # Allow up to 5 HIGH vulnerabilities
```

### SBOM Management API

```csharp
// Controllers/SBOMController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using IntelliFin.Admin.Services;
using IntelliFin.Admin.Models;

namespace IntelliFin.Admin.Controllers
{
    [ApiController]
    [Route("api/admin/sboms")]
    [Authorize(Roles = "System Administrator,Security Engineer")]
    public class SBOMController : ControllerBase
    {
        private readonly ISBOMService _sbomService;
        private readonly ILogger<SBOMController> _logger;

        public SBOMController(
            ISBOMService sbomService,
            ILogger<SBOMController> logger)
        {
            _sbomService = sbomService;
            _logger = logger;
        }

        /// <summary>
        /// List all SBOMs
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<SBOMSummaryDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> ListSBOMs(
            [FromQuery] string? serviceName = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            CancellationToken cancellationToken = default)
        {
            var result = await _sbomService.ListSBOMsAsync(
                serviceName,
                page,
                pageSize,
                cancellationToken);

            return Ok(result);
        }

        /// <summary>
        /// Get SBOM for specific service version
        /// </summary>
        [HttpGet("{serviceName}/{version}")]
        [ProducesResponseType(typeof(SBOMDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetSBOM(
            string serviceName,
            string version,
            CancellationToken cancellationToken)
        {
            var sbom = await _sbomService.GetSBOMAsync(serviceName, version, cancellationToken);

            if (sbom == null)
                return NotFound();

            return Ok(sbom);
        }

        /// <summary>
        /// Get vulnerabilities for SBOM
        /// </summary>
        [HttpGet("{serviceName}/{version}/vulnerabilities")]
        [ProducesResponseType(typeof(VulnerabilityReportDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetVulnerabilities(
            string serviceName,
            string version,
            CancellationToken cancellationToken)
        {
            var report = await _sbomService.GetVulnerabilitiesAsync(
                serviceName,
                version,
                cancellationToken);

            return Ok(report);
        }

        /// <summary>
        /// Download SBOM file
        /// </summary>
        [HttpGet("{serviceName}/{version}/download")]
        [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
        public async Task<IActionResult> DownloadSBOM(
            string serviceName,
            string version,
            [FromQuery] string format = "spdx",
            CancellationToken cancellationToken = default)
        {
            var sbomContent = await _sbomService.DownloadSBOMAsync(
                serviceName,
                version,
                format,
                cancellationToken);

            if (sbomContent == null)
                return NotFound();

            var contentType = format.ToLower() switch
            {
                "spdx" => "application/json",
                "cyclonedx" => "application/json",
                _ => "application/octet-stream"
            };

            return File(
                sbomContent,
                contentType,
                $"{serviceName}-{version}-sbom.{format}.json");
        }

        /// <summary>
        /// Get vulnerability statistics dashboard data
        /// </summary>
        [HttpGet("statistics")]
        [ProducesResponseType(typeof(VulnerabilityStatisticsDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetStatistics(
            CancellationToken cancellationToken)
        {
            var stats = await _sbomService.GetVulnerabilityStatisticsAsync(cancellationToken);
            return Ok(stats);
        }

        /// <summary>
        /// Generate compliance report
        /// </summary>
        [HttpPost("compliance-report")]
        [ProducesResponseType(typeof(ComplianceReportDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GenerateComplianceReport(
            [FromBody] ComplianceReportRequest request,
            CancellationToken cancellationToken)
        {
            var report = await _sbomService.GenerateComplianceReportAsync(
                request,
                cancellationToken);

            return Ok(report);
        }
    }
}
```

### Database Schema

```sql
-- Admin Service Database

CREATE TABLE ContainerImages (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    ServiceName NVARCHAR(100) NOT NULL,
    Version NVARCHAR(50) NOT NULL,
    ImageDigest NVARCHAR(100) NOT NULL,
    Registry NVARCHAR(200) NOT NULL,
    
    BuildNumber NVARCHAR(50),
    GitCommitSha NVARCHAR(100),
    BuildTimestamp DATETIME2 NOT NULL,
    
    IsSigned BIT NOT NULL DEFAULT 0,
    SignatureVerified BIT NOT NULL DEFAULT 0,
    SignatureTimestamp DATETIME2,
    SignedBy NVARCHAR(100),
    
    HasSBOM BIT NOT NULL DEFAULT 0,
    SBOMPath NVARCHAR(500),  -- MinIO path
    SBOMFormat NVARCHAR(50),  -- SPDX, CycloneDX
    
    VulnerabilityScanCompleted BIT NOT NULL DEFAULT 0,
    VulnerabilityScanTimestamp DATETIME2,
    CriticalCount INT NOT NULL DEFAULT 0,
    HighCount INT NOT NULL DEFAULT 0,
    MediumCount INT NOT NULL DEFAULT 0,
    LowCount INT NOT NULL DEFAULT 0,
    
    DeployedToProduction BIT NOT NULL DEFAULT 0,
    DeploymentTimestamp DATETIME2,
    
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    UNIQUE (ServiceName, Version),
    INDEX IX_ServiceName (ServiceName),
    INDEX IX_IsSigned (IsSigned),
    INDEX IX_VulnerabilityScanCompleted (VulnerabilityScanCompleted),
    INDEX IX_BuildTimestamp (BuildTimestamp DESC)
);

CREATE TABLE Vulnerabilities (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    ContainerImageId BIGINT NOT NULL,
    
    VulnerabilityId NVARCHAR(50) NOT NULL,  -- CVE-2023-12345
    PackageName NVARCHAR(200) NOT NULL,
    InstalledVersion NVARCHAR(100),
    FixedVersion NVARCHAR(100),
    
    Severity NVARCHAR(20) NOT NULL,  -- CRITICAL, HIGH, MEDIUM, LOW
    Description NVARCHAR(MAX),
    PublishedDate DATETIME2,
    
    CVSS3Score DECIMAL(3,1),  -- 0.0 to 10.0
    
    Status NVARCHAR(50) NOT NULL DEFAULT 'Open',  -- Open, Acknowledged, Mitigated, Fixed
    AcknowledgedBy NVARCHAR(100),
    AcknowledgedAt DATETIME2,
    AcknowledgmentComments NVARCHAR(500),
    
    MitigationPlan NVARCHAR(1000),
    TargetFixDate DATE,
    
    FOREIGN KEY (ContainerImageId) REFERENCES ContainerImages(Id),
    INDEX IX_VulnerabilityId (VulnerabilityId),
    INDEX IX_Severity (Severity),
    INDEX IX_Status (Status)
);

CREATE TABLE SignatureVerificationAudit (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    ImageDigest NVARCHAR(100) NOT NULL,
    ServiceName NVARCHAR(100) NOT NULL,
    Version NVARCHAR(50) NOT NULL,
    
    VerificationTimestamp DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    VerificationResult NVARCHAR(50) NOT NULL,  -- Success, Failed, KeyNotFound
    VerificationMethod NVARCHAR(50) NOT NULL,  -- Cosign, Manual
    
    VerifiedBy NVARCHAR(100),  -- System or user
    VerificationContext NVARCHAR(100),  -- Deployment, AdmissionController, Manual
    
    ErrorMessage NVARCHAR(500),
    
    INDEX IX_ImageDigest (ImageDigest),
    INDEX IX_VerificationTimestamp (VerificationTimestamp DESC),
    INDEX IX_VerificationResult (VerificationResult)
);

-- View for vulnerability dashboard
CREATE VIEW vw_VulnerabilityDashboard AS
SELECT 
    ci.ServiceName,
    ci.Version,
    ci.ImageDigest,
    ci.IsSigned,
    ci.HasSBOM,
    ci.CriticalCount,
    ci.HighCount,
    ci.MediumCount,
    ci.LowCount,
    ci.CriticalCount + ci.HighCount + ci.MediumCount + ci.LowCount AS TotalVulnerabilities,
    ci.VulnerabilityScanTimestamp,
    ci.DeployedToProduction
FROM ContainerImages ci
WHERE ci.VulnerabilityScanCompleted = 1
ORDER BY ci.BuildTimestamp DESC;
GO
```

### Grafana Dashboard for Vulnerabilities

```json
{
  "dashboard": {
    "title": "Container Image Security Dashboard",
    "panels": [
      {
        "id": 1,
        "title": "Vulnerabilities by Severity",
        "type": "piechart",
        "targets": [
          {
            "expr": "sum(container_vulnerabilities_total) by (severity)"
          }
        ]
      },
      {
        "id": 2,
        "title": "Unsigned Images in Production",
        "type": "stat",
        "targets": [
          {
            "expr": "count(container_image_signed{deployed_to_production=\"true\",is_signed=\"false\"})"
          }
        ],
        "thresholds": {
          "mode": "absolute",
          "steps": [
            {"value": 0, "color": "green"},
            {"value": 1, "color": "red"}
          ]
        }
      },
      {
        "id": 3,
        "title": "Critical Vulnerabilities Trend",
        "type": "graph",
        "targets": [
          {
            "expr": "sum(container_vulnerabilities_total{severity=\"CRITICAL\"}) by (service_name)"
          }
        ]
      },
      {
        "id": 4,
        "title": "Images Without SBOM",
        "type": "table",
        "targets": [
          {
            "expr": "container_image_sbom_available{has_sbom=\"false\"}"
          }
        ]
      }
    ]
  }
}
```

---

## Integration Verification

### IV1: Image Signing in CI/CD Pipeline
**Verification Steps**:
1. Trigger CI/CD pipeline for Identity Service
2. Verify Trivy vulnerability scan runs
3. Verify Syft generates SBOM
4. Verify Cosign signs image
5. Verify signature attached to image in registry
6. Check SBOM stored in MinIO
7. Verify pipeline completes successfully

**Success Criteria**:
- Image signed with Cosign
- Signature verifiable with public key
- SBOM generated in SPDX format
- No CRITICAL vulnerabilities detected
- Pipeline completes in <10 minutes

### IV2: Admission Controller Blocks Unsigned Images
**Verification Steps**:
1. Attempt to deploy unsigned image to production namespace
2. Verify Kyverno blocks deployment
3. Check admission controller logs for rejection reason
4. Deploy signed image to production
5. Verify deployment succeeds
6. Check signature verification audit log

**Success Criteria**:
- Unsigned image deployment blocked
- Clear error message displayed
- Signed image deployment succeeds
- Audit log records verification

### IV3: Vulnerability Detection and Blocking
**Verification Steps**:
1. Build image with known vulnerability (e.g., log4j 2.14.1)
2. Run Trivy scan
3. Verify CRITICAL vulnerability detected
4. Verify pipeline fails
5. Update dependency to patched version
6. Rebuild and verify pipeline succeeds
7. Check vulnerability count in database

**Success Criteria**:
- CRITICAL vulnerabilities detected
- Pipeline fails with clear error
- Vulnerability details logged
- Fixed image passes scan

---

## Testing Strategy

### Unit Tests

```csharp
[Fact]
public async Task GetSBOM_ExistingVersion_ReturnsSBOM()
{
    // Arrange
    var service = CreateSBOMService();
    
    // Act
    var sbom = await service.GetSBOMAsync("identity-service", "v1.2.3", CancellationToken.None);

    // Assert
    Assert.NotNull(sbom);
    Assert.Equal("identity-service", sbom.ServiceName);
    Assert.Equal("v1.2.3", sbom.Version);
    Assert.True(sbom.Components.Count > 0);
}

[Fact]
public async Task GetVulnerabilities_ImageWithCritical_ReturnsVulnerabilities()
{
    // Arrange
    var service = CreateSBOMService();

    // Act
    var report = await service.GetVulnerabilitiesAsync("loan-service", "v2.1.0", CancellationToken.None);

    // Assert
    Assert.NotNull(report);
    Assert.True(report.CriticalCount > 0);
    Assert.Contains(report.Vulnerabilities, v => v.Severity == "CRITICAL");
}
```

### Integration Tests

```bash
#!/bin/bash
# test-image-signing.sh

echo "Testing image signing workflow..."

# Test 1: Build and sign image
echo "Test 1: Build and sign image"
docker build -t test-image:latest .
cosign sign --key cosign.key test-image:latest

# Test 2: Verify signature
echo "Test 2: Verify signature"
cosign verify --key cosign.pub test-image:latest
if [ $? -eq 0 ]; then
  echo "✅ Signature verification passed"
else
  echo "❌ Signature verification failed"
  exit 1
fi

# Test 3: Generate SBOM
echo "Test 3: Generate SBOM"
syft packages test-image:latest -o spdx-json=sbom.spdx.json
if [ -f sbom.spdx.json ]; then
  echo "✅ SBOM generated"
else
  echo "❌ SBOM generation failed"
  exit 1
fi

# Test 4: Run vulnerability scan
echo "Test 4: Run vulnerability scan"
trivy image --severity CRITICAL,HIGH test-image:latest
if [ $? -eq 0 ]; then
  echo "✅ No critical vulnerabilities found"
else
  echo "⚠️ Critical vulnerabilities detected"
fi

echo "All tests completed! ✅"
```

---

## Risks and Mitigation

| Risk | Impact | Probability | Mitigation |
|------|---------|-------------|------------|
| Private key compromise | All signatures invalid, untrusted images | Low | Store key in Vault. Rotate keys annually. Implement keyless signing (Fulcio). Monitor key access. |
| Cosign unavailable in CI/CD | Pipeline fails, deployments blocked | Low | Cache Cosign binary. Use multiple signing methods. Emergency manual signing procedure. |
| False positive vulnerabilities | Pipeline blocks valid deployments | Medium | Implement vulnerability acknowledgment workflow. Use vulnerability exceptions list. Review Trivy database updates. |
| SBOM generation performance | Slow CI/CD pipeline | Medium | Optimize Syft scanning. Cache dependency analysis. Run SBOM generation in parallel. |
| Admission controller misconfiguration | Production outage | Low | Test policies in staging first. Implement audit mode before enforce. Monitor policy violations. |

---

## Definition of Done

- [ ] Cosign installed in CI/CD pipeline
- [ ] Key pair generated and stored in Vault
- [ ] CI/CD pipeline updated with signing steps
- [ ] Trivy vulnerability scanning integrated
- [ ] Syft SBOM generation integrated
- [ ] Kyverno admission policies deployed
- [ ] MinIO bucket created for SBOM storage
- [ ] SBOM API endpoints implemented
- [ ] Database schema created
- [ ] Vulnerability dashboard in Admin UI
- [ ] Grafana dashboards configured
- [ ] Integration tests: Signing, verification, SBOM
- [ ] Performance test: Pipeline <10 minutes
- [ ] Security review: Key management, policies
- [ ] Documentation: Signing procedures, troubleshooting
- [ ] Training materials for DevOps and security teams

---

## Related Documentation

### PRD References
- **Lines 1258-1282**: Story 1.26 detailed requirements
- **Lines 1244-1408**: Phase 5 (Observability & Infrastructure) overview
- **NFR15**: 100% signed images in production
- **NFR16**: Vulnerability scan <5 minutes

### Architecture References
- **Section 11**: Supply Chain Security
- **Section 6**: CI/CD Pipeline
- **Section 9**: Kubernetes Infrastructure

### External Documentation
- [Cosign Documentation](https://docs.sigstore.dev/cosign/overview/)
- [Syft Documentation](https://github.com/anchore/syft)
- [Trivy Documentation](https://aquasecurity.github.io/trivy/)
- [Kyverno Image Verification](https://kyverno.io/docs/writing-policies/verify-images/)
- [SLSA Framework](https://slsa.dev/)

---

## Notes for Development Team

### Pre-Implementation Checklist
- [ ] Generate Cosign key pair securely
- [ ] Configure Vault access for CI/CD
- [ ] Set up MinIO bucket with proper access controls
- [ ] Test Trivy database updates
- [ ] Plan key rotation schedule (annual)
- [ ] Document emergency signing procedures
- [ ] Create vulnerability exception process
- [ ] Set up PagerDuty alerts for critical vulnerabilities

### Post-Implementation Handoff
- [ ] Train DevOps team on signing workflow
- [ ] Train security team on vulnerability management
- [ ] Create runbook for key rotation
- [ ] Document vulnerability remediation SLA
- [ ] Schedule monthly compliance report reviews
- [ ] Set up weekly vulnerability review meetings
- [ ] Create incident response plan for key compromise
- [ ] Establish metrics for supply chain security KPIs

### Technical Debt / Future Enhancements
- [ ] Implement keyless signing with Sigstore Fulcio
- [ ] Add SLSA provenance attestations
- [ ] Integrate with vulnerability management platform (Snyk, WhiteSource)
- [ ] Implement automated vulnerability patching
- [ ] Add license compliance scanning
- [ ] Create SBOM comparison tool (detect drift)
- [ ] Implement continuous vulnerability monitoring
- [ ] Add image provenance chain visualization

---

**Story Created**: 2025-10-11  
**Last Updated**: 2025-10-11  
**Next Story**: [Story 1.27: Bastion Host Deployment with PAM](./story-1.27-bastion-pam.md)
