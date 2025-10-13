# IntelliFin GitOps Manifests

This folder provides the baseline Git structure for managing Kubernetes workloads with ArgoCD.  It mirrors the layout described in Story 1.25 and is intended to be cloned into the dedicated `intellifin-k8s-manifests` repository.

- `base/` contains reusable Kustomize bases for each microservice.
- `overlays/` contains environment-specific overlays that patch the base definitions.
- `infrastructure/` stores platform components such as Vault, Prometheus, Grafana, and ingress.
- `argocd-apps/` holds ArgoCD `Application` definitions that point to individual overlays.
- `argocd-projects/` defines ArgoCD `AppProject` resources with RBAC and repository restrictions.

The identity service is fully modelled as an example; other services can adopt the same pattern as they transition to GitOps.
