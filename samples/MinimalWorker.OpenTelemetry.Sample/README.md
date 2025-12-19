# MinimalWorker with Full OpenTelemetry Stack

This sample demonstrates **MinimalWorker** with a complete production-grade observability stack using OpenTelemetry, Jaeger, Prometheus, Loki, and Grafana.

## 🎯 What This Demo Shows

- **Distributed Tracing**: See worker execution spans in Jaeger with detailed timing and tags
- **Metrics**: Monitor worker executions, errors, and duration in Prometheus and Grafana
- **Logs**: Aggregate and query logs with Loki (integrated with traces)
- **Real-time Dashboards**: Pre-configured Grafana dashboard with MinimalWorker metrics

## 📊 Observability Stack

| Component | Purpose | Port | URL |
|-----------|---------|------|-----|
| **Jaeger** | Distributed tracing | 16686 | http://localhost:16686 |
| **Prometheus** | Metrics storage | 9090 | http://localhost:9090 |
| **Grafana** | Unified visualization | 3000 | http://localhost:3000 |
| **Loki** | Log aggregation | 3100 | http://localhost:3100 |

## 🚀 Quick Start

### Prerequisites

- Docker and Docker Compose installed
- .NET 9.0 SDK installed

### Step 1: Start the Observability Stack

```bash
cd samples/MinimalWorker.OpenTelemetry.Sample
docker-compose up -d
```

Wait for all services to start (about 30 seconds). Verify with:

```bash
docker-compose ps
```

All services should show "Up" status.

### Step 2: Run the MinimalWorker Sample

In a new terminal:

```bash
cd samples/MinimalWorker.OpenTelemetry.Sample
dotnet run
```

You'll see output like:

```
╔════════════════════════════════════════════════════════════════╗
║      MinimalWorker with Full OpenTelemetry Stack Sample       ║
╚════════════════════════════════════════════════════════════════╝

📊 Observability Stack:
  • Logs:    Console + OTLP → Jaeger (http://localhost:16686)
  • Traces:  Console + OTLP → Jaeger (http://localhost:16686)
  • Metrics: Console + OTLP + Prometheus scrape

🎯 Dashboards:
  • Jaeger UI:     http://localhost:16686
  • Prometheus:    http://localhost:9090
  • Grafana:       http://localhost:3000 (admin/admin)

🔧 Workers Running:
  • Continuous worker (2s delay)
  • Periodic worker (every 3s)
  • Cron worker (every 5s)

Press Ctrl+C to stop...
```

### Step 3: Explore the Observability

#### 🔍 View Traces in Jaeger

1. Open http://localhost:16686
2. Select **MinimalWorker.Sample** from the Service dropdown
3. Click **Find Traces**
4. Click on any trace to see:
   - Worker execution span
   - Tags: `worker.id`, `worker.type`, `worker.iteration`, `worker.schedule`
   - Duration breakdown
   - Exception details (if any errors occur)

#### 📈 View Metrics in Prometheus

1. Open http://localhost:9090
2. Try these queries:
   ```promql
   # Total executions
   minimal_worker_executions_total
   
   # Execution rate per worker
   rate(minimal_worker_executions_total[1m])
   
   # Average duration
   avg(minimal_worker_duration_milliseconds)
   
   # Error rate
   rate(minimal_worker_errors_total[1m])
   ```

#### 📊 View Dashboard in Grafana

1. Open http://localhost:3000
2. Login with **admin** / **admin**
3. Navigate to **Dashboards** → **MinimalWorker Observability**
4. See real-time metrics:
   - Total executions and errors
   - Average worker duration
   - Execution rate by worker type
   - Duration percentiles (p50, p95, p99)
   - Worker distribution pie chart

The dashboard auto-refreshes every 5 seconds.

#### 📝 View Logs in Loki (via Grafana)

1. In Grafana, go to **Explore**
2. Select **Loki** datasource
3. Run log queries:
   ```logql
   {job="varlogs"}
   ```

### Step 4: Stop Everything

Stop the .NET app:
```bash
Ctrl+C
```

Stop the Docker stack:
```bash
docker-compose down
```

To also remove volumes:
```bash
docker-compose down -v
```

## 🔧 What's Configured

### Workers

- **Continuous Worker**: Executes continuously with 2-second delay
- **Periodic Worker**: Executes every 3 seconds
- **Cron Worker**: Executes every 5 seconds (cron: `*/5 * * * * *`)

### OpenTelemetry Configuration

The sample uses **OTLP (OpenTelemetry Protocol)** to send telemetry to the stack:

- **Traces**: Exported to Jaeger via OTLP (gRPC on port 4317)
- **Metrics**: Exported to Prometheus via OTLP + scrape endpoint
- **Logs**: Console output with Debug level

### Instrumentation

All workers automatically instrument:

- **Metrics**:
  - `minimal_worker.executions` (Counter)
  - `minimal_worker.errors` (Counter with `error.type`)
  - `minimal_worker.duration` (Histogram in milliseconds)

- **Trace Tags**:
  - `worker.id` - Unique worker identifier
  - `worker.type` - Type (Continuous, Periodic, Cron)
  - `worker.iteration` - Execution count
  - `worker.schedule` - Schedule interval (Periodic)
  - `cron.expression` - Cron expression (Cron)
  - `cron.next_run` - Next scheduled time (Cron)
  - `exception.*` - Exception details (if error occurs)

## 🎓 Learning Resources

### Understanding the Data Flow

```
MinimalWorker App
       │
       ├─ Traces ──(OTLP:4317)──> Jaeger
       │
       ├─ Metrics ─(OTLP:4317)──> Prometheus
       │              └─(scrape:9090)──> Prometheus
       │
       └─ Logs ───(Console)────> Loki (via Promtail)
       
                       ↓
                    Grafana
              (Unified Visualization)
```

### Useful PromQL Queries

```promql
# Worker execution rate by type
sum by (worker_type) (rate(minimal_worker_executions_total[1m]))

# Error percentage
sum(rate(minimal_worker_errors_total[1m])) 
/ 
sum(rate(minimal_worker_executions_total[1m])) * 100

# 95th percentile duration
histogram_quantile(0.95, minimal_worker_duration_milliseconds)

# Top 5 slowest workers
topk(5, avg by (worker_id) (minimal_worker_duration_milliseconds))
```

### Architecture Patterns

This setup demonstrates:

1. **Observability as Code**: All dashboards and datasources are version-controlled
2. **OTLP Standard**: Uses vendor-neutral OpenTelemetry Protocol
3. **Service Mesh Ready**: Same patterns work in Kubernetes with service mesh
4. **Production-Like**: Real tools used in production environments

## 🐛 Troubleshooting

### App can't connect to OTLP endpoint

**Error**: `Status(StatusCode="Unavailable", Detail="failed to connect to all addresses")`

**Solution**: Make sure Docker stack is running:
```bash
docker-compose ps
curl http://localhost:16686  # Should return Jaeger UI
```

### No metrics in Prometheus

**Problem**: Prometheus can't scrape the app

**Solution**: Update `prometheus.yml` to use correct target:
- **macOS/Windows**: `host.docker.internal:9090`
- **Linux**: Use your machine's IP instead of `localhost`

### Grafana dashboards not loading

**Problem**: Datasources not connected

**Solution**: 
1. Go to Grafana → Configuration → Data Sources
2. Verify all three datasources are green (Prometheus, Jaeger, Loki)
3. If not, wait 30 seconds for auto-provisioning to complete

### Logs not appearing in Loki

**Problem**: Promtail can't find logs

**Solution**: This sample outputs to console only. To send logs to Loki:
1. Use structured logging (Serilog)
2. Configure Serilog Loki sink
3. Or use Docker logging driver to capture stdout

## 📚 Next Steps

- **Add Custom Metrics**: Instrument your own business logic
- **Create Alerts**: Set up Prometheus alerting rules
- **Deploy to Cloud**: Try with Azure Application Insights or AWS X-Ray
- **Scale Up**: Run multiple instances and see distributed tracing in action
- **Add Business Dashboards**: Create domain-specific Grafana dashboards

## 🔗 References

- [OpenTelemetry Documentation](https://opentelemetry.io/docs/)
- [Jaeger Documentation](https://www.jaegertracing.io/docs/)
- [Prometheus Documentation](https://prometheus.io/docs/)
- [Grafana Documentation](https://grafana.com/docs/)
- [MinimalWorker Documentation](../../README.md)

---

**Happy Observing! 🎉**
