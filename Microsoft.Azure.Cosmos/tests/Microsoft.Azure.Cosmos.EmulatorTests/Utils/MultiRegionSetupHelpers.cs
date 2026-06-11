//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
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

            // Ensure the containers exist regardless of whether the database was just created or
            // already existed. Relying solely on db.StatusCode == Created caused tests to fail with
            // a 404 on warm-up reads when the database existed but had never been seeded (for example
            // on a fresh account where a prior run created the database but failed before seeding).
            ContainerResponse containerResponse = await database.CreateContainerIfNotExistsAsync(
                id: MultiRegionSetupHelpers.containerName,
                partitionKeyPath: "/pk",
                throughput: 400);
            container = containerResponse.Container;

            ContainerResponse changeFeedContainerResponse = await database.CreateContainerIfNotExistsAsync(
                id: MultiRegionSetupHelpers.changeFeedContainerName,
                partitionKeyPath: "/partitionKey",
                throughput: 400);
            changeFeedContainer = changeFeedContainerResponse.Container;

            // Seed the documents the tests warm up against. Upsert makes this idempotent so an
            // existing-but-unseeded container is repaired instead of silently skipped.
            bool seeded = await MultiRegionSetupHelpers.EnsureSeedItemsAsync(container);

            if (db.StatusCode == HttpStatusCode.Created
                || containerResponse.StatusCode == HttpStatusCode.Created
                || changeFeedContainerResponse.StatusCode == HttpStatusCode.Created
                || seeded)
            {
                //Must Ensure the data is replicated to all regions
                await Task.Delay(60000);
            }

            return (database, container, changeFeedContainer);
        }

        private static async Task<bool> EnsureSeedItemsAsync(Container container)
        {
            (string id, string pk)[] seedItems = new[]
            {
                ("testId", "pk"),
                ("testId2", "pk2"),
                ("testId3", "pk3"),
                ("testId4", "pk4")
            };

            bool createdAny = false;
            foreach ((string id, string pk) in seedItems)
            {
                using (ResponseMessage response = await container.ReadItemStreamAsync(id, new PartitionKey(pk)))
                {
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        continue;
                    }
                }

                await container.UpsertItemAsync<CosmosIntegrationTestObject>(
                    new CosmosIntegrationTestObject { Id = id, Pk = pk },
                    new PartitionKey(pk));
                createdAny = true;
            }

            return createdAny;
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
