# Story 1.29: SSH Session Recording in MinIO (Teleport PAM)

## Story Metadata

| Field | Value |
|-------|-------|
| **Story ID** | 1.29 |
| **Epic** | System Administration Control Plane Enhancement |
|| **Phase** | Phase 5: Zero-Trust & PAM |
| **Sprint** | Sprint 9-10 |
| **Story Points** | 13 |
| **Estimated Effort** | 8-12 days |
| **Priority** | P1 (High) |
| **Status** | 📋 Backlog |
| **Assigned To** | TBD |
| **Dependencies** | Kubernetes cluster, Prometheus (Story 1.24) |
| **Blocks** | Performance optimization, Root cause analysis |

---

## User Story

**As a** DevOps Engineer and Developer,  
**I want** distributed tracing across all microservices with end-to-end request visualization,  
**so that** I can quickly identify performance bottlenecks, debug cross-service issues, and understand system behavior.

---

## Business Value

Distributed tracing with Jaeger provides critical observability benefits:

- **Performance Analysis**: Identify slow services, database queries, and API calls in production
- **Root Cause Analysis**: Trace failures across service boundaries to find the source
- **Dependency Mapping**: Visualize service interactions and understand system architecture
- **Latency Optimization**: Pinpoint exact operations causing delays
- **Error Debugging**: Track errors through entire request lifecycle
- **SLA Compliance**: Monitor and alert on P95/P99 latency targets
- **Capacity Planning**: Understand service load patterns and resource utilization

This story is **critical** for operational excellence and production troubleshooting.

---

## Acceptance Criteria

### AC1: Jaeger Infrastructure Deployment
**Given** Production requires distributed tracing  
**When** deploying Jaeger infrastructure  
**Then**:
- Jaeger deployed in Kubernetes with Helm chart
- Components deployed:
  - Jaeger Collector (3 replicas, auto-scaling)
  - Jaeger Query (2 replicas, UI and API)
  - Jaeger Agent (DaemonSet on all nodes)
  - Elasticsearch backend for trace storage
- Resource configuration:
  - Collector: 2 CPU, 4GB RAM per replica
  - Query: 1 CPU, 2GB RAM per replica
  - Agent: 500m CPU, 512MB RAM
- High availability: Multiple collector replicas with load balancing
- Trace retention: 7 days (configurable)
- Ingress configured for Jaeger UI (https://jaeger.intellifin.com)

### AC2: OpenTelemetry SDK Integration
**Given** Microservices need to emit traces  
**When** integrating OpenTelemetry SDK  
**Then**:
- OpenTelemetry SDK installed in all microservices:
  - .NET services: `OpenTelemetry.Extensions.Hosting`
  - Node.js services: `@opentelemetry/sdk-node`
  - Python services: `opentelemetry-sdk`
- Instrumentation libraries configured:
  - HTTP client/server (automatic)
  - Database clients (PostgreSQL, SQL Server, MongoDB)
  - Message queues (RabbitMQ, Kafka)
  - Redis cache
  - gRPC
- Trace context propagation via W3C Trace Context headers
- Sampling strategy:
  - Development: 100% sampling
  - Staging: 50% sampling
  - Production: 10% sampling (head-based)
- Trace export to Jaeger Collector via OTLP

### AC3: Service-Level Instrumentation
**Given** Each service needs detailed tracing  
**When** implementing custom spans  
**Then**:
- Automatic instrumentation for:
  - HTTP requests (inbound/outbound)
  - Database queries
  - Cache operations
  - Message publishing/consumption
- Custom spans created for:
  - Business logic operations
  - BPMN workflow steps
  - External API calls
  - File I/O operations
- Span attributes include:
  - Service name, version
  - Environment (dev, staging, production)
  - User ID (if authenticated)
  - Loan application ID (business context)
  - HTTP method, status code
  - Database operation type, table name
- Span events for important milestones
- Error spans tagged with exception details

### AC4: Correlation with Logs and Metrics
**Given** Traces need correlation with other signals  
**When** correlating traces, logs, and metrics  
**Then**:
- Trace ID injected into all log entries
- Log entries include:
  - `trace_id`
  - `span_id`
  - `parent_span_id`
- Elasticsearch logs linked to Jaeger traces
- Grafana "Explore" view shows:
  - Logs filtered by trace ID
  - Metrics during trace time window
  - Jaeger trace visualization
- Admin UI displays trace ID for each request
- Users can click "View Trace" from logs

### AC5: Service Dependency Graph
**Given** System architecture needs visualization  
**When** generating service dependency graph  
**Then**:
- Jaeger Service Performance Monitoring (SPM) enabled
- Dependency graph displays:
  - All microservices as nodes
  - Request flows as directed edges
  - Request rate (req/s) per edge
  - Error rate (%) per edge
  - P95 latency per service
- Graph updated in real-time (5-second refresh)
- Filterable by:
  - Time range (last 1h, 6h, 24h, 7d)
  - Environment (dev, staging, production)
  - Service name
- Exportable as PNG/SVG

### AC6: Trace Search and Filtering
**Given** Engineers need to find specific traces  
**When** searching traces in Jaeger UI  
**Then**:
- Search capabilities:
  - By service name
  - By operation name (API endpoint)
  - By trace ID
  - By span tags (user_id, loan_id, etc.)
  - By duration (min/max)
  - By time range
  - By error status
- Advanced filters:
  - Traces with errors only
  - Traces exceeding latency threshold (e.g., >5s)
  - Traces from specific user
  - Traces for specific loan application
- Search results show:
  - Trace timeline
  - Total duration
  - Number of spans
  - Error status
- Paginated results (50 traces per page)

### AC7: Performance Monitoring Dashboards
**Given** Operations team needs performance visibility  
**When** monitoring service performance  
**Then**:
- Grafana dashboards created:
  - **Service Latency**: P50, P95, P99 latency per service
  - **Request Rate**: Requests per second per service
  - **Error Rate**: Error percentage per service
  - **Trace Duration Distribution**: Histogram of trace durations
  - **Slowest Operations**: Top 10 slowest API endpoints
  - **Dependency Latency**: Breakdown of time spent in each service
- Prometheus metrics derived from traces:
  - `http_request_duration_seconds` (histogram)
  - `http_requests_total` (counter)
  - `http_requests_errors_total` (counter)
- Alerts configured:
  - P95 latency >5s for 5 minutes (WARNING)
  - P99 latency >10s for 5 minutes (CRITICAL)
  - Error rate >5% for 5 minutes (CRITICAL)

### AC8: Trace-Based Alerting
**Given** Anomalies need automatic detection  
**When** traces indicate issues  
**Then**:
- Alerting rules based on trace metrics:
  - High latency alert: P95 >5s
  - Error spike alert: Error rate >5%
  - Service degradation: Response time increase >50%
  - Dependency failure: External service error rate >10%
- Alerts include:
  - Service name
  - Operation name
  - Sample trace ID (for investigation)
  - Current metric value
  - Threshold exceeded
- Alert routing:
  - PagerDuty for CRITICAL
  - Slack for WARNING
  - Email for INFO
- Alert deduplication (5-minute window)

### AC9: Admin Service Integration
**Given** Admin UI needs trace visibility  
**When** displaying traces in Admin UI  
**Then**:
- Admin Service API endpoints:
  - `GET /api/admin/traces/search`: Search traces
  - `GET /api/admin/traces/{traceId}`: Get trace details
  - `GET /api/admin/traces/service-graph`: Get dependency graph
  - `GET /api/admin/traces/latency-stats`: Get latency statistics
- Admin UI features:
  - "Request Trace" button on every API response
  - Trace timeline embedded in UI
  - Span details with duration breakdown
  - Related logs displayed alongside trace
  - "Jump to Grafana" link for deeper analysis
- Trace context for user sessions
- Trace export (JSON format)

---

## Technical Implementation Details

### Architecture Reference

**PRD Sections**: Lines 1333-1357 (Story 1.29), Phase 5 Overview  
**Architecture Sections**: Section 9 (Observability), Section 8 (Microservices), Section 4 (Performance)  
**Requirements**: NFR8 (API response time <500ms), NFR9 (P95 latency <2s)

### Technology Stack

- **Tracing Backend**: Jaeger (latest stable)
- **Instrumentation**: OpenTelemetry SDK
- **Storage**: Elasticsearch 8.x
- **Trace Protocol**: OTLP (OpenTelemetry Protocol)
- **Visualization**: Jaeger UI, Grafana
- **Service Mesh**: Istio (optional, for automatic instrumentation)
- **Programming Languages**: .NET 8, Node.js, Python

### Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                      User Requests                               │
└────────────────────────────┬────────────────────────────────────┘
                             │
                             ▼
                  ┌──────────────────────┐
                  │   API Gateway        │
                  │  (Traced with OTLP)  │
                  └──────────┬───────────┘
                             │
        ┌────────────────────┼────────────────────┐
        │                    │                    │
        ▼                    ▼                    ▼
┌───────────────┐    ┌───────────────┐   ┌───────────────┐
│ Identity Svc  │    │  Loan Service │   │ Payment Svc   │
│ (Traced)      │    │  (Traced)     │   │ (Traced)      │
└───────┬───────┘    └───────┬───────┘   └───────┬───────┘
        │                    │                    │
        │                    ▼                    │
        │            ┌───────────────┐            │
        │            │  Database     │            │
        │            │  (Traced)     │            │
        │            └───────────────┘            │
        │                                         │
        └─────────────────┬───────────────────────┘
                          │
                          ▼ (Trace data via OTLP)
              ┌───────────────────────┐
              │   Jaeger Collector    │
              │   (Receives traces)   │
              └───────────┬───────────┘
                          │
                          ▼
              ┌───────────────────────┐
              │    Elasticsearch      │
              │   (Trace storage)     │
              └───────────┬───────────┘
                          │
        ┌─────────────────┴─────────────────┐
        │                                    │
        ▼                                    ▼
┌───────────────┐                  ┌─────────────────┐
│  Jaeger Query │                  │   Prometheus    │
│  (Jaeger UI)  │                  │ (Trace metrics) │
└───────────────┘                  └─────────┬───────┘
                                             │
                                             ▼
                                   ┌─────────────────┐
                                   │    Grafana      │
                                   │  (Dashboards)   │
                                   └─────────────────┘
```

### Jaeger Helm Deployment

```yaml
# jaeger-values.yaml

# Jaeger Helm Chart Values
provenance:
  enabled: false

allInOne:
  enabled: false  # Use production-ready components

collector:
  enabled: true
  replicaCount: 3
  
  autoscaling:
    enabled: true
    minReplicas: 3
    maxReplicas: 10
    targetCPUUtilizationPercentage: 70
    targetMemoryUtilizationPercentage: 80
  
  resources:
    requests:
      cpu: 2000m
      memory: 4Gi
    limits:
      cpu: 4000m
      memory: 8Gi
  
  service:
    type: ClusterIP
    grpc:
      port: 14250
    http:
      port: 14268
    otlp:
      grpc:
        port: 4317
      http:
        port: 4318
  
  config:
    sampling:
      strategies:
        - type: probabilistic
          param: 0.1  # 10% sampling in production
        - service: admin-service
          type: probabilistic
          param: 1.0  # 100% sampling for critical service
    
    spanStorageType: elasticsearch
    
  cmdlineParams:
    es.num-shards: 5
    es.num-replicas: 1
    es.bulk.size: 10000000
    es.bulk.workers: 10
    es.bulk.flush-interval: 1s

query:
  enabled: true
  replicaCount: 2
  
  resources:
    requests:
      cpu: 1000m
      memory: 2Gi
    limits:
      cpu: 2000m
      memory: 4Gi
  
  service:
    type: ClusterIP
    port: 16686
  
  ingress:
    enabled: true
    annotations:
      cert-manager.io/cluster-issuer: letsencrypt-prod
      nginx.ingress.kubernetes.io/ssl-redirect: "true"
    hosts:
      - host: jaeger.intellifin.com
        paths:
          - path: /
            pathType: Prefix
    tls:
      - secretName: jaeger-tls
        hosts:
          - jaeger.intellifin.com
  
  cmdlineParams:
    query.max-clock-skew-adjustment: 1s
    query.base-path: /

agent:
  enabled: true
  daemonset:
    useHostPort: true
  
  resources:
    requests:
      cpu: 500m
      memory: 512Mi
    limits:
      cpu: 1000m
      memory: 1Gi
  
  cmdlineParams:
    reporter.type: grpc
    reporter.grpc.host-port: jaeger-collector:14250

storage:
  type: elasticsearch
  elasticsearch:
    host: elasticsearch-master.elastic-system.svc.cluster.local
    port: 9200
    scheme: http
    user: jaeger
    password: jaeger_password
    indexPrefix: jaeger
    createIndexTemplates: true
    version: 8

# Service Performance Monitoring
spm:
  enabled: true

# Prometheus metrics
prometheus:
  enabled: true
  serviceMonitor:
    enabled: true
    labels:
      release: prometheus

# Spark dependencies (optional, for dependency graph)
spark:
  enabled: false  # Use Jaeger SPM instead
```

### OpenTelemetry Configuration (.NET)

```csharp
// Program.cs
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Exporter;

var builder = WebApplication.CreateBuilder(args);

// Configure OpenTelemetry Tracing
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(
            serviceName: builder.Configuration["ServiceName"] ?? "identity-service",
            serviceVersion: builder.Configuration["ServiceVersion"] ?? "1.0.0")
        .AddAttributes(new Dictionary<string, object>
        {
            ["deployment.environment"] = builder.Environment.EnvironmentName,
            ["service.namespace"] = "intellifin",
            ["service.instance.id"] = Environment.MachineName
        }))
    .WithTracing(tracing => tracing
        // Automatic instrumentation
        .AddAspNetCoreInstrumentation(options =>
        {
            options.RecordException = true;
            options.EnrichWithHttpRequest = (activity, httpRequest) =>
            {
                activity.SetTag("http.client_ip", httpRequest.HttpContext.Connection.RemoteIpAddress);
                activity.SetTag("http.request_id", httpRequest.HttpContext.TraceIdentifier);
                
                // Add user context
                if (httpRequest.HttpContext.User.Identity?.IsAuthenticated == true)
                {
                    activity.SetTag("user.id", httpRequest.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier));
                    activity.SetTag("user.name", httpRequest.HttpContext.User.Identity.Name);
                }
            };
            options.EnrichWithHttpResponse = (activity, httpResponse) =>
            {
                activity.SetTag("http.response_content_length", httpResponse.ContentLength);
            };
        })
        .AddHttpClientInstrumentation(options =>
        {
            options.RecordException = true;
            options.EnrichWithHttpRequestMessage = (activity, httpRequestMessage) =>
            {
                activity.SetTag("http.request.uri", httpRequestMessage.RequestUri?.ToString());
            };
            options.EnrichWithHttpResponseMessage = (activity, httpResponseMessage) =>
            {
                activity.SetTag("http.response.status_code", (int)httpResponseMessage.StatusCode);
            };
        })
        .AddSqlClientInstrumentation(options =>
        {
            options.SetDbStatementForText = true;
            options.RecordException = true;
            options.EnableConnectionLevelAttributes = true;
        })
        .AddEntityFrameworkCoreInstrumentation(options =>
        {
            options.SetDbStatementForText = true;
        })
        .AddRedisInstrumentation(options =>
        {
            options.SetVerboseDatabaseStatements = true;
        })
        
        // Custom instrumentation sources
        .AddSource("IntelliFin.*")
        
        // Sampling
        .SetSampler(new ParentBasedSampler(
            new TraceIdRatioBasedSampler(
                GetSamplingRatio(builder.Environment.EnvironmentName))))
        
        // Export to Jaeger via OTLP
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri(builder.Configuration["Jaeger:OtlpEndpoint"] ?? "http://jaeger-collector:4317");
            options.Protocol = OtlpExportProtocol.Grpc;
        }));

var app = builder.Build();

// Middleware to capture trace context
app.Use(async (context, next) =>
{
    var activity = Activity.Current;
    if (activity != null)
    {
        // Store trace ID in response header for client access
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.Add("X-Trace-Id", activity.TraceId.ToString());
            return Task.CompletedTask;
        });
    }
    
    await next();
});

app.Run();

static double GetSamplingRatio(string environment)
{
    return environment switch
    {
        "Development" => 1.0,  // 100% sampling
        "Staging" => 0.5,      // 50% sampling
        "Production" => 0.1,   // 10% sampling
        _ => 0.1
    };
}
```

### Custom Instrumentation Example

```csharp
// Services/LoanApplicationService.cs
using System.Diagnostics;
using OpenTelemetry.Trace;

public class LoanApplicationService
{
    private static readonly ActivitySource ActivitySource = new("IntelliFin.LoanService");
    private readonly ILogger<LoanApplicationService> _logger;
    private readonly ILoanRepository _loanRepository;
    private readonly ICreditCheckService _creditCheckService;

    public async Task<LoanApplication> ProcessLoanApplicationAsync(
        LoanApplicationRequest request,
        CancellationToken cancellationToken)
    {
        // Create a custom span for the entire operation
        using var activity = ActivitySource.StartActivity("ProcessLoanApplication");
        
        try
        {
            // Add business context to span
            activity?.SetTag("loan.amount", request.Amount);
            activity?.SetTag("loan.type", request.LoanType);
            activity?.SetTag("applicant.id", request.ApplicantId);
            
            _logger.LogInformation(
                "Processing loan application: Amount={Amount}, Type={LoanType}",
                request.Amount, request.LoanType);
            
            // Step 1: Validate application
            using (var validateActivity = ActivitySource.StartActivity("ValidateApplication"))
            {
                validateActivity?.SetTag("validation.type", "initial");
                await ValidateApplicationAsync(request, cancellationToken);
                validateActivity?.AddEvent(new ActivityEvent("Application validated"));
            }
            
            // Step 2: Check credit score
            CreditScore creditScore;
            using (var creditActivity = ActivitySource.StartActivity("CheckCreditScore"))
            {
                creditActivity?.SetTag("applicant.id", request.ApplicantId);
                creditScore = await _creditCheckService.CheckCreditAsync(
                    request.ApplicantId, 
                    cancellationToken);
                creditActivity?.SetTag("credit.score", creditScore.Score);
                creditActivity?.SetTag("credit.rating", creditScore.Rating);
            }
            
            // Step 3: Calculate interest rate
            decimal interestRate;
            using (var rateActivity = ActivitySource.StartActivity("CalculateInterestRate"))
            {
                rateActivity?.SetTag("credit.score", creditScore.Score);
                interestRate = CalculateInterestRate(creditScore, request.Amount);
                rateActivity?.SetTag("interest.rate", interestRate);
            }
            
            // Step 4: Save to database
            LoanApplication loanApplication;
            using (var saveActivity = ActivitySource.StartActivity("SaveLoanApplication"))
            {
                loanApplication = new LoanApplication
                {
                    ApplicantId = request.ApplicantId,
                    Amount = request.Amount,
                    InterestRate = interestRate,
                    Status = LoanStatus.Pending
                };
                
                await _loanRepository.AddAsync(loanApplication, cancellationToken);
                saveActivity?.SetTag("loan.id", loanApplication.Id);
                saveActivity?.AddEvent(new ActivityEvent("Loan application saved"));
            }
            
            // Set final span attributes
            activity?.SetTag("loan.id", loanApplication.Id);
            activity?.SetTag("loan.status", loanApplication.Status.ToString());
            activity?.SetStatus(ActivityStatusCode.Ok);
            
            return loanApplication;
        }
        catch (Exception ex)
        {
            // Record exception in span
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.RecordException(ex);
            
            _logger.LogError(ex, "Failed to process loan application");
            throw;
        }
    }
}
```

### Trace Context Propagation (Node.js)

```javascript
// tracer.js
const { NodeSDK } = require('@opentelemetry/sdk-node');
const { getNodeAutoInstrumentations } = require('@opentelemetry/auto-instrumentations-node');
const { OTLPTraceExporter } = require('@opentelemetry/exporter-trace-otlp-grpc');
const { Resource } = require('@opentelemetry/resources');
const { SemanticResourceAttributes } = require('@opentelemetry/semantic-conventions');

const sdk = new NodeSDK({
  resource: new Resource({
    [SemanticResourceAttributes.SERVICE_NAME]: process.env.SERVICE_NAME || 'payment-service',
    [SemanticResourceAttributes.SERVICE_VERSION]: process.env.SERVICE_VERSION || '1.0.0',
    [SemanticResourceAttributes.DEPLOYMENT_ENVIRONMENT]: process.env.NODE_ENV || 'development',
  }),
  traceExporter: new OTLPTraceExporter({
    url: process.env.JAEGER_OTLP_ENDPOINT || 'http://jaeger-collector:4317',
  }),
  instrumentations: [
    getNodeAutoInstrumentations({
      '@opentelemetry/instrumentation-http': {
        requestHook: (span, request) => {
          span.setAttribute('http.request.headers', JSON.stringify(request.headers));
        },
        responseHook: (span, response) => {
          span.setAttribute('http.response.status', response.statusCode);
        },
      },
      '@opentelemetry/instrumentation-express': {
        requestHook: (span, info) => {
          span.setAttribute('express.route', info.route);
        },
      },
      '@opentelemetry/instrumentation-mongodb': {
        enabled: true,
      },
      '@opentelemetry/instrumentation-pg': {
        enabled: true,
      },
      '@opentelemetry/instrumentation-redis': {
        enabled: true,
      },
    }),
  ],
});

sdk.start();

// Graceful shutdown
process.on('SIGTERM', () => {
  sdk.shutdown()
    .then(() => console.log('Tracing terminated'))
    .catch((error) => console.log('Error terminating tracing', error))
    .finally(() => process.exit(0));
});

module.exports = sdk;
```

### Admin Service API - Tracing Controller

```csharp
// Controllers/TracingController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using IntelliFin.Admin.Services;
using IntelliFin.Admin.Models;

namespace IntelliFin.Admin.Controllers
{
    [ApiController]
    [Route("api/admin/traces")]
    [Authorize]
    public class TracingController : ControllerBase
    {
        private readonly ITracingService _tracingService;
        private readonly ILogger<TracingController> _logger;

        public TracingController(
            ITracingService tracingService,
            ILogger<TracingController> logger)
        {
            _tracingService = tracingService;
            _logger = logger;
        }

        /// <summary>
        /// Search traces with filters
        /// </summary>
        [HttpGet("search")]
        [ProducesResponseType(typeof(TraceSearchResultDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> SearchTraces(
            [FromQuery] string? serviceName = null,
            [FromQuery] string? operationName = null,
            [FromQuery] string? traceId = null,
            [FromQuery] string? tags = null,
            [FromQuery] int? minDurationMs = null,
            [FromQuery] int? maxDurationMs = null,
            [FromQuery] DateTime? startTime = null,
            [FromQuery] DateTime? endTime = null,
            [FromQuery] bool? errorsOnly = false,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            CancellationToken cancellationToken = default)
        {
            var searchRequest = new TraceSearchRequest
            {
                ServiceName = serviceName,
                OperationName = operationName,
                TraceId = traceId,
                Tags = ParseTags(tags),
                MinDurationMs = minDurationMs,
                MaxDurationMs = maxDurationMs,
                StartTime = startTime ?? DateTime.UtcNow.AddHours(-1),
                EndTime = endTime ?? DateTime.UtcNow,
                ErrorsOnly = errorsOnly ?? false,
                Page = page,
                PageSize = pageSize
            };

            var results = await _tracingService.SearchTracesAsync(searchRequest, cancellationToken);
            
            return Ok(results);
        }

        /// <summary>
        /// Get trace by ID with full span details
        /// </summary>
        [HttpGet("{traceId}")]
        [ProducesResponseType(typeof(TraceDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetTraceById(
            string traceId,
            CancellationToken cancellationToken)
        {
            var trace = await _tracingService.GetTraceByIdAsync(traceId, cancellationToken);
            
            if (trace == null)
                return NotFound(new { error = $"Trace {traceId} not found" });

            return Ok(trace);
        }

        /// <summary>
        /// Get service dependency graph
        /// </summary>
        [HttpGet("service-graph")]
        [ProducesResponseType(typeof(ServiceGraphDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetServiceGraph(
            [FromQuery] DateTime? startTime = null,
            [FromQuery] DateTime? endTime = null,
            [FromQuery] string? environment = null,
            CancellationToken cancellationToken = default)
        {
            var graph = await _tracingService.GetServiceGraphAsync(
                startTime ?? DateTime.UtcNow.AddHours(-1),
                endTime ?? DateTime.UtcNow,
                environment,
                cancellationToken);

            return Ok(graph);
        }

        /// <summary>
        /// Get latency statistics per service
        /// </summary>
        [HttpGet("latency-stats")]
        [ProducesResponseType(typeof(List<ServiceLatencyStatsDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetLatencyStatistics(
            [FromQuery] DateTime? startTime = null,
            [FromQuery] DateTime? endTime = null,
            [FromQuery] string? serviceName = null,
            CancellationToken cancellationToken = default)
        {
            var stats = await _tracingService.GetLatencyStatisticsAsync(
                startTime ?? DateTime.UtcNow.AddHours(-1),
                endTime ?? DateTime.UtcNow,
                serviceName,
                cancellationToken);

            return Ok(stats);
        }

        /// <summary>
        /// Get slowest operations (top 10)
        /// </summary>
        [HttpGet("slowest-operations")]
        [ProducesResponseType(typeof(List<SlowOperationDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetSlowestOperations(
            [FromQuery] DateTime? startTime = null,
            [FromQuery] DateTime? endTime = null,
            [FromQuery] int limit = 10,
            CancellationToken cancellationToken = default)
        {
            var operations = await _tracingService.GetSlowestOperationsAsync(
                startTime ?? DateTime.UtcNow.AddHours(-1),
                endTime ?? DateTime.UtcNow,
                limit,
                cancellationToken);

            return Ok(operations);
        }

        private Dictionary<string, string>? ParseTags(string? tagsString)
        {
            if (string.IsNullOrWhiteSpace(tagsString))
                return null;

            // Expected format: "key1:value1,key2:value2"
            var tags = new Dictionary<string, string>();
            foreach (var pair in tagsString.Split(','))
            {
                var parts = pair.Split(':', 2);
                if (parts.Length == 2)
                {
                    tags[parts[0].Trim()] = parts[1].Trim();
                }
            }

            return tags.Count > 0 ? tags : null;
        }
    }
}
```

### Tracing Service Implementation

```csharp
// Services/TracingService.cs
using System.Net.Http.Json;

namespace IntelliFin.Admin.Services
{
    public interface ITracingService
    {
        Task<TraceSearchResult> SearchTracesAsync(
            TraceSearchRequest request, 
            CancellationToken cancellationToken);
        
        Task<TraceDetail> GetTraceByIdAsync(
            string traceId, 
            CancellationToken cancellationToken);
        
        Task<ServiceGraph> GetServiceGraphAsync(
            DateTime startTime, 
            DateTime endTime, 
            string? environment,
            CancellationToken cancellationToken);
        
        Task<List<ServiceLatencyStats>> GetLatencyStatisticsAsync(
            DateTime startTime, 
            DateTime endTime, 
            string? serviceName,
            CancellationToken cancellationToken);
        
        Task<List<SlowOperation>> GetSlowestOperationsAsync(
            DateTime startTime, 
            DateTime endTime, 
            int limit,
            CancellationToken cancellationToken);
    }

    public class TracingService : ITracingService
    {
        private readonly HttpClient _jaegerApiClient;
        private readonly ILogger<TracingService> _logger;
        private readonly IConfiguration _config;

        public TracingService(
            IHttpClientFactory httpClientFactory,
            ILogger<TracingService> logger,
            IConfiguration config)
        {
            _jaegerApiClient = httpClientFactory.CreateClient("JaegerAPI");
            _logger = logger;
            _config = config;
        }

        public async Task<TraceSearchResult> SearchTracesAsync(
            TraceSearchRequest request,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Searching traces: Service={Service}, Operation={Operation}",
                request.ServiceName, request.OperationName);

            var queryParams = new Dictionary<string, string>
            {
                ["service"] = request.ServiceName ?? "",
                ["operation"] = request.OperationName ?? "",
                ["start"] = new DateTimeOffset(request.StartTime).ToUnixTimeMilliseconds().ToString(),
                ["end"] = new DateTimeOffset(request.EndTime).ToUnixTimeMilliseconds().ToString(),
                ["limit"] = request.PageSize.ToString()
            };

            if (request.MinDurationMs.HasValue)
                queryParams["minDuration"] = $"{request.MinDurationMs}ms";
            
            if (request.MaxDurationMs.HasValue)
                queryParams["maxDuration"] = $"{request.MaxDurationMs}ms";

            if (request.Tags != null && request.Tags.Any())
            {
                foreach (var tag in request.Tags)
                {
                    queryParams[$"tags"] = $"{tag.Key}:{tag.Value}";
                }
            }

            var query = string.Join("&", queryParams.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));
            var url = $"/api/traces?{query}";

            var response = await _jaegerApiClient.GetFromJsonAsync<JaegerTracesResponse>(
                url, 
                cancellationToken);

            if (response == null)
                return new TraceSearchResult { Traces = new List<TraceDto>(), TotalCount = 0 };

            // Transform Jaeger response to our DTO
            var traces = response.Data.Select(trace => new TraceDto
            {
                TraceId = trace.TraceID,
                StartTime = DateTimeOffset.FromUnixTimeMilliseconds(trace.Spans.Min(s => s.StartTime / 1000)).UtcDateTime,
                Duration = TimeSpan.FromMicroseconds(trace.Spans.Sum(s => s.Duration)),
                SpanCount = trace.Spans.Count,
                HasErrors = trace.Spans.Any(s => s.Tags.Any(t => t.Key == "error" && t.Value == "true")),
                ServiceName = trace.Spans.FirstOrDefault()?.Process.ServiceName ?? "unknown",
                OperationName = trace.Spans.FirstOrDefault()?.OperationName ?? "unknown"
            }).ToList();

            return new TraceSearchResult
            {
                Traces = traces,
                TotalCount = traces.Count,
                Page = request.Page,
                PageSize = request.PageSize
            };
        }

        public async Task<TraceDetail> GetTraceByIdAsync(
            string traceId,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Fetching trace details: TraceId={TraceId}", traceId);

            var url = $"/api/traces/{traceId}";
            var response = await _jaegerApiClient.GetFromJsonAsync<JaegerTraceResponse>(
                url, 
                cancellationToken);

            if (response == null || !response.Data.Any())
                return null;

            var trace = response.Data.First();
            
            // Build trace detail with span hierarchy
            return new TraceDetail
            {
                TraceId = trace.TraceID,
                StartTime = DateTimeOffset.FromUnixTimeMilliseconds(trace.Spans.Min(s => s.StartTime / 1000)).UtcDateTime,
                Duration = TimeSpan.FromMicroseconds(trace.Spans.Sum(s => s.Duration)),
                Spans = BuildSpanHierarchy(trace.Spans)
            };
        }

        private List<SpanDto> BuildSpanHierarchy(List<JaegerSpan> spans)
        {
            var spanDict = spans.ToDictionary(s => s.SpanID);
            var rootSpans = new List<SpanDto>();

            foreach (var span in spans)
            {
                var spanDto = new SpanDto
                {
                    SpanId = span.SpanID,
                    ParentSpanId = span.References?.FirstOrDefault()?.SpanID,
                    OperationName = span.OperationName,
                    ServiceName = span.Process.ServiceName,
                    StartTime = DateTimeOffset.FromUnixTimeMilliseconds(span.StartTime / 1000).UtcDateTime,
                    Duration = TimeSpan.FromMicroseconds(span.Duration),
                    Tags = span.Tags.ToDictionary(t => t.Key, t => t.Value),
                    HasError = span.Tags.Any(t => t.Key == "error" && t.Value == "true"),
                    Children = new List<SpanDto>()
                };

                if (string.IsNullOrEmpty(spanDto.ParentSpanId))
                {
                    rootSpans.Add(spanDto);
                }
            }

            // Build parent-child relationships
            foreach (var span in spans)
            {
                if (span.References != null && span.References.Any())
                {
                    var parentId = span.References.First().SpanID;
                    if (spanDict.TryGetValue(parentId, out var parentSpan))
                    {
                        var parentDto = FindSpan(rootSpans, parentId);
                        var childDto = FindSpan(rootSpans, span.SpanID);
                        if (parentDto != null && childDto != null)
                        {
                            parentDto.Children.Add(childDto);
                        }
                    }
                }
            }

            return rootSpans;
        }

        private SpanDto? FindSpan(List<SpanDto> spans, string spanId)
        {
            foreach (var span in spans)
            {
                if (span.SpanId == spanId)
                    return span;
                
                var found = FindSpan(span.Children, spanId);
                if (found != null)
                    return found;
            }
            return null;
        }
    }
}
```

### Grafana Dashboard Configuration

```json
{
  "dashboard": {
    "title": "Distributed Tracing - Service Performance",
    "panels": [
      {
        "title": "Request Rate by Service",
        "targets": [
          {
            "expr": "sum(rate(traces_service_call_count[5m])) by (service_name)",
            "legendFormat": "{{ service_name }}"
          }
        ],
        "type": "graph"
      },
      {
        "title": "P95 Latency by Service",
        "targets": [
          {
            "expr": "histogram_quantile(0.95, sum(rate(traces_spanmetrics_latency_bucket[5m])) by (le, service_name))",
            "legendFormat": "{{ service_name }}"
          }
        ],
        "type": "graph"
      },
      {
        "title": "Error Rate by Service",
        "targets": [
          {
            "expr": "sum(rate(traces_service_call_count{status_code=\"STATUS_CODE_ERROR\"}[5m])) by (service_name) / sum(rate(traces_service_call_count[5m])) by (service_name)",
            "legendFormat": "{{ service_name }}"
          }
        ],
        "type": "graph"
      },
      {
        "title": "Trace Duration Distribution",
        "targets": [
          {
            "expr": "sum(rate(traces_spanmetrics_latency_bucket[5m])) by (le)",
            "legendFormat": "{{ le }}"
          }
        ],
        "type": "heatmap"
      },
      {
        "title": "Slowest Operations (Top 10)",
        "targets": [
          {
            "expr": "topk(10, histogram_quantile(0.99, sum(rate(traces_spanmetrics_latency_bucket[5m])) by (le, operation)))",
            "legendFormat": "{{ operation }}"
          }
        ],
        "type": "table"
      },
      {
        "title": "Service Dependency Graph",
        "type": "nodeGraph",
        "dataFormat": "nodes-edges"
      }
    ]
  }
}
```

---

## Integration Verification

### IV1: Jaeger Infrastructure Deployment
**Verification Steps**:
1. Deploy Jaeger via Helm
2. Verify all pods running (collector, query, agent)
3. Access Jaeger UI at https://jaeger.intellifin.com
4. Check Elasticsearch indices created
5. Verify Prometheus ServiceMonitor created

**Success Criteria**:
- All Jaeger pods healthy
- UI accessible and responsive
- Elasticsearch storing traces
- Metrics exposed to Prometheus

### IV2: Trace Instrumentation
**Verification Steps**:
1. Deploy instrumented microservice
2. Make API request to service
3. Check trace appears in Jaeger UI
4. Verify spans for HTTP, database, cache operations
5. Check trace context propagation across services

**Success Criteria**:
- Traces visible in Jaeger
- All expected spans present
- Parent-child relationships correct
- Tags and attributes populated

### IV3: Admin UI Integration
**Verification Steps**:
1. Make request via Admin UI
2. Note X-Trace-Id in response header
3. Click "View Trace" button
4. Verify trace details displayed
5. Check related logs visible

**Success Criteria**:
- Trace ID captured correctly
- Trace detail embedded in UI
- Span breakdown visible
- Logs correlated with trace

---

## Testing Strategy

### Unit Tests

```csharp
[Fact]
public async Task SearchTraces_WithServiceFilter_ReturnsFilteredTraces()
{
    // Arrange
    var service = CreateTracingService();
    var request = new TraceSearchRequest
    {
        ServiceName = "identity-service",
        StartTime = DateTime.UtcNow.AddHours(-1),
        EndTime = DateTime.UtcNow,
        PageSize = 50
    };

    // Act
    var result = await service.SearchTracesAsync(request, CancellationToken.None);

    // Assert
    Assert.NotNull(result);
    Assert.All(result.Traces, trace => Assert.Equal("identity-service", trace.ServiceName));
}
```

### Integration Tests

```bash
#!/bin/bash
# test-tracing.sh

echo "Testing distributed tracing..."

# Test 1: Make request and capture trace ID
echo "Test 1: Capture trace ID from request"
TRACE_ID=$(curl -s -X GET "$API_URL/api/identity/users/me" \
  -H "Authorization: Bearer $TOKEN" \
  -D - | grep -i "x-trace-id" | awk '{print $2}' | tr -d '\r')

echo "Trace ID: $TRACE_ID"

# Test 2: Verify trace in Jaeger
echo "Test 2: Query trace from Jaeger"
sleep 2  # Wait for trace to be indexed

TRACE=$(curl -s -X GET "https://jaeger.intellifin.com/api/traces/$TRACE_ID")

if echo "$TRACE" | jq -e '.data[0]' > /dev/null; then
  echo "✅ Trace found in Jaeger"
else
  echo "❌ Trace not found in Jaeger"
  exit 1
fi

# Test 3: Verify span count
SPAN_COUNT=$(echo "$TRACE" | jq '.data[0].spans | length')
echo "Span count: $SPAN_COUNT"

if [ "$SPAN_COUNT" -gt 0 ]; then
  echo "✅ Spans present in trace"
else
  echo "❌ No spans in trace"
  exit 1
fi

echo "All tests passed! ✅"
```

---

## Risks and Mitigation

| Risk | Impact | Probability | Mitigation |
|------|---------|-------------|------------|
| High trace volume | Elasticsearch storage exhaustion | Medium | Implement sampling (10% in prod). Configure retention (7 days). Monitor storage usage. |
| Performance overhead | Increased latency | Low | Sampling reduces overhead. Async trace export. Benchmark instrumentation impact (<5ms). |
| Trace context loss | Broken traces | Medium | Ensure W3C Trace Context propagation. Test context across service boundaries. |
| Jaeger collector unavailability | Lost traces | Low | HA collector deployment (3+ replicas). Client-side buffering. Monitoring alerts. |
| Sensitive data in traces | Data leakage | Medium | Sanitize PII from span tags. Mask database queries. Audit span attributes. |

---

## Definition of Done

- [ ] Jaeger deployed via Helm (collector, query, agent)
- [ ] Elasticsearch configured for trace storage
- [ ] OpenTelemetry SDK integrated in all microservices
- [ ] Automatic instrumentation for HTTP, DB, cache
- [ ] Custom spans for business operations
- [ ] Trace context propagation tested
- [ ] Admin Service tracing API implemented
- [ ] Admin UI trace viewer component
- [ ] Grafana dashboards created (5 panels)
- [ ] Prometheus alerts configured
- [ ] Trace-based alerting tested
- [ ] Integration tests: End-to-end trace verification
- [ ] Documentation: Instrumentation guide, troubleshooting
- [ ] Runbooks: Jaeger operations, trace analysis

---

## Related Documentation

### PRD References
- **Lines 1333-1357**: Story 1.29 detailed requirements
- **Lines 1244-1408**: Phase 5 (Observability & Infrastructure) overview
- **NFR8**: API response time <500ms
- **NFR9**: P95 latency <2s

### Architecture References
- **Section 9**: Observability
- **Section 8**: Microservices
- **Section 4**: Performance

### External Documentation
- [Jaeger Documentation](https://www.jaegertracing.io/docs/)
- [OpenTelemetry .NET](https://opentelemetry.io/docs/instrumentation/net/)
- [W3C Trace Context](https://www.w3.org/TR/trace-context/)
- [OTLP Specification](https://opentelemetry.io/docs/reference/specification/protocol/)

---

## Notes for Development Team

### Pre-Implementation Checklist
- [ ] Elasticsearch cluster deployed and configured
- [ ] Jaeger Helm chart values prepared
- [ ] OpenTelemetry SDK versions selected
- [ ] Sampling ratios determined per environment
- [ ] Trace retention policy defined
- [ ] PII sanitization rules documented
- [ ] Service naming conventions established
- [ ] Tag standardization defined

### Post-Implementation Handoff
- [ ] Train developers on instrumentation best practices
- [ ] Create instrumentation cookbook (common patterns)
- [ ] Document trace analysis workflows
- [ ] Set up monitoring dashboards
- [ ] Schedule trace analysis training sessions
- [ ] Establish SLA for trace availability
- [ ] Create incident response runbook
- [ ] Document disaster recovery procedures

### Technical Debt / Future Enhancements
- [ ] Implement tail-based sampling (adaptive)
- [ ] Add trace-based anomaly detection
- [ ] Integrate with service mesh (Istio) for automatic tracing
- [ ] Implement trace replay for debugging
- [ ] Add trace comparison (before/after deployments)
- [ ] Create AI-powered root cause analysis
- [ ] Implement distributed profiling
- [ ] Add cost analysis per trace

---

**Story Created**: 2025-10-11  
**Last Updated**: 2025-10-11  
**Next Story**: [Story 1.30: Infrastructure Cost Tracking](./story-1.30-cost-tracking.md)
