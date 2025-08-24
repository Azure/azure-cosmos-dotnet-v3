namespace Microsoft.Azure.Cosmos.Benchmarks
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading.Tasks;
    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Columns;
    using BenchmarkDotNet.Configs;
    using BenchmarkDotNet.Exporters.Csv;
    using BenchmarkDotNet.Exporters;
    using BenchmarkDotNet.Jobs;
    using BenchmarkDotNet.Running;
    using System.Collections.Immutable;

    [MemoryDiagnoser]
    [Config(typeof(CustomBenchmarkConfig))]
    public class ImmutableHashSetBenchmark
    {
        private ImmutableHashSet<int> hashSet;

        [Params(1000, 10000, 100000)]
        public int ItemCount;

        [Params(1, 10, 100)]
        public int ReaderThreads;

        [Params(1, 5, 10)]
        public int WriterThreads;

        [GlobalSetup]
        public void Setup()
        {
            ImmutableHashSet<int>.Builder builder = ImmutableHashSet.CreateBuilder<int>();
            for (int i = 0; i < this.ItemCount; i++)
            {
                builder.Add(i);
            }
            this.hashSet = builder.ToImmutable();
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
                ImmutableHashSet<int> localHashSet = this.hashSet;
                for (int i = 0; i < this.ItemCount; i++)
                {
                    localHashSet = localHashSet.Add(i + 1);
                }
                this.hashSet = localHashSet;
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
                    ImmutableHashSet<int> localHashSet = this.hashSet;
                    for (int i = 0; i < this.ItemCount; i++)
                    {
                        localHashSet = localHashSet.Add(i + 1);
                    }
                    this.hashSet = localHashSet;
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
