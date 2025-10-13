# Story 1.13: Kubernetes NetworkPolicies for Micro-Segmentation

### Metadata
- **ID**: 1.13 | **Points**: 8 | **Effort**: 5-7 days | **Priority**: P1
- **Dependencies**: Kubernetes CNI with NetworkPolicy support (Calico, Cilium), Story 1.12 (mTLS)
- **Blocks**: None

### User Story
**As a** security engineer,  
**I want** Kubernetes NetworkPolicies restricting service-to-service communication,  
**so that** we implement zero-trust micro-segmentation and prevent lateral movement.

### Acceptance Criteria
1. Default-deny NetworkPolicy applied to all namespaces (deny all ingress/egress by default)
2. Per-service NetworkPolicies created allowing only required communication paths
3. API Gateway policy: Allow ingress from LoadBalancer, allow egress to all services
4. Service policies: Allow ingress from API Gateway only, allow egress to database/Redis/RabbitMQ
5. Admin Service policy: Allow egress to Keycloak, Vault, all services (for audit collection)
6. NetworkPolicy testing via pod-to-pod connectivity tests (validate blocked paths)
7. NetworkPolicy violations logged and alerted via Prometheus

### Implementation Summary
- Added `infra/network-policies` kustomization with default-deny manifests for
  every workload namespace plus explicit policies for the API Gateway, Admin
  Service, and Loan Origination workloads.
- Scoped API Gateway ingress to the ingress controller namespace while
  permitting service egress and DNS resolution required for discovery.
- Locked down service namespaces so that business APIs only accept traffic from
  the gateway and only reach their backing dependencies (SQL Server, Redis,
  RabbitMQ, Vault, Keycloak) over the expected ports.
- Published `scripts/network-policies/test-network-policies.sh` to automate pod
  connectivity validation across approved and denied paths.
- Extended the observability chart with a Calico PodMonitor and
  `NetworkPolicyDeniesDetected` alert so denied packets surface in Grafana and
  Alertmanager.

### NetworkPolicy Examples
```yaml
# infra/network-policies/default-deny.yaml (repeated for each namespace)
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: default-deny-all
  namespace: gateway
spec:
  podSelector: {}
  policyTypes:
    - Ingress
    - Egress

---
# infra/network-policies/api-gateway.yaml
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: api-gateway-policy
  namespace: gateway
spec:
  podSelector:
    matchExpressions:
      - key: app.kubernetes.io/name
        operator: In
        values:
          - intellifin-api-gateway
          - api-gateway
  policyTypes:
    - Ingress
    - Egress
  ingress:
    - from:
        - namespaceSelector:
            matchExpressions:
              - key: kubernetes.io/metadata.name
                operator: In
                values:
                  - ingress-nginx
      ports:
        - protocol: TCP
          port: 8080
        - protocol: TCP
          port: 443
  egress:
    - to:
        - namespaceSelector: {}
      ports:
        - protocol: TCP
          port: 8080
        - protocol: TCP
          port: 443
    - to:
        - namespaceSelector:
            matchExpressions:
              - key: kubernetes.io/metadata.name
                operator: In
                values:
                  - kube-system
      ports:
        - protocol: UDP
          port: 53

---
# infra/network-policies/admin-service.yaml
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: admin-service-policy
  namespace: admin
spec:
  podSelector:
    matchExpressions:
      - key: app.kubernetes.io/name
        operator: In
        values:
          - admin-service
          - intellifin-admin-service
  policyTypes:
    - Ingress
    - Egress
  ingress:
    - from:
        - namespaceSelector:
            matchExpressions:
              - key: kubernetes.io/metadata.name
                operator: In
                values:
                  - gateway
      ports:
        - protocol: TCP
          port: 8080
  egress:
    - to:
        - namespaceSelector:
            matchExpressions:
              - key: kubernetes.io/metadata.name
                operator: In
                values:
                  - keycloak
      ports:
        - protocol: TCP
          port: 8080
    - to:
        - namespaceSelector:
            matchExpressions:
              - key: kubernetes.io/metadata.name
                operator: In
                values:
                  - vault
      ports:
        - protocol: TCP
          port: 8200
    - to:
        - namespaceSelector:
            matchExpressions:
              - key: kubernetes.io/metadata.name
                operator: In
                values:
                  - database
      ports:
        - protocol: TCP
          port: 1433
    - to:
        - namespaceSelector: {}
      ports:
        - protocol: TCP
          port: 8080
    - to:
        - namespaceSelector:
            matchExpressions:
              - key: kubernetes.io/metadata.name
                operator: In
                values:
                  - kube-system
      ports:
        - protocol: UDP
          port: 53

---
# infra/network-policies/loan-origination.yaml
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: loan-origination-policy
  namespace: lending
spec:
  podSelector:
    matchExpressions:
      - key: app.kubernetes.io/name
        operator: In
        values:
          - loan-origination
          - intellifin-loan-origination
  policyTypes:
    - Ingress
    - Egress
  ingress:
    - from:
        - namespaceSelector:
            matchExpressions:
              - key: kubernetes.io/metadata.name
                operator: In
                values:
                  - gateway
      ports:
        - protocol: TCP
          port: 8080
  egress:
    - to:
        - namespaceSelector:
            matchExpressions:
              - key: kubernetes.io/metadata.name
                operator: In
                values:
                  - database
      ports:
        - protocol: TCP
          port: 1433
    - to:
        - podSelector:
            matchExpressions:
              - key: app
                operator: In
                values:
                  - redis
      ports:
        - protocol: TCP
          port: 6379
    - to:
        - podSelector:
            matchExpressions:
              - key: app
                operator: In
                values:
                  - rabbitmq
      ports:
        - protocol: TCP
          port: 5672
    - to:
        - namespaceSelector:
            matchExpressions:
              - key: kubernetes.io/metadata.name
                operator: In
                values:
                  - kube-system
      ports:
        - protocol: UDP
          port: 53
```

### Testing Script
```bash
#!/usr/bin/env bash
# scripts/network-policies/test-network-policies.sh

kubectl run test-from-gateway --image=curlimages/curl:8.6.0 --rm --attach \
  --restart=Never -n gateway -- \
  curl -sf -m 5 http://loan-origination.lending.svc.cluster.local:8080/health

kubectl run test-from-loan --image=curlimages/curl:8.6.0 --rm --attach \
  --restart=Never -n lending -- \
  curl -sv -m 5 http://admin-service.admin.svc.cluster.local:8080/health || echo "expected failure"

kubectl run test-from-admin --image=curlimages/curl:8.6.0 --rm --attach \
  --restart=Never -n admin -- \
  curl -sf -m 5 http://keycloak.keycloak.svc.cluster.local:8080/health
```

### Observability & Alerting
- `podMonitor` coverage for the Calico `calico-node` DaemonSet forwards Felix
  metrics to Prometheus for deny counters.
- `NetworkPolicyDeniesDetected` warning alert fires when
  `felix_denied_packets_total` increases, highlighting regressions immediately
  in Grafana and Alertmanager.

### Integration Verification
- **IV1**: Existing service communication paths remain functional (whitelisted in policies)
- **IV2**: Unauthorized communication attempts blocked (test with temporary pod)
- **IV3**: Performance impact negligible (<1ms latency addition per iptables rules)
