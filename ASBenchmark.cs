
namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.IO;
    using System.Net.Http;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure.Core.Serialization;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.FaultInjection;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Database = Database;
    using PartitionKey = PartitionKey;

    [TestClass]
    public class ASBenchmark
    {

        private CosmosClient client;
        private Database database;
        private Container container;
        private Container changeFeedContainer;
        private CosmosSystemTextJsonSerializer cosmosSystemTextJsonSerializer;
        private string connectionString;
        private string dbName;
        private string containerName;
        private string changeFeedContainerName;
        private int itemCount = 10000;

        [TestInitialize]
        public async Task TestInitAsync()
        {
            this.connectionString = "";

            JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions()
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            this.cosmosSystemTextJsonSerializer = new CosmosSystemTextJsonSerializer(jsonSerializerOptions);

            if (string.IsNullOrEmpty(this.connectionString))
            {
                Assert.Fail("Set environment variable COSMOSDB_MULTI_REGION to run the tests");
            }
            this.client = new CosmosClient(
                this.connectionString,
                new CosmosClientOptions()
                {
                    Serializer = this.cosmosSystemTextJsonSerializer,
                });

            this.dbName = Guid.NewGuid().ToString();
            this.containerName = Guid.NewGuid().ToString();
            this.changeFeedContainerName = Guid.NewGuid().ToString();
            this.database = this.client.CreateDatabaseIfNotExistsAsync(this.dbName).Result;
            this.container = this.database.CreateContainerIfNotExistsAsync(this.containerName, "/pk").Result;
            this.changeFeedContainer = this.database.CreateContainerIfNotExistsAsync(this.changeFeedContainerName, "/partitionKey").Result;

            for (int i = 0; i < 100; i++)
            {
                await this.container.CreateItemAsync<AvailabilityStrategyTestObject>(
                    new AvailabilityStrategyTestObject()
                    {
                        Id = i,
                        Pk = i % 10,
                        Other = Guid.NewGuid().ToString()
                    }
            }

            //Must Ensure the data is replicated to all regions
            await Task.Delay(3000);
        }

        [TestCleanup]
        public async Task TestCleanup()
        {
            await this.database?.DeleteAsync();
            this.client?.Dispose();
        }

        [TestMethod]
        public async Task ASBenchmark()
        {
            FaultInjectionRule responseDelay = new FaultInjectionRuleBuilder(
                id: "responseDely",
                condition:
                    new FaultInjectionConditionBuilder()
                        .WithRegion("Central US")
                        .WithOperationType(FaultInjectionOperationType.ReadItem)
                        .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.ResponseDelay)
                        .WithDelay(TimeSpan.FromMilliseconds(4000))
                        .WithInjectionRate(.1)
                        .Build())
                .WithDuration(TimeSpan.FromMinutes(90))
                .Build();

            List<FaultInjectionRule> rules = new List<FaultInjectionRule>() { responseDelay };
            FaultInjector faultInjector = new FaultInjector(rules);

            responseDelay.Disable();

            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                ConnectionMode = ConnectionMode.Direct,
                ApplicationPreferredRegions = new List<string>() { "Central US", "East US" },
                AvailabilityStrategy = new CrossRegionParallelHedgingAvailabilityStrategy(
                        threshold: TimeSpan.FromMilliseconds(500)),
                Serializer = this.cosmosSystemTextJsonSerializer
            };

            CosmosClient faultInjectionClient = new CosmosClient(
                connectionString: this.connectionString,
                clientOptions: faultInjector.GetFaultInjectionClientOptions(clientOptions));

            Database database = faultInjectionClient.GetDatabase(this.dbName);
            Container container = database.GetContainer(this.containerName);

            responseDelay.Enable();
            serviceUnavailable.Enable();

            ItemResponse ir;
            int itemNum;
            int ruleHC = responseDelay.GetHitCount();
            bool isHedged;
            List<HedgeDatum> hedgeData = new List<HedgeDatum>(10000);
            for (int i = 0; i < 10000; i++)
            {
                itemNum = Random.Next(0, this.itemCount);
                ir = await container.ReadItemAsync<AvailabilityStrategyTestObject>(
                    partitionKey: new PartitionKey(itemNum % 10),
                    id: itemNum.ToString());

                isHedged = ruleHC < responseDelay.GetHitCount();
                hedgeData.Add(new HedgeDatum()
                {
                    Id = itemNum,
                    RequestTime = ir.Diagnostics.GetClientElapsedTime(),
                    IsHedged = 
                });

                if (isHedged)
                {
                    ruleHC++;
                }

                if (i % 100 == 0)
                {
                    Console.WriteLine($"{i/10000}% Complete");
                    await this.container.CreateItemAsync<AvailabilityStrategyTestObject>(
                    new AvailabilityStrategyTestObject()
                    {
                        Id = this.itemCount,
                        Pk = this.itemCount % 10,
                        Other = Guid.NewGuid().ToString()
                    }
                    this.itemCount++;
                }
            }

            using (var writer = new StreamWriter("hedgeBenchmarkBase.csv"))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                csv.WriteRecords(myPersonObjects);
            }
        }

        private class HedgeDatum
        {
            [Name("Identifier")]
            [Index(0)]
            public int Id { get; set; }
            [Index(2)]
            public Timespan RequestTime { get; set; }
            [Index(1)]
            public bool IsHedged { get; set; }
        }

        private class AvailabilityStrategyTestObject
        {

            [JsonPropertyName("id")]
            public string Id { get; set; }

            [JsonPropertyName("pk")]
            public string Pk { get; set; }

            [JsonPropertyName("other")]
            public string Other { get; set; }
        }

        private class CosmosSystemTextJsonSerializer : CosmosSerializer
        {
            private readonly JsonObjectSerializer systemTextJsonSerializer;

            public CosmosSystemTextJsonSerializer(JsonSerializerOptions jsonSerializerOptions)
            {
                this.systemTextJsonSerializer = new JsonObjectSerializer(jsonSerializerOptions);
            }

            public override T FromStream<T>(Stream stream)
            {
                using (stream)
                {
                    if (stream.CanSeek
                           && stream.Length == 0)
                    {
                        return default;
                    }

                    if (typeof(Stream).IsAssignableFrom(typeof(T)))
                    {
                        return (T)(object)stream;
                    }

                    return (T)this.systemTextJsonSerializer.Deserialize(stream, typeof(T), default);
                }
            }

            public override Stream ToStream<T>(T input)
            {
                MemoryStream streamPayload = new MemoryStream();
                this.systemTextJsonSerializer.Serialize(streamPayload, input, input.GetType(), default);
                streamPayload.Position = 0;
                return streamPayload;
            }
        }


    }
}