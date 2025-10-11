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

### NetworkPolicy Examples
```yaml
# default-deny-all.yaml (applied to all namespaces)
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: default-deny-all
  namespace: intellifin
spec:
  podSelector: {}
  policyTypes:
    - Ingress
    - Egress

---
# api-gateway-netpol.yaml
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: api-gateway-policy
  namespace: gateway
spec:
  podSelector:
    matchLabels:
      app: api-gateway
  policyTypes:
    - Ingress
    - Egress
  ingress:
    - from:
        - namespaceSelector:
            matchLabels:
              name: ingress-nginx  # Allow from ingress controller
      ports:
        - protocol: TCP
          port: 8080
  egress:
    - to:
        - namespaceSelector: {}  # Allow egress to all namespaces (gateway needs to reach all services)
      ports:
        - protocol: TCP
          port: 8080  # Service ports
    - to:  # DNS
        - namespaceSelector:
            matchLabels:
              name: kube-system
      ports:
        - protocol: UDP
          port: 53

---
# admin-service-netpol.yaml
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: admin-service-policy
  namespace: admin
spec:
  podSelector:
    matchLabels:
      app: admin-service
  policyTypes:
    - Ingress
    - Egress
  ingress:
    - from:
        - namespaceSelector:
            matchLabels:
              name: gateway  # Only API Gateway can call Admin Service
      ports:
        - protocol: TCP
          port: 8080
  egress:
    - to:  # Keycloak
        - namespaceSelector:
            matchLabels:
              name: keycloak
      ports:
        - protocol: TCP
          port: 8080
    - to:  # Database
        - namespaceSelector:
            matchLabels:
              name: database
      ports:
        - protocol: TCP
          port: 1433
    - to:  # Vault
        - namespaceSelector:
            matchLabels:
              name: vault
      ports:
        - protocol: TCP
          port: 8200
    - to:  # DNS
        - namespaceSelector:
            matchLabels:
              name: kube-system
      ports:
        - protocol: UDP
          port: 53

---
# loan-origination-netpol.yaml
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: loan-origination-policy
  namespace: intellifin
spec:
  podSelector:
    matchLabels:
      app: loan-origination
  policyTypes:
    - Ingress
    - Egress
  ingress:
    - from:
        - namespaceSelector:
            matchLabels:
              name: gateway
      ports:
        - protocol: TCP
          port: 8080
  egress:
    - to:  # Database
        - namespaceSelector:
            matchLabels:
              name: database
      ports:
        - protocol: TCP
          port: 1433
    - to:  # Redis
        - podSelector:
            matchLabels:
              app: redis
      ports:
        - protocol: TCP
          port: 6379
    - to:  # RabbitMQ
        - podSelector:
            matchLabels:
              app: rabbitmq
      ports:
        - protocol: TCP
          port: 5672
    - to:  # DNS
        - namespaceSelector:
            matchLabels:
              name: kube-system
      ports:
        - protocol: UDP
          port: 53
```

### Testing Script
```bash
#!/bin/bash
# test-network-policies.sh

# Test 1: API Gateway should reach Loan Origination
kubectl run test-from-gateway --image=curlimages/curl --rm -i --tty -n gateway -- \
  curl -v http://loan-origination.intellifin.svc.cluster.local:8080/health

# Test 2: Loan Origination should NOT reach Admin Service directly (should fail)
kubectl run test-from-loan --image=curlimages/curl --rm -i --tty -n intellifin -- \
  curl -v -m 5 http://admin-service.admin.svc.cluster.local:8080/health

# Test 3: Admin Service should reach Keycloak
kubectl run test-from-admin --image=curlimages/curl --rm -i --tty -n admin -- \
  curl -v http://keycloak.keycloak.svc.cluster.local:8080/health

# Expected: Test 1 & 3 succeed, Test 2 times out (blocked by NetworkPolicy)
```

### Integration Verification
- **IV1**: Existing service communication paths remain functional (whitelisted in policies)
- **IV2**: Unauthorized communication attempts blocked (test with temporary pod)
- **IV3**: Performance impact negligible (<1ms latency addition per iptables rules)
