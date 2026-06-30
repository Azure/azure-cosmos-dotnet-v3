//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Live-account AAD (Microsoft Entra ID) integration tests. These authenticate against a real
    /// Cosmos DB account using a real <see cref="global::Azure.Core.TokenCredential"/> (data-plane RBAC),
    /// in contrast to <see cref="CosmosAadTests"/> which fabricates a token against the local emulator.
    ///
    /// They are the AAD counterpart to the key-based <c>[TestCategory("MultiRegion")]</c> live tests and
    /// reuse the same real account: the endpoint is parsed from the <c>COSMOSDB_MULTI_REGION</c>
    /// connection string, and the test service principal credentials come from the
    /// <c>AZURE_TENANT_ID</c> / <c>AZURE_CLIENT_ID</c> / <c>AZURE_CLIENT_SECRET</c> environment variables
    /// (falling back to <see cref="global::Azure.Identity.DefaultAzureCredential"/> for local runs).
    ///
    /// When those values are not configured the tests skip cleanly via <see cref="Assert.Inconclusive(string)"/>
    /// so the suite stays green until the live account and service principal are provisioned.
    ///
    /// The service principal only needs the Cosmos DB data-plane role (Cosmos DB Built-in Data Contributor);
    /// creating the database/container therefore happens with a key-based client in <see cref="TestInitAsync"/>,
    /// and the data operations under test run on the AAD client.
    /// </summary>
    [TestClass]
    public class CosmosAadLiveTests
    {
        private const string DatabaseId = "AadLiveTestDb";
        private const string ContainerId = "AadLiveTestContainer";
        private const string PartitionKeyPath = "/pk";

        private string connectionString;
        private CosmosClient keyClient;
        private Database database;
        private Container container;

        [TestInitialize]
        public async Task TestInitAsync()
        {
            this.connectionString = TestCommon.GetMultiRegionConnectionString();
            if (string.IsNullOrEmpty(this.connectionString)
                || string.IsNullOrEmpty(TestCommon.GetMultiRegionAccountEndpoint()))
            {
                Assert.Inconclusive("Set environment variable COSMOSDB_MULTI_REGION (with an AccountEndpoint) to run the live AAD tests.");
            }

            if (TestCommon.GetAadTokenCredential() == null)
            {
                Assert.Inconclusive("Set AZURE_TENANT_ID / AZURE_CLIENT_ID / AZURE_CLIENT_SECRET (or sign in for DefaultAzureCredential) to run the live AAD tests.");
            }

            // Provision the database/container with a key-based client because the data-plane RBAC token
            // used by the AAD client cannot perform control-plane (create database/container) operations.
            this.keyClient = new CosmosClient(this.connectionString);
            this.database = await this.keyClient.CreateDatabaseIfNotExistsAsync(DatabaseId);
            this.container = await this.database.CreateContainerIfNotExistsAsync(ContainerId, PartitionKeyPath);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            // Intentionally leave the database/container in place for reuse across runs (mirrors the
            // key-based MultiRegion tests); only dispose the setup client.
            this.keyClient?.Dispose();
        }

        [TestMethod]
        [TestCategory("MultiRegionAad")]
        [DataRow(ConnectionMode.Direct)]
        [DataRow(ConnectionMode.Gateway)]
        public async Task AadReadAccountAsync(ConnectionMode connectionMode)
        {
            using CosmosClient aadClient = this.CreateAadClient(connectionMode);

            AccountProperties properties = await aadClient.ReadAccountAsync();

            Assert.IsNotNull(properties, "ReadAccountAsync should succeed with an Entra token.");
            Assert.IsNotNull(properties.Id);
        }

        [TestMethod]
        [TestCategory("MultiRegionAad")]
        [DataRow(ConnectionMode.Direct)]
        [DataRow(ConnectionMode.Gateway)]
        public async Task AadItemCrudAsync(ConnectionMode connectionMode)
        {
            using CosmosClient aadClient = this.CreateAadClient(connectionMode);
            Container aadContainer = aadClient.GetContainer(DatabaseId, ContainerId);

            ToDoActivity item = ToDoActivity.CreateRandomToDoActivity();
            PartitionKey partitionKey = new PartitionKey(item.pk);

            ItemResponse<ToDoActivity> createResponse = await aadContainer.CreateItemAsync(item, partitionKey);
            Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);

            ItemResponse<ToDoActivity> readResponse = await aadContainer.ReadItemAsync<ToDoActivity>(item.id, partitionKey);
            Assert.AreEqual(HttpStatusCode.OK, readResponse.StatusCode);
            Assert.AreEqual(item.id, readResponse.Resource.id);

            item.cost = 42.42;
            ItemResponse<ToDoActivity> replaceResponse = await aadContainer.ReplaceItemAsync(item, item.id, partitionKey);
            Assert.AreEqual(HttpStatusCode.OK, replaceResponse.StatusCode);

            item.description = "upserted";
            ItemResponse<ToDoActivity> upsertResponse = await aadContainer.UpsertItemAsync(item, partitionKey);
            Assert.AreEqual(HttpStatusCode.OK, upsertResponse.StatusCode);
            Assert.AreEqual("upserted", upsertResponse.Resource.description);

            ItemResponse<ToDoActivity> deleteResponse = await aadContainer.DeleteItemAsync<ToDoActivity>(item.id, partitionKey);
            Assert.AreEqual(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        }

        [TestMethod]
        [TestCategory("MultiRegionAad")]
        public async Task AadQueryAsync()
        {
            using CosmosClient aadClient = this.CreateAadClient(ConnectionMode.Direct);
            Container aadContainer = aadClient.GetContainer(DatabaseId, ContainerId);

            string pk = "AadQuery" + Guid.NewGuid().ToString();
            ToDoActivity item = ToDoActivity.CreateRandomToDoActivity(pk: pk);
            await aadContainer.CreateItemAsync(item, new PartitionKey(pk));

            try
            {
                QueryDefinition query = new QueryDefinition("SELECT * FROM c WHERE c.pk = @pk")
                    .WithParameter("@pk", pk);

                List<ToDoActivity> results = new List<ToDoActivity>();
                using FeedIterator<ToDoActivity> iterator = aadContainer.GetItemQueryIterator<ToDoActivity>(query);
                while (iterator.HasMoreResults)
                {
                    FeedResponse<ToDoActivity> response = await iterator.ReadNextAsync();
                    results.AddRange(response);
                }

                Assert.AreEqual(1, results.Count, "The AAD query should return the item that was just created.");
                Assert.AreEqual(item.id, results[0].id);
            }
            finally
            {
                await aadContainer.DeleteItemAsync<ToDoActivity>(item.id, new PartitionKey(pk));
            }
        }

        [TestMethod]
        [TestCategory("MultiRegionAad")]
        public async Task AadChangeFeedAsync()
        {
            using CosmosClient aadClient = this.CreateAadClient(ConnectionMode.Direct);
            Container aadContainer = aadClient.GetContainer(DatabaseId, ContainerId);

            string pk = "AadChangeFeed" + Guid.NewGuid().ToString();
            ToDoActivity item = ToDoActivity.CreateRandomToDoActivity(pk: pk);
            await aadContainer.CreateItemAsync(item, new PartitionKey(pk));

            try
            {
                int readCount = 0;
                using FeedIterator<ToDoActivity> changeFeedIterator = aadContainer.GetChangeFeedIterator<ToDoActivity>(
                    ChangeFeedStartFrom.Beginning(),
                    ChangeFeedMode.Incremental);

                while (changeFeedIterator.HasMoreResults)
                {
                    FeedResponse<ToDoActivity> response = await changeFeedIterator.ReadNextAsync();
                    if (response.StatusCode == HttpStatusCode.NotModified)
                    {
                        break;
                    }

                    readCount += response.Count;
                }

                Assert.IsTrue(readCount >= 1, "The AAD change feed read should observe at least the item that was created.");
            }
            finally
            {
                await aadContainer.DeleteItemAsync<ToDoActivity>(item.id, new PartitionKey(pk));
            }
        }

        [TestMethod]
        [TestCategory("MultiRegionAad")]
        public async Task AadControlPlaneIsForbiddenAsync()
        {
            // A data-plane-only RBAC token cannot perform control-plane operations such as creating a
            // database; the service rejects it with 403 Forbidden. This documents and guards that behavior.
            using CosmosClient aadClient = this.CreateAadClient(ConnectionMode.Gateway);

            CosmosException exception = await Assert.ThrowsExceptionAsync<CosmosException>(
                () => aadClient.CreateDatabaseAsync("AadShouldNotBeCreated" + Guid.NewGuid().ToString()));

            Assert.AreEqual(HttpStatusCode.Forbidden, exception.StatusCode,
                "Creating a database with a data-plane-only AAD token should be Forbidden.");
        }

        [TestMethod]
        [TestCategory("MultiRegionAad")]
        public void AadBackgroundTokenRefreshInterval()
        {
            using CosmosClient aadClient = this.CreateAadClient(ConnectionMode.Direct);

            TokenCredentialCache tokenCredentialCache =
                ((AuthorizationTokenProviderTokenCredential)aadClient.AuthorizationTokenProvider).tokenCredentialCache;

            Assert.IsTrue(
                tokenCredentialCache.BackgroundTokenCredentialRefreshInterval.HasValue,
                "A background refresh interval should be configured for the token credential cache.");
        }

        private CosmosClient CreateAadClient(ConnectionMode connectionMode)
        {
            CosmosClientOptions options = new CosmosClientOptions()
            {
                ConnectionMode = connectionMode,
            };

            CosmosClient aadClient = TestCommon.CreateAadCosmosClient(options);
            Assert.IsNotNull(aadClient, "Live AAD account/credentials are not configured.");
            return aadClient;
        }
    }
}
