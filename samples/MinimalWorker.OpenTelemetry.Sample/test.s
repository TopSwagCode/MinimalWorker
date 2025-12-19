# Total executions per worker
sum by (worker_id, worker_type) (minimal_worker_executions_total)

# Execution rate (executions per second)
rate(minimal_worker_executions_total[1m])

# Error rate percentage
(sum(rate(minimal_worker_errors_total[5m])) / sum(rate(minimal_worker_executions_total[5m]))) * 100

# Average execution duration
rate(minimal_worker_duration_milliseconds_sum[5m]) / rate(minimal_worker_duration_milliseconds_count[5m])

# P95 duration
histogram_quantile(0.95, rate(minimal_worker_duration_milliseconds_bucket[5m]))

# P99 duration
histogram_quantile(0.99, rate(minimal_worker_duration_milliseconds_bucket[5m]))