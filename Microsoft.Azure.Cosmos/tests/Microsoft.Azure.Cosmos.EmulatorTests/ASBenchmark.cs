
namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using CsvHelper;
    using global::Azure.Core.Serialization;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.FaultInjection;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Database = Database;
    using PartitionKey = PartitionKey;

    [TestClass]
    public class ASBenchmark
    {
        private CosmosSystemTextJsonSerializer cosmosSystemTextJsonSerializer;
        private string connectionString;
        private string dbName;
        private string containerName;

        public TestContext TestContext { get; set; }

        public static readonly FaultInjectionRule readsession = new FaultInjectionRuleBuilder(
            id: "readsession",
            condition: new FaultInjectionConditionBuilder()
                .WithRegion("Central US")
                .WithOperationType(FaultInjectionOperationType.ReadItem)
                .Build(),
            result: 
                FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.ReadSessionNotAvailable)
                .WithInjectionRate(.1)
                .Build())
            .WithDuration(TimeSpan.FromMinutes(90))
            .Build();

        public static readonly FaultInjectionRule readsession2 = new FaultInjectionRuleBuilder(
            id: "readSession2",
            condition: new FaultInjectionConditionBuilder()
                .WithRegion("East US")
                .WithOperationType(FaultInjectionOperationType.ReadItem)
                .Build(),
            result:
                FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.ReadSessionNotAvailable)
                .Build())
            .Build();

        public static readonly FaultInjectionRule responseDelay = new FaultInjectionRuleBuilder(
            id: "responseDely",
            condition:
                new FaultInjectionConditionBuilder()
                    .WithRegion("Central US")
                    .WithOperationType(FaultInjectionOperationType.ReadItem)
                    .Build(),
            result:
                FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.ResponseDelay)
                    .WithInjectionRate(.1)
                    .WithDelay(TimeSpan.FromSeconds(6))
                    .Build())
            .WithDuration(TimeSpan.FromMinutes(90))
            .Build();

        public static readonly FaultInjectionRule responseDelay2 = new FaultInjectionRuleBuilder(
            id: "responseDely2",
            condition:
                new FaultInjectionConditionBuilder()
                    .WithRegion("East US")
                    .WithOperationType(FaultInjectionOperationType.ReadItem)
                    .Build(),
            result:
                FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.ResponseDelay)
                    .WithInjectionRate(.1)
                    .WithDelay(TimeSpan.FromSeconds(6))
                    .Build())
            .WithDuration(TimeSpan.FromMinutes(90))
            .Build();

        private static readonly AvailabilityStrategy NoExclude40 = new CrossRegionParallelHedgingAvailabilityStrategy(TimeSpan.FromMilliseconds(40), TimeSpan.FromMilliseconds(40), false);

        private static readonly AvailabilityStrategy NoExclude30 = new CrossRegionParallelHedgingAvailabilityStrategy(TimeSpan.FromMilliseconds(30), TimeSpan.FromMilliseconds(30), false);

        private static readonly AvailabilityStrategy NoExclude100 = new CrossRegionParallelHedgingAvailabilityStrategy(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100), false);

        private static readonly AvailabilityStrategy NoExclude500 = new CrossRegionParallelHedgingAvailabilityStrategy(TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(500), false);

        private static readonly AvailabilityStrategy Exclude40 = new CrossRegionParallelHedgingAvailabilityStrategy(TimeSpan.FromMilliseconds(40), TimeSpan.FromMilliseconds(40), true);
        
        private static readonly AvailabilityStrategy Exclude30 = new CrossRegionParallelHedgingAvailabilityStrategy(TimeSpan.FromMilliseconds(30), TimeSpan.FromMilliseconds(30), true);

        private static readonly AvailabilityStrategy Exclude100 = new CrossRegionParallelHedgingAvailabilityStrategy(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100), true);

        private static readonly AvailabilityStrategy Exclude500 = new CrossRegionParallelHedgingAvailabilityStrategy(TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(500), true);

        private static readonly AvailabilityStrategy Disabled = new DisabledAvailabilityStrategy();

        private readonly Dictionary<string, FaultInjectionRule> rules = new Dictionary<string, FaultInjectionRule>()
        {
            { "readsession", readsession },
            { "readsession2", readsession2 },
            { "responseDelay", responseDelay },
            { "responseDelay2", responseDelay2 }
        };

        private readonly Dictionary<string, AvailabilityStrategy> strategies = new Dictionary<string, AvailabilityStrategy>()
        {
            { "NoExclude40", NoExclude40 },
            { "NoExclude100", NoExclude100 },
            { "NoExclude500", NoExclude500 },
            { "Exclude40", Exclude40 },
            { "Exclude100", Exclude100 },
            { "Exclude500", Exclude500 },
            { "Disabled", Disabled },
            { "NoExclude30", NoExclude30 },
            { "Exclude30", Exclude30 },
        };

        [TestInitialize]
        public async Task TestInitAsync()
        {
            Console.WriteLine("Setting up test");
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

            this.dbName = "TestDb";
            this.containerName = "TestContainer";

            //Must Ensure the data is replicated to all regions
            await Task.Delay(5000);
        }

        [TestMethod]
        public void ASBenchmarkTestDoNothing()
        {
            Assert.IsTrue(true);
        }

        //[DataTestMethod]
        //[DataRow("readsession", "readsession2", "RS-NoExclude40-3", "NoExclude40")]
        //[DataRow("readsession", "readsession2", "RS-NoExclude100-3", "NoExclude100")]
        //[DataRow("readsession", "readsession2", "RS-Off-3", "Disabled")]
        //[DataRow("readsession", "readsession2", "Exclude40-3", "Exclude40")]
        //[DataRow("readsession", "readsession2", "Exclude100-3", "Exclude100")]
        //[DataRow("responseDelay", "responseDelay2", "DELAY-NoExclude100-3", "NoExclude100")]
        //[DataRow("responseDelay", "responseDelay2", "DELAY-Exclude100-3", "Exclude100")]
        //[DataRow("responseDelay", "responseDelay2", "DELAY-NoExclude500-3", "NoExclude500")]
        //[DataRow("responseDelay", "responseDelay2", "DELAY-Exclude500-3", "Exclude500")]
        //[DataRow("responseDelay", "responseDelay2", "DELAY-Off-3", "Disabled")]
        [DataTestMethod]
        [DataRow("responseDelay", "readsession2", "RS-NoExclude30", "NoExclude30")]
        [DataRow("responseDelay", "readsession2", "RS-NoExclude30-2", "NoExclude30")]
        [DataRow("responseDelay", "readsession2", "RS-NoExclude30-3", "NoExclude30")]
        [DataRow("readsession", "readsession2", "RS-Exclude30", "Exclude30")]
        [DataRow("readsession", "readsession2", "RS-Exclude30-2", "Exclude30")]
        [DataRow("readsession", "readsession2", "RS-Exclude30-3", "Exclude30")]
        public async Task ASBenchmarkTest(string rule1name, string rule2name, string name, string availStrat)
        {
            FaultInjectionRule rule1 = this.rules[rule1name];
            FaultInjectionRule rule2 = this.rules[rule2name];

            AvailabilityStrategy strategy = this.strategies[availStrat];

            rule1.Disable();
            rule2.Disable();

            FaultInjector faultInjector = new FaultInjector(new List<FaultInjectionRule>() { rule1, rule2 });

            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                ConnectionMode = ConnectionMode.Direct,
                AvailabilityStrategy = strategy,
                ApplicationPreferredRegions = new List<string>() { "Central US", "East US", "South Central US"},
                Serializer = this.cosmosSystemTextJsonSerializer
            };

            CosmosClient faultInjectionClient = new CosmosClient(
                connectionString: this.connectionString,
                clientOptions: faultInjector.GetFaultInjectionClientOptions(clientOptions));

            Database database = faultInjectionClient.GetDatabase(this.dbName);
            Container container = database.GetContainer(this.containerName);

            rule1.Enable();
            rule2.Enable();

            await Task.Delay(3000);

            ItemResponse<AvailabilityStrategyTestObject> ir;
            int itemNum;
            int ruleHC = (int)rule1.GetHitCount();
            bool isHedged;
            Random random = new Random();
            List<HedgeDatum> hedgeData = new List<HedgeDatum>(10000);

            Console.WriteLine("Starting Benchmark: " + DateTime.UtcNow);
            this.TestContext.WriteLine("Starting Benchmark: " + DateTime.UtcNow);
            for (int i = 0; i < 10000; i++)
            {
                itemNum = random.Next(0, 10000);
                ir = await container.ReadItemAsync<AvailabilityStrategyTestObject>(
                    itemNum.ToString(),
                    new PartitionKey((itemNum % 10).ToString()));

                isHedged = ruleHC < rule1.GetHitCount();
                hedgeData.Add(new HedgeDatum()
                {
                    Id = itemNum,
                    RequestTime = ir.Diagnostics.GetClientElapsedTime(),
                    IsHedged = isHedged
                });

                if (isHedged)
                {
                    ruleHC++;
                }

                if (i % 100 == 0)
                {
                    await container.CreateItemAsync<AvailabilityStrategyTestObject>(
                        new AvailabilityStrategyTestObject()
                        {
                            Id = Guid.NewGuid().ToString(),
                            Pk = (itemNum % 10).ToString(),
                            Other = Guid.NewGuid().ToString()
                        });
                }
            }

            using (StreamWriter writer = new StreamWriter(name + ".csv"))
            using (CsvWriter csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                csv.WriteRecords(hedgeData);
            }
            faultInjectionClient.Dispose();
            Console.WriteLine("Ending Benchmark: " + DateTime.UtcNow);
        }

        private class HedgeDatum
        {

            public int Id { get; set; }
            public TimeSpan RequestTime { get; set; }
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