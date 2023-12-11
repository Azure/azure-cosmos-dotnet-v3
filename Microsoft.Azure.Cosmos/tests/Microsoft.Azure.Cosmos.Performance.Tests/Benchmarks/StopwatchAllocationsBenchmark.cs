namespace Microsoft.Azure.Cosmos.Benchmarks
{
    using System;
    using BenchmarkDotNet.Attributes;
    using Microsoft.Azure.Documents;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
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
#pragma warning disable CS0414 // Remove unread private members
            private readonly Stopwatch stopwatch = new Stopwatch();
#pragma warning restore CS0414 // Remove unread private members
        }

        private class ValueStopwatchHolder
        {
#pragma warning disable CS0414 // Remove unread private members
            private readonly ValueStopwatch stopwatch = new ValueStopwatch();
#pragma warning restore CS0414 // Remove unread private members
        }
    }
}
