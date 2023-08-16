namespace CosmosBenchmark
{
    using System.Diagnostics.Tracing;

    [EventSource(Name = "Azure.Cosmos.Benchmark.Metrics")]
    internal class BenchmarkExecutionEventSource : EventSource
    {
        public static BenchmarkExecutionEventSource Instance = new BenchmarkExecutionEventSource();

        private BenchmarkExecutionEventSource() { }

        [Event(2, Level = EventLevel.Informational)]
        public void NotifySuccess(BenchmarkOperationType operationType, long durationInMs)
        {
            this.WriteEvent(2, (int)operationType, durationInMs);
        }

        [Event(3, Level = EventLevel.Informational)]
        public void NotifyFailure(BenchmarkOperationType operationType, long durationInMs)
        {
            this.WriteEvent(3, (int)operationType, durationInMs);
        }
    }
}
