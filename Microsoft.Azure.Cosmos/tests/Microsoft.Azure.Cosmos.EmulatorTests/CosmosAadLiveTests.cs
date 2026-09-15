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
    ///
    /// This class covers the auth-shaped scenarios (connectivity in both connection modes, the
    /// control-plane 403, token refresh). The broader data-plane surface -- queries, batch, change feed,
    /// session tokens, request options, routing -- lives in <see cref="CosmosAadLiveDataPlaneTests"/>.
    /// </summary>
    [TestClass]
    public class CosmosAadLiveTests
    {
        private const string DatabaseId = AadLiveTestSupport.DatabaseId;
        private const string ContainerId = AadLiveTestSupport.ContainerId;

        private CosmosClient aadClient;
        private Container container;

        [TestInitialize]
        public async Task TestInitAsync()
        {
            AadLiveTestSupport.ValidateConfiguration();

            this.aadClient = AadLiveTestSupport.CreateClient();
            this.container = this.aadClient.GetContainer(DatabaseId, ContainerId);

            // The account is AAD-only and the service principal is data-plane only, so the
            // database/container must already exist. Verify with a data-plane metadata read and skip
            // (locally) or fail (in strict/CI mode) when the resources or the role assignment are not in place.
            await AadLiveTestSupport.ValidateFixtureAsync(this.container);
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
        public async Task AadBackgroundTokenRefreshIntervalAsync()
        {
            // Public-surface half: an explicitly configured refresh interval is the one the client runs
            // with, and the client still authenticates against the live account with it in place.
            // CosmosClient.ClientOptions is the supported way to observe this, so this half cannot break
            // on an internal refactor.
            TimeSpan refreshInterval = TimeSpan.FromMinutes(5);
            using CosmosClient client = TestCommon.CreateAadCosmosClient(new CosmosClientOptions()
            {
                TokenCredentialBackgroundRefreshInterval = refreshInterval,
            });

            Assert.IsNotNull(client, "Live AAD account/credentials are not configured.");
            Assert.AreEqual(
                refreshInterval,
                client.ClientOptions.TokenCredentialBackgroundRefreshInterval,
                "The configured background refresh interval should be the one the client runs with.");

            ContainerResponse containerResponse = await client.GetContainer(DatabaseId, ContainerId).ReadContainerAsync();
            Assert.AreEqual(
                HttpStatusCode.OK,
                containerResponse.StatusCode,
                "An Entra-authenticated client with a background refresh interval configured should still serve requests.");

            // Internal-surface half: when no interval is configured (this.aadClient), the SDK derives one
            // from the real Entra token's lifetime. That derived value is what makes this worth running
            // against a live account -- a real token, not a fabricated one -- and CosmosClientOptions
            // cannot express it, so there is no public accessor to read it from.
            //
            // Reach into the auth plumbing behind an explicit type check rather than a direct cast: if the
            // provider type ever changes, this fails with an actionable message instead of an
            // InvalidCastException that reads like a test bug.
            AuthorizationTokenProviderTokenCredential tokenCredentialProvider =
                this.aadClient.AuthorizationTokenProvider as AuthorizationTokenProviderTokenCredential;

            Assert.IsNotNull(
                tokenCredentialProvider,
                $"A TokenCredential-based CosmosClient is expected to authenticate through {nameof(AuthorizationTokenProviderTokenCredential)}, but it used '{this.aadClient.AuthorizationTokenProvider?.GetType().Name}'. If the AAD auth plumbing was refactored intentionally, update this assertion instead of reading the failure as a live-account regression.");

            Assert.IsTrue(
                tokenCredentialProvider.tokenCredentialCache.BackgroundTokenCredentialRefreshInterval.HasValue,
                "The SDK should derive a background refresh interval from the live Entra token acquired during test setup.");
        }

        private CosmosClient CreateAadClient(ConnectionMode connectionMode)
        {
            return AadLiveTestSupport.CreateClient(new CosmosClientOptions()
            {
                ConnectionMode = connectionMode,
            });
        }
    }

    /// <summary>
    /// Verifies that a live AAD test class cannot accidentally leave one of its test methods out of the
    /// <c>MultiRegionAad</c> pipeline lane.
    /// </summary>
    [TestClass]
    public class CosmosAadLiveTestsGateTests
    {
        private const string MultiRegionAadCategory = AadLiveTestSupport.TestCategory;

        [TestMethod]
        public void AllLiveAadTestsAreCategorized()
        {
            List<string> untagged = new List<string>();

            foreach (Type testClass in typeof(CosmosAadLiveTests).Assembly.GetTypes())
            {
                if (testClass.GetCustomAttribute<TestClassAttribute>() == null)
                {
                    continue;
                }

                List<MethodInfo> testMethods = testClass
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Where(method => method.GetCustomAttribute<TestMethodAttribute>() != null)
                    .ToList();

                // MSTest's TestCategory filter honours a class-level attribute as if it were declared on
                // every method, so the gate has to resolve categories the same way or it would undercount a
                // class that tags itself once.
                bool classIsLiveAad = testClass
                    .GetCustomAttributes<TestCategoryAttribute>()
                    .SelectMany(attribute => attribute.TestCategories)
                    .Contains(MultiRegionAadCategory);

                List<MethodInfo> liveAadMethods = testMethods
                    .Where(method => classIsLiveAad || method
                        .GetCustomAttributes<TestCategoryAttribute>()
                        .SelectMany(attribute => attribute.TestCategories)
                        .Contains(MultiRegionAadCategory))
                    .ToList();

                if (liveAadMethods.Count == 0)
                {
                    continue;
                }

                // A class that is partly tagged is the dangerous case: the untagged cases silently never
                // run in the AAD lane, so the coverage they were written for is not actually validated.
                untagged.AddRange(testMethods
                    .Except(liveAadMethods)
                    .Select(method => $"{testClass.Name}.{method.Name}"));
            }

            Assert.AreEqual(
                0,
                untagged.Count,
                $"Every test on a live AAD test class must be tagged [TestCategory(\"{MultiRegionAadCategory}\")] so it runs in the live AAD lane that the CI result gate validates. Untagged: {string.Join(", ", untagged)}.");
        }
    }
}
