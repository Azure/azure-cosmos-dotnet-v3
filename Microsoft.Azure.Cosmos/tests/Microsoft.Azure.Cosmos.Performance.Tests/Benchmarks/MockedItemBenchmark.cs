// ----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests.Benchmarks
{
    using System.Threading.Tasks;
    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Columns;
    using BenchmarkDotNet.Configs;

    public enum ScenarioType
    {
        Stream = 0,
        //OfT = 1,
        //OfTCustom = 2,
    }

    [Config(typeof(SdkBenchmarkConfiguration))]
    public class MockedItemBenchmark : IItemBenchmark
    {
        public const int NumberOfIterations = 1000;
        public static readonly IItemBenchmark[] IterParameters = new IItemBenchmark[]
        {
            new MockedItemStreamBenchmark(),
            //new MockedItemOfTBenchmark() { BenchmarkHelper = new MockedItemBenchmarkHelper() },
            //new MockedItemOfTBenchmark() { BenchmarkHelper = new MockedItemBenchmarkHelper(useCustomSerializer: true) },
        };

        [Params(ScenarioType.Stream/*, ScenarioType.OfT, ScenarioType.OfTCustom*/)]
        public ScenarioType Type
        {
            get;
            set;
        }

        private IItemBenchmark CurrentBenchmark => IterParameters[(int)this.Type];

        [Benchmark]
        [BenchmarkCategory("GateBenchmark")]
        public async Task CreateItem()
        {
            for (int i = 0; i < NumberOfIterations; i++)
            {
                await this.CurrentBenchmark.CreateItem();
            }
        }

        //[Benchmark]
        //[BenchmarkCategory("GateBenchmark")]
        //public async Task DeleteItemExists()
        //{
        //    for (int i = 0; i < NumberOfIterations; i++)
        //    {
        //        await this.CurrentBenchmark.DeleteItemExists();
        //    }
        //}

        //[Benchmark]
        //[BenchmarkCategory("GateBenchmark")]
        //public async Task DeleteItemNotExists()
        //{
        //    for (int i = 0; i < NumberOfIterations; i++)
        //    {
        //        await this.CurrentBenchmark.DeleteItemNotExists();
        //    }
        //}

        //[Benchmark]
        //public async Task ReadFeed()
        //{
        //    for (int i = 0; i < NumberOfIterations / 10; i++)
        //    {
        //        await this.CurrentBenchmark.ReadFeed();
        //    }
        //}

        //[Benchmark]
        //[BenchmarkCategory("GateBenchmark")]
        //public async Task ReadItemExists()
        //{
        //    for (int i = 0; i < NumberOfIterations; i++)
        //    {
        //        await this.CurrentBenchmark.ReadItemExists();
        //    }
        //}

        //[Benchmark]
        //[BenchmarkCategory("GateBenchmark")]
        //public async Task ReadItemNotExists()
        //{
        //    for (int i = 0; i < NumberOfIterations; i++)
        //    {
        //        await this.CurrentBenchmark.ReadItemNotExists();
        //    }
        //}

        //[Benchmark]
        //[BenchmarkCategory("GateBenchmark")]
        //public async Task UpdateItem()
        //{
        //    for (int i = 0; i < NumberOfIterations; i++)
        //    {
        //        await this.CurrentBenchmark.UpdateItem();
        //    }
        //}

        //[Benchmark]
        //[BenchmarkCategory("GateBenchmark")]
        //public async Task UpsertItem()
        //{
        //    for (int i = 0; i < NumberOfIterations; i++)
        //    {
        //        await this.CurrentBenchmark.UpsertItem();
        //    }
        //}
    }
}
