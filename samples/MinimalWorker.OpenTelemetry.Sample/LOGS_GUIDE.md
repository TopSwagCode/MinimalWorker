# 📝 Logs Guide - Where to Find Your Logs

## Overview
Logs from the MinimalWorker sample are pushed via **OTLP** (OpenTelemetry Protocol) to the **OpenTelemetry Collector**, which then forwards them to **Loki** for storage and **Grafana** for visualization.

## 🏗️ Architecture

```
MinimalWorker App (.NET Console)
    ↓ Logs via OTLP (port 4317)
OpenTelemetry Collector
    ↓ Forward to Loki
Loki (Log Aggregation)
    ↓ Query logs
Grafana (Visualization)
```

**Key Point**: Just like metrics, logs are **PUSHED** via OTLP, not scraped. This is the correct pattern for console applications and background workers.

## 📍 Where to See Logs

### 1. **Grafana Explore** (Recommended)

**URL**: http://localhost:3000/explore

**Steps**:
1. Open Grafana: http://localhost:3000 (admin/admin)
2. Click "Explore" (compass icon) in the left sidebar
3. Select **"Loki"** as the data source (top dropdown)
4. Use LogQL queries to filter logs:

**Example Queries**:

```logql
# All logs from MinimalWorker.Sample
{service_name="MinimalWorker.Sample"}

# Only error logs
{service_name="MinimalWorker.Sample"} |= "error" or "Error" or "fail"

# Only logs from flaky worker
{service_name="MinimalWorker.Sample"} |= "Flaky worker"

# Only logs with specific severity
{service_name="MinimalWorker.Sample"} | json | SeverityText="Warning"

# Logs from a specific worker type
{service_name="MinimalWorker.Sample"} |= "continuous"

# Logs with trace context (correlated with traces)
{service_name="MinimalWorker.Sample"} | json | TraceId!=""
```

5. **Time range**: Adjust in the top-right corner (e.g., "Last 15 minutes")
6. **Live tail**: Click "Live" button to see logs in real-time

### 2. **Direct Loki API** (For Testing)

```bash
# Query logs via Loki API
curl -G -s "http://localhost:3100/loki/api/v1/query_range" \
  --data-urlencode 'query={service_name="MinimalWorker.Sample"}' \
  --data-urlencode 'limit=10' | python3 -m json.tool
```

### 3. **Console Output** (Development Only)

The sample app also exports logs to the console for debugging:
```
LogRecord.FormattedMessage:        🔄 Continuous worker executing: Message #1
LogRecord.Body:                    🔄 Continuous worker executing: {Message}
LogRecord.Attributes (Key:Value):
    Message: Message #1
```

## 🎯 What You'll See

### Log Structure

Each log entry includes:

- **Timestamp**: When the log was created
- **TraceId**: Links logs to distributed traces in Jaeger
- **SpanId**: Specific span within the trace
- **Severity**: Debug, Info, Warning, Error, Critical
- **FormattedMessage**: Human-readable log message
- **Attributes**: Structured data (e.g., worker ID, message count)
- **Resource**: Service metadata (name, version, environment)

### Example Log Entry

```
info: Program[0]
      🔄 Continuous worker executing: Message #42
LogRecord.TraceId:       c0295b3665ae422372475efd9ed43263
LogRecord.SpanId:        c0e724a2b7befcf9
LogRecord.Severity:      Info
LogRecord.Attributes:
    Message: Message #42
    worker.id: 1
    worker.type: continuous
```

## 🎨 Expected Log Messages

### Continuous Worker (Every ~500ms)
```
🔄 Continuous worker executing: Message #1
🔄 Continuous worker executing: Message #2
...
```

### Periodic Worker (Every 2s)
```
⏰ Periodic worker executing. Count: 1
⏰ Periodic worker executing. Count: 2
...
```

### Cron Worker (Every 1 minute)
```
📅 Cron worker executing at: 19:45:23
📅 Cron worker executing at: 19:46:23
...
```

### Flaky Worker (Every 3s) - 50/50 Success/Fail
**Success**:
```
info: Program[0]
      🎲 Flaky worker: ✅ Success! Operation completed.
```

**Failure** (Warning Level):
```
warn: Program[0]
      🎲 Flaky worker: Attempting operation that might fail...
```

**After error handler** (Console output):
```
⚠️  Error in flaky worker (expected): 🔥 Flaky worker failed! (Simulated random failure)
```

### Slow Worker (Every 5s) - Random Delays 1-3s
```
info: Program[0]
      💤 Slow worker: Starting operation (will take ~2514ms)...
info: Program[0]
      💤 Slow worker: ✅ Completed after 2514ms
```

## 🔗 Correlating Logs with Traces

One of the most powerful features is **logs-to-traces correlation**:

1. **In Grafana Explore** (Loki):
   - Find a log entry with a `TraceId`
   - Click on the TraceId
   - Grafana will open the corresponding trace in Jaeger!

2. **In Jaeger**:
   - View a trace
   - Click "Logs" tab
   - See related logs from Loki

This allows you to:
- See logs in context of a trace timeline
- Understand what happened before/after an error
- Debug distributed systems by following the request flow

## 📊 Creating a Logs Dashboard in Grafana

### Option 1: Use Explore (Quick Start)
1. Go to Explore
2. Select Loki data source
3. Run your query
4. Click "Add to dashboard" → "New dashboard"

### Option 2: Create Custom Dashboard
1. Click "+" → "Dashboard"
2. Add Panel → Select "Loki" data source
3. Enter LogQL query: `{service_name="MinimalWorker.Sample"}`
4. Visualization type: "Logs"
5. Save dashboard

### Useful Dashboard Panels

**1. Log Stream (Real-time)**
```logql
{service_name="MinimalWorker.Sample"}
```
- Visualization: Logs
- Options: Show time, Show labels

**2. Error Rate**
```logql
sum(rate({service_name="MinimalWorker.Sample"} | json | SeverityText="Error"[5m]))
```
- Visualization: Graph
- Y-axis: errors/sec

**3. Log Volume by Severity**
```logql
sum by (SeverityText) (count_over_time({service_name="MinimalWorker.Sample"}[1m]))
```
- Visualization: Bar chart
- Legend: {{SeverityText}}

**4. Worker Errors (Flaky Worker)**
```logql
{service_name="MinimalWorker.Sample"} |= "Flaky worker" |= "fail"
```
- Visualization: Logs
- Show errors only

**5. Slow Operations**
```logql
{service_name="MinimalWorker.Sample"} |= "Slow worker" | json | DelayMs > 2000
```
- Visualization: Logs
- Filter by delay

## 🐛 Troubleshooting

### No Logs in Grafana?

1. **Check OpenTelemetry Collector**:
   ```bash
   docker logs otel-collector 2>&1 | tail -20
   ```
   Should see: "Everything is ready. Begin running and processing data."

2. **Check Loki is receiving data**:
   ```bash
   curl -s "http://localhost:3100/ready"
   ```
   Should return: "ready"

3. **Check Loki labels**:
   ```bash
   curl -s "http://localhost:3100/loki/api/v1/labels" | python3 -m json.tool
   ```
   Should see: `service_name` in the list

4. **Verify app is sending logs**:
   - Look for console output with `LogRecord.Timestamp`
   - Check OTLP exporter is configured in Program.cs

5. **Check time range in Grafana**:
   - Logs are timestamped
   - Make sure the time range includes when your app was running
   - Try "Last 15 minutes" or "Last hour"

### Logs Not Correlated with Traces?

Make sure `OpenTelemetry.Extensions.Logging` is configured:
```csharp
builder.Logging.AddOpenTelemetry(options =>
{
    options.AddOtlpExporter(/* ... */);
    options.IncludeScopes = true;  // Important for correlation!
});
```

### Empty LogQL Query Result?

Try a broader query first:
```logql
{job=~".+"}  # All logs from all jobs
```

Then narrow down to your service:
```logql
{service_name="MinimalWorker.Sample"}
```

## 📚 LogQL Cheat Sheet

### Basic Queries
```logql
# All logs from a service
{service_name="MinimalWorker.Sample"}

# Contains text (case-insensitive)
{service_name="MinimalWorker.Sample"} |= "error"

# Doesn't contain text
{service_name="MinimalWorker.Sample"} != "debug"

# Regex match
{service_name="MinimalWorker.Sample"} |~ "worker.*executing"
```

### JSON Parsing
```logql
# Parse JSON and filter
{service_name="MinimalWorker.Sample"} | json | SeverityText="Error"

# Extract field
{service_name="MinimalWorker.Sample"} | json | worker_id=`1`

# Filter by attribute
{service_name="MinimalWorker.Sample"} | json | Count > 10
```

### Aggregations
```logql
# Count logs per minute
sum(count_over_time({service_name="MinimalWorker.Sample"}[1m]))

# Rate of logs
rate({service_name="MinimalWorker.Sample"}[5m])

# Group by label
sum by (SeverityText) (count_over_time({service_name="MinimalWorker.Sample"}[1m]))
```

## 🎯 Best Practices

### 1. Use Structured Logging
✅ **Good**:
```csharp
logger.LogInformation("Worker executing: {Message}, Count: {Count}", message, count);
```

❌ **Bad**:
```csharp
logger.LogInformation($"Worker executing: {message}, Count: {count}");
```

Structured logging creates attributes you can filter on!

### 2. Include Context
Always log:
- Worker ID
- Iteration/count
- Duration (for slow operations)
- Error details

### 3. Use Appropriate Log Levels
- **Debug**: Verbose details (development only)
- **Info**: Normal operations
- **Warning**: Recoverable issues (like flaky worker failures)
- **Error**: Unhandled exceptions
- **Critical**: System failures

### 4. Leverage Trace Correlation
Logs are automatically enriched with `TraceId` and `SpanId` when:
- OpenTelemetry logging is configured
- The log occurs within an Activity span

This allows seamless navigation between logs and traces!

## 🚀 Next Steps

1. **Open Grafana Explore**: http://localhost:3000/explore
2. **Select Loki** data source
3. **Run query**: `{service_name="MinimalWorker.Sample"}`
4. **Enable Live tail** to see logs in real-time
5. **Click on a TraceId** to jump to Jaeger
6. **Create a dashboard** with your favorite queries

## 📖 Additional Resources

- [Loki LogQL Documentation](https://grafana.com/docs/loki/latest/logql/)
- [Grafana Explore Guide](https://grafana.com/docs/grafana/latest/explore/)
- [OpenTelemetry Logging](https://opentelemetry.io/docs/instrumentation/net/logs/)
- [Logs-Traces Correlation](https://grafana.com/docs/grafana/latest/datasources/loki/#correlate-logs-with-traces)

---

**Happy Logging! 📝✨**
