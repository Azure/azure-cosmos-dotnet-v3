namespace Microsoft.Azure.Cosmos.Benchmarks
{
    using System;
    using BenchmarkDotNet.Attributes;
    using Microsoft.Azure.Documents;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

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
