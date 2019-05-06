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
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class DynamicTests : BaseChangeFeedClientHelper
    {

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.ChangeFeedTestInit();

            string PartitionKey = "/pk";
            CosmosContainerResponse response = await this.database.Containers.CreateContainerAsync(
                new CosmosContainerSettings(id: Guid.NewGuid().ToString(), partitionKeyPath: PartitionKey),
                throughput: 10000,
                cancellationToken: this.cancellationToken);
            this.Container = response;
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
            ChangeFeedProcessor processor = this.Container.Items
                .CreateChangeFeedProcessorBuilder("test", (IReadOnlyCollection<dynamic> docs, CancellationToken token) =>
                {
                    processedDocCount += docs.Count();
                    foreach (var doc in docs) accumulator += doc.id.ToString() + ".";
                    if (processedDocCount == 10) allDocsProcessed.Set();

                    return Task.CompletedTask;
                })
                .WithInstanceName("random")
                .WithCosmosLeaseContainer(this.LeaseContainer).Build();

            // Start the processor, insert 1 document to generate a checkpoint
            await processor.StartAsync();
            await Task.Delay(BaseChangeFeedClientHelper.ChangeFeedSetupTime);
            foreach (int id in Enumerable.Range(0, 10))
            {
                await this.Container.Items.CreateItemAsync<dynamic>(partitionKey, new { id = id.ToString(), pk = partitionKey });
            }

            var isStartOk = allDocsProcessed.WaitOne(10 * BaseChangeFeedClientHelper.ChangeFeedSetupTime);
            await processor.StopAsync();
            Assert.IsTrue(isStartOk, "Timed out waiting for docs to process");
            Assert.AreEqual("0.1.2.3.4.5.6.7.8.9.", accumulator);
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

            CosmosStoredProcedureResponse storedProcedureResponse =
                await this.Container.StoredProcedures.CreateStoredProcedureAsync(sprocId, sprocBody);

            ManualResetEvent allDocsProcessed = new ManualResetEvent(false);

            int processedDocCount = 0;
            string accumulator = string.Empty;
            ChangeFeedProcessor processor = this.Container.Items
                .CreateChangeFeedProcessorBuilder("test", (IReadOnlyCollection<dynamic> docs, CancellationToken token) =>
                {
                    processedDocCount += docs.Count();
                    foreach (var doc in docs) accumulator += doc.id.ToString() + ".";
                    if (processedDocCount == 5) allDocsProcessed.Set();

                    return Task.CompletedTask;
                })
                .WithStartFromBeginning()
                .WithInstanceName("random")
                .WithMaxItems(6)
                .WithCosmosLeaseContainer(this.LeaseContainer).Build();

            // Generate the payload
            await storedProcedureResponse.StoredProcedure.ExecuteAsync<int, object>(partitionKey, 0);
            // Create 3 docs each 1.5MB. All 3 do not fit into MAX_RESPONSE_SIZE (4 MB). 2nd and 3rd are in same transaction.
            var content = string.Format("{{\"id\": \"doc2\", \"value\": \"{0}\", \"pk\": 0}}", new string('x', 1500000));
            await this.Container.Items.CreateItemAsync(partitionKey, JsonConvert.DeserializeObject<dynamic>(content));

            await storedProcedureResponse.StoredProcedure.ExecuteAsync<int, object>(partitionKey, 3);

            await processor.StartAsync();
            // Letting processor initialize and pickup changes
            var isStartOk = allDocsProcessed.WaitOne(10 * BaseChangeFeedClientHelper.ChangeFeedSetupTime);
            await processor.StopAsync();
            Assert.IsTrue(isStartOk, "Timed out waiting for docs to process");
            Assert.AreEqual("doc0.doc1.doc2.doc3.doc4.", accumulator);
        }
    }
}
