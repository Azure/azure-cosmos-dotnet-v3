// ----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests.Benchmarks
{
    using System.Threading.Tasks;
    using BenchmarkDotNet.Attributes;

    [Config(typeof(SdkBenchmarkConfiguration))]
    public class MockedItemBulkBenchmark : IItemBulkBenchmark
    {
        public static readonly IItemBulkBenchmark[] IterParameters = new IItemBulkBenchmark[]
            {
                new MockedItemStreamBulkBenchmark(),
                new MockedItemOfTBulkBenchmark() { BenchmarkHelper = new MockedItemBenchmarkHelper(useBulk: true) },
                new MockedItemOfTBulkBenchmark() { BenchmarkHelper = new MockedItemBenchmarkHelper(useCustomSerializer: true, useBulk: true) },
                new MockedItemOfTBulkBenchmark() { BenchmarkHelper = new MockedItemBenchmarkHelper(useCustomSerializer: false, includeDiagnosticsToString: true, useBulk: true) },
            };

        [Params(ScenarioType.Stream, ScenarioType.OfT, ScenarioType.OfTWithDiagnosticsToString, ScenarioType.OfTCustom)]
        public ScenarioType Type
        {
            get;
            set;
        }

        private IItemBulkBenchmark CurrentBenchmark => MockedItemBulkBenchmark.IterParameters[(int)this.Type];

        [Benchmark]
        [BenchmarkCategory("GateBenchmark")]
        public async Task CreateItem()
        {
            await this.CurrentBenchmark.CreateItem();
        }

        [Benchmark]
        [BenchmarkCategory("GateBenchmark")]
        public async Task DeleteItemExists()
        {
            await this.CurrentBenchmark.DeleteItemExists();
        }

        [Benchmark]
        [BenchmarkCategory("GateBenchmark")]
        public async Task DeleteItemNotExists()
        {
            await this.CurrentBenchmark.DeleteItemNotExists();
        }

        [Benchmark]
        [BenchmarkCategory("GateBenchmark")]
        public async Task ReadItemExists()
        {
            await this.CurrentBenchmark.ReadItemExists();
        }

        [Benchmark]
        [BenchmarkCategory("GateBenchmark")]
        public async Task ReadItemNotExists()
        {
            await this.CurrentBenchmark.ReadItemNotExists();
        }

        [Benchmark]
        [BenchmarkCategory("GateBenchmark")]
        public async Task UpdateItem()
        {
            await this.CurrentBenchmark.UpdateItem();
        }

        [Benchmark]
        [BenchmarkCategory("GateBenchmark")]
        public async Task UpsertItem()
        {
            await this.CurrentBenchmark.UpsertItem();
        }
    }
}