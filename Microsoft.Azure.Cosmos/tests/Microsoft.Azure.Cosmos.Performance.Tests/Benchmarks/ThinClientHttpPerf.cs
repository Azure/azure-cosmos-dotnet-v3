//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Benchmarks
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Text.Json.Serialization;
    using System.Text.Json;
    using System.Threading.Tasks;
    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Columns;
    using BenchmarkDotNet.Configs;
    using BenchmarkDotNet.Diagnosers;
    using BenchmarkDotNet.Exporters.Csv;
    using BenchmarkDotNet.Exporters;
    using BenchmarkDotNet.Jobs;

    [MemoryDiagnoser]
    [BenchmarkCategory("ThinClientHttpPerf")]
    [Config(typeof(CustomBenchmarkConfig))]
    public class ThinClientHttpPerf
    {
        private CosmosClient client;
        private Database database;
        private Container container;
        private CosmosSystemTextJsonSerializer serializer;
        private List<CosmosIntegrationTestObject> seedItems;
        private readonly Random random = new();
        private const int SeedItemCount = 1_000;

        private const int TotalOperations = 1_000_000;

        [Params(1, 4, 16, 64)]
        public int Concurrency { get; set; }

        #region Setup / Cleanup
        [GlobalSetup]
        public async Task GlobalSetup()
        {
            string cs = Environment.GetEnvironmentVariable("COSMOSDB_THINCLIENT");
            Environment.SetEnvironmentVariable(ConfigurationManager.ThinClientModeEnabled, "True");
            if (string.IsNullOrEmpty(cs))
                throw new InvalidOperationException("COSMOSDB_THINCLIENT env-var missing.");

            this.serializer = new CosmosSystemTextJsonSerializer(
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = null,
                    PropertyNameCaseInsensitive = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });

            this.client = new CosmosClient(
                cs,
                new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Gateway,   // flip to http2/direct in your code
                    Serializer = this.serializer
                });

            string db = "TestDb_" + Guid.NewGuid();
            string col = "TestContainer_" + Guid.NewGuid();

            this.database = await this.client.CreateDatabaseIfNotExistsAsync(db);
            this.container = await this.database.CreateContainerIfNotExistsAsync(col, "/pk");

            string pk = "pk_seed";
            this.seedItems = this.GenerateItems(pk).ToList();

            foreach (CosmosIntegrationTestObject it in this.seedItems)
                await this.container.CreateItemAsync(it, new PartitionKey(it.Pk));
        }

        [GlobalCleanup]
        public async Task GlobalCleanup()
        {
            Environment.SetEnvironmentVariable(ConfigurationManager.ThinClientModeEnabled, "False");
            if (this.database != null)
                await this.database.DeleteAsync();
            this.client?.Dispose();
        }
        #endregion

        /*----------------------- 1 000 000 CREATEs ---------------------------*/
        [Benchmark(
            Description = "1 000 000 point creates (configurable concurrency)",
            OperationsPerInvoke = TotalOperations)]
        public async Task MillionPointCreatesAsync()
        {
            int opsPerWorker = TotalOperations / this.Concurrency;
            Task[] tasks = new Task[this.Concurrency];

            for (int w = 0; w < this.Concurrency; w++)
                tasks[w] = this.RunCreatesAsync(opsPerWorker);

            await Task.WhenAll(tasks);
        }

        private async Task RunCreatesAsync(int iterations)
        {
            for (int i = 0; i < iterations; i++)
            {
                CosmosIntegrationTestObject item = new CosmosIntegrationTestObject
                {
                    Id = Guid.NewGuid().ToString(),
                    Pk = "pk_create",
                    Other = "bulk-create"
                };

                using Stream s = this.serializer.ToStream(item);
                ResponseMessage rsp = await this.container.CreateItemStreamAsync(s, new PartitionKey(item.Pk));
                if (rsp.StatusCode != HttpStatusCode.Created)
                    throw new Exception($"Create failed: {item.Id}");
            }
        }

        /*------------------------ 1 000 000 READs ----------------------------*/
        [Benchmark(
            Description = "1 000 000 point reads (configurable concurrency)",
            OperationsPerInvoke = TotalOperations)]
        public async Task MillionPointReadsAsync()
        {
            int opsPerWorker = TotalOperations / this.Concurrency;
            Task[] tasks = new Task[this.Concurrency];

            for (int w = 0; w < this.Concurrency; w++)
                tasks[w] = this.RunReadsAsync(opsPerWorker);

            await Task.WhenAll(tasks);
        }

        private async Task RunReadsAsync(int iterations)
        {
            Random localRand = new();
            for (int i = 0; i < iterations; i++)
            {
                CosmosIntegrationTestObject item = this.seedItems[localRand.Next(this.seedItems.Count)];
                ResponseMessage rsp = await this.container.ReadItemStreamAsync(item.Id, new PartitionKey(item.Pk));
                if (rsp.StatusCode != HttpStatusCode.OK)
                    throw new Exception($"Read failed: {item.Id}");
            }
        }
        /*--------------------------------------------------------------------*/

        private IEnumerable<CosmosIntegrationTestObject> GenerateItems(string pk)
        {
            for (int i = 0; i < SeedItemCount; i++)
                yield return new CosmosIntegrationTestObject
                {
                    Id = Guid.NewGuid().ToString(),
                    Pk = pk,
                    Other = "seed"
                };
        }

        private class CustomBenchmarkConfig : ManualConfig
        {
            public CustomBenchmarkConfig()
            {
                this.AddColumn(StatisticColumn.OperationsPerSecond);
                this.AddColumn(StatisticColumn.P95);
                this.AddColumn(StatisticColumn.P100);

                this.AddDiagnoser(MemoryDiagnoser.Default);
                this.AddDiagnoser(ThreadingDiagnoser.Default);

                this.AddColumnProvider(DefaultConfig.Instance.GetColumnProviders().ToArray());

                this.AddJob(Job.ShortRun.WithStrategy(BenchmarkDotNet.Engines.RunStrategy.Throughput));

                this.AddExporter(HtmlExporter.Default);
                this.AddExporter(CsvExporter.Default);
            }
        }
    }

    internal class CosmosIntegrationTestObject
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("pk")]
        public string Pk { get; set; }

        [JsonPropertyName("other")]
        public string Other { get; set; }
    }
}
