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

        public ClientConfigurationTraceDatumBenchmark()
        {
            this.benchmarkHelper = new MockedItemBenchmarkHelper();
        }

        [Benchmark]
        [BenchmarkCategory("GateBenchmark")]
        public async Task ReadItemTDiagnostics()
        {
            ItemResponse<ToDoActivity> response = await this.benchmarkHelper.TestContainer.ReadItemAsync<ToDoActivity>(
                            MockedItemBenchmarkHelper.ExistingItemId,
                            MockedItemBenchmarkHelper.ExistingPartitionId);

            response.Diagnostics.ToString();
        }

        [Benchmark]
        [BenchmarkCategory("GateBenchmark")]
        public async Task ReadItemStreamDiagnostics()
        {
            using (ResponseMessage response = await this.benchmarkHelper.TestContainer.ReadItemStreamAsync(
                MockedItemBenchmarkHelper.ExistingItemId,
                new Cosmos.PartitionKey(MockedItemBenchmarkHelper.ExistingItemId)))
            {
                response.Diagnostics.ToString();
            }
        }
    }
}