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

    [MemoryDiagnoser]
    [Config(typeof(CustomBenchmarkConfig))]
    public class ConcurrentDictionaryBenchmark
    {
        private ConcurrentDictionary<int, int> dictionary;

        [Params(1000, 10000, 100000)]
        public int ItemCount;

        [Params(1, 10, 100)]
        public int ReaderThreads;

        [Params(1, 5, 10)]
        public int WriterThreads;

        [GlobalSetup]
        public void Setup()
        {
            this.dictionary = new ConcurrentDictionary<int, int>();
            for (int i = 0; i < this.ItemCount; i++)
            {
                this.dictionary[i] = i;
            }
        }

        [Benchmark]
        public void ReadFromDictionary()
        {
            Parallel.For(0, this.ReaderThreads, _ =>
            {
                for (int i = 0; i < this.ItemCount; i++)
                {
                    this.dictionary.TryGetValue(i, out _);
                }
            });
        }
        [Benchmark]
        public void WriteToDictionary()
        {
            Parallel.For(0, this.WriterThreads, _ =>
            {
                for (int i = 0; i < this.ItemCount; i++)
                {
                    this.dictionary[i] = i + 1;
                }
            });
        }

        [Benchmark]
        public void ReadAndWriteToDictionary()
        {
            Parallel.Invoke(
                () => Parallel.For(0, this.ReaderThreads, _ =>
                {
                    for (int i = 0; i < this.ItemCount; i++)
                    {
                        this.dictionary.TryGetValue(i, out _);
                    }
                }),
                () => Parallel.For(0, this.WriterThreads, _ =>
                {
                    for (int i = 0; i < this.ItemCount; i++)
                    {
                        this.dictionary[i] = i + 1;
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
