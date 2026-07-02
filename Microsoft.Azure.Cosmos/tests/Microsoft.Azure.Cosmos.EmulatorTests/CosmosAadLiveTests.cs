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
    /// They target a real, AAD-only account (local/key auth disabled). The endpoint comes from the
    /// <c>COSMOSDB_MULTI_REGION_AAD</c> environment variable (a bare endpoint URL, since an AAD-only
    /// account has no key), falling back to the endpoint of the key-based <c>COSMOSDB_MULTI_REGION</c>
    /// connection string. The test service principal credentials come from the
    /// <c>AZURE_TENANT_ID</c> / <c>AZURE_CLIENT_ID</c> / <c>AZURE_CLIENT_SECRET</c> environment variables.
    ///
    /// When the endpoint / AAD credentials are not configured, or the test database/container has not been
    /// pre-created, the tests skip cleanly via <see cref="Assert.Inconclusive(string)"/> so the suite stays
    /// green until the account is provisioned.
    ///
    /// Because the account is AAD-only and the service principal only holds the data-plane role
    /// (Cosmos DB Built-in Data Contributor), the database/container cannot be created at runtime (that is a
    /// control-plane operation). They must be pre-created out of band (see the setup runbook); these tests
    /// only exercise data-plane operations.
    /// </summary>
    [TestClass]
    public class CosmosAadLiveTests
    {
        private const string DatabaseId = "AadLiveTestDb";
        private const string ContainerId = "AadLiveTestContainer";

        private CosmosClient aadClient;
        private Container container;

        [TestInitialize]
        public async Task TestInitAsync()
        {
            if (string.IsNullOrEmpty(TestCommon.GetAadAccountEndpoint()))
            {
                Assert.Inconclusive("Set COSMOSDB_MULTI_REGION_AAD (or COSMOSDB_MULTI_REGION) to the AAD account endpoint to run the live AAD tests.");
            }

            if (TestCommon.GetAadTokenCredential() == null)
            {
                Assert.Inconclusive("Set AZURE_TENANT_ID / AZURE_CLIENT_ID / AZURE_CLIENT_SECRET (or COSMOSDB_AAD_USE_DEFAULT_CREDENTIAL=true) to run the live AAD tests.");
            }

            this.aadClient = TestCommon.CreateAadCosmosClient();
            Assert.IsNotNull(this.aadClient, "Live AAD account/credentials are not configured.");
            this.container = this.aadClient.GetContainer(DatabaseId, ContainerId);

            // The account is AAD-only and the service principal is data-plane only, so the
            // database/container must already exist. Verify with a data-plane metadata read and skip
            // (rather than fail) when the resources or the role assignment are not yet in place.
            try
            {
                await this.container.ReadContainerAsync();
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                Assert.Inconclusive($"Pre-create database '{DatabaseId}' and container '{ContainerId}' (/pk) on the AAD account before running these tests.");
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Forbidden || ex.StatusCode == HttpStatusCode.Unauthorized)
            {
                Assert.Inconclusive("The AAD principal is missing the Cosmos DB data-plane role assignment (Cosmos DB Built-in Data Contributor).");
            }
        }

        [TestCleanup]
        public void TestCleanup()
        {
            this.aadClient?.Dispose();
        }

        [TestMethod]
        [TestCategory("MultiRegionAad")]
        [DataRow(ConnectionMode.Direct)]
        [DataRow(ConnectionMode.Gateway)]
        public async Task AadReadAccountAsync(ConnectionMode connectionMode)
        {
            using CosmosClient client = this.CreateAadClient(connectionMode);

            AccountProperties properties = await client.ReadAccountAsync();

            Assert.IsNotNull(properties, "ReadAccountAsync should succeed with an Entra token.");
            Assert.IsNotNull(properties.Id);
        }

        [TestMethod]
        [TestCategory("MultiRegionAad")]
        [DataRow(ConnectionMode.Direct)]
        [DataRow(ConnectionMode.Gateway)]
        public async Task AadItemCrudAsync(ConnectionMode connectionMode)
        {
            using CosmosClient client = this.CreateAadClient(connectionMode);
            Container aadContainer = client.GetContainer(DatabaseId, ContainerId);

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
            string pk = "AadQuery" + Guid.NewGuid().ToString();
            ToDoActivity item = ToDoActivity.CreateRandomToDoActivity(pk: pk);
            await this.container.CreateItemAsync(item, new PartitionKey(pk));

            try
            {
                QueryDefinition query = new QueryDefinition("SELECT * FROM c WHERE c.pk = @pk")
                    .WithParameter("@pk", pk);

                List<ToDoActivity> results = new List<ToDoActivity>();
                using FeedIterator<ToDoActivity> iterator = this.container.GetItemQueryIterator<ToDoActivity>(query);
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
                await this.container.DeleteItemAsync<ToDoActivity>(item.id, new PartitionKey(pk));
            }
        }

        [TestMethod]
        [TestCategory("MultiRegionAad")]
        public async Task AadChangeFeedAsync()
        {
            string pk = "AadChangeFeed" + Guid.NewGuid().ToString();
            ToDoActivity item = ToDoActivity.CreateRandomToDoActivity(pk: pk);
            await this.container.CreateItemAsync(item, new PartitionKey(pk));

            try
            {
                int readCount = 0;
                using FeedIterator<ToDoActivity> changeFeedIterator = this.container.GetChangeFeedIterator<ToDoActivity>(
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
                await this.container.DeleteItemAsync<ToDoActivity>(item.id, new PartitionKey(pk));
            }
        }

        [TestMethod]
        [TestCategory("MultiRegionAad")]
        public async Task AadControlPlaneIsForbiddenAsync()
        {
            // A data-plane-only RBAC token cannot perform control-plane operations such as creating a
            // database; on an AAD-only account the service rejects it with 403 Forbidden. This documents
            // and guards that behavior (and is why the test database/container are pre-created).
            using CosmosClient client = this.CreateAadClient(ConnectionMode.Gateway);

            CosmosException exception = await Assert.ThrowsExceptionAsync<CosmosException>(
                () => client.CreateDatabaseAsync("AadShouldNotBeCreated" + Guid.NewGuid().ToString()));

            Assert.AreEqual(HttpStatusCode.Forbidden, exception.StatusCode,
                "Creating a database with a data-plane-only AAD token should be Forbidden.");
        }

        [TestMethod]
        [TestCategory("MultiRegionAad")]
        public void AadBackgroundTokenRefreshInterval()
        {
            TokenCredentialCache tokenCredentialCache =
                ((AuthorizationTokenProviderTokenCredential)this.aadClient.AuthorizationTokenProvider).tokenCredentialCache;

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

            CosmosClient client = TestCommon.CreateAadCosmosClient(options);
            Assert.IsNotNull(client, "Live AAD account/credentials are not configured.");
            return client;
        }
    }
}
