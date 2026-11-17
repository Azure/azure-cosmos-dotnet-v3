// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;
    using PartitionKey = Cosmos.PartitionKey;

    [TestClass]
    [DoNotParallelize]
    public class DistributedTransactionPartitionKeyNoneE2ETests
    {
        private CosmosClient client;
        private Database database;

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public async Task TestInitialize()
        {
            string endpoint = Environment.GetEnvironmentVariable("COSMOS_DTX_ENDPOINT");
            string key = Environment.GetEnvironmentVariable("COSMOS_DTX_KEY");
            if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(key))
            {
                Assert.Inconclusive("Set COSMOS_DTX_ENDPOINT and COSMOS_DTX_KEY to run against a live DTx-enabled account.");
            }

            this.client = new CosmosClient(endpoint, key, new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Gateway,
                ConsistencyLevel = ConsistencyLevel.Session
            });
            this.database = (await this.client.CreateDatabaseAsync($"DtxNoneE2E-{Guid.NewGuid():N}")).Database;
        }

        [TestCleanup]
        public async Task TestCleanup()
        {
            try
            {
                if (this.database != null)
                {
                    await this.database.DeleteAsync();
                }
            }
            finally
            {
                this.client?.Dispose();
            }
        }

        [DataTestMethod]
        [DataRow(1, "Read")]
        [DataRow(2, "Read")]
        [DataRow(1, "Create")]
        [DataRow(2, "Create")]
        [DataRow(1, "Replace")]
        [DataRow(2, "Replace")]
        [DataRow(1, "UpsertCreate")]
        [DataRow(2, "UpsertCreate")]
        [DataRow(1, "UpsertReplace")]
        [DataRow(2, "UpsertReplace")]
        [DataRow(1, "Patch")]
        [DataRow(2, "Patch")]
        [DataRow(1, "Delete")]
        [DataRow(2, "Delete")]
        public async Task PartitionKeyNone_ReadAndWrite_Succeeds(int version, string operation)
        {
            ContainerProperties properties = new ContainerProperties("items", "/pk")
            {
                PartitionKeyDefinitionVersion = (PartitionKeyDefinitionVersion)version
            };
            Container container = (await this.database.CreateContainerAsync(properties)).Container;
            ContainerResponse metadata = await container.ReadContainerAsync();
            Assert.AreEqual(properties.PartitionKeyDefinitionVersion, metadata.Resource.PartitionKeyDefinitionVersion);

            ItemResponse<JObject> pointCreate = await container.CreateItemAsync(
                new JObject { ["id"] = "point-control", ["value"] = 0 }, PartitionKey.None);
            Assert.AreEqual(HttpStatusCode.Created, pointCreate.StatusCode);
            ItemResponse<JObject> pointRead = await container.ReadItemAsync<JObject>("point-control", PartitionKey.None);
            Assert.AreEqual(HttpStatusCode.OK, pointRead.StatusCode);
            Assert.IsNull(pointRead.Resource["pk"]);
            this.TestContext.WriteLine($"V{version} {operation}: point create=201, point read=200.");

            const string id = "target";
            bool initiallyExists = operation != "Create" && operation != "UpsertCreate";
            if (initiallyExists)
            {
                await container.CreateItemAsync(new JObject { ["id"] = id, ["value"] = 0 }, PartitionKey.None);
            }

            DistributedTransaction transaction;
            HttpStatusCode expectedStatus;
            if (operation == "Read")
            {
                transaction = this.client.CreateDistributedReadTransaction().ReadItem(container, PartitionKey.None, id);
                expectedStatus = HttpStatusCode.OK;
            }
            else
            {
                DistributedWriteTransaction write = this.client.CreateDistributedWriteTransaction();
                JObject replacement = new JObject { ["id"] = id, ["value"] = 1 };
                switch (operation)
                {
                    case "Create":
                        write.CreateItem(container, PartitionKey.None, id, replacement);
                        expectedStatus = HttpStatusCode.Created;
                        break;
                    case "Replace":
                        write.ReplaceItem(container, PartitionKey.None, id, replacement);
                        expectedStatus = HttpStatusCode.OK;
                        break;
                    case "UpsertCreate":
                    case "UpsertReplace":
                        write.UpsertItem(container, PartitionKey.None, id, replacement);
                        expectedStatus = initiallyExists ? HttpStatusCode.OK : HttpStatusCode.Created;
                        break;
                    case "Patch":
                        write.PatchItem(container, PartitionKey.None, id, new[] { PatchOperation.Set("/value", 1) });
                        expectedStatus = HttpStatusCode.OK;
                        break;
                    case "Delete":
                        write.DeleteItem(container, PartitionKey.None, id);
                        expectedStatus = HttpStatusCode.NoContent;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unsupported test operation.");
                }

                transaction = write;
            }

            using DistributedTransactionResponse response = await transaction.ExecuteTransactionAsync();
            this.TestContext.WriteLine(
                $"V{version} {operation}: DTx status={(int)response.StatusCode}, substatus={(int)response.SubStatusCode}, error={response.ErrorMessage}");
            if (!response.IsSuccessStatusCode)
            {
                using ResponseMessage unchanged = await container.ReadItemStreamAsync(id, PartitionKey.None);
                Assert.AreEqual(initiallyExists ? HttpStatusCode.OK : HttpStatusCode.NotFound, unchanged.StatusCode);
                if (initiallyExists)
                {
                    ItemResponse<JObject> original = await container.ReadItemAsync<JObject>(id, PartitionKey.None);
                    Assert.AreEqual(0, original.Resource.Value<int>("value"), "A failed transaction must not modify the item.");
                }
            }

            Assert.IsTrue(response.IsSuccessStatusCode,
                $"V{version} {operation}: DTx failed with {(int)response.StatusCode}/{(int)response.SubStatusCode}: {response.ErrorMessage}");
            Assert.AreEqual(1, response.Count);
            Assert.AreEqual(expectedStatus, response[0].StatusCode);
            if (operation == "Read")
            {
                JObject resource = response.GetOperationResultAtIndex<JObject>(0).Resource;
                Assert.AreEqual(id, resource.Value<string>("id"));
                Assert.AreEqual(0, resource.Value<int>("value"));
                Assert.IsNull(resource["pk"]);
            }

            if (operation == "Delete")
            {
                using ResponseMessage deleted = await container.ReadItemStreamAsync(id, PartitionKey.None);
                Assert.AreEqual(HttpStatusCode.NotFound, deleted.StatusCode);
            }
            else
            {
                ItemResponse<JObject> persisted = await container.ReadItemAsync<JObject>(id, PartitionKey.None);
                Assert.AreEqual(operation == "Read" ? 0 : 1, persisted.Resource.Value<int>("value"));
                Assert.IsNull(persisted.Resource["pk"]);
            }
        }
    }
}
