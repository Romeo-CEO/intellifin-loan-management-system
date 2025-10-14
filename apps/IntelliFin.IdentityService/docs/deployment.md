# Identity Service Deployment Guide

This document outlines how to deploy the Identity Service to a Kubernetes cluster.

## Prerequisites

- Kubernetes cluster with access to the required container registry
- `kubectl` installed and configured
- SQL Server instance available to the cluster

## Secrets

1. Create the SQL Server connection string secret:
   ```bash
   kubectl create secret generic identity-service-secrets \
     --from-literal=connection-string="Server=sql-server;Database=IntellifinIdentity;User Id=identity_svc;Password=<STRONG_PASSWORD>;TrustServerCertificate=True;"
   ```

## Configuration

1. Apply the configuration ConfigMap (customize values as needed):
   ```bash
   kubectl apply -f apps/IntelliFin.IdentityService/k8s/configmap.yaml
   ```

## Deploy the Service

```bash
kubectl apply -f apps/IntelliFin.IdentityService/k8s/deployment.yaml
kubectl apply -f apps/IntelliFin.IdentityService/k8s/service.yaml
kubectl apply -f apps/IntelliFin.IdentityService/k8s/hpa.yaml
```

## Verification

1. Confirm pods are running:
   ```bash
   kubectl get pods -l app=identity-service
   ```
2. Check health probes:
   ```bash
   kubectl port-forward deployment/identity-service 8080:8080
   curl http://localhost:8080/health/live
   curl http://localhost:8080/health/ready
   ```
3. Inspect metrics:
   ```bash
   curl http://localhost:8080/metrics | grep identity_service
   ```

## Cleanup

```bash
kubectl delete -f apps/IntelliFin.IdentityService/k8s/hpa.yaml
kubectl delete -f apps/IntelliFin.IdentityService/k8s/service.yaml
kubectl delete -f apps/IntelliFin.IdentityService/k8s/deployment.yaml
kubectl delete configmap identity-service-config
kubectl delete secret identity-service-secrets
```
