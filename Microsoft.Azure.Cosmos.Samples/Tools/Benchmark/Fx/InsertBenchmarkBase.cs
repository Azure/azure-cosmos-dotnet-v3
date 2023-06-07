using App.Metrics;

namespace CosmosBenchmark
{
    public class InsertBenchmarkBase : IMetricsCollector
    {
        public void CollectMetricsOnSuccess(MetricsContext metricsContext, IMetrics metrics) => metrics.Measure.Counter.Increment(metricsContext.WriteSuccessMeter);

        public void CollectMetricsOnFailure(MetricsContext metricsContext, IMetrics metrics) => metrics.Measure.Counter.Increment(metricsContext.WriteFailureMeter);
    }
}