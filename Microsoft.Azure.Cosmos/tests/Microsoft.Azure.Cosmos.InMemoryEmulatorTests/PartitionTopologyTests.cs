//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.InMemoryEmulatorTests
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;

    [TestClass]
    [TestCategory("InMemoryEmulator")]
    public class PartitionTopologyTests
    {
        private EmulatorControlFixture fixture;
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public async Task InitializeAsync()
        {
            this.fixture = await EmulatorControlFixture.StartAsync();
        }

        [TestCleanup]
        public async Task CleanupAsync()
        {
            if (this.fixture != null)
            {
                await this.fixture.DisposeAsync();
            }
        }

        [DataTestMethod]
        [DataRow("midpoint")]
        [DataRow("epk")]
        [DataRow("storage")]
        [Timeout(60000)]
        public async Task ManualSplitMergePreservesPartitionScopedContinuation(string mode)
        {
            (CosmosClient client, _) = this.fixture.CreateClient();
            Container container = client.GetContainer(EmulatorControlFixture.DatabaseId, EmulatorControlFixture.ContainerId);
            Dictionary<string, (string Key, int Value, string ETag)> expected = new Dictionary<string, (string, int, string)>();
            for (int index = 0; index < 32; index++)
            {
                await CreateAsync("varied-" + index, "key-" + index, index);
            }

            HashSet<string> sharedIds = new HashSet<string>();
            for (int index = 0; index < 6; index++)
            {
                string id = "shared-" + index;
                await CreateAsync(id, "shared", index);
                sharedIds.Add(id);
            }

            JArray original = await this.fixture.ReadTopologyAsync();
            Assert.AreEqual(1, original.Count);
            string parent = (string)original[0]["id"];
            using FeedIterator<JObject> firstIterator = ReadShared(container);
            FeedResponse<JObject> firstPage = await firstIterator.ReadNextAsync(this.fixture.CancellationToken);
            Assert.AreEqual(HttpStatusCode.OK, firstPage.StatusCode);
            Assert.AreEqual(2, firstPage.Count);
            string continuation = firstPage.ContinuationToken;
            Assert.IsFalse(string.IsNullOrEmpty(continuation));
            HashSet<string> firstIds = firstPage.Select(item => (string)item["id"]).ToHashSet();
            Assert.AreEqual(2, firstIds.Count);

            JObject request = new JObject { ["mode"] = mode, ["progressionMode"] = "manual" };
            if (mode == "epk")
            {
                request["epk"] = "10000000000000000000000000000000";
            }

            JObject operation = await this.fixture.SendAsync(HttpMethod.Post,
                $"databases/complexdb/containers/items/partitions/{parent}/split", request, HttpStatusCode.Accepted);
            Assert.AreEqual("Preparing", (string)operation["phase"]);
            CollectionAssert.AreEquivalent(Ids(original), Ids(await this.fixture.ReadTopologyAsync()));
            string operationId = (string)operation["operationId"];
            Assert.AreEqual("Swapping", (string)(await this.fixture.AdvanceAsync(operationId))["phase"]);
            CollectionAssert.AreEquivalent(Ids(original), Ids(await this.fixture.ReadTopologyAsync()));
            await this.fixture.AdvanceAsync(operationId);
            JObject split = await this.fixture.WaitForSuccessAsync(operationId);
            Assert.AreEqual(int.Parse(parent), (int)split["parent"]);
            Assert.AreEqual(mode, (string)split["mode"]);
            Assert.IsFalse(string.IsNullOrEmpty((string)split["splitEpk"]));
            if (mode == "epk")
            {
                Assert.AreEqual((string)request["epk"], (string)split["splitEpk"]);
            }

            string[] children = split["children"].Select(value => value.ToString()).ToArray();
            Assert.AreEqual(2, children.Length);
            Assert.AreEqual(2, children.Distinct().Count());
            Assert.IsFalse(children.Contains(parent));
            CollectionAssert.AreEquivalent(children, Ids(await this.fixture.ReadTopologyAsync()));
            HashSet<string> tail = await DrainSharedAsync(container, continuation);
            Assert.IsFalse(firstIds.Overlaps(tail));
            CollectionAssert.AreEquivalent(sharedIds.ToArray(), firstIds.Concat(tail).ToArray());
            (CosmosClient fresh, _) = this.fixture.CreateClient();
            Container freshContainer = fresh.GetContainer(EmulatorControlFixture.DatabaseId, EmulatorControlFixture.ContainerId);
            CollectionAssert.AreEquivalent(tail.ToArray(), (await DrainSharedAsync(freshContainer, continuation)).ToArray());
            await VerifyItemsAsync(children);

            JObject merge = await this.fixture.SendAsync(HttpMethod.Post,
                "databases/complexdb/containers/items/partitions/merge",
                new JObject { ["partitionIds"] = split["children"], ["progressionMode"] = "manual" },
                HttpStatusCode.Accepted);
            Assert.AreEqual("Preparing", (string)merge["phase"]);
            CollectionAssert.AreEquivalent(children, Ids(await this.fixture.ReadTopologyAsync()));
            string mergeId = (string)merge["operationId"];
            Assert.AreEqual("Swapping", (string)(await this.fixture.AdvanceAsync(mergeId))["phase"]);
            CollectionAssert.AreEquivalent(children, Ids(await this.fixture.ReadTopologyAsync()));
            await this.fixture.AdvanceAsync(mergeId);
            JObject merged = await this.fixture.WaitForSuccessAsync(mergeId);
            string mergedId = merged["into"].ToString();
            Assert.IsFalse(children.Contains(mergedId));
            CollectionAssert.AreEquivalent(new[] { mergedId }, Ids(await this.fixture.ReadTopologyAsync()));
            CollectionAssert.AreEquivalent(sharedIds.ToArray(), (await DrainSharedAsync(freshContainer, null)).ToArray());
            CollectionAssert.AreEquivalent(tail.ToArray(), (await DrainSharedAsync(freshContainer, continuation)).ToArray());
            await VerifyItemsAsync(new[] { mergedId });
            this.TestContext.WriteLine(
                $"Mode={mode}; topology={parent} -> [{string.Join(",", children)}] -> {mergedId}; " +
                $"preserved items={expected.Count}; logical-PK continuation rows={sharedIds.Count}; no duplicates.");

            async Task CreateAsync(string id, string key, int value)
            {
                ItemResponse<JObject> created = await container.CreateItemAsync(
                    new JObject { ["id"] = id, ["pk"] = key, ["value"] = value },
                    new PartitionKey(key), cancellationToken: this.fixture.CancellationToken);
                expected.Add(id, (key, value, created.ETag));
            }

            async Task VerifyItemsAsync(string[] activeRanges)
            {
                HashSet<string> observedRanges = new HashSet<string>();
                foreach (KeyValuePair<string, (string Key, int Value, string ETag)> entry in expected)
                {
                    ItemResponse<JObject> read = await container.ReadItemAsync<JObject>(
                        entry.Key, new PartitionKey(entry.Value.Key), cancellationToken: this.fixture.CancellationToken);
                    Assert.AreEqual(entry.Key, (string)read.Resource["id"]);
                    Assert.AreEqual(entry.Value.Key, (string)read.Resource["pk"]);
                    Assert.AreEqual(entry.Value.Value, (int)read.Resource["value"]);
                    Assert.AreEqual(entry.Value.ETag, read.ETag);
                    observedRanges.Add(read.Headers.Session.Split(':')[0]);
                }

                CollectionAssert.AreEquivalent(activeRanges, observedRanges.ToArray());
            }
        }

        private static string[] Ids(JArray ranges) => ranges.Select(range => (string)range["id"]).ToArray();

        private static FeedIterator<JObject> ReadShared(Container container, string continuation = null)
        {
            return container.GetItemQueryIterator<JObject>(continuationToken: continuation,
                requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey("shared"), MaxItemCount = 2 });
        }

        private async Task<HashSet<string>> DrainSharedAsync(Container container, string continuation)
        {
            HashSet<string> ids = new HashSet<string>();
            using FeedIterator<JObject> iterator = ReadShared(container, continuation);
            int pages = 0;
            while (iterator.HasMoreResults)
            {
                Assert.IsTrue(++pages <= 6, "The continuation must finish within six pages.");
                foreach (JObject item in await iterator.ReadNextAsync(this.fixture.CancellationToken))
                {
                    Assert.AreEqual("shared", (string)item["pk"]);
                    Assert.IsTrue(ids.Add((string)item["id"]), "A continuation returned a duplicate item.");
                }
            }

            return ids;
        }
    }
}
