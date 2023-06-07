using App.Metrics;

namespace CosmosBenchmark
{
    public class QueryBenchmarkBase : IMetricsCollector
    {
        public void CollectMetricsOnSuccess(MetricsContext metricsContext, IMetrics metrics) => metrics.Measure.Counter.Increment(metricsContext.QuerySuccessMeter);

        public void CollectMetricsOnFailure(MetricsContext metricsContext, IMetrics metrics) => metrics.Measure.Counter.Increment(metricsContext.QueryFailureMeter);
    }
}