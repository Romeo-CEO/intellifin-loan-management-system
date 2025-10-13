# Linkerd Service Mesh Rollout

This module bootstraps the Linkerd control plane and enables automatic proxy
injection for every IntelliFin workload namespace so that all inter-service HTTP
requests are protected by mutual TLS. Follow this guide whenever the platform
cluster is rebuilt or a new environment is created.

## Prerequisites

- Kubernetes 1.27 or later with cluster-admin access
- [`linkerd` CLI 2.14+](https://linkerd.io/2.14/getting-started/) available in your `$PATH`
- Cluster-wide Prometheus and Alertmanager (see `infra/observability`)
- Root/intermediate certificates for Linkerd identity (stored securely in Vault)

Generate or rotate the root/workload certificates before installing the mesh:

```bash
linkerd upgrade --identity-external-ca --set identityTrustAnchorsPEM="$(cat ca.crt)" \
  --set identity.issuer.tls.crtPEM="$(cat issuer.crt)" \
  --set identity.issuer.tls.keyPEM="$(cat issuer.key)"
```

## Installation Steps

1. **Pre-flight checks** – verifies cluster compatibility and required RBAC:
   ```bash
   linkerd check --pre
   ```

2. **Install the Linkerd control plane**:
   ```bash
   linkerd install | kubectl apply -f -
   linkerd check
   ```

3. **Install Linkerd Viz for diagnostics and topology graphs**:
   ```bash
   linkerd viz install | kubectl apply -f -
   linkerd viz check
   ```

4. **Enable proxy injection for IntelliFin namespaces** using the provided
   kustomization:
   ```bash
   kubectl apply -k infra/linkerd
   ```
   This annotates every application namespace (gateway, identity, admin,
   lending, integrations, communications, collections, reporting, finance, kyc,
   offline) with `linkerd.io/inject=enabled`.

5. **Patch service workloads** that live outside of those namespaces (e.g.
   Keycloak) with explicit annotations. The manifests in `infra/keycloak`
   already set `linkerd.io/inject=enabled` on the Keycloak deployment while
   keeping PostgreSQL excluded via `linkerd.io/inject=disabled`.

## Verification

- Confirm all application pods have the Linkerd sidecar and certificates:
  ```bash
  kubectl get pods -A -l linkerd.io/proxy-deployment
  linkerd check --proxy -n gateway
  ```

- Validate that traffic between workloads is encrypted and identities are
  populated:
  ```bash
  linkerd viz edges deploy -A
  ```
  The `TLS` column must read `true` and the `CLIENT`/`SERVER` identity columns
  should list workload service accounts.

- Observe live requests (useful during rollout and performance sampling):
  ```bash
  linkerd viz tap deploy/api-gateway -n gateway
  ```

- Gather latency metrics to ensure <20 ms p95 overhead in steady state:
  ```bash
  linkerd viz stat deploy -A --from deploy/api-gateway --window 30s
  ```

## Alerting and Telemetry

Two new Prometheus rules ship with the observability stack:

- `LinkerdTlsHandshakeFailures` warns when any proxy reports TLS handshake
  errors over a 5 minute window.
- `LinkerdProxyRestarts` alerts if Linkerd sidecars restart more than once in
  five minutes.

These alerts surface in Grafana and trigger Alertmanager notifications. They
pull metrics from Linkerd Viz (`linkerd_tls_acceptor_events_total` and
`kube_pod_container_status_restarts_total`).

## Operational Notes

- Use `linkerd upgrade` during version bumps; it reuses the same manifests
  without downtime.
- Disable injection for stateful data stores or third-party components that
  cannot tolerate proxying by adding `linkerd.io/inject: "disabled"` to the pod
  template annotations.
- When onboarding a new service namespace, add it to
  `infra/linkerd/application-namespaces.yaml` so the mesh injects proxies by
  default.
- Keep the Linkerd CLI in sync with the control plane version; mismatched
  versions fail `linkerd check`.
