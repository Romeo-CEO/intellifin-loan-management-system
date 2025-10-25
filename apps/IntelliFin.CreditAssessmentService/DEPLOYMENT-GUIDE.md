# Credit Assessment Service - Production Deployment Guide

## Story 1.20: Production Deployment and Cutover

---

## Pre-Deployment Checklist

### Infrastructure Prerequisites
- [ ] PostgreSQL 15 database available
- [ ] Redis 7.x cache available
- [ ] RabbitMQ message broker available
- [ ] HashiCorp Vault accessible
- [ ] Kubernetes cluster ready
- [ ] Container registry accessible

### Configuration Prerequisites
- [ ] Database connection string configured
- [ ] Vault secrets configured
- [ ] External service URLs configured
- [ ] JWT secret configured
- [ ] TLS certificates ready

### Testing Prerequisites
- [ ] All unit tests passing (85%+ coverage)
- [ ] All integration tests passing
- [ ] Performance tests completed (< 5s p95 latency)
- [ ] Load tests completed (100 concurrent requests)
- [ ] Security scan completed (no critical vulnerabilities)

---

## Deployment Procedure

### Phase 1: Deploy Service (Passive)

#### 1. Build and Push Docker Image

```bash
# Build image
docker build -t intellifin/credit-assessment-service:v1.0.0 \
  -f apps/IntelliFin.CreditAssessmentService/Dockerfile .

# Tag as latest
docker tag intellifin/credit-assessment-service:v1.0.0 \
  intellifin/credit-assessment-service:latest

# Push to registry
docker push intellifin/credit-assessment-service:v1.0.0
docker push intellifin/credit-assessment-service:latest
```

#### 2. Apply Database Migrations

```bash
# Generate migration SQL script
cd libs/IntelliFin.Shared.DomainModels
dotnet ef migrations script --context LmsDbContext --output migration.sql

# Review migration script
cat migration.sql

# Backup database
pg_dump -h <host> -U <user> intellifin_lms > backup_$(date +%Y%m%d_%H%M%S).sql

# Apply migration
psql -h <host> -U <user> -d intellifin_lms -f migration.sql

# Verify migration
psql -h <host> -U <user> -d intellifin_lms -c "SELECT * FROM __EFMigrationsHistory ORDER BY MigrationId DESC LIMIT 5;"
```

#### 3. Create Kubernetes Secrets

```bash
# Create namespace
kubectl create namespace intellifin --dry-run=client -o yaml | kubectl apply -f -

# Create secrets from template
kubectl create secret generic credit-assessment-secrets \
  --from-literal=database-connection-string="Host=postgres.intellifin;Database=intellifin_lms;Username=<USER>;Password=<PASS>" \
  --from-literal=vault-token="<VAULT_TOKEN>" \
  --from-literal=rabbitmq-username="<USER>" \
  --from-literal=rabbitmq-password="<PASS>" \
  --namespace=intellifin
```

#### 4. Deploy Using Helm

```bash
# Install (first time)
helm install credit-assessment-service \
  apps/IntelliFin.CreditAssessmentService/k8s/helm/ \
  --namespace intellifin \
  --set image.tag=v1.0.0 \
  --values production-values.yaml

# Or upgrade (subsequent deployments)
helm upgrade credit-assessment-service \
  apps/IntelliFin.CreditAssessmentService/k8s/helm/ \
  --namespace intellifin \
  --set image.tag=v1.0.0
```

#### 5. Verify Deployment

```bash
# Check pod status
kubectl get pods -n intellifin -l app=credit-assessment-service

# Check logs
kubectl logs -n intellifin -l app=credit-assessment-service --tail=100

# Port forward for testing
kubectl port-forward -n intellifin svc/credit-assessment-service 8080:80

# Test health checks
curl http://localhost:8080/health/ready
curl http://localhost:8080/metrics

# Test API (with valid JWT)
curl -X POST http://localhost:8080/api/v1/credit-assessment/assess \
  -H "Authorization: Bearer <TOKEN>" \
  -H "Content-Type: application/json" \
  -d '{"loanApplicationId":"<ID>","clientId":"<ID>","requestedAmount":50000,"termMonths":24,"productType":"PAYROLL"}'
```

### Phase 2: Gradual Rollout (10% → 50% → 100%)

#### 1. Enable Feature Flag at 10%

In Loan Origination Service `appsettings.json`:
```json
{
  "FeatureFlags": {
    "UseNewCreditAssessmentService": true,
    "RolloutPercentage": 10,
    "CreditAssessmentServiceUrl": "http://credit-assessment-service.intellifin.svc.cluster.local"
  }
}
```

#### 2. Monitor for 48 Hours

**Key Metrics to Watch**:
- Request success rate (should be > 95%)
- p95 latency (should be < 5 seconds)
- Error rate (should be < 5%)
- Decision parity with embedded service (>90% match)

**Monitoring Commands**:
```bash
# Check Grafana dashboard
open https://grafana.intellifin.com/d/credit-assessment

# Check logs for errors
kubectl logs -n intellifin -l app=credit-assessment-service --since=1h | grep ERROR

# Check Prometheus alerts
open https://prometheus.intellifin.com/alerts
```

#### 3. Increase to 50% (if metrics healthy)

```json
{
  "FeatureFlags": {
    "RolloutPercentage": 50
  }
}
```

Monitor for another 48 hours.

#### 4. Full Cutover (100%)

```json
{
  "FeatureFlags": {
    "RolloutPercentage": 100
  }
}
```

Monitor for 1 week.

### Phase 3: Decommission Embedded Logic

#### 1. Remove Embedded Assessment Code

Once 100% cutover is stable for 2 weeks:

```bash
# In Loan Origination Service
# Remove: Services/CreditAssessmentService.cs
# Remove: Services/RiskCalculationEngine.cs
# Update: Services/LoanApplicationService.cs to always use external service
```

#### 2. Archive Legacy Code

```bash
git mv apps/IntelliFin.LoanOriginationService/Services/CreditAssessmentService.cs \
  archive/legacy/CreditAssessmentService.cs.deprecated

git commit -m "Archive embedded credit assessment logic - migrated to microservice"
```

---

## Rollback Plan

### Scenario 1: Deployment Failure

**If Kubernetes deployment fails:**

```bash
# Rollback to previous version
helm rollback credit-assessment-service --namespace intellifin

# Or delete and redeploy previous version
kubectl delete -f apps/IntelliFin.CreditAssessmentService/k8s/
kubectl apply -f apps/IntelliFin.CreditAssessmentService/k8s/ --set image.tag=<PREVIOUS_VERSION>
```

### Scenario 2: Database Migration Failure

**If migration causes issues:**

```bash
# Rollback migration
cd libs/IntelliFin.Shared.DomainModels
dotnet ef database update <PreviousMigrationName> --context LmsDbContext

# Restore from backup if needed
psql -h <host> -U <user> -d intellifin_lms < backup_<timestamp>.sql
```

### Scenario 3: High Error Rate in Production

**If error rate > 10% after deployment:**

```bash
# 1. Disable feature flag immediately (emergency kill switch)
# Update Loan Origination Service config:
{
  "FeatureFlags": {
    "UseNewCreditAssessmentService": false
  }
}

# 2. Restart Loan Origination pods to pick up config
kubectl rollout restart deployment/loan-origination-service -n intellifin

# 3. Investigate issues in Credit Assessment Service
kubectl logs -n intellifin -l app=credit-assessment-service --tail=1000 > investigation.log

# 4. Fix issues and redeploy
```

---

## Post-Deployment Verification

### Automated Checks

```bash
# Run smoke tests
./scripts/smoke-tests.sh production

# Verify all endpoints
curl https://credit-assessment.intellifin.com/health/ready
curl https://credit-assessment.intellifin.com/metrics
```

### Manual Verification

- [ ] Perform test assessment via API
- [ ] Verify assessment stored in database
- [ ] Check audit trail in AdminService
- [ ] Verify decision matches expected result
- [ ] Check Grafana dashboards show data
- [ ] Verify no errors in logs

### 24-Hour Monitoring

- [ ] Monitor error rate (target: < 2%)
- [ ] Monitor p95 latency (target: < 5s)
- [ ] Monitor decision distribution
- [ ] Monitor external API success rate
- [ ] Check for any alerts
- [ ] Review user feedback

---

## Performance Targets

| Metric | Target | Measurement |
|--------|--------|-------------|
| Availability | 99.9% | Uptime monitoring |
| p95 Latency | < 5 seconds | Prometheus metrics |
| p99 Latency | < 10 seconds | Prometheus metrics |
| Error Rate | < 2% | HTTP status codes |
| Cache Hit Rate | > 70% | Redis metrics |
| External API Success | > 95% | Circuit breaker metrics |

---

## Runbook

### Common Issues

**Issue**: Service pods failing health checks  
**Resolution**:
1. Check database connectivity: `kubectl exec -it <pod> -- nc -zv postgres 5432`
2. Check logs: `kubectl logs <pod>`
3. Verify secrets: `kubectl get secret credit-assessment-secrets -o yaml`

**Issue**: High latency  
**Resolution**:
1. Check external API latency in metrics
2. Verify Redis cache is working
3. Check database query performance
4. Scale up pods if needed: `kubectl scale deployment credit-assessment-service --replicas=5`

**Issue**: Authentication failures  
**Resolution**:
1. Verify JWT secret matches Identity Service
2. Check token expiration settings
3. Verify issuer/audience configuration

---

## Success Criteria

### Deployment Success
- ✅ Service deployed with 99.9% availability
- ✅ Zero critical incidents during deployment
- ✅ All health checks passing
- ✅ Metrics reporting correctly

### Performance Success
- ✅ p95 latency < 5 seconds
- ✅ Cache hit rate > 70%
- ✅ External API success > 95%
- ✅ Zero timeout errors

### Business Success
- ✅ 100% of assessments using microservice
- ✅ Manual override rate < 15%
- ✅ Zero compliance findings
- ✅ Credit officer satisfaction > 4/5

---

**Created**: Story 1.20 - Production Deployment and Cutover  
**Status**: Ready for Production Deployment  
**Last Updated**: 2025-01-12
