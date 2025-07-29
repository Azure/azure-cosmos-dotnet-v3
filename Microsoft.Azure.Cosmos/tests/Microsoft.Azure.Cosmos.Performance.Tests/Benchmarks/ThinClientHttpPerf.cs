//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Benchmarks
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Columns;
    using BenchmarkDotNet.Configs;
    using BenchmarkDotNet.Diagnosers;
    using BenchmarkDotNet.Exporters;
    using BenchmarkDotNet.Exporters.Csv;
    using BenchmarkDotNet.Jobs;
    using Microsoft.Azure.Cosmos;

    [MemoryDiagnoser]
    [BenchmarkCategory("ThinClientHttpPerf")]
    [Config(typeof(CustomConfig))]
    public class ThinClientHttpPerf
    {
        private CosmosClient client;
        private Container container;
        private CosmosSystemTextJsonSerializer serializer;

        [Params(1, 4, 16, 64)]
        public int Concurrency { get; set; }

        [Params(1_000_000)]
        public int Operations { get; set; }

        #region setup / teardown
        [GlobalSetup]
        public async Task GlobalSetup()
        {
            string cs = Environment.GetEnvironmentVariable("COSMOSDB_THINCLIENT")
                        ?? throw new InvalidOperationException("COSMOSDB_THINCLIENT not set.");

            ConnectionMode Mode = ConnectionMode.Gateway;
            Environment.SetEnvironmentVariable(
                ConfigurationManager.ThinClientModeEnabled, "True");

            this.serializer = new CosmosSystemTextJsonSerializer(
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = null,
                    PropertyNameCaseInsensitive = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });

            // Enable bulk execution for high throughput
            this.client = new CosmosClient(
                cs,
                new CosmosClientOptions
                {
                    ConnectionMode = Mode,
                    Serializer = this.serializer,
                    AllowBulkExecution = true,
                    MaxRetryAttemptsOnRateLimitedRequests = 9,
                    MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(30)
                });

            Database db = await this.client.CreateDatabaseIfNotExistsAsync("Perf_" + Guid.NewGuid());
            this.container = await db.CreateContainerIfNotExistsAsync("Cn_" + Guid.NewGuid(), "/pk");
        }

        [GlobalCleanup]
        public async Task Cleanup()
        {
            Environment.SetEnvironmentVariable(ConfigurationManager.ThinClientModeEnabled, "False");
            if (this.container != null)
                await this.container.Database.DeleteAsync();
            this.client?.Dispose();
        }
        #endregion

        [Benchmark(Description = "Bulk creates")]
        public async Task BulkCreatesAsync()
        {
            int baseOps = this.Operations / this.Concurrency;
            int remainder = this.Operations % this.Concurrency;

            Task[] workers = new Task[this.Concurrency];
            for (int w = 0; w < this.Concurrency; w++)
            {
                int work = baseOps + (w < remainder ? 1 : 0);
                workers[w] = this.DoBulkCreatesAsync(work);
            }
            await Task.WhenAll(workers);
        }

        private async Task DoBulkCreatesAsync(int count)
        {
            List<Task> batch = new List<Task>(count);
            for (int i = 0; i < count; i++)
            {
                Doc doc = new Doc
                {
                    Id = Guid.NewGuid().ToString(),
                    Pk = "pk_bulk",
                    Other = "bulk"
                };
                batch.Add(this.container.CreateItemAsync(doc, new PartitionKey(doc.Pk)));
            }
            await Task.WhenAll(batch);
        }

        private class CustomConfig : ManualConfig
        {
            public CustomConfig()
            {
                this.AddColumn(StatisticColumn.OperationsPerSecond);
                this.AddColumn(StatisticColumn.P95);
                this.AddColumn(StatisticColumn.P100);
                this.AddDiagnoser(MemoryDiagnoser.Default);
                this.AddJob(Job.Dry
                       .WithStrategy(BenchmarkDotNet.Engines.RunStrategy.Throughput)
                       .WithIterationCount(1)
                       .WithWarmupCount(1));
                this.AddExporter(HtmlExporter.Default, CsvExporter.Default);
            }
        }

        internal class Doc
        {
            [JsonPropertyName("id")]
            public string Id { get; set; }

            [JsonPropertyName("pk")]
            public string Pk { get; set; }

            [JsonPropertyName("other")]
            public string Other { get; set; }
        }
    }
}
