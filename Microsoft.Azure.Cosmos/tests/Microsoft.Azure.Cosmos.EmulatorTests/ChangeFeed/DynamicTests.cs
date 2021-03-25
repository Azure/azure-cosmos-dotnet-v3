//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests.ChangeFeed
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Scripts;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class DynamicTests : BaseChangeFeedClientHelper
    {
        private ContainerInternal Container;

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.ChangeFeedTestInit();

            string PartitionKey = "/pk";
            ContainerResponse response = await this.database.CreateContainerAsync(
                new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: PartitionKey),
                throughput: 10000,
                cancellationToken: this.cancellationToken);
            this.Container = (ContainerInternal)response;
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        public async Task TestWithRunningProcessor()
        {
            int partitionKey = 0;
            ManualResetEvent allDocsProcessed = new ManualResetEvent(false);

            int processedDocCount = 0;
            string accumulator = string.Empty;
            ChangeFeedProcessor processor = this.Container
                .GetChangeFeedProcessorBuilder("test", (ChangeFeedProcessorContext context, IReadOnlyCollection<dynamic> docs, CancellationToken token) =>
                {
                    this.ValidateContext(context);
                    processedDocCount += docs.Count();
                    foreach (dynamic doc in docs)
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
                .WithLeaseContainer(this.LeaseContainer).Build();

            // Start the processor, insert 1 document to generate a checkpoint
            await processor.StartAsync();
            await Task.Delay(BaseChangeFeedClientHelper.ChangeFeedSetupTime);
            foreach (int id in Enumerable.Range(0, 10))
            {
                await this.Container.CreateItemAsync<dynamic>(new { id = id.ToString(), pk = partitionKey });
            }

            bool isStartOk = allDocsProcessed.WaitOne(10 * BaseChangeFeedClientHelper.ChangeFeedSetupTime);
            await processor.StopAsync();
            Assert.IsTrue(isStartOk, "Timed out waiting for docs to process");
            Assert.AreEqual("0.1.2.3.4.5.6.7.8.9.", accumulator);
        }

        [TestMethod]
        public async Task TestWithRunningProcessor_WithManualCheckpoint()
        {
            int partitionKey = 0;
            ManualResetEvent allDocsProcessed = new ManualResetEvent(false);

            int processedDocCount = 0;
            string accumulator = string.Empty;
            ChangeFeedProcessor processor = this.Container
                .GetChangeFeedProcessorBuilderWithManualCheckpoint("test", async (ChangeFeedProcessorContext context, IReadOnlyCollection<dynamic> docs, Func<Task<(bool isSuccess, Exception error)>> tryCheckpointAsync, CancellationToken token) =>
                {
                    this.ValidateContext(context);
                    processedDocCount += docs.Count();
                    foreach (dynamic doc in docs)
                    {
                        accumulator += doc.id.ToString() + ".";
                    }

                    if (processedDocCount == 3)
                    {
                        // Throwing on the 3rd document, since we checkpointed only on the 1st, we would repeat 2nd and 3rd
                        throw new Exception("Stop here");
                    }

                    if (processedDocCount == 1) {
                        // Checkpointing on the first document to be able to have a point to rollback to
                        (bool isSuccess, Exception exception) = await tryCheckpointAsync();
                        Assert.IsTrue(isSuccess);
                        Assert.IsNull(exception);
                    }

                    if (processedDocCount == 12)
                    {
                        allDocsProcessed.Set();
                    }

                })
                .WithInstanceName("random")
                .WithMaxItems(1)
                .WithLeaseContainer(this.LeaseContainer).Build();

            // Start the processor, insert 1 document to generate a checkpoint
            await processor.StartAsync();
            await Task.Delay(BaseChangeFeedClientHelper.ChangeFeedSetupTime);
            foreach (int id in Enumerable.Range(0, 10))
            {
                await this.Container.CreateItemAsync<dynamic>(new { id = id.ToString(), pk = partitionKey });
            }

            bool isStartOk = allDocsProcessed.WaitOne(30 * BaseChangeFeedClientHelper.ChangeFeedSetupTime);
            await processor.StopAsync();
            Assert.IsTrue(isStartOk, "Timed out waiting for docs to process");
            Assert.AreEqual("0.1.2.1.2.3.4.5.6.7.8.9.", accumulator);
        }

        [TestMethod]
        public async Task TestWithFixedLeaseContainer()
        {
            await NonPartitionedContainerHelper.CreateNonPartitionedContainer(
                    this.database,
                    "fixedLeases");

            Container fixedLeasesContainer = this.cosmosClient.GetContainer(this.database.Id, "fixedLeases");

            try
            {

                int partitionKey = 0;
                ManualResetEvent allDocsProcessed = new ManualResetEvent(false);

                int processedDocCount = 0;
                string accumulator = string.Empty;
                ChangeFeedProcessor processor = this.Container
                    .GetChangeFeedProcessorBuilder("test", (ChangeFeedProcessorContext context, IReadOnlyCollection<dynamic> docs, CancellationToken token) =>
                    {
                        this.ValidateContext(context);
                        processedDocCount += docs.Count();
                        foreach (dynamic doc in docs)
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
                    .WithLeaseContainer(fixedLeasesContainer).Build();

                // Start the processor, insert 1 document to generate a checkpoint
                await processor.StartAsync();
                await Task.Delay(BaseChangeFeedClientHelper.ChangeFeedSetupTime);
                foreach (int id in Enumerable.Range(0, 10))
                {
                    await this.Container.CreateItemAsync<dynamic>(new { id = id.ToString(), pk = partitionKey });
                }

                bool isStartOk = allDocsProcessed.WaitOne(10 * BaseChangeFeedClientHelper.ChangeFeedSetupTime);
                await processor.StopAsync();
                Assert.IsTrue(isStartOk, "Timed out waiting for docs to process");
                Assert.AreEqual("0.1.2.3.4.5.6.7.8.9.", accumulator);
            }
            finally
            {
                await fixedLeasesContainer.DeleteContainerAsync();
            }
        }

        [TestMethod]
        public async Task TestReducePageSizeScenario()
        {
            int partitionKey = 0;
            // Create some docs to make sure that one separate response is returned for 1st execute of query before retries.
            // These are to make sure continuation token is passed along during retries.
            string sprocId = "createTwoDocs";
            string sprocBody = @"function(startIndex) { for (var i = 0; i < 2; ++i) __.createDocument(
                            __.getSelfLink(),
                            { id: 'doc' + (i + startIndex).toString(), value: 'y'.repeat(1500000), pk:0 },
                            err => { if (err) throw err;}
                        );}";

            Scripts scripts = this.Container.Scripts;

            StoredProcedureResponse storedProcedureResponse =
                await scripts.CreateStoredProcedureAsync(new StoredProcedureProperties(sprocId, sprocBody));

            ManualResetEvent allDocsProcessed = new ManualResetEvent(false);

            int processedDocCount = 0;
            string accumulator = string.Empty;
            ChangeFeedProcessor processor = this.Container
                .GetChangeFeedProcessorBuilder("test", (ChangeFeedProcessorContext context, IReadOnlyCollection<dynamic> docs, CancellationToken token) =>
                {
                    this.ValidateContext(context);
                    processedDocCount += docs.Count();
                    foreach (dynamic doc in docs)
                    {
                        accumulator += doc.id.ToString() + ".";
                    }

                    if (processedDocCount == 5)
                    {
                        allDocsProcessed.Set();
                    }

                    return Task.CompletedTask;
                })
                .WithStartFromBeginning()
                .WithInstanceName("random")
                .WithMaxItems(6)
                .WithLeaseContainer(this.LeaseContainer).Build();

            // Generate the payload
            await scripts.ExecuteStoredProcedureAsync<object>(
                sprocId, 
                new PartitionKey(partitionKey),
                new dynamic[] { 0 });

            // Create 3 docs each 1.5MB. All 3 do not fit into MAX_RESPONSE_SIZE (4 MB). 2nd and 3rd are in same transaction.
            string content = string.Format("{{\"id\": \"doc2\", \"value\": \"{0}\", \"pk\": 0}}", new string('x', 1500000));
            await this.Container.CreateItemAsync(JsonConvert.DeserializeObject<dynamic>(content), new PartitionKey(partitionKey));

            await scripts.ExecuteStoredProcedureAsync<object>(sprocId, new PartitionKey(partitionKey), new dynamic[] { 3 });

            await processor.StartAsync();
            // Letting processor initialize and pickup changes
            bool isStartOk = allDocsProcessed.WaitOne(10 * BaseChangeFeedClientHelper.ChangeFeedSetupTime);
            await processor.StopAsync();
            Assert.IsTrue(isStartOk, "Timed out waiting for docs to process");
            Assert.AreEqual("doc0.doc1.doc2.doc3.doc4.", accumulator);
        }

        [TestMethod]
        public async Task TestWithStartTime_Beginning()
        {
            int partitionKey = 0;

            foreach (int id in Enumerable.Range(0, 5))
            {
                await this.Container.CreateItemAsync<dynamic>(new { id = $"doc{id}", pk = partitionKey });
            }

            ManualResetEvent allDocsProcessed = new ManualResetEvent(false);

            int processedDocCount = 0;
            string accumulator = string.Empty;
            ChangeFeedProcessor processor = this.Container
                .GetChangeFeedProcessorBuilder("test", (ChangeFeedProcessorContext context, IReadOnlyCollection<dynamic> docs, CancellationToken token) =>
                {
                    this.ValidateContext(context);
                    Assert.IsTrue(docs.Count > 0);
                    processedDocCount += docs.Count;
                    foreach (dynamic doc in docs)
                    {
                        accumulator += doc.id.ToString() + ".";
                    }

                    if (processedDocCount == 5)
                    {
                        allDocsProcessed.Set();
                    }

                    return Task.CompletedTask;
                })
                .WithStartTime(DateTime.MinValue.ToUniversalTime())
                .WithInstanceName("random")
                .WithLeaseContainer(this.LeaseContainer).Build();

            await processor.StartAsync();
            // Letting processor initialize and pickup changes
            bool isStartOk = allDocsProcessed.WaitOne(10 * BaseChangeFeedClientHelper.ChangeFeedSetupTime);
            await processor.StopAsync();
            Assert.IsTrue(isStartOk, "Timed out waiting for docs to process");
            Assert.AreEqual("doc0.doc1.doc2.doc3.doc4.", accumulator);
        }

        [TestMethod]
        public async Task TestWithStartTime_CustomTime()
        {
            int partitionKey = 0;

            foreach (int id in Enumerable.Range(0, 5))
            {
                await this.Container.CreateItemAsync<dynamic>(new { id = $"doc{id}", pk = partitionKey });
            }

            await Task.Delay(1000);

            DateTime now = DateTime.UtcNow;

            await Task.Delay(1000);

            foreach (int id in Enumerable.Range(5, 5))
            {
                await this.Container.CreateItemAsync<dynamic>(new { id = $"doc{id}", pk = partitionKey });
            }

            ManualResetEvent allDocsProcessed = new ManualResetEvent(false);

            int processedDocCount = 0;
            string accumulator = string.Empty;
            ChangeFeedProcessor processor = this.Container
                .GetChangeFeedProcessorBuilder("test", (ChangeFeedProcessorContext context, IReadOnlyCollection<dynamic> docs, CancellationToken token) =>
                {
                    this.ValidateContext(context);
                    Assert.IsTrue(docs.Count > 0);
                    processedDocCount += docs.Count;
                    foreach (dynamic doc in docs)
                    {
                        accumulator += doc.id.ToString() + ".";
                    }

                    if (processedDocCount == 5)
                    {
                        allDocsProcessed.Set();
                    }

                    return Task.CompletedTask;
                })
                .WithStartTime(now)
                .WithInstanceName("random")
                .WithLeaseContainer(this.LeaseContainer).Build();

            await processor.StartAsync();
            // Letting processor initialize and pickup changes
            bool isStartOk = allDocsProcessed.WaitOne(10 * BaseChangeFeedClientHelper.ChangeFeedSetupTime);
            await processor.StopAsync();
            Assert.IsTrue(isStartOk, "Timed out waiting for docs to process");
            Assert.AreEqual("doc5.doc6.doc7.doc8.doc9.", accumulator);
        }

        private void ValidateContext(ChangeFeedProcessorContext changeFeedProcessorContext)
        {
            Assert.IsNotNull(changeFeedProcessorContext.LeaseToken);
            Assert.IsNotNull(changeFeedProcessorContext.Diagnostics);
            Assert.IsNotNull(changeFeedProcessorContext.Headers);
            Assert.IsNotNull(changeFeedProcessorContext.Headers.Session);
            Assert.IsTrue(changeFeedProcessorContext.Headers.RequestCharge > 0);
        }
    }
}
