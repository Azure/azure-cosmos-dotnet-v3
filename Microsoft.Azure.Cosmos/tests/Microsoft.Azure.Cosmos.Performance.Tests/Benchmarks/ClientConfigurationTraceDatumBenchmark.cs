//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Performance.Tests
{
    using System.Threading.Tasks;
    using BenchmarkDotNet.Attributes;
    using Microsoft.Azure.Cosmos.Performance.Tests.Benchmarks;

    [Config(typeof(SdkBenchmarkConfiguration))]
    public class ClientConfigurationTraceDatumBenchmark
    {
        private readonly MockedItemBenchmarkHelper benchmarkHelper;
        private readonly ItemResponse<ToDoActivity> typedResponse;

        public ClientConfigurationTraceDatumBenchmark()
        {
            this.benchmarkHelper = new MockedItemBenchmarkHelper();
        }

        [Benchmark]
        [BenchmarkCategory("GateBenchmark")]
        public async Task ReadItemDiagnostics()
        {
            ItemResponse<ToDoActivity> response = await this.benchmarkHelper.TestContainer.ReadItemAsync<ToDoActivity>(
                            MockedItemBenchmarkHelper.ExistingItemId,
                            MockedItemBenchmarkHelper.ExistingPartitionId);

            response.Diagnostics.ToString();
        }

        [Benchmark(Baseline = true)]
        [BenchmarkCategory("GateBenchmark")]
        public async Task ReadItemDiagnosticsBaseline()
        {
            await this.benchmarkHelper.TestContainer.ReadItemAsync<ToDoActivity>(
                    MockedItemBenchmarkHelper.ExistingItemId,
                    MockedItemBenchmarkHelper.ExistingPartitionId);
        }
    }
}