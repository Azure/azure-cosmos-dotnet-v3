using App.Metrics;

namespace CosmosBenchmark
{
    public class ReadBenchmarkBase : IMetricsCollector
    {
        public void CollectMetricsOnSuccess(MetricsContext metricsContext, IMetrics metrics) => metrics.Measure.Counter.Increment(metricsContext.ReadSuccessMeter);

        public void CollectMetricsOnFailure(MetricsContext metricsContext, IMetrics metrics) => metrics.Measure.Counter.Increment(metricsContext.ReadFailureMeter);
    }
}