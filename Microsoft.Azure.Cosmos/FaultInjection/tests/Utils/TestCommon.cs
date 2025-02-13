namespace Microsoft.Azure.Cosmos.FaultInjection.Tests.Utils
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using global::Azure.Core.Serialization;
    using Microsoft.Azure.Cosmos.Fluent;

    internal static class TestCommon
    {
        public const string FaultInjectionContainerName = "faultInjectionContainer";
        public const string FaultInjectionHTPContainerName = "faultInjectionContainerHTP";
        public const string FaultInjectionDatabaseName = "faultInjectionDatabase";

        public const string EndpointMultiRegion = "";
        public const string AuthKeyMultiRegion = "";

        internal static string GetConnectionString()
        {
            return ConfigurationManager.GetEnvironmentVariable<string>("COSMOSDB_MULTI_REGION", string.Empty);
        }

        internal static async Task<(Database, Container)> GetOrCreateMultiRegionFIDatabaseAndContainersAsync(CosmosClient client)
        {
            Database database;
            Container container;

            DatabaseResponse db = await client.CreateDatabaseIfNotExistsAsync(
                id: TestCommon.FaultInjectionDatabaseName,
                throughput: 400);
            database = db.Database;

            if (db.StatusCode == HttpStatusCode.Created)
            {
                container = await database.CreateContainerIfNotExistsAsync(
                    id: TestCommon.FaultInjectionContainerName,
                    partitionKeyPath: "/pk",
                    throughput: 400);

                List<Task> tasks = new List<Task>()
                {
                    container.CreateItemAsync<FaultInjectionTestObject>(
                        new FaultInjectionTestObject { Id = "testId", Pk = "pk" }),
                    container.CreateItemAsync<FaultInjectionTestObject>(
                        new FaultInjectionTestObject { Id = "testId2", Pk = "pk2" }),
                    container.CreateItemAsync<FaultInjectionTestObject>(
                        new FaultInjectionTestObject { Id = "testId3", Pk = "pk3" }),
                    container.CreateItemAsync<FaultInjectionTestObject>(
                        new FaultInjectionTestObject { Id = "testId4", Pk = "pk4" }),
                    container.CreateItemAsync<FaultInjectionTestObject>(
                        //unsued but needed to create multiple feed ranges
                        new FaultInjectionTestObject { Id = "testId5", Pk = "qwertyuiop" }),
                    container.CreateItemAsync<FaultInjectionTestObject>(
                        new FaultInjectionTestObject { Id = "testId6", Pk = "asdfghjkl" }),
                    container.CreateItemAsync<FaultInjectionTestObject>(
                        new FaultInjectionTestObject { Id = "testId7", Pk = "zxcvbnm" }),
                    container.CreateItemAsync<FaultInjectionTestObject>(
                        new FaultInjectionTestObject { Id = "testId8", Pk = "2wsx3edc" }),
                    container.CreateItemAsync<FaultInjectionTestObject>(
                        new FaultInjectionTestObject { Id = "testId9", Pk = "5tgb6yhn" }),
                    container.CreateItemAsync<FaultInjectionTestObject>(
                        new FaultInjectionTestObject { Id = "testId10", Pk = "7ujm8ik" }),
                    container.CreateItemAsync<FaultInjectionTestObject>(
                        new FaultInjectionTestObject { Id = "testId11", Pk = "9ol" }),
                    container.CreateItemAsync<FaultInjectionTestObject>(
                        new FaultInjectionTestObject { Id = "testId12", Pk = "1234567890" })
                };

                await Task.WhenAll(tasks);

                //Must Ensure the data is replicated to all regions
                await Task.Delay(60000);

                return (database, container);
            }

            container = database.GetContainer(TestCommon.FaultInjectionContainerName);

            return (database, container);
        }

        internal class FaultInjectionTestObject
        {
            [JsonPropertyName("id")]
            public string Id { get; set; }

            [JsonPropertyName("pk")]
            public string Pk { get; set; }

            [JsonPropertyName("other")]
            public string Other { get; set; }
        }

        internal class CosmosSystemTextJsonSerializer : CosmosSerializer
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
