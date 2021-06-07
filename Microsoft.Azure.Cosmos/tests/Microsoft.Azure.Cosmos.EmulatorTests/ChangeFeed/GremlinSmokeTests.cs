//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests.ChangeFeed
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class GremlinSmokeTests : BaseChangeFeedClientHelper
    {
        private Container Container;

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.ChangeFeedTestInit("/partitionKey");
            ContainerResponse response = await this.database.CreateContainerAsync(
                new ContainerProperties(id: "monitored", partitionKeyPath: "/pk"),
                cancellationToken: this.cancellationToken);
            this.Container = response;
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        public async Task WritesTriggerDelegate_WithLeaseContainer()
        {
            ManualResetEvent allDocsProcessed = new ManualResetEvent(false);

            IEnumerable<int> expectedIds = Enumerable.Range(0, 100);
            List<int> receivedIds = new List<int>();
            ChangeFeedProcessor processor = this.Container
                .GetChangeFeedProcessorBuilder("test", (IReadOnlyCollection<TestClass> docs, CancellationToken token) =>
                {
                    foreach (TestClass doc in docs)
                    {
                        receivedIds.Add(int.Parse(doc.id));
                    }

                    if (receivedIds.Count == 100)
                    {
                        allDocsProcessed.Set();
                    }

                    return Task.CompletedTask;
                })
                .WithInstanceName("random")
                .WithLeaseContainer(this.LeaseContainer).Build();

            await processor.StartAsync();
            // Letting processor initialize
            await Task.Delay(BaseChangeFeedClientHelper.ChangeFeedSetupTime);
            // Inserting documents
            foreach (int id in expectedIds)
            {
                await this.Container.CreateItemAsync<dynamic>(new { id = id.ToString() });
            }

            // Waiting on all notifications to finish
            bool isStartOk = allDocsProcessed.WaitOne(30 * BaseChangeFeedClientHelper.ChangeFeedSetupTime);
            await processor.StopAsync();
            Assert.IsTrue(isStartOk, "Timed out waiting for docs to process");
            // Verify that we maintain order
            CollectionAssert.AreEqual(expectedIds.ToList(), receivedIds);

        }

        [TestMethod]
        public async Task Schema_DefaultsToHavingPartitionKey()
        {
            ChangeFeedProcessor processor = this.Container
                .GetChangeFeedProcessorBuilder("test", (IReadOnlyCollection<TestClass> docs, CancellationToken token) => Task.CompletedTask)
                .WithInstanceName("random")
                .WithLeaseContainer(this.LeaseContainer).Build();

            await processor.StartAsync();
            // Letting processor initialize
            await Task.Delay(BaseChangeFeedClientHelper.ChangeFeedSetupTime);

            // Verify that leases have the partitionKey attribute
            using FeedIterator<dynamic> iterator = this.LeaseContainer.GetItemQueryIterator<dynamic>();
            while (iterator.HasMoreResults)
            {
                FeedResponse<dynamic> page = await iterator.ReadNextAsync();
                foreach (dynamic lease in page)
                {
                    string leaseId = lease.id;
                    Assert.IsNotNull(lease.partitionKey);
                    if (leaseId.Contains(".info") || leaseId.Contains(".lock"))
                    {
                        // These are the store initialization marks
                        continue;
                    }

                    Assert.IsNotNull(lease.LeaseToken);
                    Assert.IsNull(lease.PartitionId);
                }
            }

            await processor.StopAsync();
        }

        public class TestClass
        {
            public string id { get; set; }
        }
    }
}
