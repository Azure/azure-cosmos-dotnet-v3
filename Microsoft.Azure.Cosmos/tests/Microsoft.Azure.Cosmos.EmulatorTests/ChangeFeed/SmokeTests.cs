//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests.ChangeFeed
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class SmokeTests : BaseChangeFeedClientHelper
    {
        private Container Container;

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.ChangeFeedTestInit();
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
            IEnumerable<int> expectedIds = Enumerable.Range(0, 100);
            List<int> receivedIds = new List<int>();
            ChangeFeedProcessor processor = this.Container
                .GetChangeFeedProcessorBuilder("test", (IReadOnlyCollection<TestClass> docs, CancellationToken token) =>
                {
                    foreach (TestClass doc in docs)
                    {
                        receivedIds.Add(int.Parse(doc.id));
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
            await Task.Delay(BaseChangeFeedClientHelper.ChangeFeedCleanupTime);
            await processor.StopAsync();
            // Verify that we maintain order
            CollectionAssert.AreEqual(expectedIds.ToList(), receivedIds);
        }

        [TestMethod]
        public async Task ExceptionsRetryBatch()
        {
            ManualResetEvent allDocsProcessed = new ManualResetEvent(false);

            bool thrown = false;
            IEnumerable<int> expectedIds = Enumerable.Range(0, 3);
            List<int> receivedIds = new List<int>();
            ChangeFeedProcessor processor = this.Container
                .GetChangeFeedProcessorBuilder("test", (IReadOnlyCollection<TestClass> docs, CancellationToken token) =>
                {
                    if (receivedIds.Count == 1
                        && !thrown)
                    {
                        thrown = true;
                        throw new Exception("Retry batch");
                    }

                    foreach (TestClass doc in docs)
                    {
                        receivedIds.Add(int.Parse(doc.id));
                    }

                    if (receivedIds.Count == 3)
                    {
                        allDocsProcessed.Set();
                    }

                    return Task.CompletedTask;
                })
                .WithInstanceName("random")
                .WithMaxItems(1)
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
        public async Task Schema_DefaultsToNoPartitionId()
        {
            ChangeFeedProcessor processor = this.Container
                .GetChangeFeedProcessorBuilder("test", (IReadOnlyCollection<TestClass> docs, CancellationToken token) => Task.CompletedTask)
                .WithInstanceName("random")
                .WithLeaseContainer(this.LeaseContainer).Build();

            await processor.StartAsync();
            // Letting processor initialize
            await Task.Delay(BaseChangeFeedClientHelper.ChangeFeedSetupTime);

            // Verify that no leases have PartitionId (V2 contract)
            using FeedIterator<dynamic> iterator = this.LeaseContainer.GetItemQueryIterator<dynamic>();
            while (iterator.HasMoreResults)
            {
                FeedResponse<dynamic> page = await iterator.ReadNextAsync();
                foreach (dynamic lease in page)
                {
                    string leaseId = lease.id;
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

        /// <summary>
        /// When the user migrates from V2 CFP, the leases contain PartitionId.
        /// To allow for backward compatibility (V2 -> V3 -> V2) we need to honor the existence of PartitionId and maintain its value.
        /// </summary>
        [TestMethod]
        public async Task Schema_OnV2MigrationMaintainPartitionId()
        {
            IEnumerable<int> expectedIds = Enumerable.Range(0, 20);
            List<int> receivedIds = new List<int>();
            ChangeFeedProcessor processor = this.Container
                .GetChangeFeedProcessorBuilder("test", (IReadOnlyCollection<TestClass> docs, CancellationToken token) =>
                {
                    foreach (TestClass doc in docs)
                    {
                        receivedIds.Add(int.Parse(doc.id));
                    }

                    return Task.CompletedTask;
                })
                .WithInstanceName("random")
                .WithLeaseContainer(this.LeaseContainer).Build();

            await processor.StartAsync();
            // Letting processor initialize
            await Task.Delay(BaseChangeFeedClientHelper.ChangeFeedSetupTime);

            // Inserting some documents
            foreach (int id in expectedIds.Take(10))
            {
                await this.Container.CreateItemAsync<dynamic>(new { id = id.ToString() });
            }

            // Waiting on all notifications to finish
            await Task.Delay(BaseChangeFeedClientHelper.ChangeFeedCleanupTime);
            await processor.StopAsync();

            // At this point we have leases for V3, so we will simulate V2 by manually adding PartitionId and removing LeaseToken
            using FeedIterator<JObject> iterator = this.LeaseContainer.GetItemQueryIterator<JObject>();
            while (iterator.HasMoreResults)
            {
                FeedResponse<JObject> page = await iterator.ReadNextAsync();
                foreach (JObject lease in page)
                {
                    string leaseId = lease.Value<string>("id");
                    if (leaseId.Contains(".info") || leaseId.Contains(".lock"))
                    {
                        // These are the store initialization marks
                        continue;
                    }

                    // create the PartitionId property
                    lease.Add("PartitionId", lease.Value<string>("LeaseToken"));

                    lease.Remove("LeaseToken");

                    await this.LeaseContainer.UpsertItemAsync<JObject>(lease);
                }
            }

            // Now all leases are V2 leases, create the rest of the documents
            foreach (int id in expectedIds.TakeLast(10))
            {
                await this.Container.CreateItemAsync<dynamic>(new { id = id.ToString() });
            }

            await processor.StartAsync();
            // Letting processor initialize
            await Task.Delay(BaseChangeFeedClientHelper.ChangeFeedSetupTime);

            // Waiting on all notifications to finish, should be using PartitionId from the V2 lease
            await Task.Delay(BaseChangeFeedClientHelper.ChangeFeedCleanupTime);
            await processor.StopAsync();

            // Verify we processed all items (including when using the V2 leases)
            CollectionAssert.AreEqual(expectedIds.ToList(), receivedIds);

            // Verify the after-migration leases have both PartitionId and LeaseToken with the same value
            using FeedIterator<dynamic> iteratorAfter = this.LeaseContainer.GetItemQueryIterator<dynamic>();
            while (iteratorAfter.HasMoreResults)
            {
                FeedResponse<dynamic> page = await iteratorAfter.ReadNextAsync();
                foreach (dynamic lease in page)
                {
                    string leaseId = lease.id;
                    if (leaseId.Contains(".info") || leaseId.Contains(".lock"))
                    {
                        // These are the store initialization marks
                        continue;
                    }

                    Assert.IsNotNull(lease.LeaseToken, "LeaseToken is missing after migration of lease schema");
                    Assert.IsNotNull(lease.PartitionId, "PartitionId is missing after migration of lease schema");
                    Assert.AreEqual(lease.LeaseToken, lease.PartitionId, "LeaseToken and PartitionId should be equal after migration");
                }
            }
        }

        [TestMethod]
        public async Task NotExistentLeaseContainer()
        {
            Container notFoundContainer = this.cosmosClient.GetContainer(this.database.Id, "NonExistent");
            ChangeFeedProcessor processor = this.Container
                .GetChangeFeedProcessorBuilder("test", (IReadOnlyCollection<TestClass> docs, CancellationToken token) => Task.CompletedTask)
                .WithInstanceName("random")
                .WithLeaseContainer(notFoundContainer).Build();

            CosmosException exception = await Assert.ThrowsExceptionAsync<CosmosException>(() => processor.StartAsync());
            Assert.AreEqual(HttpStatusCode.NotFound, exception.StatusCode);
        }

        [TestMethod]
        public async Task WritesTriggerDelegate_WithLeaseContainerWithDynamic()
        {
            IEnumerable<int> expectedIds = Enumerable.Range(0, 100);
            List<int> receivedIds = new List<int>();
            ChangeFeedProcessor processor = this.Container
                .GetChangeFeedProcessorBuilder("test", (IReadOnlyCollection<dynamic> docs, CancellationToken token) =>
                {
                    foreach (dynamic doc in docs)
                    {
                        receivedIds.Add(int.Parse(doc.id.Value));
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
            ChangeFeedProcessor processor = this.Container
                .GetChangeFeedProcessorBuilder("test", (IReadOnlyCollection<TestClass> docs, CancellationToken token) =>
                {
                    foreach (TestClass doc in docs)
                    {
                        receivedIds.Add(int.Parse(doc.id));
                    }

                    return Task.CompletedTask;
                })
                .WithInstanceName("random")
                .WithInMemoryLeaseContainer()
                .Build();

            await processor.StartAsync();

            // Letting processor initialize
            await Task.Delay(BaseChangeFeedClientHelper.ChangeFeedSetupTime);

            // Inserting documents
            foreach (int id in expectedIds)
            {
                await this.Container.CreateItemAsync<dynamic>(new { id = id.ToString() });
            }

            // Waiting on all notifications to finish
            await Task.Delay(BaseChangeFeedClientHelper.ChangeFeedCleanupTime);
            await processor.StopAsync();

            // Verify that we maintain order
            CollectionAssert.AreEqual(expectedIds.ToList(), receivedIds);
        }

        [TestMethod]
        public async Task WritesTriggerDelegate_WithInMemoryContainerWithDynamic()
        {
            IEnumerable<int> expectedIds = Enumerable.Range(0, 100);
            List<int> receivedIds = new List<int>();
            ChangeFeedProcessor processor = this.Container
                .GetChangeFeedProcessorBuilder("test", (IReadOnlyCollection<dynamic> docs, CancellationToken token) =>
                {
                    foreach (dynamic doc in docs)
                    {
                        receivedIds.Add(int.Parse(doc.id.Value));
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
                await this.Container.CreateItemAsync<dynamic>(new { id = id.ToString() });
            }

            // Waiting on all notifications to finish
            await Task.Delay(BaseChangeFeedClientHelper.ChangeFeedCleanupTime);
            await processor.StopAsync();
            // Verify that we maintain order
            CollectionAssert.AreEqual(expectedIds.ToList(), receivedIds);
        }

        [TestMethod]
        public async Task DoesNotUseUserSerializer()
        {
            CosmosClient cosmosClient = TestCommon.CreateCosmosClient(builder => builder.WithCustomSerializer(new FailedUserSerializer()));

            ManualResetEvent allDocsProcessed = new ManualResetEvent(false);

            int processedDocCount = 0;
            string accumulator = string.Empty;
            ChangeFeedProcessor processor = cosmosClient.GetContainer(this.database.Id, this.Container.Id)
                .GetChangeFeedProcessorBuilder("test", (IReadOnlyCollection<TestClass> docs, CancellationToken token) =>
                {
                    processedDocCount += docs.Count();
                    foreach (TestClass doc in docs)
                    {
                        accumulator += doc.id.ToString() + ".";
                    }

                    if (processedDocCount == 10)
                    {
                        allDocsProcessed.Set();
                    }

                    return Task.CompletedTask;
                })
                .WithInstanceName("random")
                .WithLeaseContainer(cosmosClient.GetContainer(this.database.Id, this.LeaseContainer.Id)).Build();

            // Start the processor, insert 1 document to generate a checkpoint
            await processor.StartAsync();
            await Task.Delay(BaseChangeFeedClientHelper.ChangeFeedSetupTime);
            foreach (int id in Enumerable.Range(0, 10))
            {
                await this.Container.CreateItemAsync<TestClass>(new TestClass { id = id.ToString() });
            }

            bool isStartOk = allDocsProcessed.WaitOne(10 * BaseChangeFeedClientHelper.ChangeFeedSetupTime);
            await processor.StopAsync();
            Assert.IsTrue(isStartOk, "Timed out waiting for docs to process");
            Assert.AreEqual("0.1.2.3.4.5.6.7.8.9.", accumulator);
        }

        private class FailedUserSerializer : CosmosSerializer
        {
            private readonly CosmosSerializer cosmosSerializer = new CosmosJsonDotNetSerializer();
            public override T FromStream<T>(Stream stream)
            {
                // Only let changes serialization pass through
                if (typeof(T) == typeof(TestClass[]))
                {
                    return this.cosmosSerializer.FromStream<T>(stream);
                }

                throw new System.NotImplementedException();
            }

            public override Stream ToStream<T>(T input)
            {
                throw new System.NotImplementedException();
            }
        }

        public class TestClass
        {
            public string id { get; set; }
        }
    }
}
