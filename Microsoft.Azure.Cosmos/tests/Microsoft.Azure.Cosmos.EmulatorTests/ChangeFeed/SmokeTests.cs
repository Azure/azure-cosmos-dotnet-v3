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
    public class SmokeTests : BaseChangeFeedClientHelper
    {
        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.ChangeFeedTestInit();
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        public async Task WritesTriggerDelegate_WithLeaseContainer()
        {
            IEnumerable<int> expectedIds = Enumerable.Range(0, 100);
            List<int> receivedIds = new List<int>();
            ChangeFeedProcessor processor = this.Container.Items
                .CreateChangeFeedProcessorBuilder("test", (IReadOnlyList<dynamic> docs, CancellationToken token) =>
                {
                    foreach (dynamic doc in docs)
                    {
                        receivedIds.Add(int.Parse(doc.id));
                    }

                    return Task.CompletedTask;
                })
                .WithInstanceName("random")
                .WithCosmosLeaseContainer(this.LeaseContainer).Build();

            await processor.StartAsync();
            // Letting processor initialize
            await Task.Delay(BaseChangeFeedClientHelper.ChangeFeedSetupTime);
            // Inserting documents
            foreach (int id in expectedIds)
            {
                await this.Container.Items.CreateItemAsync<dynamic>(id.ToString(), new { id = id.ToString() });
            }

            // Waiting on all notifications to finish
            await Task.Delay(BaseChangeFeedClientHelper.ChangeFeedCleanupTime);
            await processor.StopAsync();
            // Verify that we maintain order
            CollectionAssert.AreEqual(expectedIds.ToList(), receivedIds);
        }

        [TestMethod]
        public async Task WritesTriggerDelegate_WithInMemoryContainer()
        {
            IEnumerable<int> expectedIds = Enumerable.Range(0, 100);
            List<int> receivedIds = new List<int>();
            ChangeFeedProcessor processor = this.Container.Items
                .CreateChangeFeedProcessorBuilder("test", (IReadOnlyList<dynamic> docs, CancellationToken token) =>
                {
                    foreach (dynamic doc in docs)
                    {
                        receivedIds.Add(int.Parse(doc.id));
                    }

                    return Task.CompletedTask;
                })
                .WithInstanceName("random")
                .WithInMemoryLeaseContainer().Build();

            await processor.StartAsync();
            // Letting processor initialize
            await Task.Delay(BaseChangeFeedClientHelper.ChangeFeedSetupTime);
            // Inserting documents
            foreach (int id in expectedIds)
            {
                await this.Container.Items.CreateItemAsync<dynamic>(id.ToString(), new { id = id.ToString() });
            }

            // Waiting on all notifications to finish
            await Task.Delay(BaseChangeFeedClientHelper.ChangeFeedCleanupTime);
            await processor.StopAsync();
            // Verify that we maintain order
            CollectionAssert.AreEqual(expectedIds.ToList(), receivedIds);
        }
    }
}
