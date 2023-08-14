namespace CosmosBenchmark
{
    using System.Diagnostics.Tracing;

    internal class BenchmarkExecutionEventSource : EventSource
    {
        public static BenchmarkExecutionEventSource Instance = new BenchmarkExecutionEventSource();

        private BenchmarkExecutionEventSource() { }

        [Event(1, Level = EventLevel.Informational)]
        public void Completed(bool isWarmup)
        {
            if (!isWarmup)
            {
                this.WriteEvent(1);
            }
        }
    }
}
