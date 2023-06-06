namespace Microsoft.Azure.Cosmos.Benchmarks
{
    using System;
    using BenchmarkDotNet.Attributes;
    using Microsoft.Azure.Documents;

    [MemoryDiagnoser]
    public class DateTimeAllocationsBenchmark
    {
        public DateTimeAllocationsBenchmark() 
        { 
        }

        [Benchmark]
        public void DateTimeToString()
        {
            _ = DateTime.UtcNow.ToString("r");
        }

        [Benchmark]
        public void Rfc1123DateTimeCacheUtcNow()
        {
            _ = Rfc1123DateTimeCache.UtcNow();
        }

    }
}
