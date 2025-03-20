namespace Microsoft.Azure.Cosmos.Benchmarks
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Columns;
    using BenchmarkDotNet.Configs;
    using BenchmarkDotNet.Exporters.Csv;
    using BenchmarkDotNet.Exporters;
    using BenchmarkDotNet.Jobs;

    [MemoryDiagnoser]
    [Config(typeof(CustomBenchmarkConfig))]
    public class ThreadSafeHashSetBenchmark
    {
        private ThreadSafeHashSet<int> hashSet;

        [Params(1000, 10000, 100000)]
        public int ItemCount;

        [Params(1, 10, 100)]
        public int ReaderThreads;

        [Params(1, 5, 10)]
        public int WriterThreads;

        [GlobalSetup]
        public void Setup()
        {
            this.hashSet = new ThreadSafeHashSet<int>();
            for (int i = 0; i < this.ItemCount; i++)
            {
                this.hashSet.Add(i);
            }
        }

        [Benchmark]
        public void ReadFromHashSet()
        {
            Parallel.For(0, this.ReaderThreads, _ =>
            {
                for (int i = 0; i < this.ItemCount; i++)
                {
                    this.hashSet.Contains(i);
                }
            });
        }
        [Benchmark]
        public void WriteToHashSet()
        {
            Parallel.For(0, this.WriterThreads, _ =>
            {
                for (int i = 0; i < this.ItemCount; i++)
                {
                    this.hashSet.Add(i + this.ItemCount);
                }
            });
        }

        [Benchmark]
        public void ReadAndWriteToHashSet()
        {
            Parallel.Invoke(
                () => Parallel.For(0, this.ReaderThreads, _ =>
                {
                    for (int i = 0; i < this.ItemCount; i++)
                    {
                        this.hashSet.Contains(i);
                    }
                }),
                () => Parallel.For(0, this.WriterThreads, _ =>
                {
                    for (int i = 0; i < this.ItemCount; i++)
                    {
                        this.hashSet.Add(i + this.ItemCount);
                    }
                })
            );
        }
        private class CustomBenchmarkConfig : ManualConfig
        {
            public CustomBenchmarkConfig()
            {
                this.AddColumn(StatisticColumn.OperationsPerSecond);  // Show RPS
                this.AddColumn(StatisticColumn.P95);

                this.AddJob(Job.Default
                    .WithLaunchCount(1)
                    .WithWarmupCount(3)
                    .WithIterationCount(5)
                    .WithStrategy(BenchmarkDotNet.Engines.RunStrategy.Throughput));

                this.AddExporter(HtmlExporter.Default);
                this.AddExporter(CsvExporter.Default);
            }
        }
    }
}
