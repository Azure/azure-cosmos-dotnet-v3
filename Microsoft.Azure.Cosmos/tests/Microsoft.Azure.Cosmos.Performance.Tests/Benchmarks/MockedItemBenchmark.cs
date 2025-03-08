// ----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests.Benchmarks
{
    using System;
    using System.Threading.Tasks;
    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Columns;
    using BenchmarkDotNet.Configs;
    using Microsoft.Azure.Cosmos.Json;

    public enum ScenarioType
    {
        Stream = 0,
        OfT = 1,
        OfTCustom = 2,
        OfTWithDiagnosticsToString = 3,
        OfTWithDistributedTracingEnabled = 4,
        OfTWithClientMetricsEnabled = 5
    }

    [Config(typeof(SdkBenchmarkConfiguration))]
    public class MockedItemBenchmark : IItemBenchmark
    {
        // Removed array initialization here — we'll do it in GlobalSetup instead
        public static IItemBenchmark[] IterParameters;

        [Params(
            ScenarioType.Stream,
            ScenarioType.OfT,
            ScenarioType.OfTCustom,
            ScenarioType.OfTWithDiagnosticsToString,
            ScenarioType.OfTWithDistributedTracingEnabled,
            ScenarioType.OfTWithClientMetricsEnabled)]
        public ScenarioType Type { get; set; }

        [Params(true, false)]
        public bool EnableBinaryResponseOnPointOperations { get; set; }

        private IItemBenchmark CurrentBenchmark => MockedItemBenchmark.IterParameters[(int)this.Type];

        [GlobalSetup]
        public void GlobalSetup()
        {
            // Set the environment variable based on the parameter
            Environment.SetEnvironmentVariable(
                ConfigurationManager.BinaryEncodingEnabled,
                this.EnableBinaryResponseOnPointOperations.ToString());

            // Determine the serialization format
            JsonSerializationFormat serializationFormat = this.EnableBinaryResponseOnPointOperations
                ? JsonSerializationFormat.Binary
                : JsonSerializationFormat.Text;

            IterParameters = new IItemBenchmark[]
            {
                // Stream scenario
                new MockedItemStreamBenchmark
                {
                    BenchmarkHelper = new MockedItemBenchmarkHelper(serializationFormat: serializationFormat)
                },

                // OfT scenario
                new MockedItemOfTBenchmark
                {
                    BenchmarkHelper = new MockedItemBenchmarkHelper(serializationFormat: serializationFormat)
                },

                // OfTCustom scenario
                new MockedItemOfTBenchmark
                {
                    BenchmarkHelper = new MockedItemBenchmarkHelper(
                        useCustomSerializer: true,
                        serializationFormat: serializationFormat)
                },

                // OfTWithDiagnosticsToString
                new MockedItemOfTBenchmark
                {
                    BenchmarkHelper = new MockedItemBenchmarkHelper(
                        useCustomSerializer: false,
                        includeDiagnosticsToString: true,
                        serializationFormat: serializationFormat)
                },

                // OfTWithDistributedTracingEnabled
                new MockedItemOfTBenchmark
                {
                    BenchmarkHelper = new MockedItemBenchmarkHelper(
                        useCustomSerializer: false,
                        includeDiagnosticsToString: false,
                        isDistributedTracingEnabled: true,
                        serializationFormat: serializationFormat)
                },

                // OfTWithClientMetricsEnabled
                new MockedItemOfTBenchmark
                {
                    BenchmarkHelper = new MockedItemBenchmarkHelper(
                        useCustomSerializer: false,
                        includeDiagnosticsToString: false,
                        isClientMetricsEnabled: true,
                        serializationFormat: serializationFormat)
                }
            };
        }

        [Benchmark]
        [BenchmarkCategory("GateBenchmark")]
        public async Task CreateItem()
        {
            await this.CurrentBenchmark.CreateItem();
        }

        [Benchmark]
        [BenchmarkCategory("GateBenchmark")]
        public async Task CreateStreamItem_EnableBinaryResponseOnPointOperations_False()
        {
            if (this.CurrentBenchmark is MockedItemStreamBenchmark streamBenchmark)
            {
                await streamBenchmark.CreateStreamItem_SetBinaryResponseOnPointOperationsFalse_ReturnsText();
            }
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
        public async Task ReadFeed()
        {
            await this.CurrentBenchmark.ReadFeed();
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

        [Benchmark]
        [BenchmarkCategory("GateBenchmark")]
        public async Task QuerySinglePartitionOnePage()
        {
            await this.CurrentBenchmark.QuerySinglePartitionOnePage();
        }

        [Benchmark]
        [BenchmarkCategory("GateBenchmark")]
        public async Task QuerySinglePartitionMultiplePages()
        {
            await this.CurrentBenchmark.QuerySinglePartitionMultiplePages();
        }
    }
}