//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;

    [TestClass]
    public class BatchUnitTests
    {
        private const string DatabaseId = "mockDatabase";

        private const string ContainerId = "mockContainer";

        private const string PartitionKey1 = "somePartKey";

        [TestMethod]
        [Owner("abpai")]
        public async Task BatchInvalidOptionsAsync()
        {
            Container container = BatchUnitTests.GetContainer();
            List<TransactionalBatchRequestOptions> badBatchOptionsList = new List<TransactionalBatchRequestOptions>()
            {
                new TransactionalBatchRequestOptions()
                {
                    IfMatchEtag = "cond",
                },
                new TransactionalBatchRequestOptions()
                {
                    IfNoneMatchEtag = "cond2",
                }
            };

            foreach (TransactionalBatchRequestOptions batchOptions in badBatchOptionsList)
            {
                BatchCore batch = (BatchCore)
                        new BatchCore((ContainerInternal)container, new Cosmos.PartitionKey(BatchUnitTests.PartitionKey1))
                            .ReadItem("someId");

                await BatchUnitTests.VerifyExceptionThrownOnExecuteAsync(
                    batch,
                    typeof(ArgumentException),
                    ClientResources.BatchRequestOptionNotSupported,
                    batchOptions);
            }
        }

        [TestMethod]
        [Owner("abpai")]
        public async Task BatchInvalidItemOptionsAsync()
        {
            Container container = BatchUnitTests.GetContainer();

            List<TransactionalBatchItemRequestOptions> badItemOptionsList = new List<TransactionalBatchItemRequestOptions>()
            {
                new TransactionalBatchItemRequestOptions()
                {
                    Properties = new Dictionary<string, object>
                    {
                        // EPK without string representation
                        { WFConstants.BackendHeaders.EffectivePartitionKey, new byte[1] { 0x41 } }
                    }
                },
                new TransactionalBatchItemRequestOptions()
                {
                    Properties = new Dictionary<string, object>
                    {
                        // EPK string without corresponding byte representation
                        { WFConstants.BackendHeaders.EffectivePartitionKeyString, "epk" }
                    }
                },
                new TransactionalBatchItemRequestOptions()
                {
                    Properties = new Dictionary<string, object>
                    {
                        // Partition key without EPK string
                        { HttpConstants.HttpHeaders.PartitionKey, "epk" }
                    }
                }
            };

            foreach (TransactionalBatchItemRequestOptions itemOptions in badItemOptionsList)
            {
                TransactionalBatch batch = new BatchCore((ContainerInternal)container, new Cosmos.PartitionKey(BatchUnitTests.PartitionKey1))
                        .ReplaceItem("someId", new TestItem("repl"), itemOptions);

                await BatchUnitTests.VerifyExceptionThrownOnExecuteAsync(
                    batch,
                    typeof(ArgumentException));
            }
        }

        [TestMethod]
        [Owner("abpai")]
        public async Task BatchNoOperationsAsync()
        {
            Container container = BatchUnitTests.GetContainer();
            TransactionalBatch batch = new BatchCore((ContainerInternal)container, new Cosmos.PartitionKey(BatchUnitTests.PartitionKey1));
            await BatchUnitTests.VerifyExceptionThrownOnExecuteAsync(
                batch,
                typeof(ArgumentException),
                ClientResources.BatchNoOperations);
        }

        [TestMethod]
        [Owner("abpai")]
        public async Task BatchCrudRequestAsync()
        {
            Random random = new Random();

            TestItem createItem = new TestItem("create");
            byte[] createStreamContent = new byte[20];
            random.NextBytes(createStreamContent);
            byte[] createStreamBinaryId = new byte[20];
            random.NextBytes(createStreamBinaryId);
            int createTtl = 45;
            TransactionalBatchItemRequestOptions createRequestOptions = new TransactionalBatchItemRequestOptions()
            {
                Properties = new Dictionary<string, object>()
                {
                    { WFConstants.BackendHeaders.BinaryId, createStreamBinaryId },
                    { WFConstants.BackendHeaders.TimeToLiveInSeconds, createTtl.ToString() },
                },
                IndexingDirective = Microsoft.Azure.Cosmos.IndexingDirective.Exclude
            };

            string readId = Guid.NewGuid().ToString();
            byte[] readStreamBinaryId = new byte[20];
            random.NextBytes(readStreamBinaryId);
            TransactionalBatchItemRequestOptions readRequestOptions = new TransactionalBatchItemRequestOptions()
            {
                Properties = new Dictionary<string, object>()
                {
                    { WFConstants.BackendHeaders.BinaryId, readStreamBinaryId }
                },
                IfNoneMatchEtag = "readCondition"
            };

            TestItem replaceItem = new TestItem("repl");
            byte[] replaceStreamContent = new byte[20];
            random.NextBytes(replaceStreamContent);
            const string replaceStreamId = "replStream";
            byte[] replaceStreamBinaryId = new byte[20];
            random.NextBytes(replaceStreamBinaryId);
            TransactionalBatchItemRequestOptions replaceRequestOptions = new TransactionalBatchItemRequestOptions()
            {
                Properties = new Dictionary<string, object>()
                {
                    { WFConstants.BackendHeaders.BinaryId, replaceStreamBinaryId }
                },
                IfMatchEtag = "replCondition",
                IndexingDirective = Microsoft.Azure.Cosmos.IndexingDirective.Exclude
            };

            TestItem upsertItem = new TestItem("upsert");
            byte[] upsertStreamContent = new byte[20];
            random.NextBytes(upsertStreamContent);
            byte[] upsertStreamBinaryId = new byte[20];
            random.NextBytes(upsertStreamBinaryId);
            TransactionalBatchItemRequestOptions upsertRequestOptions = new TransactionalBatchItemRequestOptions()
            {
                Properties = new Dictionary<string, object>()
                {
                    { WFConstants.BackendHeaders.BinaryId, upsertStreamBinaryId }
                },
                IfMatchEtag = "upsertCondition",
                IndexingDirective = Microsoft.Azure.Cosmos.IndexingDirective.Exclude
            };

            string deleteId = Guid.NewGuid().ToString();
            byte[] deleteStreamBinaryId = new byte[20];
            random.NextBytes(deleteStreamBinaryId);
            TransactionalBatchItemRequestOptions deleteRequestOptions = new TransactionalBatchItemRequestOptions()
            {
                Properties = new Dictionary<string, object>()
                {
                    { WFConstants.BackendHeaders.BinaryId, deleteStreamBinaryId }
                },
                IfNoneMatchEtag = "delCondition"
            };

            CosmosJsonDotNetSerializer jsonSerializer = new CosmosJsonDotNetSerializer();
            BatchTestHandler testHandler = new BatchTestHandler((request, operations) =>
            {
                Assert.AreEqual(new Cosmos.PartitionKey(BatchUnitTests.PartitionKey1).ToString(), request.Headers.PartitionKey);
                Assert.AreEqual(bool.TrueString, request.Headers[HttpConstants.HttpHeaders.IsBatchAtomic]);
                Assert.AreEqual(bool.TrueString, request.Headers[HttpConstants.HttpHeaders.IsBatchOrdered]);
                Assert.IsFalse(request.Headers.TryGetValue(HttpConstants.HttpHeaders.ShouldBatchContinueOnError, out string unused));

                Assert.AreEqual(16, operations.Count);

                int operationIndex = 0;

                // run the loop twice, once for operations without item request options, and one for with item request options
                for (int loopCount = 0; loopCount < 2; loopCount++)
                {
                    bool hasItemRequestOptions = loopCount == 1;

                    ItemBatchOperation operation = operations[operationIndex++];
                    Assert.AreEqual(OperationType.Create, operation.OperationType);
                    Assert.IsNull(operation.Id);
                    Assert.AreEqual(createItem, BatchUnitTests.Deserialize(operation.ResourceBody, jsonSerializer));
                    BatchUnitTests.VerifyBatchItemRequestOptionsAreEqual(hasItemRequestOptions ? createRequestOptions : null, operation.RequestOptions);

                    operation = operations[operationIndex++];
                    Assert.AreEqual(OperationType.Read, operation.OperationType);
                    Assert.AreEqual(readId, operation.Id);
                    BatchUnitTests.VerifyBatchItemRequestOptionsAreEqual(hasItemRequestOptions ? readRequestOptions : null, operation.RequestOptions);

                    operation = operations[operationIndex++];
                    Assert.AreEqual(OperationType.Replace, operation.OperationType);
                    Assert.AreEqual(replaceItem.Id, operation.Id);
                    Assert.AreEqual(replaceItem, BatchUnitTests.Deserialize(operation.ResourceBody, jsonSerializer));
                    BatchUnitTests.VerifyBatchItemRequestOptionsAreEqual(hasItemRequestOptions ? replaceRequestOptions : null, operation.RequestOptions);

                    operation = operations[operationIndex++];
                    Assert.AreEqual(OperationType.Upsert, operation.OperationType);
                    Assert.IsNull(operation.Id);
                    Assert.AreEqual(upsertItem, BatchUnitTests.Deserialize(operation.ResourceBody, jsonSerializer));
                    BatchUnitTests.VerifyBatchItemRequestOptionsAreEqual(hasItemRequestOptions ? upsertRequestOptions : null, operation.RequestOptions);

                    operation = operations[operationIndex++];
                    Assert.AreEqual(OperationType.Delete, operation.OperationType);
                    Assert.AreEqual(deleteId, operation.Id);
                    BatchUnitTests.VerifyBatchItemRequestOptionsAreEqual(hasItemRequestOptions ? deleteRequestOptions : null, operation.RequestOptions);

                    operation = operations[operationIndex++];
                    Assert.AreEqual(OperationType.Create, operation.OperationType);
                    Assert.IsNull(operation.Id);
                    Assert.IsTrue(operation.ResourceBody.Span.SequenceEqual(createStreamContent));
                    BatchUnitTests.VerifyBatchItemRequestOptionsAreEqual(hasItemRequestOptions ? createRequestOptions : null, operation.RequestOptions);

                    operation = operations[operationIndex++];
                    Assert.AreEqual(OperationType.Replace, operation.OperationType);
                    Assert.AreEqual(replaceStreamId, operation.Id);
                    Assert.IsTrue(operation.ResourceBody.Span.SequenceEqual(replaceStreamContent));
                    BatchUnitTests.VerifyBatchItemRequestOptionsAreEqual(hasItemRequestOptions ? replaceRequestOptions : null, operation.RequestOptions);

                    operation = operations[operationIndex++];
                    Assert.AreEqual(OperationType.Upsert, operation.OperationType);
                    Assert.IsNull(operation.Id);
                    Assert.IsTrue(operation.ResourceBody.Span.SequenceEqual(upsertStreamContent));
                    BatchUnitTests.VerifyBatchItemRequestOptionsAreEqual(hasItemRequestOptions ? upsertRequestOptions : null, operation.RequestOptions);
                }

                return Task.FromResult(new ResponseMessage(HttpStatusCode.OK));
            });

            Container container = BatchUnitTests.GetContainer(testHandler);

            TransactionalBatchResponse batchResponse = await new BatchCore((ContainerInternal)container, new Cosmos.PartitionKey(BatchUnitTests.PartitionKey1))
                .CreateItem(createItem)
                .ReadItem(readId)
                .ReplaceItem(replaceItem.Id, replaceItem)
                .UpsertItem(upsertItem)
                .DeleteItem(deleteId)

                // stream
                .CreateItemStream(new MemoryStream(createStreamContent))
                .ReplaceItemStream(replaceStreamId, new MemoryStream(replaceStreamContent))
                .UpsertItemStream(new MemoryStream(upsertStreamContent))

                // regular with options
                .CreateItem(createItem, createRequestOptions)
                .ReadItem(readId, readRequestOptions)
                .ReplaceItem(replaceItem.Id, replaceItem, replaceRequestOptions)
                .UpsertItem(upsertItem, upsertRequestOptions)
                .DeleteItem(deleteId, deleteRequestOptions)

                // stream with options
                .CreateItemStream(new MemoryStream(createStreamContent), createRequestOptions)
                .ReplaceItemStream(replaceStreamId, new MemoryStream(replaceStreamContent), replaceRequestOptions)
                .UpsertItemStream(new MemoryStream(upsertStreamContent), upsertRequestOptions)
                .ExecuteAsync();
        }

        [TestMethod]
        [Owner("abpai")]
        public async Task BatchSingleServerResponseAsync()
        {
            List<TransactionalBatchOperationResult> expectedResults = new List<TransactionalBatchOperationResult>();
            CosmosJsonDotNetSerializer jsonSerializer = new CosmosJsonDotNetSerializer();
            TestItem testItem = new TestItem("tst");

            Stream itemStream = jsonSerializer.ToStream<TestItem>(testItem);
            MemoryStream resourceStream = itemStream as MemoryStream;
            if (resourceStream == null)
            {
                await itemStream.CopyToAsync(resourceStream);
                resourceStream.Position = 0;
            }

            expectedResults.Add(
                new TransactionalBatchOperationResult(HttpStatusCode.OK)
                {
                    ETag = "theETag",
                    SubStatusCode = (SubStatusCodes)1100,
                    ResourceStream = resourceStream
                });
            expectedResults.Add(new TransactionalBatchOperationResult(HttpStatusCode.Conflict));

            double requestCharge = 3.6;

            TestHandler testHandler = new TestHandler(async (request, cancellationToken) =>
            {
                ResponseMessage responseMessage = new ResponseMessage(HttpStatusCode.OK, requestMessage: null, errorMessage: null)
                {
                    Content = await new BatchResponsePayloadWriter(expectedResults).GeneratePayloadAsync()
                };

                responseMessage.Headers.RequestCharge = requestCharge;
                return responseMessage;
            });

            Container container = BatchUnitTests.GetContainer(testHandler);

            TransactionalBatchResponse batchResponse = await new BatchCore((ContainerInternal)container, new Cosmos.PartitionKey(BatchUnitTests.PartitionKey1))
                .ReadItem("id1")
                .ReadItem("id2")
                .ExecuteAsync();

            Assert.AreEqual(HttpStatusCode.OK, batchResponse.StatusCode);
            Assert.AreEqual(requestCharge, batchResponse.RequestCharge);

            TransactionalBatchOperationResult<TestItem> result0 = batchResponse.GetOperationResultAtIndex<TestItem>(0);
            Assert.AreEqual(expectedResults[0].StatusCode, result0.StatusCode);
            Assert.AreEqual(expectedResults[0].SubStatusCode, result0.SubStatusCode);
            Assert.AreEqual(expectedResults[0].ETag, result0.ETag);
            Assert.AreEqual(testItem, result0.Resource);

            Assert.AreEqual(expectedResults[1].StatusCode, batchResponse[1].StatusCode);
            Assert.AreEqual(SubStatusCodes.Unknown, batchResponse[1].SubStatusCode);
            Assert.IsNull(batchResponse[1].ETag);
            Assert.IsNull(batchResponse[1].ResourceStream);
        }

        [TestMethod]
        [Owner("abpai")]
        public async Task BatchJsonServerResponseAsync()
        {
            TestHandler testHandler = new TestHandler((request, cancellationToken) =>
            {
                HttpStatusCode serverStatusCode = HttpStatusCode.Gone;
                Stream serverContent = new MemoryStream(Encoding.UTF8.GetBytes("{ \"random\": 1 }"));

                ResponseMessage responseMessage = new ResponseMessage(serverStatusCode, requestMessage: null, errorMessage: null)
                {
                    Content = serverContent
                };

                return Task.FromResult(responseMessage);
            });

            Container container = BatchUnitTests.GetContainer(testHandler);

            TransactionalBatchResponse batchResponse = await new BatchCore((ContainerInternal)container, new Cosmos.PartitionKey(BatchUnitTests.PartitionKey1))
                .ReadItem("id1")
                .ReadItem("id2")
                .ExecuteAsync();

            Assert.AreEqual(HttpStatusCode.Gone, batchResponse.StatusCode);
        }

        /// <summary>
        /// Test to make sure IsFeedRequest is true for Batch operation
        /// </summary>
        [TestMethod]
        public void BatchIsFeedRequest()
        {
            Assert.IsTrue(GatewayStoreClient.IsFeedRequest(OperationType.Batch));
        }

        /// <summary>
        /// Test to make sure IsWriteOperation is true for batch operation
        /// </summary>
        [TestMethod]
        public void BatchIsWriteOperation()
        {
            Assert.IsTrue(OperationType.Batch.IsWriteOperation());
        }

        [TestMethod]
        public void FromItemRequestOptions_FromNull()
        {
            Assert.IsNull(TransactionalBatchItemRequestOptions.FromItemRequestOptions(null));
        }

        [TestMethod]
        public void FromItemRequestOptions_WithCustomValues()
        {
            ItemRequestOptions itemRequestOptions = new ItemRequestOptions
            {
                IfMatchEtag = Guid.NewGuid().ToString(),
                IfNoneMatchEtag = Guid.NewGuid().ToString(),
                IndexingDirective = Cosmos.IndexingDirective.Exclude,
                Properties = new Dictionary<string, object>() { { "test", "test" } }
            };

            TransactionalBatchItemRequestOptions batchItemRequestOptions = TransactionalBatchItemRequestOptions.FromItemRequestOptions(itemRequestOptions);
            Assert.AreEqual(itemRequestOptions.IfMatchEtag, batchItemRequestOptions.IfMatchEtag);
            Assert.AreEqual(itemRequestOptions.IfNoneMatchEtag, batchItemRequestOptions.IfNoneMatchEtag);
            Assert.AreEqual(itemRequestOptions.IndexingDirective, batchItemRequestOptions.IndexingDirective);
            Assert.AreEqual(itemRequestOptions.Properties, batchItemRequestOptions.Properties);
        }

        [TestMethod]
        public void FromItemRequestOptions_WithDefaultValues()
        {
            ItemRequestOptions itemRequestOptions = new ItemRequestOptions();
            TransactionalBatchItemRequestOptions batchItemRequestOptions = TransactionalBatchItemRequestOptions.FromItemRequestOptions(itemRequestOptions);
            Assert.AreEqual(itemRequestOptions.IfMatchEtag, batchItemRequestOptions.IfMatchEtag);
            Assert.AreEqual(itemRequestOptions.IfNoneMatchEtag, batchItemRequestOptions.IfNoneMatchEtag);
            Assert.AreEqual(itemRequestOptions.IndexingDirective, batchItemRequestOptions.IndexingDirective);
            Assert.AreEqual(itemRequestOptions.Properties, batchItemRequestOptions.Properties);
        }

        private static void VerifyBatchItemRequestOptionsAreEqual(TransactionalBatchItemRequestOptions expected, TransactionalBatchItemRequestOptions actual)
        {
            if (expected != null)
            {
                Assert.AreEqual(expected.IfMatchEtag, actual.IfMatchEtag);
                Assert.AreEqual(expected.IfNoneMatchEtag, actual.IfNoneMatchEtag);

                if (expected.IndexingDirective.HasValue)
                {
                    Assert.AreEqual(expected.IndexingDirective.Value, actual.IndexingDirective.Value);
                }
                else
                {
                    Assert.IsTrue(!actual.IndexingDirective.HasValue);
                }

                if (expected.Properties != null)
                {
                    Assert.IsNotNull(actual.Properties);
                    if (expected.Properties.TryGetValue(WFConstants.BackendHeaders.BinaryId, out object expectedBinaryIdObj))
                    {
                        byte[] expectedBinaryId = expectedBinaryIdObj as byte[];
                        Assert.IsTrue(actual.Properties.TryGetValue(WFConstants.BackendHeaders.BinaryId, out object actualBinaryIdObj));
                        byte[] actualBinaryId = actualBinaryIdObj as byte[];
                        CollectionAssert.AreEqual(expectedBinaryId, actualBinaryId);
                    }

                    if (expected.Properties.TryGetValue(WFConstants.BackendHeaders.EffectivePartitionKey, out object expectedEpkObj))
                    {
                        byte[] expectedEpk = expectedEpkObj as byte[];
                        Assert.IsTrue(actual.Properties.TryGetValue(WFConstants.BackendHeaders.EffectivePartitionKey, out object actualEpkObj));
                        byte[] actualEpk = actualEpkObj as byte[];
                        CollectionAssert.AreEqual(expectedEpk, actualEpk);
                    }

                    if (expected.Properties.TryGetValue(WFConstants.BackendHeaders.TimeToLiveInSeconds, out object expectedTtlObj))
                    {
                        string expectedTtlStr = expectedTtlObj as string;
                        Assert.IsTrue(actual.Properties.TryGetValue(WFConstants.BackendHeaders.TimeToLiveInSeconds, out object actualTtlObj));
                        Assert.AreEqual(expectedTtlStr, actualTtlObj as string);
                    }
                }
            }
            else
            {
                Assert.IsNull(actual);
            }
        }

        private static async Task VerifyExceptionThrownOnExecuteAsync(
            TransactionalBatch batch,
            Type expectedTypeOfException,
            string expectedExceptionMessage = null,
            TransactionalBatchRequestOptions requestOptions = null)
        {
            bool wasExceptionThrown = false;
            try
            {
                if (requestOptions != null)
                {
                    await batch.ExecuteAsync(requestOptions);
                }
                else
                {
                    await batch.ExecuteAsync();
                }
            }
            catch (Exception ex)
            {
                Assert.AreEqual(expectedTypeOfException, ex.GetType());
                if (expectedExceptionMessage != null)
                {
                    Assert.IsTrue(ex.Message.Contains(expectedExceptionMessage));
                }
                wasExceptionThrown = true;
            }

            if (!wasExceptionThrown)
            {
                Assert.Fail("Exception was expected to be thrown but was not.");
            }
        }

        private static Container GetContainer(TestHandler testHandler = null)
        {
            CosmosClient client = testHandler != null
                ? MockCosmosUtil.CreateMockCosmosClient((builder) => builder.AddCustomHandlers(testHandler))
                : MockCosmosUtil.CreateMockCosmosClient();
            DatabaseInternal database = new DatabaseInlineCore(client.ClientContext, BatchUnitTests.DatabaseId);
            ContainerInternal container = new ContainerInlineCore(client.ClientContext, database, BatchUnitTests.ContainerId);
            return container;
        }

        private static TestItem Deserialize(Memory<byte> body, CosmosSerializer serializer)
        {
            return serializer.FromStream<TestItem>(new MemoryStream(body.Span.ToArray()));
        }

        private class BatchTestHandler : TestHandler
        {
            private readonly Func<RequestMessage, List<ItemBatchOperation>, Task<ResponseMessage>> func;

            public BatchTestHandler(Func<RequestMessage, List<ItemBatchOperation>, Task<ResponseMessage>> func)
            {
                this.func = func;
            }

            public List<Tuple<RequestMessage, List<ItemBatchOperation>>> Received { get; } = new List<Tuple<RequestMessage, List<ItemBatchOperation>>>();

            public override async Task<ResponseMessage> SendAsync(
                RequestMessage request, CancellationToken cancellationToken)
            {
                BatchTestHandler.VerifyServerRequestProperties(request);
                List<ItemBatchOperation> operations = await new BatchRequestPayloadReader().ReadPayloadAsync(request.Content);

                this.Received.Add(new Tuple<RequestMessage, List<ItemBatchOperation>>(request, operations));
                return await this.func(request, operations);
            }

            private static void VerifyServerRequestProperties(RequestMessage request)
            {
                Assert.AreEqual(OperationType.Batch, request.OperationType);
                Assert.AreEqual(ResourceType.Document, request.ResourceType);
                Assert.AreEqual(HttpConstants.HttpMethods.Post, request.Method.ToString());

                Uri expectedRequestUri = new Uri(
                    string.Format(
                        "dbs/{0}/colls/{1}",
                        BatchUnitTests.DatabaseId,
                        BatchUnitTests.ContainerId),
                    UriKind.Relative);
                Assert.AreEqual(expectedRequestUri, request.RequestUri);
            }
        }

        private class TestItem
        {
            [JsonProperty("id")]
            public string Id { get; set; }

            public string Attr { get; set; }

            public TestItem(string attr)
            {
                this.Id = Guid.NewGuid().ToString();
                this.Attr = attr;
            }

            public override bool Equals(object obj)
            {
                if (!(obj is TestItem other))
                {
                    return false;
                }

                return this.Id == other.Id && this.Attr == other.Attr;
            }

            public override int GetHashCode()
            {
                int hashCode = -2138196334;
                hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.Id);
                hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.Attr);
                return hashCode;
            }
        }
    }
}