# Story 1.12: mTLS Service-to-Service Communication

### Metadata
- **ID**: 1.12 | **Points**: 10 | **Effort**: 7-10 days | **Priority**: P1
- **Dependencies**: Kubernetes, existing service HTTP communication
- **Blocks**: 1.13

### User Story
**As a** security engineer,  
**I want** all inter-service HTTP communication secured with mutual TLS,  
**so that** we prevent man-in-the-middle attacks and implement zero-trust networking.

### Acceptance Criteria
1. Linkerd control plane installed and healthy (`linkerd check` passes)
2. Sidecar injection enabled for all services (namespace or per-deployment annotations)
3. All inter-service traffic encrypted with Linkerd mTLS (verified via `linkerd viz edges` showing TLS)
4. Alerting on mTLS handshake failures and proxy restarts via Prometheus/Alertmanager
5. Backward compatibility maintained (no application code changes required)
6. Performance testing validates <20ms p95 latency overhead (NFR9 target)

### Linkerd mTLS Implementation
```yaml
# Enable Linkerd sidecar injection at the namespace level
apiVersion: v1
kind: Namespace
metadata:
  name: intellifin
  annotations:
    linkerd.io/inject: enabled
---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: admin-service
  namespace: admin
  annotations:
    linkerd.io/inject: enabled
spec:
  template:
    metadata:
      annotations:
        config.linkerd.io/proxy-log-level: warn,linkerd=info
```

```bash
# Install Linkerd (control plane) and viz extension
linkerd check --pre
linkerd install | kubectl apply -f -
linkerd check
linkerd viz install | kubectl apply -f -
linkerd viz check

# Validate mTLS is active between workloads
linkerd viz edges deploy -A
# Look for TLS=TRUE and identities populated
```

```yaml
# Example Linkerd Server and Authorization (optional fine-grained policy)
apiVersion: policy.linkerd.io/v1beta2
kind: Server
metadata:
  name: admin-http
  namespace: admin
spec:
  podSelector:
    matchLabels:
      app: admin-service
  port: 8080
  protocol: HTTP/1
---
apiVersion: policy.linkerd.io/v1beta2
kind: ServerAuthorization
metadata:
  name: allow-gateway-to-admin
  namespace: admin
spec:
  server:
    name: admin-http
  client:
    meshTLS:
      identities:
        - "sa/api-gateway.gateway.serviceaccount.identity.linkerd.cluster.local"
```

### Service Mesh Evaluation
**Linkerd Service Mesh (Chosen)**:
- ✅ Zero code changes: transparent mTLS by default
- ✅ Lightweight data plane with minimal overhead compared to heavier meshes
- ✅ Strong identity via mTLS with per-pod identities and automatic rotation
- ✅ Excellent diagnostics (viz, edges, tap) and golden signals out of the box
- ❌ Sidecar overhead per pod and additional control plane to operate

**Manual mTLS**:
- ❌ Requires pervasive code/config changes across services
- ❌ Complex certificate distribution and rotation management
- ❌ Harder to validate/monitor mTLS posture

**Decision**: Linkerd service mesh mTLS across all inter-service traffic (see ADR-006 updated)

### Integration Verification
- **IV1**: Existing HTTP calls continue working with mTLS (transparent to application logic)
- **IV2**: Certificate rotation tested without service downtime (rolling update)
- **IV3**: Security test validates mTLS rejects connections without valid client certificates
