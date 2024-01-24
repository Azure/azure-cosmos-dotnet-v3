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

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class AllVersionsAndDeletesDynamicTests : BaseChangeFeedClientHelper
    {
        private ContainerInternal Container;

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.ChangeFeedTestInit();

            string PartitionKey = "/pk";
            ContainerProperties properties = new ContainerProperties(id: Guid.NewGuid().ToString(), 
                partitionKeyPath: PartitionKey);
            properties.ChangeFeedPolicy.FullFidelityRetention = TimeSpan.FromMinutes(5);

            ContainerResponse response = await this.database.CreateContainerAsync(properties,
                throughput: 10000,
                cancellationToken: this.cancellationToken);
            this.Container = (ContainerInternal)response;
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        async Task CreateItems(int count, int partitionKey)
        {
            foreach (int id in Enumerable.Range(0, count))
            {
                await this.Container.CreateItemAsync<dynamic>(new { id = id.ToString(), pk = partitionKey, test = "test" });
            }
        }

        [TestMethod]
        [Owner("philipthomas-MSFT")]
        public async Task TestFFCFProcessorCreate()
        {
            ManualResetEvent allDocsProcessed = new ManualResetEvent(false);

            int processedDocCount = 0;
            string accumulator = string.Empty;

            ChangeFeedProcessor processor = this.Container
                .GetAllVersionsAndDeletesChangeFeedProcessorBuilder("testcreate", (ChangeFeedProcessorContext context, IReadOnlyCollection<ChangeFeedItemChange<dynamic>> docs, CancellationToken token) =>
                {
                    this.ValidateContext(context);
                    processedDocCount += docs.Count;

                    foreach (ChangeFeedItemChange<dynamic> change in docs)
                    {
                        Assert.IsTrue(change.Metadata.OperationType == ChangeFeedOperationType.Create);

                        int id = change.Current.id;

                        accumulator += id.ToString() + ".";
                    }

                    if (processedDocCount == 10)
                    {
                        allDocsProcessed.Set();
                    }

                    return Task.CompletedTask;
                })
                .WithInstanceName("ffcfcreatetest")
                .WithLeaseContainer(this.LeaseContainer).Build();

            // Start the processor, insert 1 document to generate a checkpoint
            await processor.StartAsync();
            await Task.Delay(BaseChangeFeedClientHelper.ChangeFeedSetupTime);

            await this.CreateItems(10, partitionKey: 0);

            bool isStartOk = allDocsProcessed.WaitOne(10 * BaseChangeFeedClientHelper.ChangeFeedSetupTime);

            Assert.IsTrue(isStartOk, "Timed out waiting for create to process");
            Assert.AreEqual("0.1.2.3.4.5.6.7.8.9.", accumulator);
        }

        [TestMethod]
        [Owner("philipthomas-MSFT")]
        public async Task TestFFCFProcessorReplace()
        {
            // Create ten items
            await this.CreateItems(10, partitionKey: 0);

            ManualResetEvent allDocsProcessed = new ManualResetEvent(false);

            int partitionKey = 0;
            int processedDocCount = 0;
            string accumulator = string.Empty;

            ChangeFeedProcessor processor = this.Container
                .GetAllVersionsAndDeletesChangeFeedProcessorBuilder("testreplace", (ChangeFeedProcessorContext context, IReadOnlyCollection<ChangeFeedItemChange<dynamic>> docs, CancellationToken token) =>
                {
                    this.ValidateContext(context);
                    processedDocCount += docs.Count;

                    foreach (ChangeFeedItemChange<dynamic> change in docs)
                    {
                        Assert.IsTrue(change.Metadata.OperationType == ChangeFeedOperationType.Replace);

                        int id = change.Current.id;

                        accumulator += id.ToString() + ".";
                    }

                    if (processedDocCount == 10)
                    {
                        allDocsProcessed.Set();
                    }

                    return Task.CompletedTask;
                })
                .WithInstanceName("ffcfreplacetest")
                .WithLeaseContainer(this.LeaseContainer).Build();

            await processor.StartAsync();
            await Task.Delay(BaseChangeFeedClientHelper.ChangeFeedSetupTime);

            foreach (int id in Enumerable.Range(0, 10))
            {
                await this.Container.UpsertItemAsync<dynamic>(new { id = id.ToString(), pk = partitionKey, update = "true" });
            }

            bool isStartOk = allDocsProcessed.WaitOne(10 * BaseChangeFeedClientHelper.ChangeFeedSetupTime);

            Assert.IsTrue(isStartOk, "Timed out waiting for replace to process");
            Assert.AreEqual("0.1.2.3.4.5.6.7.8.9.", accumulator);
        }

        [TestMethod]
        [Owner("philipthomas-MSFT")]
        public async Task TestFFCFProcessorDelete()
        {
            // Create ten items
            await this.CreateItems(10, partitionKey: 0);

            ManualResetEvent allDocsProcessed = new ManualResetEvent(false);

            int partitionKey = 0;
            int processedDocCount = 0;
            string accumulator = string.Empty;

            ChangeFeedProcessor processor = this.Container
                .GetAllVersionsAndDeletesChangeFeedProcessorBuilder("testdelete", (ChangeFeedProcessorContext context, IReadOnlyCollection<ChangeFeedItemChange<dynamic>> docs, CancellationToken token) =>
                {
                    this.ValidateContext(context);
                    processedDocCount += docs.Count;

                    foreach (ChangeFeedItemChange<dynamic> change in docs)
                    {
                        Assert.IsTrue(change.Metadata.OperationType == ChangeFeedOperationType.Delete);

                        int id = change.Previous.id;

                        accumulator += id.ToString() + ".";
                    }

                    if (processedDocCount == 10)
                    {
                        allDocsProcessed.Set();
                    }

                    return Task.CompletedTask;
                })
                .WithInstanceName("ffcfdeletetest")
                .WithLeaseContainer(this.LeaseContainer).Build();

            await processor.StartAsync();
            await Task.Delay(BaseChangeFeedClientHelper.ChangeFeedSetupTime);

            foreach (int id in Enumerable.Range(0, 10))
            {
                await this.Container.DeleteItemAsync<dynamic>(id.ToString(), new PartitionKey(partitionKey));
            }

            bool isStartOk = allDocsProcessed.WaitOne(10 * BaseChangeFeedClientHelper.ChangeFeedSetupTime);

            Assert.IsTrue(isStartOk, "Timed out waiting for delete to process");
            Assert.AreEqual("0.1.2.3.4.5.6.7.8.9.", accumulator);
        }

        private void ValidateContext(ChangeFeedProcessorContext changeFeedProcessorContext)
        {
            Assert.IsNotNull(changeFeedProcessorContext.LeaseToken);
            Assert.IsNotNull(changeFeedProcessorContext.Diagnostics);
            Assert.IsNotNull(changeFeedProcessorContext.Headers);
            Assert.IsNotNull(changeFeedProcessorContext.Headers.Session);
            Assert.IsTrue(changeFeedProcessorContext.Headers.RequestCharge > 0);
            string diagnosticsAsString = changeFeedProcessorContext.Diagnostics.ToString();
            Assert.IsTrue(diagnosticsAsString.Contains("Change Feed Processor Read Next Async"));
        }

    }
}
