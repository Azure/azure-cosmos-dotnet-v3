//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestClass]
    public class BatchSinglePartitionKeyTests : BatchTestBase
    {
        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            BatchTestBase.ClassInit(context);
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            BatchTestBase.ClassClean();
        }

        [TestMethod]
        [Owner("abpai")]
        [Description("Verify batch CRUD with default options at all levels (client/batch/operation) and all operations expected to pass works")]
        public async Task BatchCrudAsync()
        {
            await this.RunCrudAsync(isStream: false, isSchematized: false, useEpk: false, container: BatchTestBase.JsonContainer);
        }

        [TestMethod]
        [Owner("abpai")]
        [Description("Verify batch CRUD with JSON stream operation resource bodies with default options and all operations expected to pass works")]
        public async Task BatchCrudStreamAsync()
        {
            await this.RunCrudAsync(isStream: true, isSchematized: false, useEpk: false, container: BatchTestBase.JsonContainer);
        }

        [TestMethod]
        [Owner("abpai")]
        [Description("Verify batch CRUD with HybridRow stream operation resource bodies and EPK with default options and all operations expected to pass works")]
        public async Task BatchCrudHybridRowStreamWithEpkAsync()
        {
            await this.RunCrudAsync(isStream: true, isSchematized: true, useEpk: true, container: BatchTestBase.SchematizedContainer);
        }

        [TestMethod]
        [Owner("rakkuma")]
        [Description("Verify batch CRUD with default options at all levels (client/batch/operation) and all operations expected to pass works in gateway mode")]
        public async Task BatchCrudGatewayAsync()
        {
            await this.RunCrudAsync(isStream: false, isSchematized: false, useEpk: false, container: BatchTestBase.GatewayJsonContainer);
        }

        [TestMethod]
        [Owner("rakkuma")]
        [Description("Verify batch CRUD with JSON stream operation resource bodies with default options and all operations expected to pass works in gateway mode")]
        public async Task BatchCrudStreamGatewayAsync()
        {
            await this.RunCrudAsync(isStream: true, isSchematized: false, useEpk: false, container: BatchTestBase.GatewayJsonContainer);
        }

        [TestMethod]
        [Owner("rakkuma")]
        [Description("Verify batch CRUD with default options at all levels (client/batch/operation) and all operations expected to pass works in shared throughput ")]
        public async Task BatchCrudSharedThroughputAsync()
        {
            await this.RunCrudAsync(isStream: false, isSchematized: false, useEpk: false, container: BatchTestBase.SharedThroughputContainer);
        }

        [TestMethod]
        [Owner("rakkuma")]
        [Description("Verify batch CRUD with JSON stream operation resource bodies with default options and all operations expected to pass works in shared throughput")]
        public async Task BatchCrudSharedThroughputStreamAsync()
        {
            await this.RunCrudAsync(isStream: true, isSchematized: false, useEpk: false, container: BatchTestBase.SharedThroughputContainer);
        }

        [TestMethod]
        [Owner("abpai")]
        [Description("Verify batch with multiple operations on the same entity works")]
        public async Task BatchOrderedAsync()
        {
            Container container = BatchTestBase.JsonContainer;
            await this.CreateJsonTestDocsAsync(container);

            TestDoc firstDoc = BatchTestBase.PopulateTestDoc(this.PartitionKey1);

            TestDoc replaceDoc = this.GetTestDocCopy(firstDoc);
            replaceDoc.Cost += 20;

            TransactionalBatchResponse batchResponse = await new BatchCore((ContainerInlineCore)container, BatchTestBase.GetPartitionKey(this.PartitionKey1))
                .CreateItem(firstDoc)
                .ReplaceItem(replaceDoc.Id, replaceDoc)
                .ExecuteAsync();

            BatchSinglePartitionKeyTests.VerifyBatchProcessed(batchResponse, numberOfOperations: 2);

            Assert.AreEqual(HttpStatusCode.Created, batchResponse[0].StatusCode);
            Assert.AreEqual(HttpStatusCode.OK, batchResponse[1].StatusCode);

            // Ensure that the replace overwrote the doc from the first operation
            await BatchTestBase.VerifyByReadAsync(container, replaceDoc);
        }

        [TestMethod]
        [Owner("abpai")]
        [Description("Verify eTags passed to batch operations or returned in batch results flow as expected")]
        public async Task BatchItemETagAsync()
        {
            Container container = BatchTestBase.JsonContainer;
            await this.CreateJsonTestDocsAsync(container);
            {
                TestDoc testDocToCreate = BatchTestBase.PopulateTestDoc(this.PartitionKey1);

                TestDoc testDocToReplace = this.GetTestDocCopy(this.TestDocPk1ExistingA);
                testDocToReplace.Cost++;

                ItemResponse<TestDoc> readResponse = await BatchTestBase.JsonContainer.ReadItemAsync<TestDoc>(
                    this.TestDocPk1ExistingA.Id,
                    BatchTestBase.GetPartitionKey(this.PartitionKey1));

                TransactionalBatchItemRequestOptions firstReplaceOptions = new TransactionalBatchItemRequestOptions()
                {
                    IfMatchEtag = readResponse.ETag
                };

                TransactionalBatchResponse batchResponse = await new BatchCore((ContainerInlineCore)container, BatchTestBase.GetPartitionKey(this.PartitionKey1))
                   .CreateItem(testDocToCreate)
                   .ReplaceItem(testDocToReplace.Id, testDocToReplace, requestOptions: firstReplaceOptions)
                   .ExecuteAsync();

                BatchSinglePartitionKeyTests.VerifyBatchProcessed(batchResponse, numberOfOperations: 2);

                Assert.AreEqual(HttpStatusCode.Created, batchResponse[0].StatusCode);
                Assert.AreEqual(HttpStatusCode.OK, batchResponse[1].StatusCode);

                await BatchTestBase.VerifyByReadAsync(container, testDocToCreate, eTag: batchResponse[0].ETag);
                await BatchTestBase.VerifyByReadAsync(container, testDocToReplace, eTag: batchResponse[1].ETag);
            }

            {
                TestDoc testDocToReplace = this.GetTestDocCopy(this.TestDocPk1ExistingB);
                testDocToReplace.Cost++;

                TransactionalBatchItemRequestOptions replaceOptions = new TransactionalBatchItemRequestOptions()
                {
                    IfMatchEtag = BatchTestBase.Random.Next().ToString()
                };

                TransactionalBatchResponse batchResponse = await new BatchCore((ContainerInlineCore)container, BatchTestBase.GetPartitionKey(this.PartitionKey1))
                   .ReplaceItem(testDocToReplace.Id, testDocToReplace, requestOptions: replaceOptions)
                   .ExecuteAsync();

                BatchSinglePartitionKeyTests.VerifyBatchProcessed(
                    batchResponse,
                    numberOfOperations: 1,
                    expectedStatusCode: HttpStatusCode.PreconditionFailed);

                Assert.AreEqual(HttpStatusCode.PreconditionFailed, batchResponse[0].StatusCode);

                // ensure the document was not updated
                await BatchTestBase.VerifyByReadAsync(container, this.TestDocPk1ExistingB);
            }
        }

        [TestMethod]
        [Owner("rakkuma")]
        [Description("Verify session token received from batch operations")]
        public async Task BatchItemSessionTokenAsync()
        {
            Container container = BatchTestBase.JsonContainer;
            await this.CreateJsonTestDocsAsync(container);

            TestDoc testDocToCreate = BatchTestBase.PopulateTestDoc(this.PartitionKey1);

            TestDoc testDocToReplace = this.GetTestDocCopy(this.TestDocPk1ExistingA);
            testDocToReplace.Cost++;

            ItemResponse<TestDoc> readResponse = await BatchTestBase.JsonContainer.ReadItemAsync<TestDoc>(
                this.TestDocPk1ExistingA.Id,
                BatchTestBase.GetPartitionKey(this.PartitionKey1));

            ISessionToken beforeRequestSessionToken = BatchTestBase.GetSessionToken(readResponse.Headers.Session);

            TransactionalBatchResponse batchResponse = await new BatchCore((ContainerInlineCore)container, BatchTestBase.GetPartitionKey(this.PartitionKey1))
                .CreateItem(testDocToCreate)
                .ReplaceItem(testDocToReplace.Id, testDocToReplace)
                .ExecuteAsync();

            BatchSinglePartitionKeyTests.VerifyBatchProcessed(batchResponse, numberOfOperations: 2);
            Assert.AreEqual(HttpStatusCode.Created, batchResponse[0].StatusCode);
            Assert.AreEqual(HttpStatusCode.OK, batchResponse[1].StatusCode);

            ISessionToken afterRequestSessionToken = BatchTestBase.GetSessionToken(batchResponse.Headers.Session);
            Assert.IsTrue(afterRequestSessionToken.LSN > beforeRequestSessionToken.LSN, "Response session token should be more than request session token");
        }

        [TestMethod]
        [Owner("abpai")]
        [Description("Verify TTL passed to binary passthrough batch operations flow as expected")]
        public async Task BatchItemTimeToLiveAsync()
        {
            // Verify with schematized containers where we are allowed to send TTL as a header
            const bool isSchematized = true;
            const bool isStream = true;
            Container container = BatchTestBase.SchematizedContainer;
            await this.CreateSchematizedTestDocsAsync(container);
            {
                TestDoc testDocToCreate = BatchTestBase.PopulateTestDoc(this.PartitionKey1);
                TestDoc anotherTestDocToCreate = BatchTestBase.PopulateTestDoc(this.PartitionKey1);

                TestDoc testDocToReplace = this.GetTestDocCopy(this.TestDocPk1ExistingA);
                testDocToReplace.Cost++;

                const int ttlInSeconds = 3;
                const int infiniteTtl = -1;

                TestDoc testDocToUpsert = await BatchTestBase.CreateSchematizedTestDocAsync(container, this.PartitionKey1, ttlInSeconds: ttlInSeconds);
                testDocToUpsert.Cost++;

                BatchCore batch = (BatchCore)new BatchCore((ContainerInlineCore)container, BatchTestBase.GetPartitionKey(this.PartitionKey1))
                       .CreateItemStream(
                            BatchTestBase.TestDocToStream(testDocToCreate, isSchematized),
                            BatchTestBase.GetBatchItemRequestOptions(testDocToCreate, isSchematized, ttlInSeconds: ttlInSeconds))
                       .CreateItemStream(
                            BatchTestBase.TestDocToStream(anotherTestDocToCreate, isSchematized),
                            BatchTestBase.GetBatchItemRequestOptions(anotherTestDocToCreate, isSchematized))
                       .ReplaceItemStream(
                            BatchTestBase.GetId(testDocToReplace, isSchematized),
                            BatchTestBase.TestDocToStream(testDocToReplace, isSchematized),
                            BatchTestBase.GetBatchItemRequestOptions(testDocToReplace, isSchematized, ttlInSeconds: ttlInSeconds))
                       .UpsertItemStream(
                            BatchTestBase.TestDocToStream(testDocToUpsert, isSchematized),
                            BatchTestBase.GetBatchItemRequestOptions(testDocToUpsert, isSchematized, ttlInSeconds: infiniteTtl));

                TransactionalBatchResponse batchResponse = await batch.ExecuteAsync(BatchTestBase.GetUpdatedBatchRequestOptions(isSchematized: true));

                BatchSinglePartitionKeyTests.VerifyBatchProcessed(batchResponse, numberOfOperations: 4);

                Assert.AreEqual(HttpStatusCode.Created, batchResponse[0].StatusCode);
                Assert.AreEqual(HttpStatusCode.Created, batchResponse[1].StatusCode);
                Assert.AreEqual(HttpStatusCode.OK, batchResponse[2].StatusCode);
                Assert.AreEqual(HttpStatusCode.OK, batchResponse[3].StatusCode);

                // wait for TTL to expire
                await Task.Delay(TimeSpan.FromSeconds(ttlInSeconds + 1));

                await BatchTestBase.VerifyNotFoundAsync(container, testDocToCreate, isSchematized);
                await BatchTestBase.VerifyByReadAsync(container, anotherTestDocToCreate, isStream, isSchematized);
                await BatchTestBase.VerifyNotFoundAsync(container, testDocToReplace, isSchematized);
                await BatchTestBase.VerifyByReadAsync(container, testDocToUpsert, isStream, isSchematized);
            }
        }

        [TestMethod]
        [Owner("abpai")]
        public async Task BatchLargerThanServerRequestAsync()
        {
            Container container = BatchTestBase.JsonContainer;

            const int operationCount = 20;
            int appxDocSize = Constants.MaxDirectModeBatchRequestBodySizeInBytes / operationCount;

            // Increase the doc size by a bit so all docs won't fit in one server request.
            appxDocSize = (int)(appxDocSize * 1.05);
            TransactionalBatch batch = new BatchCore((ContainerInlineCore)container, new Cosmos.PartitionKey(this.PartitionKey1));
            for (int i = 0; i < operationCount; i++)
            {
                TestDoc doc = BatchTestBase.PopulateTestDoc(this.PartitionKey1, minDesiredSize: appxDocSize);
                batch.CreateItem(doc);
            }

            TransactionalBatchResponse batchResponse = await batch.ExecuteAsync();
            Assert.AreEqual(HttpStatusCode.RequestEntityTooLarge, batchResponse.StatusCode);
        }

        [TestMethod]
        [Owner("abpai")]
        public async Task BatchWithTooManyOperationsAsync()
        {
            Container container = BatchTestBase.JsonContainer;
            const int operationCount = Constants.MaxOperationsInDirectModeBatchRequest + 1;

            TransactionalBatch batch = new BatchCore((ContainerInlineCore)container, new Cosmos.PartitionKey(this.PartitionKey1));
            for (int i = 0; i < operationCount; i++)
            {
                batch.ReadItem("someId");
            }

            TransactionalBatchResponse batchResponse = await batch.ExecuteAsync();
            Assert.AreEqual(HttpStatusCode.BadRequest, batchResponse.StatusCode);
        }

        [TestMethod]
        [Owner("abpai")]
        public async Task BatchServerResponseTooLargeAsync()
        {
            Container container = BatchTestBase.JsonContainer;
            const int operationCount = 10;
            int appxDocSizeInBytes = 1 * 1024 * 1024;

            TestDoc doc = await BatchTestBase.CreateJsonTestDocAsync(container, this.PartitionKey1, appxDocSizeInBytes);

            TransactionalBatch batch = new BatchCore((ContainerInlineCore)container, BatchTestBase.GetPartitionKey(this.PartitionKey1));
            for (int i = 0; i < operationCount; i++)
            {
                batch.ReadItem(doc.Id);
            }

            TransactionalBatchResponse batchResponse = await batch.ExecuteAsync();

            BatchSinglePartitionKeyTests.VerifyBatchProcessed(
                batchResponse, 
                numberOfOperations: operationCount,
                expectedStatusCode: HttpStatusCode.RequestEntityTooLarge);

            Assert.AreEqual((int)StatusCodes.FailedDependency, (int)batchResponse[0].StatusCode);
            Assert.AreEqual(HttpStatusCode.RequestEntityTooLarge, batchResponse[operationCount - 1].StatusCode);
        }

        [TestMethod]
        [Owner("abpai")]
        public async Task BatchReadsOnlyAsync()
        {
            Container container = BatchTestBase.JsonContainer;
            await this.CreateJsonTestDocsAsync(container);

            TransactionalBatchResponse batchResponse = await new BatchCore((ContainerInlineCore)container, BatchTestBase.GetPartitionKey(this.PartitionKey1))
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

        [TestMethod]
        [Owner("antoshni")]
        [Description("Verify patch operation can follow create operation.")]
        public async Task BatchCreateAndPatchAsync()
        {
            TestDoc testDoc = BatchTestBase.PopulateTestDoc(this.PartitionKey1);
            List<PatchOperation> patchOperations = new List<PatchOperation>()
            {
                PatchOperation.CreateReplaceOperation("/Cost", testDoc.Cost + 1)
            };

            BatchCore batch = (BatchCore)new BatchCore((ContainerInlineCore)BatchTestBase.JsonContainer, BatchTestBase.GetPartitionKey(this.PartitionKey1))
                .CreateItem(testDoc);

            batch = (BatchCore)batch.PatchItem(testDoc.Id, patchOperations);

            TransactionalBatchResponse batchResponse = await batch.ExecuteAsync();

            BatchSinglePartitionKeyTests.VerifyBatchProcessed(batchResponse, numberOfOperations: 2);

            Assert.AreEqual(HttpStatusCode.Created, batchResponse[0].StatusCode);
            Assert.AreEqual(HttpStatusCode.OK, batchResponse[1].StatusCode);
            testDoc.Cost = testDoc.Cost + 1;
            await BatchTestBase.VerifyByReadAsync(BatchTestBase.JsonContainer, testDoc, isStream: false, isSchematized: false, useEpk:false);
        }

        [TestMethod]
        [Owner("antoshni")]
        [Description("Verify custom serializer is used for patch operation.")]
        public async Task BatchCustomSerializerUsedForPatchAsync()
        {
            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                Serializer = new CosmosJsonDotNetSerializer(
                    new JsonSerializerSettings()
                    {
                        DateFormatString = "yyyy--MM--dd hh:mm"
                    })
            };

            CosmosClient customSerializationClient = TestCommon.CreateCosmosClient(clientOptions);
            Container customSerializationContainer = customSerializationClient.GetContainer(BatchTestBase.Database.Id, BatchTestBase.JsonContainer.Id);

            TestDoc testDoc = BatchTestBase.PopulateTestDoc(this.PartitionKey1);

            DateTime patchDate = new DateTime(2020, 07, 01, 01, 02, 03);
            List<PatchOperation> patchOperations = new List<PatchOperation>()
            {
                PatchOperation.CreateAddOperation("/date", patchDate)
            };

            BatchCore batch = (BatchCore)new BatchCore((ContainerInlineCore)customSerializationContainer, BatchTestBase.GetPartitionKey(this.PartitionKey1))
                .CreateItem(testDoc);

            batch = (BatchCore)batch.PatchItem(testDoc.Id, patchOperations);

            TransactionalBatchResponse batchResponse = await batch.ExecuteAsync();

            BatchSinglePartitionKeyTests.VerifyBatchProcessed(batchResponse, numberOfOperations: 2);

            Assert.AreEqual(HttpStatusCode.Created, batchResponse[0].StatusCode);
            Assert.AreEqual(HttpStatusCode.OK, batchResponse[1].StatusCode);

            JsonSerializerSettings jsonSettings = new JsonSerializerSettings();
            jsonSettings.DateFormatString = "yyyy--MM--dd hh:mm";
            string dateJson = JsonConvert.SerializeObject(patchDate, jsonSettings);

            // regular container
            ItemResponse<dynamic> response = await BatchTestBase.JsonContainer.ReadItemAsync<dynamic>(
                testDoc.Id,
                BatchTestBase.GetPartitionKey(this.PartitionKey1));

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.IsNotNull(response.Resource);
            Assert.IsTrue(dateJson.Contains(response.Resource["date"].ToString()));
        }

        private async Task<TransactionalBatchResponse> RunCrudAsync(bool isStream, bool isSchematized, bool useEpk, Container container)
        {
            TransactionalBatchRequestOptions batchOptions = null;
            if (isSchematized)
            {
                await this.CreateSchematizedTestDocsAsync(container);

                batchOptions = BatchTestBase.GetUpdatedBatchRequestOptions(batchOptions, isSchematized, useEpk, this.PartitionKey1);
            }
            else
            {
                await this.CreateJsonTestDocsAsync(container);
            }

            TestDoc testDocToCreate = BatchTestBase.PopulateTestDoc(this.PartitionKey1);

            TestDoc testDocToUpsert = BatchTestBase.PopulateTestDoc(this.PartitionKey1);

            TestDoc anotherTestDocToUpsert = this.GetTestDocCopy(this.TestDocPk1ExistingA);
            anotherTestDocToUpsert.Cost++;

            TestDoc testDocToReplace = this.GetTestDocCopy(this.TestDocPk1ExistingB);
            testDocToReplace.Cost++;

            TestDoc testDocToPatch = this.GetTestDocCopy(this.TestDocPk1ExistingC);
            List<PatchOperation> patchOperations = new List<PatchOperation>()
            {
                PatchOperation.CreateReplaceOperation("/Cost", testDocToPatch.Cost + 1)
            };

            // We run CRUD operations where all are expected to return HTTP 2xx.
            TransactionalBatchResponse batchResponse;
            BatchCore batch;
            if (!isStream)
            {
                batch = (BatchCore)new BatchCore((ContainerInlineCore)container, BatchTestBase.GetPartitionKey(this.PartitionKey1))
                    .CreateItem(testDocToCreate)
                    .ReadItem(this.TestDocPk1ExistingC.Id)
                    .ReplaceItem(testDocToReplace.Id, testDocToReplace)
                    .UpsertItem(testDocToUpsert)
                    .UpsertItem(anotherTestDocToUpsert)
                    .DeleteItem(this.TestDocPk1ExistingD.Id);

                batch = (BatchCore)batch.PatchItem(testDocToPatch.Id, patchOperations);
            }
            else
            {
                batch = (BatchCore)new BatchCore((ContainerInlineCore)container, BatchTestBase.GetPartitionKey(this.PartitionKey1))
                    .CreateItemStream(
                        BatchTestBase.TestDocToStream(testDocToCreate, isSchematized),
                        BatchTestBase.GetBatchItemRequestOptions(testDocToCreate, isSchematized))
                    .ReadItem(
                        BatchTestBase.GetId(this.TestDocPk1ExistingC, isSchematized),
                        BatchTestBase.GetBatchItemRequestOptions(this.TestDocPk1ExistingC, isSchematized))
                    .ReplaceItemStream(
                        BatchTestBase.GetId(testDocToReplace, isSchematized),
                        BatchTestBase.TestDocToStream(testDocToReplace, isSchematized),
                        BatchTestBase.GetBatchItemRequestOptions(testDocToReplace, isSchematized))
                    .UpsertItemStream(
                        BatchTestBase.TestDocToStream(testDocToUpsert, isSchematized),
                        BatchTestBase.GetBatchItemRequestOptions(testDocToUpsert, isSchematized))
                    .UpsertItemStream(
                        BatchTestBase.TestDocToStream(anotherTestDocToUpsert, isSchematized),
                        BatchTestBase.GetBatchItemRequestOptions(anotherTestDocToUpsert, isSchematized))
                    .DeleteItem(
                        BatchTestBase.GetId(this.TestDocPk1ExistingD, isSchematized),
                        BatchTestBase.GetBatchItemRequestOptions(this.TestDocPk1ExistingD, isSchematized));                
            }

            batchResponse = await batch.ExecuteAsync(batchOptions);
            BatchSinglePartitionKeyTests.VerifyBatchProcessed(batchResponse, numberOfOperations: isStream ? 6 :7);

            Assert.AreEqual(HttpStatusCode.Created, batchResponse[0].StatusCode);
            Assert.AreEqual(HttpStatusCode.OK, batchResponse[1].StatusCode);
            Assert.AreEqual(HttpStatusCode.OK, batchResponse[2].StatusCode);
            Assert.AreEqual(HttpStatusCode.Created, batchResponse[3].StatusCode);
            Assert.AreEqual(HttpStatusCode.OK, batchResponse[4].StatusCode);
            Assert.AreEqual(HttpStatusCode.NoContent, batchResponse[5].StatusCode);

            if (!isStream)
            {
                Assert.AreEqual(this.TestDocPk1ExistingC, batchResponse.GetOperationResultAtIndex<TestDoc>(1).Resource);
                Assert.AreEqual(HttpStatusCode.OK, batchResponse[6].StatusCode);
                testDocToPatch.Cost = testDocToPatch.Cost + 1;
                await BatchTestBase.VerifyByReadAsync(container, testDocToPatch, isStream, isSchematized, useEpk);
            }
            else
            {
                Assert.AreEqual(this.TestDocPk1ExistingC, BatchTestBase.StreamToTestDoc(batchResponse[1].ResourceStream, isSchematized));
            }

            await BatchTestBase.VerifyByReadAsync(container, testDocToCreate, isStream, isSchematized, useEpk);
            await BatchTestBase.VerifyByReadAsync(container, testDocToReplace, isStream, isSchematized, useEpk);
            await BatchTestBase.VerifyByReadAsync(container, testDocToUpsert, isStream, isSchematized, useEpk);
            await BatchTestBase.VerifyByReadAsync(container, anotherTestDocToUpsert, isStream, isSchematized, useEpk);
            await BatchTestBase.VerifyNotFoundAsync(container, this.TestDocPk1ExistingD, isSchematized, useEpk);

            return batchResponse;
        }

        [TestMethod]
        [Ignore]
        [Owner("abpai")]
        [Description("Verify batch with a large set of read operations that is expected to be rate limited.")]
        public async Task BatchRateLimitingAsync()
        {
            Container containerWithDefaultRetryPolicy = BatchTestBase.LowThroughputJsonContainer;

            await this.CreateJsonTestDocsAsync(containerWithDefaultRetryPolicy);
            CosmosClient clientWithNoThrottleRetry = new CosmosClientBuilder(
                    BatchTestBase.Client.Endpoint.ToString(),
                    BatchTestBase.Client.AccountKey)
                    .WithThrottlingRetryOptions(
                    maxRetryWaitTimeOnThrottledRequests: default(TimeSpan),
                    maxRetryAttemptsOnThrottledRequests: 0)
                .Build();

            Container containerWithNoThrottleRetry = 
                clientWithNoThrottleRetry.GetContainer(BatchTestBase.Database.Id, BatchTestBase.LowThroughputJsonContainer.Id);
            
            // The second batch started should be rate limited by the backend in admission control.
            {
                TransactionalBatchResponse[] batchResponses = await this.RunTwoLargeBatchesAsync(containerWithNoThrottleRetry);

                Assert.AreEqual(HttpStatusCode.OK, batchResponses[0].StatusCode);
                Assert.AreEqual((int)StatusCodes.TooManyRequests, (int)batchResponses[1].StatusCode);
                Assert.AreEqual(3200, (int)batchResponses[1].SubStatusCode);
            }

            // The default retry policy around throttling should ensure the second batch also succeeds.
            {
                TransactionalBatchResponse[] batchResponses = await this.RunTwoLargeBatchesAsync(containerWithDefaultRetryPolicy);

                Assert.AreEqual(HttpStatusCode.OK, batchResponses[0].StatusCode);
                Assert.AreEqual(HttpStatusCode.OK, batchResponses[1].StatusCode);
            }
        }

        private async Task<TransactionalBatchResponse[]> RunTwoLargeBatchesAsync(Container container)
        {
            TransactionalBatch batch1 = new BatchCore((ContainerInlineCore)container, BatchTestBase.GetPartitionKey(this.PartitionKey1));
            TransactionalBatch batch2 = new BatchCore((ContainerInlineCore)container, BatchTestBase.GetPartitionKey(this.PartitionKey1));

            for (int i = 0; i < Constants.MaxOperationsInDirectModeBatchRequest; i++)
            {
                batch1.CreateItem(BatchSinglePartitionKeyTests.PopulateTestDoc(this.PartitionKey1));
                batch2.CreateItem(BatchSinglePartitionKeyTests.PopulateTestDoc(this.PartitionKey1));
            }

            Task<TransactionalBatchResponse> batch1Task = batch1.ExecuteAsync();
            await Task.Delay(50);
            Task<TransactionalBatchResponse> batch2Task = batch2.ExecuteAsync();

            TransactionalBatchResponse[] batchResponses = await Task.WhenAll(batch1Task, batch2Task);
            return batchResponses;
        }

        [TestMethod]
        [Owner("abpai")]
        [Description("Verify batch with a create operation having a conflict rolls back prior operations")]
        public async Task BatchWithCreateConflictAsync()
        {
            await this.RunBatchWithCreateConflictAsync(BatchTestBase.JsonContainer);
        }

        [TestMethod]
        [Owner("rakkuma")]
        [Description("Verify batch with a create operation having a conflict rolls back prior operations in gateway mode")]
        public async Task BatchWithCreateConflictGatewayAsync()
        {
            await this.RunBatchWithCreateConflictAsync(BatchTestBase.GatewayJsonContainer);
        }

        [TestMethod]
        [Owner("abpai")]
        [Description("Verify batch with a create operation having a conflict rolls back prior operations in shared throughput")]
        public async Task BatchWithCreateConflictSharedThroughputAsync()
        {
            await this.RunBatchWithCreateConflictAsync(BatchTestBase.SharedThroughputContainer);
        }

        [TestMethod]
        [Owner("abpai")]
        [Description("Verify batch with an invalid create operation rolls back prior operations")]
        public async Task BatchWithInvalidCreateAsync()
        {
            Container container = BatchTestBase.JsonContainer;

            // partition key mismatch between doc and and value passed in to the operation
            await this.RunWithErrorAsync(
                container,
                batch => batch.CreateItem(BatchTestBase.PopulateTestDoc(partitionKey: Guid.NewGuid().ToString())),
                HttpStatusCode.BadRequest);
        }

        [TestMethod]
        [Owner("abpai")]
        [Description("Verify batch with a read operation on a non-existent entity rolls back prior operations")]
        public async Task BatchWithReadOfNonExistentEntityAsync()
        {
            Container container = BatchTestBase.JsonContainer;
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
            Container container = BatchTestBase.JsonContainer;
            await this.CreateJsonTestDocsAsync(container);

            TestDoc staleTestDocToReplace = this.GetTestDocCopy(this.TestDocPk1ExistingA);
            staleTestDocToReplace.Cost++;
            TransactionalBatchItemRequestOptions staleReplaceOptions = new TransactionalBatchItemRequestOptions()
            {
                IfMatchEtag = Guid.NewGuid().ToString()
            };

            await this.RunWithErrorAsync(
                container,
                batch => batch.ReplaceItem(staleTestDocToReplace.Id, staleTestDocToReplace, staleReplaceOptions),
                HttpStatusCode.PreconditionFailed);

            // make sure the stale doc hasn't changed
            await BatchTestBase.VerifyByReadAsync(container, this.TestDocPk1ExistingA);
        }

        [TestMethod]
        [Owner("abpai")]
        [Description("Verify batch with a delete operation on a non-existent entity rolls back prior operations")]
        public async Task BatchWithDeleteOfNonExistentEntityAsync()
        {
            Container container = BatchTestBase.JsonContainer;
            await this.RunWithErrorAsync(
                container,
                batch => batch.DeleteItem(Guid.NewGuid().ToString()),
                HttpStatusCode.NotFound);
        }

        private async Task RunBatchWithCreateConflictAsync(Container container)
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
            await BatchTestBase.VerifyByReadAsync(container, this.TestDocPk1ExistingA);
        }

        private async Task<Container> RunWithErrorAsync(
            Container container,
            Action<TransactionalBatch> appendOperation, 
            HttpStatusCode expectedFailedOperationStatusCode)
        { 
            TestDoc testDocToCreate = BatchTestBase.PopulateTestDoc(this.PartitionKey1);
            TestDoc anotherTestDocToCreate = BatchTestBase.PopulateTestDoc(this.PartitionKey1);

            TransactionalBatch batch = new BatchCore((ContainerInlineCore)container, BatchTestBase.GetPartitionKey(this.PartitionKey1))
                .CreateItem(testDocToCreate);

            appendOperation(batch);

            TransactionalBatchResponse batchResponse = await batch
                .CreateItem(anotherTestDocToCreate)
                .ExecuteAsync();

            BatchSinglePartitionKeyTests.VerifyBatchProcessed(
                batchResponse, 
                numberOfOperations: 3,
                expectedStatusCode: expectedFailedOperationStatusCode);

            Assert.AreEqual((HttpStatusCode)StatusCodes.FailedDependency, batchResponse[0].StatusCode);
            Assert.AreEqual(expectedFailedOperationStatusCode, batchResponse[1].StatusCode);
            Assert.AreEqual((HttpStatusCode)StatusCodes.FailedDependency, batchResponse[2].StatusCode);

            await BatchTestBase.VerifyNotFoundAsync(container, testDocToCreate);
            await BatchTestBase.VerifyNotFoundAsync(container, anotherTestDocToCreate);
            return container;
        }

        private static void VerifyBatchProcessed(TransactionalBatchResponse batchResponse, int numberOfOperations, HttpStatusCode expectedStatusCode = HttpStatusCode.OK)
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

            Assert.IsTrue(batchResponse.RequestCharge > 0);

            // Allow a delta since we round both the total charge and the individual operation
            // charges to 2 decimal places.
            Assert.AreEqual(
                batchResponse.RequestCharge,
                batchResponse.Sum(result => result.RequestCharge),
                0.1);
        }
    }
}
