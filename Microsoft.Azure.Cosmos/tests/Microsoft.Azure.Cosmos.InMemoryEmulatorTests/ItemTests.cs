//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.InMemoryEmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;

    [TestClass]
    [TestCategory("InMemoryEmulator")]
    public class ItemTests
    {
        private static InMemoryEmulator emulator;
        private CosmosClient client;
        private HttpClient httpClient;
        private Container container;

        [ClassInitialize]
        public static async Task InitializeAsync(TestContext context)
        {
            string executable = Environment.GetEnvironmentVariable(InMemoryEmulator.ExecutableVariable);
            if (string.IsNullOrEmpty(executable))
            {
                Assert.Inconclusive($"Set {InMemoryEmulator.ExecutableVariable} to run against the Rust in-memory emulator. See this project's README.md.");
            }

            emulator = await InMemoryEmulator.StartAsync(
                executable, Path.Combine(AppContext.BaseDirectory, "emulator-config.json"));
            context.WriteLine($"In-memory emulator ready: {emulator.AccountEndpoint}");
        }

        [ClassCleanup]
        public static async Task CleanupAsync()
        {
            if (emulator != null)
            {
                await emulator.DisposeAsync();
            }
        }

        [TestInitialize]
        public void InitializeClient()
        {
            // Authentication is not enforced by this test host; never use account credentials here.
            this.httpClient = new HttpClient(new HttpClientHandler { UseProxy = false });
            this.client = CreateClient(emulator.AccountEndpoint, this.httpClient);
            this.container = this.client.GetContainer("itemdb", "items");
        }

        [TestCleanup]
        public void CleanupClient()
        {
            this.client?.Dispose();
            this.httpClient?.Dispose();
        }

        [TestMethod]
        [Timeout(60000)]
        public async Task ItemCrudRoundTripsThroughGateway()
        {
            string id = Guid.NewGuid().ToString();
            PartitionKey key = new PartitionKey(id);
            JObject item = new JObject { ["id"] = id, ["pk"] = id, ["value"] = 42 };
            try
            {
                ItemResponse<JObject> created = await this.container.CreateItemAsync(item, key);
                Assert.AreEqual(HttpStatusCode.Created, created.StatusCode);
                Assert.IsFalse(string.IsNullOrEmpty(created.ETag));
                Assert.IsTrue(created.RequestCharge > 0);

                ItemResponse<JObject> read = await this.container.ReadItemAsync<JObject>(id, key);
                Assert.AreEqual(42, (int)read.Resource["value"]);
                Assert.AreEqual(created.ETag, read.ETag);

                item["value"] = 43;
                ItemResponse<JObject> replaced = await this.container.ReplaceItemAsync(item, id, key);
                Assert.AreEqual(HttpStatusCode.OK, replaced.StatusCode);
                Assert.AreEqual(43, (int)replaced.Resource["value"]);

                item["value"] = 44;
                ItemResponse<JObject> upserted = await this.container.UpsertItemAsync(item, key);
                Assert.AreEqual(HttpStatusCode.OK, upserted.StatusCode);
                Assert.AreEqual(44, (int)(await this.container.ReadItemAsync<JObject>(id, key)).Resource["value"]);
            }
            finally
            {
                await this.DeleteIfPresentAsync(id, key);
            }

            using ResponseMessage missing = await this.container.ReadItemStreamAsync(id, key);
            Assert.AreEqual(HttpStatusCode.NotFound, missing.StatusCode);
        }

        [TestMethod]
        [Timeout(60000)]
        public async Task PartitionKeysIsolateIdenticalIds()
        {
            string id = Guid.NewGuid().ToString();
            PartitionKey first = new PartitionKey(id + "-a");
            PartitionKey second = new PartitionKey(id + "-b");
            try
            {
                await this.container.CreateItemAsync(new { id, pk = id + "-a", value = 1 }, first);
                await this.container.CreateItemAsync(new { id, pk = id + "-b", value = 2 }, second);
                Assert.AreEqual(1, (int)(await this.container.ReadItemAsync<JObject>(id, first)).Resource["value"]);
                Assert.AreEqual(2, (int)(await this.container.ReadItemAsync<JObject>(id, second)).Resource["value"]);
                using ResponseMessage missing = await this.container.ReadItemStreamAsync(id, new PartitionKey(id + "-missing"));
                Assert.AreEqual(HttpStatusCode.NotFound, missing.StatusCode);
            }
            finally
            {
                await this.DeleteIfPresentAsync(id, first);
                await this.DeleteIfPresentAsync(id, second);
            }
        }

        [TestMethod]
        [Timeout(60000)]
        public async Task DuplicateAndStaleEtagReturnServiceErrors()
        {
            string id = Guid.NewGuid().ToString();
            PartitionKey key = new PartitionKey(id);
            JObject item = new JObject { ["id"] = id, ["pk"] = id, ["value"] = 1 };
            try
            {
                ItemResponse<JObject> created = await this.container.CreateItemAsync(item, key);
                CosmosException conflict = await Assert.ThrowsExceptionAsync<CosmosException>(
                    () => this.container.CreateItemAsync(item, key));
                Assert.AreEqual(HttpStatusCode.Conflict, conflict.StatusCode);
                item["value"] = 2;
                await this.container.ReplaceItemAsync(item, id, key);
                CosmosException stale = await Assert.ThrowsExceptionAsync<CosmosException>(
                    () => this.container.ReplaceItemAsync(item, id, key, new ItemRequestOptions { IfMatchEtag = created.ETag }));
                Assert.AreEqual(HttpStatusCode.PreconditionFailed, stale.StatusCode);
                Assert.AreEqual(2, (int)(await this.container.ReadItemAsync<JObject>(id, key)).Resource["value"]);
            }
            finally
            {
                await this.DeleteIfPresentAsync(id, key);
            }
        }

        [TestMethod]
        [Timeout(60000)]
        public async Task PartitionScopedReadFeedPaginates()
        {
            string partition = Guid.NewGuid().ToString();
            PartitionKey key = new PartitionKey(partition);
            List<string> ids = new List<string> { partition + "-1", partition + "-2", partition + "-3" };
            try
            {
                foreach (string id in ids)
                {
                    await this.container.CreateItemAsync(new { id, pk = partition }, key);
                }

                HashSet<string> actual = new HashSet<string>();
                int pages = 0;
                using FeedIterator<JObject> iterator = this.container.GetItemQueryIterator<JObject>(
                    requestOptions: new QueryRequestOptions { PartitionKey = key, MaxItemCount = 1 });
                while (iterator.HasMoreResults)
                {
                    FeedResponse<JObject> page = await iterator.ReadNextAsync();
                    Assert.AreEqual(HttpStatusCode.OK, page.StatusCode);
                    Assert.IsTrue(page.Count <= 1);
                    foreach (JObject item in page)
                    {
                        Assert.AreEqual(partition, (string)item["pk"]);
                        Assert.IsTrue(actual.Add((string)item["id"]), "The feed returned a duplicate item.");
                    }

                    pages++;
                    Assert.IsTrue(pages <= 10, "The feed did not finish within the expected page bound.");
                }

                CollectionAssert.AreEquivalent(ids, new List<string>(actual));
                Assert.IsTrue(pages >= 3);
            }
            finally
            {
                foreach (string id in ids)
                {
                    await this.DeleteIfPresentAsync(id, key);
                }
            }
        }

        [TestMethod]
        [Timeout(60000)]
        public async Task PatchPersistsUpdatedValue()
        {
            string id = Guid.NewGuid().ToString();
            PartitionKey key = new PartitionKey(id);
            try
            {
                await this.container.CreateItemAsync(new { id, pk = id, value = 1 }, key);
                ItemResponse<JObject> patched = await this.container.PatchItemAsync<JObject>(
                    id, key, new[] { PatchOperation.Increment("/value", 4) });
                Assert.AreEqual(HttpStatusCode.OK, patched.StatusCode);
                Assert.AreEqual(5, (int)patched.Resource["value"]);
                Assert.AreEqual(5, (int)(await this.container.ReadItemAsync<JObject>(id, key)).Resource["value"]);
            }
            finally
            {
                await this.DeleteIfPresentAsync(id, key);
            }
        }

        [TestMethod]
        [Timeout(60000)]
        public async Task InvalidConfigurationReportsStartupFailure()
        {
            string config = Path.GetTempFileName();
            try
            {
                await File.WriteAllTextAsync(config, "not json");
                InvalidOperationException error = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                    () => InMemoryEmulator.StartAsync(
                        Environment.GetEnvironmentVariable(InMemoryEmulator.ExecutableVariable), config));
                StringAssert.Contains(error.Message, "startup failed");
                StringAssert.Contains(error.Message, "exited before");
                StringAssert.Contains(error.Message, "Error:");
            }
            finally
            {
                File.Delete(config);
            }
        }

        [TestMethod]
        [Timeout(60000)]
        public async Task HostsHaveIsolatedStateAndStopOnDisposal()
        {
            string id = Guid.NewGuid().ToString();
            PartitionKey key = new PartitionKey(id);
            Uri managementEndpoint;
            using HttpClient http = new HttpClient(new HttpClientHandler { UseProxy = false })
            {
                Timeout = TimeSpan.FromSeconds(5),
            };
            await using (InMemoryEmulator isolated = await InMemoryEmulator.StartAsync(
                Environment.GetEnvironmentVariable(InMemoryEmulator.ExecutableVariable),
                Path.Combine(AppContext.BaseDirectory, "emulator-config.json")))
            {
                managementEndpoint = isolated.ManagementEndpoint;
                using CosmosClient isolatedClient = CreateClient(isolated.AccountEndpoint, http);
                Container isolatedContainer = isolatedClient.GetContainer("itemdb", "items");
                await isolatedContainer.CreateItemAsync(new { id, pk = id }, key);
                Assert.AreEqual(HttpStatusCode.OK, (await isolatedContainer.ReadItemAsync<JObject>(id, key)).StatusCode);
                using ResponseMessage missing = await this.container.ReadItemStreamAsync(id, key);
                Assert.AreEqual(HttpStatusCode.NotFound, missing.StatusCode);
            }

            using HttpClient probe = new HttpClient(new HttpClientHandler { UseProxy = false })
            {
                Timeout = TimeSpan.FromSeconds(5),
            };
            await Assert.ThrowsExceptionAsync<HttpRequestException>(
                () => probe.GetAsync(new Uri(managementEndpoint, "health")));
        }

        private static CosmosClient CreateClient(Uri endpoint, HttpClient httpClient)
        {
            return new CosmosClient(endpoint.ToString(), Convert.ToBase64String(new byte[64]),
                new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Gateway,
                    LimitToEndpoint = true,
                    RequestTimeout = TimeSpan.FromSeconds(10),
                    HttpClientFactory = () => httpClient,
                });
        }

        private async Task DeleteIfPresentAsync(string id, PartitionKey key)
        {
            using ResponseMessage response = await this.container.DeleteItemStreamAsync(id, key);
            if (response.StatusCode != HttpStatusCode.NotFound)
            {
                response.EnsureSuccessStatusCode();
            }
        }
    }
}