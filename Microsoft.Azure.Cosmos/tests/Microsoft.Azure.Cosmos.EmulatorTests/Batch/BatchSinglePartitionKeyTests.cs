//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class BatchSinglePartitionKeyTests : CosmosBatchTestBase
    {
        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            CosmosBatchTestBase.ClassInit(context);
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            CosmosBatchTestBase.ClassClean();
        }

        [TestMethod]
        [Owner("abpai")]
        [Description("Verify batch CRUD with default options at all levels (client/batch/operation) and all operations expected to pass works")]
        public async Task BatchCrudAsync()
        {
            await this.RunCrudAsync(isStream: false, isSchematized: false, useEpk: false, container: CosmosBatchTestBase.JsonContainer);
        }

        [TestMethod]
        [Owner("abpai")]
        [Description("Verify batch CRUD with JSON stream operation resource bodies with default options and all operations expected to pass works")]
        public async Task BatchCrudStreamAsync()
        {
            await this.RunCrudAsync(isStream: true, isSchematized: false, useEpk: false, container: CosmosBatchTestBase.JsonContainer);
        }

        [TestMethod]
        [Owner("abpai")]
        [Description("Verify batch CRUD with HybridRow stream operation resource bodies and EPK with default options and all operations expected to pass works")]
        public async Task BatchCrudHybridRowStreamWithEpkAsync()
        {
            await this.RunCrudAsync(isStream: true, isSchematized: true, useEpk: true, container: CosmosBatchTestBase.SchematizedContainer);
        }

        [TestMethod]
        [Owner("rakkuma")]
        [Description("Verify batch CRUD with default options at all levels (client/batch/operation) and all operations expected to pass works in gateway mode")]
        public async Task BatchCrudGatewayAsync()
        {
            await this.RunCrudAsync(isStream: false, isSchematized: false, useEpk: false, container: CosmosBatchTestBase.GatewayJsonContainer);
        }

        [TestMethod]
        [Owner("rakkuma")]
        [Description("Verify batch CRUD with JSON stream operation resource bodies with default options and all operations expected to pass works in gateway mode")]
        public async Task BatchCrudStreamGatewayAsync()
        {
            await this.RunCrudAsync(isStream: true, isSchematized: false, useEpk: false, container: CosmosBatchTestBase.GatewayJsonContainer);
        }

        [TestMethod]
        [Owner("rakkuma")]
        [Description("Verify batch CRUD with default options at all levels (client/batch/operation) and all operations expected to pass works in shared throughput ")]
        public async Task BatchCrudSharedThroughputAsync()
        {
            await this.RunCrudAsync(isStream: false, isSchematized: false, useEpk: false, container: CosmosBatchTestBase.SharedThroughputContainer);
        }

        [TestMethod]
        [Owner("rakkuma")]
        [Description("Verify batch CRUD with JSON stream operation resource bodies with default options and all operations expected to pass works in shared throughput")]
        public async Task BatchCrudSharedThroughputStreamAsync()
        {
            await this.RunCrudAsync(isStream: true, isSchematized: false, useEpk: false, container: CosmosBatchTestBase.SharedThroughputContainer);
        }

        [TestMethod]
        [Owner("abpai")]
        [Description("Verify batch with multiple operations on the same entity works")]
        public async Task BatchOrderedAsync()
        {
            CosmosContainer container = CosmosBatchTestBase.JsonContainer;
            await this.CreateJsonTestDocsAsync(container);

            TestDoc firstDoc = CosmosBatchTestBase.PopulateTestDoc(this.PartitionKey1);

            TestDoc replaceDoc = this.GetTestDocCopy(firstDoc);
            replaceDoc.Cost += 20;

            CosmosBatchResponse batchResponse = await container.CreateBatch(CosmosBatchTestBase.GetPartitionKey(this.PartitionKey1))
                .CreateItem(firstDoc)
                .ReplaceItem(replaceDoc.Id, replaceDoc)
                .ExecuteAsync();

            BatchSinglePartitionKeyTests.VerifyBatchProcessed(batchResponse, numberOfOperations: 2);

            Assert.AreEqual(HttpStatusCode.Created, batchResponse[0].StatusCode);
            Assert.AreEqual(HttpStatusCode.OK, batchResponse[1].StatusCode);

            // Ensure that the replace overwrote the doc from the first operation
            await CosmosBatchTestBase.VerifyByReadAsync(container, replaceDoc);
        }

        [TestMethod]
        [Owner("abpai")]
        [Description("Verify eTags passed to batch operations or returned in batch results flow as expected")]
        public async Task BatchItemETagAsync()
        {
            CosmosContainer container = CosmosBatchTestBase.JsonContainer;
            await this.CreateJsonTestDocsAsync(container);
            {
                TestDoc testDocToCreate = CosmosBatchTestBase.PopulateTestDoc(this.PartitionKey1);

                TestDoc testDocToReplace = this.GetTestDocCopy(this.TestDocPk1ExistingA);
                testDocToReplace.Cost++;

                ItemResponse<TestDoc> readResponse = await CosmosBatchTestBase.JsonContainer.ReadItemAsync<TestDoc>(
                    CosmosBatchTestBase.GetPartitionKey(this.PartitionKey1),
                    this.TestDocPk1ExistingA.Id);

                ItemRequestOptions firstReplaceOptions = new ItemRequestOptions()
                {
                    IfMatchEtag = readResponse.ETag
                };

                CosmosBatchResponse batchResponse = await container.CreateBatch(CosmosBatchTestBase.GetPartitionKey(this.PartitionKey1))
                   .CreateItem(testDocToCreate)
                   .ReplaceItem(testDocToReplace.Id, testDocToReplace, itemRequestOptions: firstReplaceOptions)
                   .ExecuteAsync();

                BatchSinglePartitionKeyTests.VerifyBatchProcessed(batchResponse, numberOfOperations: 2);

                Assert.AreEqual(HttpStatusCode.Created, batchResponse[0].StatusCode);
                Assert.AreEqual(HttpStatusCode.OK, batchResponse[1].StatusCode);

                await CosmosBatchTestBase.VerifyByReadAsync(container, testDocToCreate, eTag: batchResponse[0].ETag);
                await CosmosBatchTestBase.VerifyByReadAsync(container, testDocToReplace, eTag: batchResponse[1].ETag);
            }

            {
                TestDoc testDocToReplace = this.GetTestDocCopy(this.TestDocPk1ExistingB);
                testDocToReplace.Cost++;

                ItemRequestOptions replaceOptions = new ItemRequestOptions()
                {
                    IfMatchEtag = CosmosBatchTestBase.Random.Next().ToString()
                };

                CosmosBatchResponse batchResponse = await container.CreateBatch(CosmosBatchTestBase.GetPartitionKey(this.PartitionKey1))
                   .ReplaceItem(testDocToReplace.Id, testDocToReplace, itemRequestOptions: replaceOptions)
                   .ExecuteAsync();

                BatchSinglePartitionKeyTests.VerifyBatchProcessed(
                    batchResponse,
                    numberOfOperations: 1,
                    expectedStatusCode: (HttpStatusCode)StatusCodes.MultiStatus);

                Assert.AreEqual(HttpStatusCode.PreconditionFailed, batchResponse[0].StatusCode);

                // ensure the document was not updated
                await CosmosBatchTestBase.VerifyByReadAsync(container, this.TestDocPk1ExistingB);
            }
        }

        [TestMethod]
        [Owner("abpai")]
        [Description("Verify TTL passed to binary passthrough batch operations flow as expected")]
        public async Task BatchItemTimeToLiveAsync()
        {
            // Verify with schematized containers where we are allowed to send TTL as a header
            const bool isSchematized = true;
            const bool isStream = true;
            CosmosContainer container = CosmosBatchTestBase.SchematizedContainer;
            await this.CreateSchematizedTestDocsAsync(container);
            {
                TestDoc testDocToCreate = CosmosBatchTestBase.PopulateTestDoc(this.PartitionKey1);
                TestDoc anotherTestDocToCreate = CosmosBatchTestBase.PopulateTestDoc(this.PartitionKey1);

                TestDoc testDocToReplace = this.GetTestDocCopy(this.TestDocPk1ExistingA);
                testDocToReplace.Cost++;

                const int ttlInSeconds = 3;
                const int infiniteTtl = -1;

                TestDoc testDocToUpsert = await CosmosBatchTestBase.CreateSchematizedTestDocAsync(container, this.PartitionKey1, ttlInSeconds: ttlInSeconds);
                testDocToUpsert.Cost++;

                CosmosBatchResponse batchResponse = await container.CreateBatch(CosmosBatchTestBase.GetPartitionKey(this.PartitionKey1))
                   .CreateItemStream(
                        CosmosBatchTestBase.TestDocToStream(testDocToCreate, isSchematized),
                        CosmosBatchTestBase.GetItemRequestOptions(testDocToCreate, isSchematized, ttlInSeconds: ttlInSeconds))
                   .CreateItemStream(
                        CosmosBatchTestBase.TestDocToStream(anotherTestDocToCreate, isSchematized),
                        CosmosBatchTestBase.GetItemRequestOptions(anotherTestDocToCreate, isSchematized))
                   .ReplaceItemStream(
                        CosmosBatchTestBase.GetId(testDocToReplace, isSchematized),
                        CosmosBatchTestBase.TestDocToStream(testDocToReplace, isSchematized),
                        CosmosBatchTestBase.GetItemRequestOptions(testDocToReplace, isSchematized, ttlInSeconds: ttlInSeconds))
                   .UpsertItemStream(
                        CosmosBatchTestBase.TestDocToStream(testDocToUpsert, isSchematized),
                        CosmosBatchTestBase.GetItemRequestOptions(testDocToUpsert, isSchematized, ttlInSeconds: infiniteTtl))
                   .ExecuteAsync(CosmosBatchTestBase.GetUpdatedBatchRequestOptions(isSchematized: true));

                BatchSinglePartitionKeyTests.VerifyBatchProcessed(batchResponse, numberOfOperations: 4);

                Assert.AreEqual(HttpStatusCode.Created, batchResponse[0].StatusCode);
                Assert.AreEqual(HttpStatusCode.Created, batchResponse[1].StatusCode);
                Assert.AreEqual(HttpStatusCode.OK, batchResponse[2].StatusCode);
                Assert.AreEqual(HttpStatusCode.OK, batchResponse[3].StatusCode);

                // wait for TTL to expire
                await Task.Delay(TimeSpan.FromSeconds(ttlInSeconds + 1));

                await CosmosBatchTestBase.VerifyNotFoundAsync(container, testDocToCreate, isSchematized);
                await CosmosBatchTestBase.VerifyByReadAsync(container, anotherTestDocToCreate, isStream, isSchematized);
                await CosmosBatchTestBase.VerifyNotFoundAsync(container, testDocToReplace, isSchematized);
                await CosmosBatchTestBase.VerifyByReadAsync(container, testDocToUpsert, isStream, isSchematized);
            }
        }

        [TestMethod]
        [Owner("abpai")]
        public async Task BatchLargerThanServerRequestAsync()
        {
            CosmosContainer container = CosmosBatchTestBase.JsonContainer;
            const int operationCount = 20;
            int appxDocSize = Constants.MaxDirectModeBatchRequestBodySizeInBytes / operationCount;

            // Increase the doc size by a bit so all docs won't fit in one server request.
            appxDocSize = (int)(appxDocSize * 1.05);
            {
                CosmosBatch batch = container.CreateBatch(CosmosBatchTestBase.GetPartitionKey(this.PartitionKey1));
                for (int i = 0; i < operationCount; i++)
                {
                    TestDoc doc = CosmosBatchTestBase.PopulateTestDoc(this.PartitionKey1, appxDocSize);
                    batch.CreateItem(doc);
                }

                CosmosBatchResponse batchResponse = await batch.ExecuteAsync();

                Assert.AreEqual(HttpStatusCode.RequestEntityTooLarge, batchResponse.StatusCode);
            }

            // Validate the server enforces this as well
            {
                CosmosBatch batch = container.CreateBatch(CosmosBatchTestBase.GetPartitionKey(this.PartitionKey1));
                for (int i = 0; i < operationCount; i++)
                {
                    TestDoc doc = CosmosBatchTestBase.PopulateTestDoc(this.PartitionKey1, appxDocSize);
                    batch.CreateItem(doc);
                }

                CosmosBatchResponse batchResponse = await batch.ExecuteAsync(
                    maxServerRequestBodyLength: int.MaxValue,
                    maxServerRequestOperationCount: int.MaxValue);

                Assert.AreEqual(HttpStatusCode.RequestEntityTooLarge, batchResponse.StatusCode);
            }
        }

        [TestMethod]
        [Owner("abpai")]
        public async Task BatchWithTooManyOperationsAsync()
        {
            CosmosContainer container = CosmosBatchTestBase.JsonContainer;
            await this.CreateJsonTestDocsAsync(container);

            const int operationCount = Constants.MaxOperationsInDirectModeBatchRequest + 1;

            // Validate SDK enforces this
            {
                CosmosBatch batch = container.CreateBatch(CosmosBatchTestBase.GetPartitionKey(this.PartitionKey1));
                for (int i = 0; i < operationCount; i++)
                {
                    batch.ReadItem(this.TestDocPk1ExistingA.Id);
                }

                CosmosBatchResponse batchResponse = await batch.ExecuteAsync();

                Assert.AreEqual(HttpStatusCode.BadRequest, batchResponse.StatusCode);
            }

            // Validate the server enforces this as well
            {
                CosmosBatch batch = container.CreateBatch(CosmosBatchTestBase.GetPartitionKey(this.PartitionKey1));
                for (int i = 0; i < operationCount; i++)
                {
                    batch.ReadItem(this.TestDocPk1ExistingA.Id);
                }

                CosmosBatchResponse batchResponse = await batch.ExecuteAsync(
                    maxServerRequestBodyLength: int.MaxValue,
                    maxServerRequestOperationCount: int.MaxValue);

                Assert.AreEqual(HttpStatusCode.BadRequest, batchResponse.StatusCode);
            }
        }

        [TestMethod]
        [Owner("abpai")]
        public async Task BatchServerResponseTooLargeAsync()
        {
            CosmosContainer container = CosmosBatchTestBase.JsonContainer;
            const int operationCount = 10;
            int appxDocSizeInBytes = 1 * 1024 * 1024;

            TestDoc doc = await CosmosBatchTestBase.CreateJsonTestDocAsync(container, this.PartitionKey1, appxDocSizeInBytes);

            CosmosBatch batch = container.CreateBatch(CosmosBatchTestBase.GetPartitionKey(this.PartitionKey1));
            for (int i = 0; i < operationCount; i++)
            {
                batch.ReadItem(doc.Id);
            }

            CosmosBatchResponse batchResponse = await batch.ExecuteAsync();

            BatchSinglePartitionKeyTests.VerifyBatchProcessed(
                batchResponse, 
                numberOfOperations: operationCount,
                expectedStatusCode: (HttpStatusCode)StatusCodes.MultiStatus);

            Assert.AreEqual((int)StatusCodes.FailedDependency, (int)batchResponse[0].StatusCode);
            Assert.AreEqual(HttpStatusCode.RequestEntityTooLarge, batchResponse[operationCount - 1].StatusCode);
        }

        [TestMethod]
        [Owner("abpai")]
        public async Task BatchReadsOnlyAsync()
        {
            CosmosContainer container = CosmosBatchTestBase.JsonContainer;
            await this.CreateJsonTestDocsAsync(container);

            CosmosBatchResponse batchResponse = await container.CreateBatch(CosmosBatchTestBase.GetPartitionKey(this.PartitionKey1))
                    .ReadItem(this.TestDocPk1ExistingA.Id)
                    .ReadItem(this.TestDocPk1ExistingB.Id)
                    .ReadItem(this.TestDocPk1ExistingC.Id)
                    .ExecuteAsync();

            BatchSinglePartitionKeyTests.VerifyBatchProcessed(batchResponse, numberOfOperations: 3);

            Assert.AreEqual(HttpStatusCode.OK, batchResponse[0].StatusCode);
            Assert.AreEqual(HttpStatusCode.OK, batchResponse[1].StatusCode);
            Assert.AreEqual(HttpStatusCode.OK, batchResponse[2].StatusCode);

            Assert.AreEqual(this.TestDocPk1ExistingA, batchResponse.GetOperationResultAtIndex<TestDoc>(0).Resource);
            Assert.AreEqual(this.TestDocPk1ExistingB, batchResponse.GetOperationResultAtIndex<TestDoc>(1).Resource);
            Assert.AreEqual(this.TestDocPk1ExistingC, batchResponse.GetOperationResultAtIndex<TestDoc>(2).Resource);
        }

        private async Task<CosmosBatchResponse> RunCrudAsync(bool isStream, bool isSchematized, bool useEpk, CosmosContainer container, RequestOptions batchOptions = null)
        {
            if (isSchematized)
            {
                await this.CreateSchematizedTestDocsAsync(container);

                batchOptions = CosmosBatchTestBase.GetUpdatedBatchRequestOptions(batchOptions, isSchematized, useEpk, this.PartitionKey1);
            }
            else
            {
                await this.CreateJsonTestDocsAsync(container);
            }

            TestDoc testDocToCreate = CosmosBatchTestBase.PopulateTestDoc(this.PartitionKey1);

            TestDoc testDocToUpsert = CosmosBatchTestBase.PopulateTestDoc(this.PartitionKey1);

            TestDoc anotherTestDocToUpsert = this.GetTestDocCopy(this.TestDocPk1ExistingA);
            anotherTestDocToUpsert.Cost++;

            TestDoc testDocToReplace = this.GetTestDocCopy(this.TestDocPk1ExistingB);
            testDocToReplace.Cost++;

            // We run CRUD operations where all are expected to return HTTP 2xx.
            CosmosBatchResponse batchResponse;
            if (!isStream)
            {
                batchResponse = await container.CreateBatch(CosmosBatchTestBase.GetPartitionKey(this.PartitionKey1))
                    .CreateItem(testDocToCreate)
                    .ReadItem(this.TestDocPk1ExistingC.Id)
                    .ReplaceItem(testDocToReplace.Id, testDocToReplace)
                    .UpsertItem(testDocToUpsert)
                    .UpsertItem(anotherTestDocToUpsert)
                    .DeleteItem(this.TestDocPk1ExistingD.Id)
                    .ExecuteAsync(batchOptions);
            }
            else
            {
                batchResponse = await container.CreateBatch(CosmosBatchTestBase.GetPartitionKey(this.PartitionKey1, useEpk))
                    .CreateItemStream(
                        CosmosBatchTestBase.TestDocToStream(testDocToCreate, isSchematized),
                        CosmosBatchTestBase.GetItemRequestOptions(testDocToCreate, isSchematized))
                    .ReadItem(
                        CosmosBatchTestBase.GetId(this.TestDocPk1ExistingC, isSchematized),
                        CosmosBatchTestBase.GetItemRequestOptions(this.TestDocPk1ExistingC, isSchematized))
                    .ReplaceItemStream(
                        CosmosBatchTestBase.GetId(testDocToReplace, isSchematized),
                        CosmosBatchTestBase.TestDocToStream(testDocToReplace, isSchematized),
                        CosmosBatchTestBase.GetItemRequestOptions(testDocToReplace, isSchematized))
                    .UpsertItemStream(
                        CosmosBatchTestBase.TestDocToStream(testDocToUpsert, isSchematized),
                        CosmosBatchTestBase.GetItemRequestOptions(testDocToUpsert, isSchematized))
                    .UpsertItemStream(
                        CosmosBatchTestBase.TestDocToStream(anotherTestDocToUpsert, isSchematized),
                        CosmosBatchTestBase.GetItemRequestOptions(anotherTestDocToUpsert, isSchematized))
                    .DeleteItem(
                        CosmosBatchTestBase.GetId(this.TestDocPk1ExistingD, isSchematized),
                        CosmosBatchTestBase.GetItemRequestOptions(this.TestDocPk1ExistingD, isSchematized))
                    .ExecuteAsync(batchOptions);
            }

            BatchSinglePartitionKeyTests.VerifyBatchProcessed(batchResponse, numberOfOperations: 6);

            Assert.AreEqual(HttpStatusCode.Created, batchResponse[0].StatusCode);
            Assert.AreEqual(HttpStatusCode.OK, batchResponse[1].StatusCode);
            Assert.AreEqual(HttpStatusCode.OK, batchResponse[2].StatusCode);
            Assert.AreEqual(HttpStatusCode.Created, batchResponse[3].StatusCode);
            Assert.AreEqual(HttpStatusCode.OK, batchResponse[4].StatusCode);
            Assert.AreEqual(HttpStatusCode.NoContent, batchResponse[5].StatusCode);

            if (!isStream)
            {
                Assert.AreEqual(this.TestDocPk1ExistingC, batchResponse.GetOperationResultAtIndex<TestDoc>(1).Resource);
            }
            else
            {
                Assert.AreEqual(this.TestDocPk1ExistingC, CosmosBatchTestBase.StreamToTestDoc(batchResponse[1].ResourceStream, isSchematized));
            }

            await CosmosBatchTestBase.VerifyByReadAsync(container, testDocToCreate, isStream, isSchematized, useEpk);
            await CosmosBatchTestBase.VerifyByReadAsync(container, testDocToReplace, isStream, isSchematized, useEpk);
            await CosmosBatchTestBase.VerifyByReadAsync(container, testDocToUpsert, isStream, isSchematized, useEpk);
            await CosmosBatchTestBase.VerifyByReadAsync(container, anotherTestDocToUpsert, isStream, isSchematized, useEpk);
            await CosmosBatchTestBase.VerifyNotFoundAsync(container, this.TestDocPk1ExistingD, isSchematized, useEpk);

            return batchResponse;
        }

        [TestMethod]
        [Owner("abpai")]
        [Description("Verify batch with a large set of read operations that is expected to be rate limited.")]
        public async Task BatchRateLimitingAsync()
        {
            CosmosContainer containerWithDefaultRetryPolicy = CosmosBatchTestBase.LowThroughputJsonContainer;

            await this.CreateJsonTestDocsAsync(containerWithDefaultRetryPolicy);
            CosmosClient clientWithNoThrottleRetry = new CosmosClientBuilder(
                    CosmosBatchTestBase.Client.ClientOptions.EndPoint.ToString(),
                    CosmosBatchTestBase.Client.ClientOptions.AccountKey.Key)
                    .WithThrottlingRetryOptions(
                    maxRetryWaitTimeOnThrottledRequests: default(TimeSpan),
                    maxRetryAttemptsOnThrottledRequests: 0)
                .Build();

            CosmosContainer containerWithNoThrottleRetry = 
                clientWithNoThrottleRetry.GetContainer(CosmosBatchTestBase.Database.Id, CosmosBatchTestBase.LowThroughputJsonContainer.Id);
            
            // The second batch started should be rate limited by the backend in admission control.
            {
                CosmosBatchResponse[] batchResponses = await this.RunTwoLargeBatchesAsync(containerWithNoThrottleRetry);

                Assert.AreEqual(HttpStatusCode.OK, batchResponses[0].StatusCode);
                Assert.AreEqual((int)StatusCodes.TooManyRequests, (int)batchResponses[1].StatusCode);
                Assert.AreEqual(3200, (int)batchResponses[1].SubStatusCode);
            }

            // The default retry policy around throttling should ensure the second batch also succeeds.
            {
                CosmosBatchResponse[] batchResponses = await this.RunTwoLargeBatchesAsync(containerWithDefaultRetryPolicy);

                Assert.AreEqual(HttpStatusCode.OK, batchResponses[0].StatusCode);
                Assert.AreEqual(HttpStatusCode.OK, batchResponses[1].StatusCode);
            }
        }

        private async Task<CosmosBatchResponse[]> RunTwoLargeBatchesAsync(CosmosContainer container)
        {
            CosmosBatch batch1 = container.CreateBatch(CosmosBatchTestBase.GetPartitionKey(this.PartitionKey1));
            CosmosBatch batch2 = container.CreateBatch(CosmosBatchTestBase.GetPartitionKey(this.PartitionKey1));

            for (int i = 0; i < Constants.MaxOperationsInDirectModeBatchRequest; i++)
            {
                batch1.CreateItem(BatchSinglePartitionKeyTests.PopulateTestDoc(this.PartitionKey1));
                batch2.CreateItem(BatchSinglePartitionKeyTests.PopulateTestDoc(this.PartitionKey1));
            }

            Task<CosmosBatchResponse> batch1Task = batch1.ExecuteAsync();
            await Task.Delay(50);
            Task<CosmosBatchResponse> batch2Task = batch2.ExecuteAsync();

            CosmosBatchResponse[] batchResponses = await Task.WhenAll(batch1Task, batch2Task);
            return batchResponses;
        }

        [TestMethod]
        [Owner("abpai")]
        [Description("Verify batch with a create operation having a conflict rolls back prior operations")]
        public async Task BatchWithCreateConflictAsync()
        {
            await this.RunBatchWithCreateConflictAsync(CosmosBatchTestBase.JsonContainer);
        }

        [TestMethod]
        [Owner("rakkuma")]
        [Description("Verify batch with a create operation having a conflict rolls back prior operations in gateway mode")]
        public async Task BatchWithCreateConflictGatewayAsync()
        {
            await this.RunBatchWithCreateConflictAsync(CosmosBatchTestBase.GatewayJsonContainer);
        }

        [TestMethod]
        [Owner("abpai")]
        [Description("Verify batch with a create operation having a conflict rolls back prior operations in shared throughput")]
        public async Task BatchWithCreateConflictSharedThroughputAsync()
        {
            await this.RunBatchWithCreateConflictAsync(CosmosBatchTestBase.SharedThroughputContainer);
        }

        private async Task RunBatchWithCreateConflictAsync(CosmosContainer container)
        {
            await this.CreateJsonTestDocsAsync(container);

            // try to create a doc with id that already exists (should return a Conflict)
            TestDoc conflictingTestDocToCreate = this.GetTestDocCopy(this.TestDocPk1ExistingA);
            conflictingTestDocToCreate.Cost++;

            await this.RunWithErrorAsync(
                container,
                batch => batch.CreateItem(conflictingTestDocToCreate),
                HttpStatusCode.Conflict);

            // make sure the conflicted doc hasn't changed
            await CosmosBatchTestBase.VerifyByReadAsync(container, this.TestDocPk1ExistingA);
        }

        [TestMethod]
        [Owner("abpai")]
        [Description("Verify batch with an invalid create operation rolls back prior operations")]
        public async Task BatchWithInvalidCreateAsync()
        {
            CosmosContainer container = CosmosBatchTestBase.JsonContainer;

            // partition key mismatch between doc and and value passed in to the operation
            await this.RunWithErrorAsync(
                container,
                batch => batch.CreateItem(CosmosBatchTestBase.PopulateTestDoc(partitionKey: Guid.NewGuid().ToString())),
                HttpStatusCode.BadRequest);
        }

        [TestMethod]
        [Owner("abpai")]
        [Description("Verify batch with a read operation on a non-existent entity rolls back prior operations")]
        public async Task BatchWithReadOfNonExistentEntityAsync()
        {
            CosmosContainer container = CosmosBatchTestBase.JsonContainer;
            await this.RunWithErrorAsync(
                container,
                batch => batch.ReadItem(Guid.NewGuid().ToString()), 
                HttpStatusCode.NotFound);
        }

        [TestMethod]
        [Owner("abpai")]
        [Description("Verify batch with a replace operation on a stale entity rolls back prior operations")]
        public async Task BatchWithReplaceOfStaleEntityAsync()
        {
            CosmosContainer container = CosmosBatchTestBase.JsonContainer;
            await this.CreateJsonTestDocsAsync(container);

            TestDoc staleTestDocToReplace = this.GetTestDocCopy(this.TestDocPk1ExistingA);
            staleTestDocToReplace.Cost++;
            ItemRequestOptions staleReplaceOptions = new ItemRequestOptions()
            {
                IfMatchEtag = Guid.NewGuid().ToString()
            };

            await this.RunWithErrorAsync(
                container,
                batch => batch.ReplaceItem(staleTestDocToReplace.Id, staleTestDocToReplace, staleReplaceOptions),
                HttpStatusCode.PreconditionFailed);

            // make sure the stale doc hasn't changed
            await CosmosBatchTestBase.VerifyByReadAsync(container, this.TestDocPk1ExistingA);
        }

        [TestMethod]
        [Owner("abpai")]
        [Description("Verify batch with a delete operation on a non-existent entity rolls back prior operations")]
        public async Task BatchWithDeleteOfNonExistentEntityAsync()
        {
            CosmosContainer container = CosmosBatchTestBase.JsonContainer;
            await this.RunWithErrorAsync(
                container,
                batch => batch.DeleteItem(Guid.NewGuid().ToString()),
                HttpStatusCode.NotFound);
        }

        private async Task<CosmosContainer> RunWithErrorAsync(
            CosmosContainer container,
            Action<CosmosBatch> appendOperation, 
            HttpStatusCode expectedFailedOperationStatusCode)
        { 
            TestDoc testDocToCreate = CosmosBatchTestBase.PopulateTestDoc(this.PartitionKey1);
            TestDoc anotherTestDocToCreate = CosmosBatchTestBase.PopulateTestDoc(this.PartitionKey1);

            CosmosBatch batch = container.CreateBatch(CosmosBatchTestBase.GetPartitionKey(this.PartitionKey1))
                .CreateItem(testDocToCreate);

            appendOperation(batch);

            CosmosBatchResponse batchResponse = await batch
                .CreateItem(anotherTestDocToCreate)
                .ExecuteAsync();

            BatchSinglePartitionKeyTests.VerifyBatchProcessed(
                batchResponse, 
                numberOfOperations: 3,
                expectedStatusCode: (HttpStatusCode)StatusCodes.MultiStatus);

            Assert.AreEqual((HttpStatusCode)StatusCodes.FailedDependency, batchResponse[0].StatusCode);
            Assert.AreEqual(expectedFailedOperationStatusCode, batchResponse[1].StatusCode);
            Assert.AreEqual((HttpStatusCode)StatusCodes.FailedDependency, batchResponse[2].StatusCode);

            await CosmosBatchTestBase.VerifyNotFoundAsync(container, testDocToCreate);
            await CosmosBatchTestBase.VerifyNotFoundAsync(container, anotherTestDocToCreate);
            return container;
        }

        private static void VerifyBatchProcessed(CosmosBatchResponse batchResponse, int numberOfOperations, HttpStatusCode expectedStatusCode = HttpStatusCode.OK)
        {
            Assert.IsNotNull(batchResponse);
            Assert.AreEqual(
                expectedStatusCode, 
                batchResponse.StatusCode,
                string.Format("Batch server response had StatusCode {0} instead of {1} expected and had ErrorMessage {2}",
                        batchResponse.StatusCode,
                        expectedStatusCode,
                        batchResponse.ErrorMessage));

            Assert.AreEqual(numberOfOperations, batchResponse.Count);
        }
    }
}
