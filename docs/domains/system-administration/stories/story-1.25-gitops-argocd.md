# Story 1.25: GitOps Configuration Deployment with ArgoCD

## Story Metadata

| Field | Value |
|-------|-------|
| **Story ID** | 1.25 |
| **Epic** | System Administration Control Plane Enhancement |
| **Phase** | Phase 4: Governance & Workflows |
| **Sprint** | Sprint 8-9 |
| **Story Points** | 13 |
| **Estimated Effort** | 8-12 days |
| **Priority** | P1 (High - Infrastructure) |
| **Status** | 📋 Backlog |
| **Assigned To** | TBD |
| **Dependencies** | Kubernetes cluster, Git repository, Story 1.22 (Config management) |
| **Blocks** | Automated deployments, Configuration drift detection |

---

## User Story

**As a** DevOps Engineer,  
**I want** infrastructure and application configurations deployed via GitOps with ArgoCD,  
**so that** all changes are version-controlled, auditable, and automatically synced to Kubernetes.

---

## Business Value

GitOps with ArgoCD provides critical operational and security benefits:

- **Declarative Infrastructure**: All Kubernetes manifests stored in Git as the single source of truth
- **Automated Deployment**: Changes to Git automatically synced to cluster (no manual kubectl apply)
- **Audit Trail**: Complete Git history shows who changed what and when
- **Rollback Capability**: One-click rollback to any previous Git commit
- **Drift Detection**: Automatically detects and corrects manual changes to cluster
- **Multi-Environment Support**: Separate branches/overlays for dev, staging, production
- **Compliance**: Meets change management requirements with approval workflows

This story is **essential** for production-grade infrastructure automation and operational excellence.

---

## Acceptance Criteria

### AC1: ArgoCD Installation and Configuration
**Given** Kubernetes cluster is available  
**When** installing ArgoCD  
**Then**:
- ArgoCD installed via Helm chart in `argocd` namespace
- ArgoCD components deployed:
  - `argocd-server`: UI and API server
  - `argocd-repo-server`: Git repository service
  - `argocd-application-controller`: Sync controller
  - `argocd-dex-server`: SSO/OIDC authentication
  - `argocd-redis`: Cache and queue
- ArgoCD CLI installed on admin workstations
- Initial admin password retrieved and changed
- ArgoCD UI accessible via ingress: `https://argocd.intellifin.local`
- TLS certificate configured (Let's Encrypt or internal CA)

### AC2: Git Repository Structure for GitOps
**Given** GitOps requires version-controlled manifests  
**When** organizing Git repository  
**Then**:
- Git repository created: `intellifin-k8s-manifests`
- Directory structure:
  ```
  intellifin-k8s-manifests/
  ├── base/                      # Base manifests (environment-agnostic)
  │   ├── identity-service/
  │   │   ├── deployment.yaml
  │   │   ├── service.yaml
  │   │   ├── configmap.yaml
  │   │   └── kustomization.yaml
  │   ├── loan-service/
  │   ├── admin-service/
  │   └── api-gateway/
  ├── overlays/                  # Environment-specific overlays
  │   ├── dev/
  │   │   ├── kustomization.yaml
  │   │   └── patches/
  │   ├── staging/
  │   │   ├── kustomization.yaml
  │   │   └── patches/
  │   └── production/
  │       ├── kustomization.yaml
  │       └── patches/
  ├── infrastructure/            # Infrastructure components
  │   ├── vault/
  │   ├── prometheus/
  │   ├── grafana/
  │   └── ingress-nginx/
  └── argocd-apps/               # ArgoCD Application manifests
      ├── identity-service.yaml
      ├── loan-service.yaml
      └── infrastructure.yaml
  ```
- Kustomize used for overlay management
- Sensitive data excluded (stored in Vault, referenced via external-secrets)

### AC3: ArgoCD Application Definitions
**Given** Services need to be managed by ArgoCD  
**When** creating ArgoCD Application resources  
**Then**:
- Application manifest for Identity Service:
  ```yaml
  apiVersion: argoproj.io/v1alpha1
  kind: Application
  metadata:
    name: identity-service-production
    namespace: argocd
  spec:
    project: intellifin-production
    source:
      repoURL: https://github.com/intellifin/intellifin-k8s-manifests.git
      targetRevision: main
      path: overlays/production/identity-service
    destination:
      server: https://kubernetes.default.svc
      namespace: default
    syncPolicy:
      automated:
        prune: true
        selfHeal: true
        allowEmpty: false
      syncOptions:
        - CreateNamespace=true
        - PruneLast=true
      retry:
        limit: 5
        backoff:
          duration: 5s
          factor: 2
          maxDuration: 3m
    revisionHistoryLimit: 10
  ```
- Applications created for all microservices (Identity, Loan, Admin, API Gateway)
- Applications created for infrastructure components (Vault, Prometheus, Grafana)
- Application health checks configured

### AC4: Auto-Sync and Self-Healing Configuration
**Given** Changes pushed to Git should auto-deploy  
**When** configuring sync policies  
**Then**:
- Auto-sync enabled: Changes in Git automatically synced to cluster within 3 minutes
- Self-heal enabled: Manual changes to cluster automatically reverted
- Prune enabled: Resources removed from Git are deleted from cluster
- Sync options configured:
  - `CreateNamespace=true`: Automatically create target namespace
  - `PruneLast=true`: Delete resources after new ones are created
  - `RespectIgnoreDifferences=true`: Ignore specified fields (e.g., replicas for HPA)
- Retry policy: 5 attempts with exponential backoff (5s, 10s, 20s, 40s, 80s)
- Sync timeout: 5 minutes

### AC5: Health Checks and Sync Status
**Given** Deployments need health validation  
**When** ArgoCD checks application health  
**Then**:
- Health assessment for resources:
  - **Deployment**: `Healthy` if all replicas ready
  - **StatefulSet**: `Healthy` if all replicas ready
  - **Service**: `Healthy` if endpoints exist
  - **Ingress**: `Healthy` if backend service exists
  - **Job**: `Healthy` if completed successfully
  - **PersistentVolumeClaim**: `Healthy` if bound
- Custom health checks for CRDs (e.g., Vault, Prometheus)
- Sync status indicators:
  - **Synced**: Git matches cluster
  - **OutOfSync**: Drift detected
  - **Progressing**: Sync in progress
  - **Degraded**: Health checks failing
- Health status visible in ArgoCD UI and CLI
- Prometheus metrics for sync status

### AC6: Rollback and Revision History
**Given** Deployments may need rollback  
**When** using ArgoCD rollback features  
**Then**:
- Revision history retained: Last 10 Git commits
- ArgoCD UI displays sync history with:
  - Git commit SHA
  - Commit message
  - Author
  - Timestamp
  - Sync status
- One-click rollback to any previous revision
- Rollback CLI command: `argocd app rollback <app-name> <revision>`
- Rollback creates new sync operation (doesn't modify Git)
- Rollback audit event logged
- Rollback notification sent to DevOps team

### AC7: Multi-Environment Management
**Given** Multiple environments (dev, staging, production) exist  
**When** managing environment-specific configurations  
**Then**:
- ArgoCD Projects created per environment:
  - `intellifin-dev`: Dev environment applications
  - `intellifin-staging`: Staging environment applications
  - `intellifin-production`: Production environment applications
- Project restrictions:
  - Source repos: Only `intellifin-k8s-manifests` allowed
  - Destination clusters: Only assigned cluster allowed
  - Resource whitelist: Define allowed Kubernetes resources
  - RBAC: Role-based access per project
- Overlays customize per environment:
  - Dev: 1 replica, debug logging, mock external services
  - Staging: 2 replicas, info logging, staging external services
  - Production: 3+ replicas, warning logging, production external services, resource limits
- Branch strategy:
  - `main`: Production manifests
  - `staging`: Staging manifests
  - `develop`: Dev manifests (optional)

### AC8: Notifications and Alerting
**Given** Deployment events need visibility  
**When** ArgoCD triggers notifications  
**Then**:
- Notification integrations configured:
  - **Slack**: Deployment status updates to `#devops-deployments` channel
  - **Email**: Critical failures to DevOps team
  - **Webhook**: Events sent to Admin Service for audit logging
- Notification triggers:
  - Sync started
  - Sync succeeded
  - Sync failed (with error details)
  - Health status degraded
  - Out-of-sync detected
- Notification templates:
  ```
  🚀 Deployment: identity-service-production
  Status: ✅ Synced
  Revision: abc123f (main)
  Commit: "Add rate limiting to API gateway"
  Author: john.doe@intellifin.com
  Duration: 2m 15s
  ```
- Prometheus AlertManager integration for critical failures

### AC9: SSO Integration with Keycloak
**Given** Users need to access ArgoCD UI  
**When** configuring authentication  
**Then**:
- Keycloak configured as OIDC provider
- ArgoCD Dex server configured with Keycloak connector
- RBAC policies mapped to Keycloak groups:
  - `argocd-admins` (Keycloak group) → `role:admin` (ArgoCD)
  - `argocd-developers` (Keycloak group) → `role:developer` (ArgoCD)
  - `argocd-viewers` (Keycloak group) → `role:readonly` (ArgoCD)
- Role permissions:
  - **Admin**: Full access to all projects and applications
  - **Developer**: Deploy to dev/staging, read-only for production
  - **Viewer**: Read-only access to all applications
- SSO login flow tested and documented
- Session timeout: 8 hours

---

## Technical Implementation Details

### Architecture Reference

**PRD Sections**: Lines 1233-1257 (Story 1.25), Phase 4 Overview  
**Architecture Sections**: Section 9 (Kubernetes Infrastructure), Section 8 (GitOps), Story 1.22 (Config Management)  
**Requirements**: NFR12 (Config deployment <5 minutes), NFR14 (Automated drift correction)

### Technology Stack

- **GitOps Tool**: ArgoCD 2.9+
- **Version Control**: Git (GitHub, GitLab, Azure DevOps)
- **Configuration Management**: Kustomize
- **Container Registry**: Docker Hub, Azure Container Registry
- **Secrets Management**: Vault with external-secrets-operator
- **Monitoring**: Prometheus, Grafana
- **Notifications**: Slack, Email, Webhooks

### ArgoCD Installation

```bash
# Install ArgoCD via Helm
helm repo add argo https://argoproj.github.io/argo-helm
helm repo update

# Create namespace
kubectl create namespace argocd

# Install ArgoCD with custom values
helm install argocd argo/argo-cd \
  --namespace argocd \
  --version 5.51.6 \
  --values argocd-values.yaml

# Wait for ArgoCD to be ready
kubectl wait --for=condition=available --timeout=600s \
  deployment/argocd-server -n argocd

# Get initial admin password
kubectl -n argocd get secret argocd-initial-admin-secret \
  -o jsonpath="{.data.password}" | base64 -d

# Install ArgoCD CLI
curl -sSL -o argocd https://github.com/argoproj/argo-cd/releases/latest/download/argocd-linux-amd64
chmod +x argocd
sudo mv argocd /usr/local/bin/

# Login via CLI
argocd login argocd.intellifin.local --username admin --password <initial-password>

# Change admin password
argocd account update-password
```

### ArgoCD Configuration

```yaml
# argocd-values.yaml
global:
  domain: argocd.intellifin.local

server:
  ingress:
    enabled: true
    ingressClassName: nginx
    annotations:
      cert-manager.io/cluster-issuer: letsencrypt-prod
      nginx.ingress.kubernetes.io/ssl-redirect: "true"
      nginx.ingress.kubernetes.io/backend-protocol: "HTTPS"
    hosts:
      - argocd.intellifin.local
    tls:
      - secretName: argocd-tls
        hosts:
          - argocd.intellifin.local
  
  config:
    url: https://argocd.intellifin.local
    
    # OIDC configuration (Keycloak)
    oidc.config: |
      name: Keycloak
      issuer: https://keycloak.intellifin.local/realms/intellifin
      clientID: argocd
      clientSecret: $oidc.keycloak.clientSecret
      requestedScopes:
        - openid
        - profile
        - email
        - groups
    
    # Repository credentials
    repositories: |
      - url: https://github.com/intellifin/intellifin-k8s-manifests.git
        name: intellifin-k8s-manifests
        type: git
        username: not-used
        password: $repos.github.token
    
    # Resource customizations (health checks)
    resource.customizations: |
      argoproj.io/Application:
        health.lua: |
          hs = {}
          hs.status = "Progressing"
          hs.message = ""
          if obj.status ~= nil then
            if obj.status.health ~= nil then
              hs.status = obj.status.health.status
              hs.message = obj.status.health.message
            end
          end
          return hs

  rbacConfig:
    policy.default: role:readonly
    policy.csv: |
      # Admins - full access
      g, argocd-admins, role:admin
      
      # Developers - deploy to dev/staging, read production
      p, role:developer, applications, *, intellifin-dev/*, allow
      p, role:developer, applications, *, intellifin-staging/*, allow
      p, role:developer, applications, get, intellifin-production/*, allow
      p, role:developer, projects, get, *, allow
      g, argocd-developers, role:developer
      
      # Viewers - read-only
      p, role:viewer, applications, get, */*, allow
      p, role:viewer, projects, get, *, allow
      g, argocd-viewers, role:viewer

notifications:
  enabled: true
  argocdUrl: https://argocd.intellifin.local
  
  notifiers:
    service.slack: |
      token: $slack-token
    
    service.email: |
      host: smtp.sendgrid.net
      port: 587
      username: apikey
      password: $email-password
      from: argocd@intellifin.com
  
  templates:
    template.app-deployed: |
      message: |
        🚀 Deployment: {{.app.metadata.name}}
        Status: {{.app.status.sync.status}}
        Revision: {{.app.status.sync.revision}}
        {{if eq .app.status.health.status "Healthy"}}✅{{else}}❌{{end}} Health: {{.app.status.health.status}}
      slack:
        attachments: |
          [{
            "title": "{{ .app.metadata.name}}",
            "title_link":"{{.context.argocdUrl}}/applications/{{.app.metadata.name}}",
            "color": "#18be52",
            "fields": [
              {"title": "Sync Status", "value": "{{.app.status.sync.status}}", "short": true},
              {"title": "Health", "value": "{{.app.status.health.status}}", "short": true}
            ]
          }]
  
  triggers:
    trigger.on-deployed: |
      - when: app.status.sync.status == 'Synced'
        send: [app-deployed]
    
    trigger.on-health-degraded: |
      - when: app.status.health.status == 'Degraded'
        send: [app-health-degraded]
    
    trigger.on-sync-failed: |
      - when: app.status.sync.status == 'Failed'
        send: [app-sync-failed]

controller:
  metrics:
    enabled: true
    serviceMonitor:
      enabled: true

repoServer:
  metrics:
    enabled: true
    serviceMonitor:
      enabled: true

dex:
  enabled: true

redis:
  enabled: true
```

### Git Repository Structure

```bash
# Create Git repository structure
mkdir -p intellifin-k8s-manifests
cd intellifin-k8s-manifests

# Base directory structure
mkdir -p base/{identity-service,loan-service,admin-service,api-gateway}
mkdir -p overlays/{dev,staging,production}
mkdir -p infrastructure/{vault,prometheus,grafana,ingress-nginx}
mkdir -p argocd-apps

# Initialize Git
git init
git remote add origin https://github.com/intellifin/intellifin-k8s-manifests.git
```

### Base Manifests (Identity Service Example)

```yaml
# base/identity-service/deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: identity-service
  labels:
    app: identity-service
    version: v1.0.0
spec:
  replicas: 2
  selector:
    matchLabels:
      app: identity-service
  template:
    metadata:
      labels:
        app: identity-service
        version: v1.0.0
      annotations:
        prometheus.io/scrape: "true"
        prometheus.io/port: "8080"
        prometheus.io/path: "/metrics"
    spec:
      serviceAccountName: identity-service
      containers:
      - name: identity-service
        image: intellifin/identity-service:latest
        imagePullPolicy: Always
        ports:
        - containerPort: 8080
          name: http
        - containerPort: 8081
          name: metrics
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: Production
        - name: DATABASE_CREDENTIALS_PATH
          value: /vault/secrets/database-credentials
        envFrom:
        - configMapRef:
            name: identity-service-config
        resources:
          requests:
            memory: "512Mi"
            cpu: "250m"
          limits:
            memory: "1Gi"
            cpu: "500m"
        livenessProbe:
          httpGet:
            path: /health/live
            port: 8080
          initialDelaySeconds: 30
          periodSeconds: 10
          timeoutSeconds: 5
          failureThreshold: 3
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 8080
          initialDelaySeconds: 10
          periodSeconds: 5
          timeoutSeconds: 3
          failureThreshold: 3
        volumeMounts:
        - name: vault-secrets
          mountPath: /vault/secrets
          readOnly: true
      volumes:
      - name: vault-secrets
        emptyDir:
          medium: Memory
---
# base/identity-service/service.yaml
apiVersion: v1
kind: Service
metadata:
  name: identity-service
  labels:
    app: identity-service
spec:
  type: ClusterIP
  ports:
  - port: 80
    targetPort: 8080
    protocol: TCP
    name: http
  - port: 8081
    targetPort: 8081
    protocol: TCP
    name: metrics
  selector:
    app: identity-service
---
# base/identity-service/configmap.yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: identity-service-config
data:
  ASPNETCORE_URLS: "http://+:8080"
  Logging__LogLevel__Default: "Information"
  Logging__LogLevel__Microsoft: "Warning"
  ConnectionStrings__IdentityDb: "Server=identity-db.database.windows.net;Database=IdentityDb;TrustServerCertificate=true"
  Jwt__Issuer: "https://identity.intellifin.com"
  Jwt__Audience: "https://api.intellifin.com"
  Vault__Address: "http://vault.vault.svc.cluster.local:8200"
---
# base/identity-service/kustomization.yaml
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization

resources:
  - deployment.yaml
  - service.yaml
  - configmap.yaml

commonLabels:
  app: identity-service
  managed-by: argocd

namespace: default
```

### Environment Overlays

```yaml
# overlays/production/identity-service/kustomization.yaml
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization

namespace: production

resources:
  - ../../../base/identity-service

replicas:
  - name: identity-service
    count: 3

images:
  - name: intellifin/identity-service
    newTag: v1.2.3

patches:
  - patch: |-
      - op: replace
        path: /spec/template/spec/containers/0/resources/requests/memory
        value: 1Gi
      - op: replace
        path: /spec/template/spec/containers/0/resources/limits/memory
        value: 2Gi
      - op: replace
        path: /spec/template/spec/containers/0/resources/requests/cpu
        value: 500m
      - op: replace
        path: /spec/template/spec/containers/0/resources/limits/cpu
        value: 1000m
    target:
      kind: Deployment
      name: identity-service

configMapGenerator:
  - name: identity-service-config
    behavior: merge
    literals:
      - Logging__LogLevel__Default=Warning
      - Logging__LogLevel__Microsoft=Error

commonLabels:
  environment: production
```

### ArgoCD Application Manifest

```yaml
# argocd-apps/identity-service-production.yaml
apiVersion: argoproj.io/v1alpha1
kind: Application
metadata:
  name: identity-service-production
  namespace: argocd
  finalizers:
    - resources-finalizer.argocd.argoproj.io
spec:
  project: intellifin-production
  
  source:
    repoURL: https://github.com/intellifin/intellifin-k8s-manifests.git
    targetRevision: main
    path: overlays/production/identity-service
  
  destination:
    server: https://kubernetes.default.svc
    namespace: production
  
  syncPolicy:
    automated:
      prune: true
      selfHeal: true
      allowEmpty: false
    syncOptions:
      - CreateNamespace=true
      - PruneLast=true
    retry:
      limit: 5
      backoff:
        duration: 5s
        factor: 2
        maxDuration: 3m
  
  revisionHistoryLimit: 10
  
  ignoreDifferences:
    - group: apps
      kind: Deployment
      jsonPointers:
        - /spec/replicas  # Ignore replicas if HPA is used
---
# argocd-apps/app-of-apps.yaml (Application of Applications pattern)
apiVersion: argoproj.io/v1alpha1
kind: Application
metadata:
  name: intellifin-production-apps
  namespace: argocd
spec:
  project: intellifin-production
  
  source:
    repoURL: https://github.com/intellifin/intellifin-k8s-manifests.git
    targetRevision: main
    path: argocd-apps
  
  destination:
    server: https://kubernetes.default.svc
    namespace: argocd
  
  syncPolicy:
    automated:
      prune: true
      selfHeal: true
```

### ArgoCD Project Definition

```yaml
# argocd-projects/intellifin-production.yaml
apiVersion: argoproj.io/v1alpha1
kind: AppProject
metadata:
  name: intellifin-production
  namespace: argocd
spec:
  description: IntelliFinLMS Production Applications
  
  sourceRepos:
    - https://github.com/intellifin/intellifin-k8s-manifests.git
  
  destinations:
    - namespace: production
      server: https://kubernetes.default.svc
    - namespace: default
      server: https://kubernetes.default.svc
  
  clusterResourceWhitelist:
    - group: '*'
      kind: '*'
  
  namespaceResourceWhitelist:
    - group: '*'
      kind: '*'
  
  roles:
    - name: production-admin
      description: Full access to production project
      policies:
        - p, proj:intellifin-production:production-admin, applications, *, intellifin-production/*, allow
      groups:
        - argocd-admins
    
    - name: production-readonly
      description: Read-only access to production project
      policies:
        - p, proj:intellifin-production:production-readonly, applications, get, intellifin-production/*, allow
      groups:
        - argocd-developers
        - argocd-viewers
```

### CLI Operations

```bash
# List all applications
argocd app list

# Get application details
argocd app get identity-service-production

# Sync application manually
argocd app sync identity-service-production

# Rollback to previous revision
argocd app rollback identity-service-production 5

# View sync history
argocd app history identity-service-production

# Get application manifests
argocd app manifests identity-service-production

# Diff between Git and cluster
argocd app diff identity-service-production

# Delete application (with cascade delete of resources)
argocd app delete identity-service-production --cascade

# Create application from manifest
kubectl apply -f argocd-apps/identity-service-production.yaml

# Monitor sync progress
argocd app wait identity-service-production --health --timeout 300
```

### Prometheus Monitoring

```yaml
# prometheus-argocd-rules.yaml
apiVersion: monitoring.coreos.com/v1
kind: PrometheusRule
metadata:
  name: argocd-alerts
  namespace: argocd
spec:
  groups:
  - name: argocd.rules
    interval: 30s
    rules:
    - alert: ArgoCDAppOutOfSync
      expr: argocd_app_info{sync_status!="Synced"} == 1
      for: 10m
      labels:
        severity: warning
      annotations:
        summary: "ArgoCD application out of sync"
        description: "Application {{ $labels.name }} is out of sync for more than 10 minutes"
    
    - alert: ArgoCDAppUnhealthy
      expr: argocd_app_info{health_status!="Healthy"} == 1
      for: 5m
      labels:
        severity: critical
      annotations:
        summary: "ArgoCD application unhealthy"
        description: "Application {{ $labels.name }} health status is {{ $labels.health_status }}"
    
    - alert: ArgoCDSyncFailed
      expr: argocd_app_sync_total{phase="Error"} > 0
      for: 1m
      labels:
        severity: critical
      annotations:
        summary: "ArgoCD sync failed"
        description: "Application {{ $labels.name }} sync failed"
```

### Integration with Admin Service

```csharp
// Services/ArgoCDIntegrationService.cs
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace IntelliFin.Admin.Services
{
    public interface IArgoCDIntegrationService
    {
        Task<List<ArgoCDApplicationDto>> GetApplicationsAsync(CancellationToken cancellationToken);
        Task<ArgoCDApplicationDto> GetApplicationAsync(string appName, CancellationToken cancellationToken);
        Task SyncApplicationAsync(string appName, CancellationToken cancellationToken);
        Task RollbackApplicationAsync(string appName, int revision, CancellationToken cancellationToken);
    }

    public class ArgoCDIntegrationService : IArgoCDIntegrationService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ArgoCDIntegrationService> _logger;

        public ArgoCDIntegrationService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<ArgoCDIntegrationService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;

            var argoCDUrl = _configuration["ArgoCD:Url"];
            var authToken = _configuration["ArgoCD:Token"];

            _httpClient.BaseAddress = new Uri(argoCDUrl);
            _httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", authToken);
        }

        public async Task<List<ArgoCDApplicationDto>> GetApplicationsAsync(
            CancellationToken cancellationToken)
        {
            var response = await _httpClient.GetAsync("/api/v1/applications", cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<ArgoCDApplicationList>(content);

            return result?.Items?.Select(app => new ArgoCDApplicationDto
            {
                Name = app.Metadata.Name,
                Namespace = app.Metadata.Namespace,
                SyncStatus = app.Status.Sync.Status,
                HealthStatus = app.Status.Health.Status,
                Revision = app.Status.Sync.Revision
            }).ToList() ?? new List<ArgoCDApplicationDto>();
        }

        public async Task SyncApplicationAsync(
            string appName,
            CancellationToken cancellationToken)
        {
            var payload = new
            {
                prune = false,
                dryRun = false,
                strategy = new
                {
                    hook = new { }
                }
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"/api/v1/applications/{appName}/sync",
                payload,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            _logger.LogInformation("ArgoCD application sync triggered: {AppName}", appName);
        }

        public async Task RollbackApplicationAsync(
            string appName,
            int revision,
            CancellationToken cancellationToken)
        {
            var payload = new
            {
                id = revision
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"/api/v1/applications/{appName}/rollback",
                payload,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            _logger.LogInformation(
                "ArgoCD application rolled back: {AppName}, Revision={Revision}",
                appName, revision);
        }
    }
}
```

---

## Integration Verification

### IV1: ArgoCD Installation and UI Access
**Verification Steps**:
1. Deploy ArgoCD via Helm with custom values
2. Verify all ArgoCD pods running in `argocd` namespace
3. Access ArgoCD UI at `https://argocd.intellifin.local`
4. Login with admin credentials
5. Verify TLS certificate valid
6. Check ArgoCD version and components

**Success Criteria**:
- ArgoCD UI accessible via HTTPS
- All pods in `Running` state
- Admin login successful
- TLS certificate valid (no browser warnings)

### IV2: Application Deployment via GitOps
**Verification Steps**:
1. Push Identity Service manifests to Git (`main` branch)
2. Create ArgoCD Application manifest
3. Apply Application manifest: `kubectl apply -f identity-service-production.yaml`
4. Wait for auto-sync (max 3 minutes)
5. Verify Identity Service deployed to cluster
6. Check ArgoCD UI shows "Synced" and "Healthy"
7. Verify all resources created (Deployment, Service, ConfigMap)

**Success Criteria**:
- Application synced automatically within 3 minutes
- Health status: `Healthy`
- Sync status: `Synced`
- Identity Service pods running

### IV3: Auto-Sync and Drift Correction
**Verification Steps**:
1. Manually edit Identity Service Deployment: `kubectl edit deployment identity-service -n production`
2. Change replica count from 3 to 5
3. Wait up to 5 minutes for ArgoCD self-heal
4. Verify ArgoCD detects drift (`OutOfSync`)
5. Verify ArgoCD auto-corrects (self-heal)
6. Check replica count reverted to 3
7. Verify sync status: `Synced`

**Success Criteria**:
- Drift detected within 3 minutes
- Self-heal triggered automatically
- Cluster state matches Git within 5 minutes
- ArgoCD UI shows sync event

### IV4: Rollback to Previous Revision
**Verification Steps**:
1. View Identity Service revision history in ArgoCD UI
2. Select previous revision (e.g., revision 5)
3. Click "Rollback" button
4. Confirm rollback
5. Wait for sync to complete
6. Verify application rolled back to previous state
7. Check Git commit SHA matches previous revision

**Success Criteria**:
- Rollback completes within 2 minutes
- Application reverted to previous state
- No downtime during rollback
- Rollback event logged in ArgoCD

---

## Testing Strategy

### Unit Tests

```csharp
[Fact]
public async Task GetApplications_ReturnsApplicationList()
{
    // Arrange
    var mockHttp = new MockHttpMessageHandler();
    mockHttp.When("/api/v1/applications")
        .Respond("application/json", @"{
            ""items"": [
                {
                    ""metadata"": { ""name"": ""identity-service-production"", ""namespace"": ""argocd"" },
                    ""status"": {
                        ""sync"": { ""status"": ""Synced"", ""revision"": ""abc123"" },
                        ""health"": { ""status"": ""Healthy"" }
                    }
                }
            ]
        }");

    var httpClient = mockHttp.ToHttpClient();
    var service = new ArgoCDIntegrationService(httpClient, CreateConfig(), Mock.Of<ILogger>());

    // Act
    var apps = await service.GetApplicationsAsync(CancellationToken.None);

    // Assert
    Assert.Single(apps);
    Assert.Equal("identity-service-production", apps[0].Name);
    Assert.Equal("Synced", apps[0].SyncStatus);
    Assert.Equal("Healthy", apps[0].HealthStatus);
}
```

### Integration Tests

```bash
# Integration test script
#!/bin/bash

echo "Testing ArgoCD GitOps workflow..."

# Test 1: Deploy application
echo "Test 1: Deploy application"
kubectl apply -f argocd-apps/identity-service-production.yaml
argocd app wait identity-service-production --health --timeout 300
if [ $? -eq 0 ]; then
  echo "✅ Application deployed successfully"
else
  echo "❌ Application deployment failed"
  exit 1
fi

# Test 2: Verify sync status
echo "Test 2: Verify sync status"
SYNC_STATUS=$(argocd app get identity-service-production -o json | jq -r '.status.sync.status')
if [ "$SYNC_STATUS" == "Synced" ]; then
  echo "✅ Application synced"
else
  echo "❌ Application not synced (status: $SYNC_STATUS)"
  exit 1
fi

# Test 3: Trigger manual sync
echo "Test 3: Trigger manual sync"
argocd app sync identity-service-production --timeout 300
if [ $? -eq 0 ]; then
  echo "✅ Manual sync successful"
else
  echo "❌ Manual sync failed"
  exit 1
fi

# Test 4: Verify drift detection
echo "Test 4: Verify drift detection"
kubectl scale deployment identity-service -n production --replicas=5
sleep 60  # Wait for ArgoCD to detect drift
SYNC_STATUS=$(argocd app get identity-service-production -o json | jq -r '.status.sync.status')
if [ "$SYNC_STATUS" == "OutOfSync" ]; then
  echo "✅ Drift detected"
else
  echo "❌ Drift not detected"
  exit 1
fi

# Test 5: Verify self-heal
echo "Test 5: Verify self-heal"
sleep 180  # Wait for self-heal
REPLICAS=$(kubectl get deployment identity-service -n production -o jsonpath='{.spec.replicas}')
if [ "$REPLICAS" == "3" ]; then
  echo "✅ Self-heal successful"
else
  echo "❌ Self-heal failed (replicas: $REPLICAS)"
  exit 1
fi

echo "All tests passed! ✅"
```

---

## Risks and Mitigation

| Risk | Impact | Probability | Mitigation |
|------|---------|-------------|------------|
| Git repository unavailable | Deployments blocked | Low | ArgoCD caches Git repos locally. Configure Git repository mirroring. Emergency manual deployment procedure. |
| ArgoCD down | Can't deploy or sync | Low | Deploy ArgoCD in HA mode (3 replicas). Monitor ArgoCD health. Manual kubectl fallback. |
| Self-heal deletes intentional changes | Disrupts operations | Medium | Use `ignoreDifferences` for fields managed by other controllers (e.g., HPA replicas). Document manual change procedures. |
| Incorrect manifests in Git | Broken deployments | Medium | Implement PR approval workflows. Use CI validation (kubeval, kustomize build). Test in dev/staging first. |
| Secrets in Git | Security breach | High | Never commit secrets to Git. Use Vault with external-secrets-operator. Secret scanning in CI. |

---

## Definition of Done

- [ ] ArgoCD installed via Helm in `argocd` namespace
- [ ] ArgoCD UI accessible with TLS certificate
- [ ] Git repository created with proper structure (base, overlays, argocd-apps)
- [ ] Kustomize overlays configured for dev, staging, production
- [ ] ArgoCD Application manifests created for all services
- [ ] ArgoCD Projects configured with RBAC
- [ ] Auto-sync and self-heal enabled
- [ ] SSO integration with Keycloak configured
- [ ] Notification integrations configured (Slack, email)
- [ ] Prometheus monitoring and alerts configured
- [ ] Admin Service integration with ArgoCD API
- [ ] CLI operations documented and tested
- [ ] Integration tests: Deploy, sync, rollback workflows
- [ ] Drift detection and self-heal verified
- [ ] Performance test: 50+ applications, <5 min sync time
- [ ] Security review: RBAC, secret management, TLS
- [ ] Documentation: GitOps workflow guide, troubleshooting
- [ ] Training materials for DevOps team

---

## Related Documentation

### PRD References
- **Lines 1233-1257**: Story 1.25 detailed requirements
- **Lines 1079-1243**: Phase 4 (Governance & Workflows) overview
- **NFR12**: Config deployment <5 minutes
- **NFR14**: Automated drift correction

### Architecture References
- **Section 8**: GitOps and Continuous Deployment
- **Section 9**: Kubernetes Infrastructure
- **Story 1.22**: Configuration Management Integration

### External Documentation
- [ArgoCD Documentation](https://argo-cd.readthedocs.io/)
- [Kustomize Documentation](https://kubectl.docs.kubernetes.io/references/kustomize/)
- [GitOps Principles](https://opengitops.dev/)
- [ArgoCD Best Practices](https://argo-cd.readthedocs.io/en/stable/user-guide/best_practices/)

---

## Notes for Development Team

### Pre-Implementation Checklist
- [ ] Provision Kubernetes cluster with sufficient resources
- [ ] Create Git repository with proper access controls
- [ ] Generate GitHub/GitLab access tokens for ArgoCD
- [ ] Configure DNS for argocd.intellifin.local
- [ ] Set up TLS certificates (Let's Encrypt or internal CA)
- [ ] Configure Keycloak OIDC client for ArgoCD
- [ ] Plan branch strategy (main, staging, develop)
- [ ] Document emergency rollback procedures

### Post-Implementation Handoff
- [ ] Train DevOps team on ArgoCD operations
- [ ] Demo GitOps workflow to development teams
- [ ] Create runbook for common operations (sync, rollback, troubleshooting)
- [ ] Set up monitoring dashboards for ArgoCD health
- [ ] Schedule quarterly GitOps workflow review
- [ ] Document PR approval process for production deployments
- [ ] Create incident response plan for deployment failures
- [ ] Establish SLA for deployment sync times

### Technical Debt / Future Enhancements
- [ ] Implement automated Kustomize validation in CI
- [ ] Add policy enforcement with Open Policy Agent (OPA)
- [ ] Create custom health checks for complex applications
- [ ] Implement progressive delivery with Argo Rollouts
- [ ] Add automated testing in pre-production environments
- [ ] Integrate with Terraform for infrastructure provisioning
- [ ] Implement multi-cluster deployments
- [ ] Add cost optimization policies (resource limits, HPA)

---

**Story Created**: 2025-10-11  
**Last Updated**: 2025-10-11  
**Next Story**: [Story 1.26: Container Image Signing and SBOM Generation](./story-1.26-container-signing-sbom.md)
