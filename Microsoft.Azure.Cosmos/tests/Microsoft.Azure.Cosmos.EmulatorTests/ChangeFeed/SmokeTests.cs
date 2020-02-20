//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests.ChangeFeed
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
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
        public async Task NotExistentLeaseContainer()
        {
            Container notFoundContainer = this.cosmosClient.GetContainer(this.database.Id, "NonExistent");
            ChangeFeedProcessor processor = this.Container
                .GetChangeFeedProcessorBuilder("test", (IReadOnlyCollection<TestClass> docs, CancellationToken token) =>
                {
                    return Task.CompletedTask;
                })
                .WithInstanceName("random")
                .WithLeaseContainer(notFoundContainer).Build();

            CosmosException exception = await Assert.ThrowsExceptionAsync<CosmosNotFoundException>(() => processor.StartAsync());
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
                if (typeof(T) == typeof(TestClass))
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
