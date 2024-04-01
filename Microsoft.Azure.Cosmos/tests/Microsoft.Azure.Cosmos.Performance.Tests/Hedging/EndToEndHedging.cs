namespace Microsoft.Azure.Cosmos.Performance.Tests
{
    using System;
    using System.Collections.Generic;
    using BenchmarkDotNet.Attributes;
    using Microsoft.Azure.Cosmos.FaultInjection;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos;
    using System.Net;
    using Microsoft.Azure.Cosmos.Tests;
    using Microsoft.Azure.Cosmos.Tests.Json;
    using Database = Database;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using Microsoft.Azure.Cosmos.Routing;

    [MemoryDiagnoser]
    internal class EndToEndHedging
    {
        private readonly string accountEndpoint = string.Empty; // insert your endpoint here.
        private readonly string accountKey = string.Empty; // insert your key here.
        private readonly FaultInjectionRule responseDelay;
        private readonly FaultInjectionRule queryResponseDelay;
        private readonly FaultInjector faultInjector;

        private CosmosClient client;
        private Database database;
        private Container container;

        [GlobalCleanup]
        public async Task CleanupAsync()
        {
            await this.database.DeleteAsync();
            this.client.Dispose();
        }

        [GlobalSetup]
        public async Task SetUp()
        {
            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                ConnectionMode = ConnectionMode.Direct,
                ApplicationPreferredRegions = new List<string>() { "West US", "East US" },
                AvailabilityStrategy = new CrossRegionParallelHedgingAvailabilityStrategy(
                        threshold: TimeSpan.FromMilliseconds(300),
                        thresholdStep: TimeSpan.FromMilliseconds(50))
            };

            this.client = new CosmosClient(
                accountEndpoint: this.accountEndpoint,
                authKeyOrResourceToken: this.accountKey,
                clientOptions: this.faultInjector.GetFaultInjectionClientOptions(clientOptions));
            this.database = this.client.CreateDatabaseIfNotExistsAsync("BenchmarkDB").Result;
            ContainerResponse containerResponse = this.database.CreateContainerIfNotExistsAsync(
               id: "BenchmarkContainer",
               partitionKeyPath: "/id",
               throughput: 10000).Result;

            this.container = containerResponse;

            if (containerResponse.StatusCode == HttpStatusCode.Created)
            {
                for (int i = 0; i < 1000; i++)
                {
                    await this.container.CreateItemAsync(
                        item: new JObject()
                        {
                            { "id", i.ToString() },
                            { "name", Guid.NewGuid().ToString() },
                            { "otherdata", Guid.NewGuid().ToString() },
                        },
                        partitionKey: new PartitionKey(i.ToString()));
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ItemBenchmark"/> class.
        /// </summary>
        public EndToEndHedging()
        {
            if (string.IsNullOrEmpty(this.accountEndpoint) && string.IsNullOrEmpty(this.accountKey))
            {
                return;
            }

            this.responseDelay = new FaultInjectionRuleBuilder(
               id: "responseDely",
               condition:
                   new FaultInjectionConditionBuilder()
                       .WithRegion("West US")
                       .WithOperationType(FaultInjectionOperationType.ReadItem)
                       .Build(),
               result:
                   FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.ResponseDelay)
                       .WithDelay(TimeSpan.FromMilliseconds(400))
                       .Build())
               .Build();

            this.queryResponseDelay = new FaultInjectionRuleBuilder(
               id: "responseDely",
               condition:
                   new FaultInjectionConditionBuilder()
                       .WithRegion("West US")
                       .WithOperationType(FaultInjectionOperationType.ReadItem)
                       .Build(),
               result:
                   FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.ResponseDelay)
                       .WithDelay(TimeSpan.FromMilliseconds(400))
                       .Build())
               .Build();

            List<FaultInjectionRule> rules = new List<FaultInjectionRule>() { this.responseDelay, this.queryResponseDelay };
            this.faultInjector = new FaultInjector(rules);

            this.responseDelay.Disable();
            this.queryResponseDelay.Disable();
        }

        [Benchmark]
        public async Task ReadItemWithHedgeAsync()
        {
            this.responseDelay.Enable();
            for (int i = 0; i < 1000; i++)
            {
                await this.container.ReadItemAsync<CosmosElement>(
                    partitionKey: new PartitionKey(i.ToString()),
                    id: i.ToString());
            }
            this.responseDelay.Disable();
        }

        [Benchmark]
        public async Task QueryWithHedgeAsync()
        {
            this.queryResponseDelay.Enable();
            FeedIterator feedIterator = this.container.GetItemQueryStreamIterator(
                "SELECT * FROM c",
                requestOptions: new QueryRequestOptions()
                {
                    MaxConcurrency = -1,
                    MaxItemCount = -1,
                    MaxBufferedItemCount = -1,
                });
            
            while (feedIterator.HasMoreResults)
            {
                using (ResponseMessage response = await feedIterator.ReadNextAsync())
                {
                    response.EnsureSuccessStatusCode();
                }
            }
            this.queryResponseDelay.Disable();
        }

        [Benchmark]
        public async Task ReadItemWithoutHedgeAsync()
        {
            for (int i = 0; i < 1000; i++)
            {
                await this.container.ReadItemAsync<CosmosElement>(
                    partitionKey: new PartitionKey(i.ToString()),
                    id: i.ToString());
            }
        }

        [Benchmark]
        public async Task QueryWithoutHedgeAsync()
        {
            FeedIterator feedIterator = this.container.GetItemQueryStreamIterator(
                "SELECT * FROM c",
                requestOptions: new QueryRequestOptions()
                {
                    MaxConcurrency = -1,
                    MaxItemCount = -1,
                    MaxBufferedItemCount = -1,
                });

            while (feedIterator.HasMoreResults)
            {
                using (ResponseMessage response = await feedIterator.ReadNextAsync())
                {
                    response.EnsureSuccessStatusCode();
                }
            }
        }
    }
}
