//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using global::Azure.Core.Serialization;

    public class MultiRegionSetupHelpers
    {
        public const string dbName = "integrationTestDb";
        public const string containerName = "integrationTestContainer";
        public const string changeFeedContainerName = "integrationTestChangeFeedContainer";

        public static async Task<(Database, Container, Container)> GetOrCreateMultiRegionDatabaseAndContainers(CosmosClient client)
        {
            Database database;
            Container container;
            Container changeFeedContainer;

            DatabaseResponse db = await client.CreateDatabaseIfNotExistsAsync(
                id: MultiRegionSetupHelpers.dbName,
                throughput: 400);
            database = db.Database;

            if (db.StatusCode == HttpStatusCode.Created)
            {
                container = await database.CreateContainerIfNotExistsAsync(
                    id: MultiRegionSetupHelpers.containerName,
                    partitionKeyPath: "/pk",
                    throughput: 400);
                changeFeedContainer = await database.CreateContainerIfNotExistsAsync(
                    id: MultiRegionSetupHelpers.changeFeedContainerName,
                    partitionKeyPath: "/partitionKey",
                    throughput: 400);

                List<Task> tasks = new List<Task>()
                {
                    container.CreateItemAsync<CosmosIntegrationTestObject>(
                        new CosmosIntegrationTestObject { Id = "testId", Pk = "pk" }),
                    container.CreateItemAsync<CosmosIntegrationTestObject>(
                        new CosmosIntegrationTestObject { Id = "testId2", Pk = "pk2" }),
                    container.CreateItemAsync<CosmosIntegrationTestObject>(
                        new CosmosIntegrationTestObject { Id = "testId3", Pk = "pk3" }),
                    container.CreateItemAsync<CosmosIntegrationTestObject>(
                        new CosmosIntegrationTestObject { Id = "testId4", Pk = "pk4" })
                };

                await Task.WhenAll(tasks);

                //Must Ensure the data is replicated to all regions
                await Task.Delay(60000);

                return (database, container, changeFeedContainer);
            }

            container = database.GetContainer(MultiRegionSetupHelpers.containerName);
            changeFeedContainer = database.GetContainer(MultiRegionSetupHelpers.changeFeedContainerName);

            return (database, container, changeFeedContainer);
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
