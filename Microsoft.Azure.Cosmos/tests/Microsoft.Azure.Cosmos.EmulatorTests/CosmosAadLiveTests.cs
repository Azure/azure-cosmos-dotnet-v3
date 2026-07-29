//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Reflection;
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
    /// green until the account is provisioned. That skip behavior is for local, opt-in runs only: CI sets
    /// <c>COSMOSDB_AAD_STRICT=true</c>, which turns every one of those unsatisfied prerequisites into a hard
    /// failure so the dedicated AAD lane can never go green without actually exercising Entra auth.
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

        /// <summary>
        /// The number of <c>MultiRegionAad</c> test cases in this class, counting each
        /// <see cref="DataRowAttribute"/> separately. The CI lane gates on exactly this many passing with
        /// zero skipped/inconclusive results, so keep the <c>ExpectedTestCount</c> parameter in
        /// <c>templates/build-test-aad.yml</c> in sync when adding or removing cases.
        /// </summary>
        internal const int ExpectedTestCaseCount = 8;

        private CosmosClient aadClient;
        private Container container;

        [TestInitialize]
        public async Task TestInitAsync()
        {
            if (string.IsNullOrEmpty(TestCommon.GetAadAccountEndpoint()))
            {
                CosmosAadLiveTests.SkipOrFail("Set COSMOSDB_MULTI_REGION_AAD (or COSMOSDB_MULTI_REGION) to the AAD account endpoint to run the live AAD tests.");
            }

            if (TestCommon.GetAadTokenCredential() == null)
            {
                CosmosAadLiveTests.SkipOrFail("Set AZURE_TENANT_ID / AZURE_CLIENT_ID / AZURE_CLIENT_SECRET (or COSMOSDB_AAD_USE_DEFAULT_CREDENTIAL=true) to run the live AAD tests.");
            }

            this.aadClient = TestCommon.CreateAadCosmosClient();
            Assert.IsNotNull(this.aadClient, "Live AAD account/credentials are not configured.");
            this.container = this.aadClient.GetContainer(DatabaseId, ContainerId);

            // The account is AAD-only and the service principal is data-plane only, so the
            // database/container must already exist. Verify with a data-plane metadata read and skip
            // (locally) or fail (in strict/CI mode) when the resources or the role assignment are not in place.
            try
            {
                await this.container.ReadContainerAsync();
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                CosmosAadLiveTests.SkipOrFail($"Pre-create database '{DatabaseId}' and container '{ContainerId}' (/pk) on the AAD account before running these tests. Response: {ex.Message}");
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Forbidden || ex.StatusCode == HttpStatusCode.Unauthorized)
            {
                CosmosAadLiveTests.SkipOrFail($"The AAD principal is missing the Cosmos DB data-plane role assignment (Cosmos DB Built-in Data Contributor). Response: {ex.Message}");
            }
        }

        /// <summary>
        /// Reports an unsatisfied prerequisite for the live AAD tests.
        ///
        /// By default the case is skipped via <see cref="Assert.Inconclusive(string)"/>, which keeps local,
        /// opt-in runs green for developers who have not provisioned the AAD account. When
        /// <c>COSMOSDB_AAD_STRICT</c> is set (the dedicated CI lane does so), the same condition becomes a
        /// hard failure instead: an inconclusive run still lets MSTest exit successfully, so without this a
        /// missing role assignment, a stale endpoint, or a missing fixture could leave the lane green
        /// without validating a single Entra-authenticated operation.
        /// </summary>
        private static void SkipOrFail(string message)
        {
            if (TestCommon.IsAadStrictMode())
            {
                Assert.Fail($"COSMOSDB_AAD_STRICT is enabled, so an unsatisfied live AAD prerequisite is a failure rather than a skip: {message}");
            }
            else
            {
                Assert.Inconclusive(message);
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

    /// <summary>
    /// Guards the constant the CI result gate is built on.
    ///
    /// The live AAD lane requires exactly <see cref="CosmosAadLiveTests.ExpectedTestCaseCount"/> passing
    /// cases with zero skipped/inconclusive results (see the <c>ExpectedTestCount</c> parameter in
    /// <c>templates/build-test-aad.yml</c>). This test runs in the ordinary emulator lane -- it needs no
    /// live account -- and fails with an actionable message the moment a case is added or removed, so the
    /// expected count cannot silently drift away from what the pipeline enforces.
    /// </summary>
    [TestClass]
    public class CosmosAadLiveTestsGateTests
    {
        private const string MultiRegionAadCategory = "MultiRegionAad";

        [TestMethod]
        public void ExpectedTestCaseCountMatchesDiscoveredTestCases()
        {
            int discovered = 0;
            foreach (MethodInfo method in typeof(CosmosAadLiveTests).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (method.GetCustomAttribute<TestMethodAttribute>() == null)
                {
                    continue;
                }

                IEnumerable<string> categories = method
                    .GetCustomAttributes<TestCategoryAttribute>()
                    .SelectMany(attribute => attribute.TestCategories);

                Assert.IsTrue(
                    categories.Contains(MultiRegionAadCategory),
                    $"{method.Name} must be tagged [TestCategory(\"{MultiRegionAadCategory}\")] so it runs in the live AAD lane that the CI result gate validates.");

                int dataRowCount = method.GetCustomAttributes<DataRowAttribute>().Count();
                discovered += dataRowCount == 0 ? 1 : dataRowCount;
            }

            Assert.AreEqual(
                CosmosAadLiveTests.ExpectedTestCaseCount,
                discovered,
                $"The number of {MultiRegionAadCategory} test cases changed. Update CosmosAadLiveTests.ExpectedTestCaseCount and the ExpectedTestCount parameter in templates/build-test-aad.yml together, otherwise the live AAD lane's result gate will reject an otherwise healthy run.");
        }
    }
}
