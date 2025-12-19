# 🔍 Observability Demo Guide - What to Look For

This guide shows you exactly what to check in Jaeger, Prometheus, and Grafana to see MinimalWorker's observability in action.

## 🚀 Step 1: Start Everything

### Start the Docker Stack (if not running)
```bash
cd samples/MinimalWorker.OpenTelemetry.Sample
docker-compose up -d
```

Wait ~30 seconds for all services to be ready.

### Start the MinimalWorker Sample
```bash
dotnet run --project samples/MinimalWorker.OpenTelemetry.Sample/MinimalWorker.OpenTelemetry.Sample.csproj
```

**Let it run for at least 2-3 minutes** to generate enough data.

You should see console output like:
```
info: Program[0]
      🔄 Continuous worker executing: Message #1
info: Program[0]
      ⏰ Periodic worker executing. Count: 1
info: Program[0]
      📅 Cron worker executing at: 19:25:10
```

---

## 📊 Step 2: Explore Jaeger (Distributed Tracing)

### Open Jaeger UI
**URL**: http://localhost:16686

### What to Look For:

#### 1. Find Traces
- In the **Service** dropdown, select: `MinimalWorker.Sample`
- Set **Lookback**: Last 1 hour
- Click **Find Traces**

**Expected Result**: You should see multiple traces listed (one per worker execution)

#### 2. Examine a Trace
Click on any trace to see details:

**✅ Check These:**
- **Operation Name**: Should be `worker.execute`
- **Duration**: 
  - Continuous worker: ~500ms
  - Periodic worker: <10ms (just runs, no delay)
  - Cron worker: <10ms
  
#### 3. Check Span Tags
Click on a span and look at the **Tags** section:

**For Continuous Worker:**
```
worker.id = 1
worker.type = continuous
worker.iteration = 1, 2, 3, ... (incrementing)
```

**For Periodic Worker:**
```
worker.id = 2
worker.type = periodic
worker.schedule = 00:00:02 (2 second interval)
```

**For Cron Worker:**
```
worker.id = 3
worker.type = cron
cron.expression = */1 * * * *
cron.next_run = <timestamp>
```

#### 4. Compare Worker Types
- Filter traces by clicking on a tag (e.g., `worker.type=continuous`)
- Notice how continuous worker has **many** traces (runs every 500ms)
- Periodic worker has **fewer** traces (runs every 2 seconds)
- Cron worker has **fewest** traces (runs every 10 seconds)

#### 5. Check Timeline
Click on **"Timeline"** view to see when workers executed over time.

**Expected Pattern:**
- Continuous: Dense, constant activity
- Periodic: Regular 2-second intervals
- Cron: Regular 1-minute intervals

---

## 📈 Step 3: Explore Prometheus (Metrics)

### Open Prometheus UI
**URL**: http://localhost:9090

### What to Look For:

#### 1. Check Metrics Exist
In the **Expression** box, start typing `minimalworker` and you should see autocomplete suggestions:
- `minimalworker_minimal_worker_executions_total`
- `minimalworker_minimal_worker_duration_milliseconds_bucket`
- `minimalworker_minimal_worker_duration_milliseconds_sum`
- `minimalworker_minimal_worker_duration_milliseconds_count`

> **Note**: The `minimalworker_` prefix is added by the OpenTelemetry Collector's Prometheus exporter.

#### 2. Query: Total Executions
**Query:**
```promql
minimalworker_minimal_worker_executions_total
```

**Expected Result:**
```
minimalworker_minimal_worker_executions_total{worker_id="1", worker_type="continuous"} → ~240 (after 2 minutes)
minimalworker_minimal_worker_executions_total{worker_id="2", worker_type="periodic"} → ~60 (after 2 minutes)
minimalworker_minimal_worker_executions_total{worker_id="3", worker_type="cron"} → ~2 (after 2 minutes)
```

Click **Graph** tab to see execution count increase over time (should be linear).

#### 3. Query: Execution Rate (per second)
**Query:**
```promql
rate(minimalworker_minimal_worker_executions_total[1m])
```

**Expected Result:**
```
continuous → ~2.0 executions/second
periodic → ~0.5 executions/second
cron → ~0.017 executions/second (1 per minute)
```

This shows how often each worker type is executing.

#### 4. Query: Average Duration
**Query:**
```promql
rate(minimalworker_minimal_worker_duration_milliseconds_sum[5m]) 
/ 
rate(minimalworker_minimal_worker_duration_milliseconds_count[5m])
```

**Expected Result:**
```
continuous → ~500ms (matches our Task.Delay(500))
periodic → <5ms (no delay, just logging)
cron → <5ms (no delay, just logging)
```

#### 5. Query: 95th Percentile Duration
**Query:**
```promql
histogram_quantile(0.95, 
  rate(minimalworker_minimal_worker_duration_milliseconds_bucket[5m])
)
```

**Expected Result:**
Shows that 95% of executions complete within X milliseconds.

#### 6. Query: Error Rate (should be 0)
**Query:**
```promql
rate(minimalworker_minimal_worker_errors_total[5m])
```

**Expected Result:**
Should return **no data** or **0** (no errors in this sample).

---

## 📊 Step 4: Explore Grafana (Unified Dashboard)

### Open Grafana
**URL**: http://localhost:3000

**Login:**
- Username: `admin`
- Password: `admin`
- (Skip password change or set a new one)

### Navigate to Dashboard

1. Click **hamburger menu** (☰) in top-left
2. Click **Dashboards**
3. Select **MinimalWorker Observability**

### What to Look For:

#### Panel 1: Worker Executions (Total)
**Top-left stat panel**

**Expected Value:** Large number (e.g., 300+) that increases as workers execute

**What it shows:** Total number of times ALL workers have executed since start

**To verify:** Should increase by ~2.5/second (2 continuous + 0.5 periodic + ~0 cron)

---

#### Panel 2: Worker Errors (Total)
**Top-center stat panel**

**Expected Value:** 0 (green)

**What it shows:** Total error count across all workers

**To verify:** Should stay at 0 (no errors in this sample)

---

#### Panel 3: Average Worker Duration
**Top-right stat panel**

**Expected Value:** ~200-300ms

**What it shows:** Average time workers take to execute

**Why this value:** Continuous worker (500ms) runs most frequently, but periodic/cron (<5ms) bring the average down

---

#### Panel 4: Worker Execution Rate
**Bottom-left graph**

**Expected Pattern:**
- **Continuous (worker_id=1)**: Steady line at ~2.0/sec
- **Periodic (worker_id=2)**: Steady line at ~0.5/sec  
- **Cron (worker_id=3)**: Steady line at ~0.1/sec

**How to read:**
- Hover over lines to see exact values
- Lines should be mostly flat (stable execution rate)
- Continuous worker line should be highest

---

#### Panel 5: Worker Duration (p50, p95, p99)
**Bottom-right graph**

**Expected Pattern:**
- **p50 (median)**: ~500ms
- **p95**: ~500ms
- **p99**: ~500ms

**What it shows:** 
- 50% of executions complete in X ms
- 95% of executions complete in X ms
- 99% of executions complete in X ms

**Why similar values:** The continuous worker (with 500ms delay) dominates the metrics

---

#### Panel 6: Worker Executions by Type
**Pie chart**

**Expected Distribution:**
- Continuous: ~90% (runs most frequently)
- Periodic: ~9%
- Cron: ~1%

**What it shows:** Proportion of total executions by worker type

---

#### Panel 7: Worker Errors by Type
**Table at bottom**

**Expected Result:** Empty table (no errors)

**If errors occurred:** Would show worker_id, worker_type, error_type, and count

---

### Dashboard Features to Try:

1. **Time Range** (top-right)
   - Change to "Last 5 minutes"
   - Watch metrics update in real-time

2. **Refresh Rate** (top-right)
   - Set to "5s" auto-refresh
   - Watch counters increase live

3. **Panel Zoom**
   - Click and drag on any graph to zoom into a time range
   - Click "Reset zoom" to go back

4. **Panel Full Screen**
   - Click panel title → View
   - See metrics in full screen

---

## 🎯 Step 5: Verify End-to-End Correlation

### Test: Trace → Metrics Correlation

1. **In Jaeger**: Find a trace for continuous worker at time X
2. **In Grafana**: Check "Worker Execution Rate" graph at same time X
3. **Verify**: You should see a data point on the graph matching that execution

### Test: Metrics → Logs Correlation

1. **In Terminal**: Note the timestamp of a log entry (e.g., `19:25:10`)
2. **In Jaeger**: Find traces around that timestamp
3. **Verify**: Traces exist at that exact time

---

## 🐛 Troubleshooting

### No Data in Jaeger?
```bash
# Check if sample is running
ps aux | grep MinimalWorker.OpenTelemetry.Sample

# Check if Jaeger received data
curl http://localhost:16686/api/services
# Should show: "MinimalWorker.Sample"
```

### No Metrics in Prometheus?
```bash
# Check if Prometheus can scrape
curl http://localhost:9090/api/v1/targets

# Verify OTLP endpoint is reachable
curl http://localhost:4317
```

### Grafana Dashboard Empty?
1. Go to **Configuration** → **Data Sources**
2. Verify **Prometheus** is green
3. Verify **Jaeger** is green
4. Try clicking **Test** button on each

### Sample Not Showing Logs?
```bash
# Make sure log level is Info (not Warning)
# Check Program.cs:
builder.Logging.SetMinimumLevel(LogLevel.Information);
```

---

## 📚 Advanced: Custom Queries to Try

### Prometheus

**Top 3 Slowest Workers:**
```promql
topk(3, avg by (worker_id) (minimalworker_minimal_worker_duration_milliseconds_sum))
```

**Worker Throughput (total exec/sec):**
```promql
sum(rate(minimalworker_minimal_worker_executions_total[1m]))
```

**Worker Success Rate (should be 100%):**
```promql
(sum(rate(minimalworker_minimal_worker_executions_total[5m])) - sum(rate(minimalworker_minimal_worker_errors_total[5m]))) 
/ 
sum(rate(minimalworker_minimal_worker_executions_total[5m])) * 100
```

### Jaeger

**Find Longest Traces:**
- Set **Min Duration**: 400ms
- Should find continuous worker traces

**Find by Tag:**
- Add tag: `worker.iteration:100`
- Finds the 100th execution of continuous worker

---

## ✅ Success Checklist

After running for 3 minutes, you should have:

- [ ] **Jaeger**: 300+ traces visible
- [ ] **Jaeger**: Traces have correct tags (worker.id, worker.type, worker.iteration)
- [ ] **Prometheus**: `minimalworker_minimal_worker_executions_total` > 300
- [ ] **Prometheus**: Graph shows linear increase
- [ ] **Grafana**: "Worker Executions" panel shows 300+
- [ ] **Grafana**: Execution rate graphs show stable lines
- [ ] **Grafana**: No errors in "Worker Errors" panel
- [ ] **Console**: Seeing log entries for all 3 worker types

---

## 🎉 What You've Demonstrated

You now have a **production-grade observability stack** showing:

✅ **Distributed Tracing** - See individual worker executions with timing  
✅ **Metrics Collection** - Track execution counts, rates, and duration  
✅ **Real-time Monitoring** - Live dashboards updating every 5 seconds  
✅ **Zero-code Instrumentation** - MinimalWorker adds observability automatically  
✅ **Industry Standard Tools** - Jaeger, Prometheus, Grafana (used in production)  
✅ **OpenTelemetry Native** - Vendor-neutral, future-proof telemetry

This is the **exact same setup** you'd use in production Kubernetes clusters!

---

**Happy Observing! 🚀**
