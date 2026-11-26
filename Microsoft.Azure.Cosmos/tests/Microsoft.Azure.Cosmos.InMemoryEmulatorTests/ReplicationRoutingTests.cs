//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.InMemoryEmulatorTests
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;

    [TestClass]
    [TestCategory("InMemoryEmulator")]
    public class ReplicationRoutingTests
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
        [DataRow("preferred-east")]
        [DataRow("preferred-west")]
        [DataRow("application-west")]
        [Timeout(60000)]
        public async Task PausedReplicaHonorsEventualReadRouting(string strategy)
        {
            await this.fixture.PauseWestAsync();
            (CosmosClient writer, _) = this.fixture.CreateClient();
            Container writeContainer = writer.GetContainer(EmulatorControlFixture.DatabaseId, EmulatorControlFixture.ContainerId);
            await writeContainer.CreateItemAsync(new { id = "routed", pk = "routed", value = 42 },
                new PartitionKey("routed"), cancellationToken: this.fixture.CancellationToken);
            CosmosClientOptions options = strategy switch
            {
                "preferred-east" => new CosmosClientOptions { ApplicationPreferredRegions = new[] { "East US" } },
                "preferred-west" => new CosmosClientOptions { ApplicationPreferredRegions = new[] { "West US" } },
                "application-west" => new CosmosClientOptions { ApplicationRegion = "West US" },
                _ => throw new ArgumentOutOfRangeException(nameof(strategy)),
            };

            (CosmosClient reader, ObservingHttpHandler observer) = this.fixture.CreateClient(
                options: options, limitToEndpoint: false);
            using ResponseMessage response = await reader.GetContainer(EmulatorControlFixture.DatabaseId, EmulatorControlFixture.ContainerId)
                .ReadItemStreamAsync("routed", new PartitionKey("routed"),
                    new ItemRequestOptions { ConsistencyLevel = ConsistencyLevel.Eventual },
                    this.fixture.CancellationToken);
            bool east = strategy == "preferred-east";
            Assert.AreEqual(east ? HttpStatusCode.OK : HttpStatusCode.NotFound, response.StatusCode);
            ObservedRequest read = observer.ItemReads("routed").Single();
            Uri expected = this.fixture.Regions[east ? "East US" : "West US"];
            Assert.AreEqual(expected.Authority, read.Endpoint.Authority);
            Assert.AreEqual(response.StatusCode, read.Status);
            await this.fixture.ResumeWestAsync();
            this.TestContext.WriteLine($"West replication paused; strategy={strategy}; " +
                $"actual item endpoint={read.Endpoint}; status={(int)read.Status}; Eventual read.");
        }

        [TestMethod]
        [Timeout(60000)]
        public async Task ExplicitSessionTokenRecoversFromReplicaLagAndResume()
        {
            await this.fixture.PauseWestAsync();
            (CosmosClient writer, _) = this.fixture.CreateClient();
            ItemResponse<JObject> created = await writer.GetContainer(EmulatorControlFixture.DatabaseId, EmulatorControlFixture.ContainerId)
                .CreateItemAsync(new JObject { ["id"] = "lagged", ["pk"] = "lagged", ["value"] = 42 },
                    new PartitionKey("lagged"), cancellationToken: this.fixture.CancellationToken);
            string token = created.Headers.Session;
            Assert.IsFalse(string.IsNullOrEmpty(token));
            ItemRequestOptions session = new ItemRequestOptions { SessionToken = token, ConsistencyLevel = ConsistencyLevel.Session };
            (CosmosClient west, ObservingHttpHandler westObserver) = this.fixture.CreateClient(this.fixture.Regions["West US"]);
            Container westContainer = west.GetContainer(EmulatorControlFixture.DatabaseId, EmulatorControlFixture.ContainerId);
            CosmosException unavailable = await Assert.ThrowsExceptionAsync<CosmosException>(
                () => westContainer.ReadItemAsync<JObject>("lagged", new PartitionKey("lagged"), session, this.fixture.CancellationToken));
            Assert.AreEqual(HttpStatusCode.NotFound, unavailable.StatusCode);
            Assert.AreEqual(1002, unavailable.SubStatusCode);
            Assert.IsTrue(westObserver.ItemReads("lagged").Any(read => read.Status == HttpStatusCode.NotFound && read.SubStatus == 1002));
            (CosmosClient reader, ObservingHttpHandler observer) = this.fixture.CreateClient(
                options: new CosmosClientOptions { ApplicationPreferredRegions = new[] { "West US", "East US" } },
                limitToEndpoint: false);
            Container container = reader.GetContainer(EmulatorControlFixture.DatabaseId, EmulatorControlFixture.ContainerId);
            ItemResponse<JObject> recovered = await container.ReadItemAsync<JObject>(
                "lagged", new PartitionKey("lagged"), session, this.fixture.CancellationToken);
            Assert.AreEqual(42, (int)recovered.Resource["value"]);
            ObservedRequest[] attempts = observer.ItemReads("lagged").ToArray();
            Assert.AreEqual(2, attempts.Length);
            Assert.AreEqual(this.fixture.Regions["West US"].Authority, attempts[0].Endpoint.Authority);
            Assert.AreEqual(HttpStatusCode.NotFound, attempts[0].Status);
            Assert.AreEqual(1002, attempts[0].SubStatus);
            Assert.IsFalse(string.IsNullOrEmpty(attempts[0].SessionToken));
            Assert.AreEqual(this.fixture.Regions["East US"].Authority, attempts[1].Endpoint.Authority);
            Assert.AreEqual(HttpStatusCode.OK, attempts[1].Status);
            await this.fixture.ResumeWestAsync();
            ItemResponse<JObject> caughtUp = await WaitForCatchUpAsync();
            Assert.AreEqual(42, (int)caughtUp.Resource["value"]);
            ItemResponse<JObject> next = await container.ReadItemAsync<JObject>(
                "lagged", new PartitionKey("lagged"), session, this.fixture.CancellationToken);
            Assert.AreEqual(42, (int)next.Resource["value"]);
            ObservedRequest final = observer.ItemReads("lagged").Last();
            Assert.AreEqual(3, observer.ItemReads("lagged").Count);
            Assert.AreEqual(this.fixture.Regions["West US"].Authority, final.Endpoint.Authority);
            Assert.AreEqual(HttpStatusCode.OK, final.Status);
            this.TestContext.WriteLine(
                "Actual replica lag: West 404/1002; SDK recovery: West 404/1002 -> East 200; " +
                "after resume: same West endpoint 200, next preferred-West read 200; value=42.");

            async Task<ItemResponse<JObject>> WaitForCatchUpAsync()
            {
                Stopwatch timer = Stopwatch.StartNew();
                while (timer.Elapsed < TimeSpan.FromSeconds(10))
                {
                    try
                    {
                        return await westContainer.ReadItemAsync<JObject>(
                            "lagged", new PartitionKey("lagged"), session, this.fixture.CancellationToken);
                    }
                    catch (CosmosException error) when (error.StatusCode == HttpStatusCode.NotFound && error.SubStatusCode == 1002)
                    {
                        await Task.Delay(10, this.fixture.CancellationToken);
                    }
                }

                throw new TimeoutException("The resumed West replica did not satisfy the original session token.");
            }
        }
    }
}
