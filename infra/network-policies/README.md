# IntelliFin NetworkPolicies

The manifests in this folder enforce zero-trust micro-segmentation across the
IntelliFin platform namespaces. Every namespace inherits a default deny stance
and individual services opt in to the traffic they require.

## Contents

- `default-deny.yaml` — blanket ingress/egress deny policies for each
  application namespace
- `api-gateway.yaml` — ingress from the ingress controller and unrestricted
  egress for the API gateway façade
- `admin-service.yaml` — API Gateway ingress only, with egress scoped to
  Keycloak, Vault, SQL Server, and service discovery ports
- `loan-origination.yaml` — ingress restricted to the API Gateway and egress
  limited to database, Redis, RabbitMQ, and DNS lookups

## Deployment

```bash
kubectl apply -k infra/network-policies
```

`kustomization.yaml` aggregates the policies so that GitOps pipelines or manual
rollouts stay consistent.

## Verification

Run `scripts/network-policies/test-network-policies.sh` against the target
cluster to confirm that permitted flows continue to function while unauthorized
paths are blocked.
