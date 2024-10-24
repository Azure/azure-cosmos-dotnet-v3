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
                new MockedItemOfTBulkBenchmark() { BenchmarkHelper = new MockedItemBenchmarkHelper(useCustomSerializer: false, includeDiagnosticsToString: true, isDistributedTracingEnabled:true, useBulk: true) },
                new MockedItemOfTBulkBenchmark() { BenchmarkHelper = new MockedItemBenchmarkHelper(useCustomSerializer: false, includeDiagnosticsToString: true, isClientMetricsEnabled:true, useBulk: true) }
            };

        [Params(ScenarioType.Stream, 
                ScenarioType.OfT, 
                ScenarioType.OfTWithDiagnosticsToString, 
                ScenarioType.OfTCustom,
                ScenarioType.OfTWithDistributedTracingEnabled,
                ScenarioType.OfTWithClientMetricsEnabled)]
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
        public async Task DeleteItem()
        {
            await this.CurrentBenchmark.DeleteItem();
        }

        [Benchmark]
        [BenchmarkCategory("GateBenchmark")]
        public async Task ReadItem()
        {
            await this.CurrentBenchmark.ReadItem();
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