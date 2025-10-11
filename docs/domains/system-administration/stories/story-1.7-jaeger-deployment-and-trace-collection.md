# Story 1.7: Jaeger Deployment and Trace Collection

### Metadata
- **ID**: 1.7 | **Points**: 5 | **Effort**: 3-5 days | **Priority**: P1
- **Dependencies**: 1.6 (OpenTelemetry)
- **Blocks**: 1.17

### User Story
**As a** DevOps engineer,  
**I want** Jaeger deployed to Kubernetes receiving traces from OpenTelemetry Collector,  
**so that** I can visualize distributed request traces across IntelliFin microservices.

### Acceptance Criteria
1. Jaeger all-in-one deployed via Helm chart
2. OpenTelemetry Collector deployed as DaemonSet
3. OTLP Collector exports traces to Jaeger (OTLP/gRPC)
4. Jaeger UI accessible at `https://jaeger.intellifin.local`
5. Trace retention 7 days with automated cleanup
6. Sample traces validated: API Gateway → Loan Origination → Credit Bureau
7. Jaeger Prometheus metrics exposed

### Helm Values
```yaml
# values-jaeger.yaml
jaeger-all-in-one:
  allInOne:
    enabled: true
    replicas: 1
    image:
      repository: jaegertracing/all-in-one
      tag: "1.51"
    
    resources:
      limits:
        cpu: 1
        memory: 2Gi
      requests:
        cpu: 500m
        memory: 1Gi
    
    ingress:
      enabled: true
      hosts:
        - jaeger.intellifin.local
      tls:
        - secretName: jaeger-tls
          hosts:
            - jaeger.intellifin.local
    
    storage:
      type: memory
      options:
        memory:
          max-traces: 100000
    
    env:
      - name: COLLECTOR_OTLP_ENABLED
        value: "true"
      - name: SPAN_STORAGE_TYPE
        value: "memory"
```

### Integration Verification
- **IV1**: Trace collection doesn't impact service latency
- **IV2**: Traces searchable by correlation ID, service name, operation
- **IV3**: Error traces (5xx responses) automatically highlighted
