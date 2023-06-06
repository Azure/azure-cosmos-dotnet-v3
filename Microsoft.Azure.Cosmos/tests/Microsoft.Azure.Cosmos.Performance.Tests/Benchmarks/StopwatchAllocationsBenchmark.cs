namespace Microsoft.Azure.Cosmos.Benchmarks
{
    using BenchmarkDotNet.Attributes;
    using Microsoft.Azure.Documents;
    using System.Diagnostics;

    [MemoryDiagnoser]

    public class StopwatchAllocationsBenchmark
    {
        [Benchmark]
        public void StopwatchAllocation()
        {
            _ = new Stopwatch();
        }

        [Benchmark]
        public void ValueStopwatchAllocation()
        {
            _ = new ValueStopwatch();
        }

        [Benchmark]
        public void StopwatchAsField()
        {
            _ = new StopwatchHolder();
        }

        [Benchmark]
        public void ValueStopwatchBoxedAsField()
        {
            _ = new ValueStopwatchHolder();
        }

        private class StopwatchHolder
        {
            private readonly Stopwatch stopwatch = new Stopwatch();
        }

        private class ValueStopwatchHolder
        {
            private readonly ValueStopwatch stopwatch = new ValueStopwatch();
        }
    }
}
