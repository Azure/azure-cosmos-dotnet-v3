// ----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests.Benchmarks
{
    using System.Threading.Tasks;
    using BenchmarkDotNet.Attributes;

    public enum ScenarioType
    {
        Stream = 0,
        OfT = 1,
        OfTCustom = 2,
    }

    [Config(typeof(SdkBenchmarkConfiguration))]
    public class MockedItemBenchmark : IItemBenchmark
    {
        public static readonly IItemBenchmark[] IterParameters = new IItemBenchmark[]
            {
                new MockedItemStreamBenchmark(),
                new MockedItemOfTBenchmark() { BenchmarkHelper = new MockedItemBenchmarkHelper() },
                new MockedItemOfTBenchmark() { BenchmarkHelper = new MockedItemBenchmarkHelper(useCustomSerializer: true) },
            };

        [Params(ScenarioType.Stream, ScenarioType.OfT, ScenarioType.OfTCustom)]
        public ScenarioType Type
        {
            get;
            set;
        }

        private IItemBenchmark CurrentBenchmark => MockedItemBenchmark.IterParameters[(int)this.Type];

        [Benchmark]
        public async Task CreateItem()
        {
            await this.CurrentBenchmark.CreateItem();
        }

        [Benchmark]
        public async Task DeleteItemExists()
        {
            await this.CurrentBenchmark.DeleteItemExists();
        }

        [Benchmark]
        public async Task DeleteItemNotExists()
        {
            await this.CurrentBenchmark.DeleteItemNotExists();
        }

        [Benchmark]
        public async Task ReadFeed()
        {
            await this.CurrentBenchmark.ReadFeed();
        }

        [Benchmark]
        public async Task ReadItemExists()
        {
            await this.CurrentBenchmark.ReadItemExists();
        }

        [Benchmark]
        public async Task ReadItemNotExists()
        {
            await this.CurrentBenchmark.ReadItemNotExists();
        }

        [Benchmark]
        public async Task UpdateItem()
        {
            await this.CurrentBenchmark.UpdateItem();
        }

        [Benchmark]
        public async Task UpsertItem()
        {
            await this.CurrentBenchmark.UpsertItem();
        }
    }
}
