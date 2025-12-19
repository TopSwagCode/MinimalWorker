# MinimalWorker Metrics Documentation

## Core Metrics

### Worker Execution Metrics

#### `worker.executions`
**Prometheus**: `minimalworker_worker_executions_total` or `worker_executions_total`

- **Type**: Counter
- **Unit**: `{execution}` (dimensionless)
- **Description**: Total number of worker executions (successful + failed)
- **Labels**:
  - `worker.id`: Unique worker identifier (e.g., `"1"`, `"2"`)
  - `worker.name`: User-provided or auto-generated worker name (e.g., `"order-processor"`)
  - `worker.type`: Worker type (`"continuous"`, `"periodic"`, `"cron"`)

**Use Cases**:
- Monitor worker activity and execution frequency
- Calculate execution rate over time
- Verify workers are running at expected intervals
- Detect unexpected drops in worker activity

---

#### `worker.errors`
**Prometheus**: `minimalworker_worker_errors_total` or `worker_errors_total`

- **Type**: Counter
- **Unit**: `{error}` (dimensionless)
- **Description**: Total number of worker execution failures
- **Labels**:
  - `worker.id`: Unique worker identifier
  - `worker.name`: Worker name
  - `worker.type`: Worker type
  - `exception.type`: Fully qualified exception type (e.g., `"System.InvalidOperationException"`)

**Use Cases**:
- Track error frequency and patterns
- Calculate error rates and percentages
- Alert on elevated error rates
- Identify problematic workers
- Root cause analysis by exception type

**Example PromQL Queries**:
```promql
# Error rate per worker
rate(worker_errors_total[5m])

# Error percentage
(rate(worker_errors_total[5m]) / rate(worker_executions_total[5m])) * 100

# Errors by exception type
sum by (exception_type) (worker_errors_total)
```

---

### Worker Performance Metrics

#### `worker.duration`
**Prometheus**: `minimalworker_worker_duration_milliseconds_*` or `worker_duration_milliseconds_*`

- **Type**: Histogram
- **Unit**: `ms` (milliseconds)
- **Description**: Distribution of worker execution durations
- **Labels**:
  - `worker.id`: Unique worker identifier
  - `worker.name`: Worker name
  - `worker.type`: Worker type

**Histogram Components**:
- `_bucket`: Duration buckets (le="10", le="50", le="100", etc.)
- `_sum`: Total sum of all execution durations
- `_count`: Total number of measurements (equals executions)

**Use Cases**:
- Calculate percentiles (p50, p95, p99) for latency analysis
- Identify slow executions and performance degradation
- SLA monitoring and compliance
- Capacity planning based on execution times

**Example PromQL Queries**:
```promql
# 95th percentile execution time
histogram_quantile(0.95, 
  rate(worker_duration_milliseconds_bucket[5m])
)

# Average execution time
rate(worker_duration_milliseconds_sum[5m]) 
/ rate(worker_duration_milliseconds_count[5m])

# Median (p50) execution time
histogram_quantile(0.50, 
  rate(worker_duration_milliseconds_bucket[5m])
)
```

---

### Worker Health Metrics

#### `worker.active`
**Prometheus**: `minimalworker_worker_active` or `worker_active`

- **Type**: Gauge (Observable)
- **Unit**: `{worker}` (dimensionless)
- **Description**: Indicates if worker is currently running (`1`) or stopped (`0`)
- **Labels**:
  - `worker.id`: Unique worker identifier
  - `worker.name`: Worker name
  - `worker.type`: Worker type

**Use Cases**:
- Monitor worker lifecycle and availability
- Alert when critical workers stop unexpectedly
- Dashboard visualization of active/inactive workers
- Verify workers started successfully after deployment
- Track worker restarts and downtime

**Example PromQL Queries**:
```promql
# Count of active workers
sum(worker_active)

# Inactive workers
worker_active == 0

# Workers by type and status
sum by (worker_type, worker_name) (worker_active)
```

---

#### `worker.consecutive_failures`
**Prometheus**: `minimalworker_worker_consecutive_failures` or `worker_consecutive_failures`

- **Type**: Gauge (Observable)
- **Unit**: `{failure}` (dimensionless)
- **Description**: Number of consecutive failures since last successful execution
- **Labels**:
  - `worker.id`: Unique worker identifier
  - `worker.name`: Worker name
  - `worker.type`: Worker type

**Behavior**:
- Increments on each failure
- Resets to `0` on successful execution
- Persists across worker iterations

**Use Cases**:
- Distinguish transient errors from persistent failures
- Identify workers in a failing state
- Trigger circuit breaker or backoff logic
- Alert on persistent issues requiring immediate attention
- Track recovery after incidents

**Example PromQL Queries**:
```promql
# Workers with consecutive failures
worker_consecutive_failures > 0

# Workers in critical failure state
worker_consecutive_failures > 5

# Average consecutive failures
avg(worker_consecutive_failures)
```

---

#### `worker.last_success_time`
**Prometheus**: `minimalworker_worker_last_success_time_seconds` or `worker_last_success_time_seconds`

- **Type**: Gauge (Observable)
- **Unit**: `s` (seconds, Unix timestamp)
- **Description**: Unix timestamp of the last successful worker execution
- **Labels**:
  - `worker.id`: Unique worker identifier
  - `worker.name`: Worker name
  - `worker.type`: Worker type

**Use Cases**:
- Calculate "time since last success"
- Detect stalled or hung workers
- Monitor infrequent workers (e.g., cron jobs that run hourly/daily)
- Alert when workers haven't succeeded within expected timeframe
- Troubleshoot workers that appear active but aren't completing

**Example PromQL Queries**:
```promql
# Time since last success (seconds)
time() - worker_last_success_time_seconds

# Workers with no success in 10 minutes
time() - worker_last_success_time_seconds > 600

# Time since last success in minutes
(time() - worker_last_success_time_seconds) / 60
```

---

### Metadata Metrics

#### `target.info`
**Prometheus**: `minimalworker_target_info` or `target_info`

- **Type**: Info (always value `1`)
- **Unit**: N/A
- **Description**: Static metadata about the MinimalWorker service instance
- **Labels**: Typically includes:
  - `service.name`: Service name (e.g., `"MinimalWorker.Sample"`)
  - `service.version`: MinimalWorker version (e.g., `"3.0.0"`)
  - `service.instance.id`: Unique instance identifier
  - Additional runtime/environment metadata

**Use Cases**:
- Service discovery and inventory
- Version tracking across deployments
- Environment identification (dev, staging, prod)
- Join with other metrics for enriched queries
- Audit and compliance tracking

**Example PromQL Queries**:
```promql
# Service instances
count(target_info)

# Versions in use
group by (service_version) (target_info)
```

---

## 📊 Complete Metrics Summary

| Metric Name | Type | Description | Key Labels |
|-------------|------|-------------|-----------|
| `worker.executions` | Counter | Total worker executions | worker.id, worker.name, worker.type |
| `worker.errors` | Counter | Total worker errors | worker.id, worker.name, worker.type, exception.type |
| `worker.duration` | Histogram | Execution duration (ms) | worker.id, worker.name, worker.type |
| `worker.active` | Gauge | Worker running status (1=active, 0=stopped) | worker.id, worker.name, worker.type |
| `worker.consecutive_failures` | Gauge | Consecutive failure count | worker.id, worker.name, worker.type |
| `worker.last_success_time` | Gauge | Last success timestamp (Unix) | worker.id, worker.name, worker.type |
| `target.info` | Info | Service metadata | service.name, service.version, etc. |

---

## 🔧 Metric Naming Conventions

MinimalWorker follows **OpenTelemetry Semantic Conventions** for metric naming:

- **Metric names** use dot notation: `worker.executions`, `worker.duration`
- **Prometheus exporters** automatically convert to snake_case: `worker_executions_total`, `worker_duration_milliseconds_bucket`
- **Unit suffixes** are added by exporters: `_total` for counters, `_seconds` for time gauges
- **Labels/Tags** use dot notation in code, converted to snake_case in Prometheus: `worker.name` → `worker_name`

### Namespace Prefix

The OpenTelemetry Collector or Prometheus exporter may add a namespace prefix. For example:
- Collector config: `namespace: "minimalworker"` → `minimalworker_worker_executions_total`
- No namespace → `worker_executions_total`

**Recommendation**: Avoid namespace prefixes in the collector config since the Meter name (`"MinimalWorker"`) already provides sufficient scoping.