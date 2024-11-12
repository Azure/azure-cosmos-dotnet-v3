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
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class BatchAsyncBatcherTests
    {
        private static readonly Exception expectedException = new Exception();
        private static readonly BatchPartitionMetric metric = new BatchPartitionMetric();

        private ItemBatchOperation CreateItemBatchOperation(bool withContext = false)
        {
            ItemBatchOperation operation = new ItemBatchOperation(
                operationType: OperationType.Create,
                operationIndex: 0,
                partitionKey: Cosmos.PartitionKey.Null,
                id: string.Empty,
                resourceStream: new MemoryStream(new byte[] { 0x41, 0x42 }, index: 0, count: 2, writable: false, publiclyVisible: true));
            if (withContext)
            {
                operation.AttachContext(new ItemBatchOperationContext(string.Empty, NoOpTrace.Singleton));
            }

            return operation;
        }

        private readonly BatchAsyncBatcherExecuteDelegate Executor
            = async (PartitionKeyRangeServerBatchRequest request, ITrace trace, CancellationToken cancellationToken) =>
            {
                List<TransactionalBatchOperationResult> results = new List<TransactionalBatchOperationResult>();
                ItemBatchOperation[] arrayOperations = new ItemBatchOperation[request.Operations.Count];
                int index = 0;
                foreach (ItemBatchOperation operation in request.Operations)
                {
                    results.Add(
                    new TransactionalBatchOperationResult(HttpStatusCode.OK)
                    {
                        ResourceStream = new MemoryStream(new byte[] { 0x41, 0x42 }, index: 0, count: 2, writable: false, publiclyVisible: true),
                        ETag = operation.Id
                    });

                    arrayOperations[index++] = operation;
                }

                MemoryStream responseContent = await new BatchResponsePayloadWriter(results).GeneratePayloadAsync();

                SinglePartitionKeyServerBatchRequest batchRequest = await SinglePartitionKeyServerBatchRequest.CreateAsync(
                    partitionKey: null,
                    operations: new ArraySegment<ItemBatchOperation>(arrayOperations),
                    serializerCore: MockCosmosUtil.Serializer,
                    trace: trace,
                    cancellationToken: cancellationToken);

                TransactionalBatchResponse batchresponse = await TransactionalBatchResponse.FromResponseMessageAsync(
                    new ResponseMessage(HttpStatusCode.OK)
                    {
                        Content = responseContent
                    },
                    batchRequest,
                    MockCosmosUtil.Serializer,
                    true,
                    trace,
                    CancellationToken.None);

                return new PartitionKeyRangeBatchExecutionResult(request.PartitionKeyRangeId, request.Operations, batchresponse);
            };

        private readonly BatchAsyncBatcherExecuteDelegate ExecutorWithSplit
            = async (PartitionKeyRangeServerBatchRequest request, ITrace trace, CancellationToken cancellationToken) =>
            {
                List<TransactionalBatchOperationResult> results = new List<TransactionalBatchOperationResult>();
                ItemBatchOperation[] arrayOperations = new ItemBatchOperation[request.Operations.Count];
                int index = 0;
                foreach (ItemBatchOperation operation in request.Operations)
                {
                    results.Add(
                    new TransactionalBatchOperationResult(HttpStatusCode.Gone)
                    {
                        ETag = operation.Id,
                        SubStatusCode = SubStatusCodes.PartitionKeyRangeGone
                    });

                    arrayOperations[index++] = operation;
                }

                MemoryStream responseContent = await new BatchResponsePayloadWriter(results).GeneratePayloadAsync();

                SinglePartitionKeyServerBatchRequest batchRequest = await SinglePartitionKeyServerBatchRequest.CreateAsync(
                    partitionKey: null,
                    operations: new ArraySegment<ItemBatchOperation>(arrayOperations),
                    serializerCore: MockCosmosUtil.Serializer,
                    trace: trace,
                    cancellationToken: cancellationToken);

                ResponseMessage responseMessage = new ResponseMessage(HttpStatusCode.Gone)
                {
                    Content = responseContent
                };
                responseMessage.Headers.SubStatusCode = SubStatusCodes.PartitionKeyRangeGone;

                TransactionalBatchResponse batchresponse = await TransactionalBatchResponse.FromResponseMessageAsync(
                    responseMessage,
                    batchRequest,
                    MockCosmosUtil.Serializer,
                    true,
                    trace,
                    CancellationToken.None);

                return new PartitionKeyRangeBatchExecutionResult(request.PartitionKeyRangeId, request.Operations, batchresponse);
            };

        private readonly BatchAsyncBatcherExecuteDelegate ExecutorWithCompletingSplit
            = async (PartitionKeyRangeServerBatchRequest request, ITrace trace, CancellationToken cancellationToken) =>
            {
                List<TransactionalBatchOperationResult> results = new List<TransactionalBatchOperationResult>();
                ItemBatchOperation[] arrayOperations = new ItemBatchOperation[request.Operations.Count];
                int index = 0;
                foreach (ItemBatchOperation operation in request.Operations)
                {
                    results.Add(
                    new TransactionalBatchOperationResult(HttpStatusCode.Gone)
                    {
                        ETag = operation.Id,
                        SubStatusCode = SubStatusCodes.CompletingSplit
                    });

                    arrayOperations[index++] = operation;
                }

                MemoryStream responseContent = await new BatchResponsePayloadWriter(results).GeneratePayloadAsync();

                SinglePartitionKeyServerBatchRequest batchRequest = await SinglePartitionKeyServerBatchRequest.CreateAsync(
                    partitionKey: null,
                    operations: new ArraySegment<ItemBatchOperation>(arrayOperations),
                    serializerCore: MockCosmosUtil.Serializer,
                    trace: trace,
                    cancellationToken: cancellationToken);

                ResponseMessage responseMessage = new ResponseMessage(HttpStatusCode.Gone)
                {
                    Content = responseContent
                };
                responseMessage.Headers.SubStatusCode = SubStatusCodes.PartitionKeyRangeGone;

                TransactionalBatchResponse batchresponse = await TransactionalBatchResponse.FromResponseMessageAsync(
                    responseMessage,
                    batchRequest,
                    MockCosmosUtil.Serializer,
                    true,
                    trace,
                    CancellationToken.None);

                return new PartitionKeyRangeBatchExecutionResult(request.PartitionKeyRangeId, request.Operations, batchresponse);
            };

        private readonly BatchAsyncBatcherExecuteDelegate ExecutorWithCompletingPartitionMigration
            = async (PartitionKeyRangeServerBatchRequest request, ITrace trace, CancellationToken cancellationToken) =>
            {
                List<TransactionalBatchOperationResult> results = new List<TransactionalBatchOperationResult>();
                ItemBatchOperation[] arrayOperations = new ItemBatchOperation[request.Operations.Count];
                int index = 0;
                foreach (ItemBatchOperation operation in request.Operations)
                {
                    results.Add(
                    new TransactionalBatchOperationResult(HttpStatusCode.Gone)
                    {
                        ETag = operation.Id,
                        SubStatusCode = SubStatusCodes.CompletingSplit
                    });

                    arrayOperations[index++] = operation;
                }

                MemoryStream responseContent = await new BatchResponsePayloadWriter(results).GeneratePayloadAsync();

                SinglePartitionKeyServerBatchRequest batchRequest = await SinglePartitionKeyServerBatchRequest.CreateAsync(
                    partitionKey: null,
                    operations: new ArraySegment<ItemBatchOperation>(arrayOperations),
                    serializerCore: MockCosmosUtil.Serializer,
                    trace: trace,
                    cancellationToken: cancellationToken);

                ResponseMessage responseMessage = new ResponseMessage(HttpStatusCode.Gone)
                {
                    Content = responseContent
                };
                responseMessage.Headers.SubStatusCode = SubStatusCodes.CompletingPartitionMigration;

                TransactionalBatchResponse batchresponse = await TransactionalBatchResponse.FromResponseMessageAsync(
                    responseMessage,
                    batchRequest,
                    MockCosmosUtil.Serializer,
                    true,
                    trace,
                    CancellationToken.None);

                return new PartitionKeyRangeBatchExecutionResult(request.PartitionKeyRangeId, request.Operations, batchresponse);
            };

        private readonly BatchAsyncBatcherExecuteDelegate ExecutorWith413
            = async (PartitionKeyRangeServerBatchRequest request, ITrace trace, CancellationToken cancellationToken) =>
            {
                List<TransactionalBatchOperationResult> results = new List<TransactionalBatchOperationResult>();
                ItemBatchOperation[] arrayOperations = new ItemBatchOperation[request.Operations.Count];
                int index = 0;
                foreach (ItemBatchOperation operation in request.Operations)
                {
                    if (index == 0)
                    {
                        // First operation is fine
                        results.Add(
                            new TransactionalBatchOperationResult(HttpStatusCode.OK)
                            {
                                ETag = operation.Id
                            });
                    }
                    else
                    {
                        // second operation is too big
                        results.Add(
                            new TransactionalBatchOperationResult(HttpStatusCode.RequestEntityTooLarge)
                            {
                                ETag = operation.Id
                            });
                    }

                    arrayOperations[index++] = operation;
                }

                MemoryStream responseContent = await new BatchResponsePayloadWriter(results).GeneratePayloadAsync();

                SinglePartitionKeyServerBatchRequest batchRequest = await SinglePartitionKeyServerBatchRequest.CreateAsync(
                    partitionKey: null,
                    operations: new ArraySegment<ItemBatchOperation>(arrayOperations),
                    serializerCore: MockCosmosUtil.Serializer,
                    trace: trace,
                    cancellationToken: cancellationToken);

                ResponseMessage responseMessage = new ResponseMessage((HttpStatusCode)207)
                {
                    Content = responseContent
                };

                TransactionalBatchResponse batchresponse = await TransactionalBatchResponse.FromResponseMessageAsync(
                    responseMessage,
                    batchRequest,
                    MockCosmosUtil.Serializer,
                    true,
                    trace: trace,
                    CancellationToken.None);

                return new PartitionKeyRangeBatchExecutionResult(request.PartitionKeyRangeId, request.Operations, batchresponse);
            };

        private readonly BatchAsyncBatcherExecuteDelegate ExecutorWith413_3402
            = async (PartitionKeyRangeServerBatchRequest request, ITrace trace, CancellationToken cancellationToken) =>
            {
                List<TransactionalBatchOperationResult> results = new List<TransactionalBatchOperationResult>();
                ItemBatchOperation[] arrayOperations = new ItemBatchOperation[request.Operations.Count];
                int index = 0;
                foreach (ItemBatchOperation operation in request.Operations)
                {
                    if (index == 0)
                    {
                        // First operation is fine
                        results.Add(
                    new TransactionalBatchOperationResult(HttpStatusCode.OK)
                    {
                        ETag = operation.Id
                    });
                    }
                    else
                    {
                        // second operation is too big
                        results.Add(
                    new TransactionalBatchOperationResult(HttpStatusCode.RequestEntityTooLarge)
                    {
                        SubStatusCode = (SubStatusCodes)3402,
                        ETag = operation.Id
                    });
                    }

                    arrayOperations[index++] = operation;
                }

                MemoryStream responseContent = await new BatchResponsePayloadWriter(results).GeneratePayloadAsync();

                SinglePartitionKeyServerBatchRequest batchRequest = await SinglePartitionKeyServerBatchRequest.CreateAsync(
                    partitionKey: null,
                    operations: new ArraySegment<ItemBatchOperation>(arrayOperations),
                    serializerCore: MockCosmosUtil.Serializer,
                    trace: trace,
                    cancellationToken: cancellationToken);

                ResponseMessage responseMessage = new ResponseMessage((HttpStatusCode)207)
                {
                    Content = responseContent
                };

                TransactionalBatchResponse batchresponse = await TransactionalBatchResponse.FromResponseMessageAsync(
                    responseMessage,
                    batchRequest,
                    MockCosmosUtil.Serializer,
                    true,
                    trace: trace,
                    CancellationToken.None);

                return new PartitionKeyRangeBatchExecutionResult(request.PartitionKeyRangeId, request.Operations, batchresponse);
            };

        // The response will include all but 2 operation responses
        private readonly BatchAsyncBatcherExecuteDelegate ExecutorWithLessResponses
            = async (PartitionKeyRangeServerBatchRequest request, ITrace trace, CancellationToken cancellationToken) =>
            {
                int operationCount = request.Operations.Count - 2;
                List<TransactionalBatchOperationResult> results = new List<TransactionalBatchOperationResult>();
                ItemBatchOperation[] arrayOperations = new ItemBatchOperation[operationCount];
                int index = 0;
                foreach (ItemBatchOperation operation in request.Operations.Skip(1).Take(operationCount))
                {
                    results.Add(
                    new TransactionalBatchOperationResult(HttpStatusCode.OK)
                    {
                        ResourceStream = new MemoryStream(new byte[] { 0x41, 0x42 }, index: 0, count: 2, writable: false, publiclyVisible: true),
                        ETag = operation.Id
                    });

                    arrayOperations[index++] = operation;
                }

                MemoryStream responseContent = await new BatchResponsePayloadWriter(results).GeneratePayloadAsync();

                SinglePartitionKeyServerBatchRequest batchRequest = await SinglePartitionKeyServerBatchRequest.CreateAsync(
                    partitionKey: null,
                    operations: new ArraySegment<ItemBatchOperation>(arrayOperations),
                    serializerCore: MockCosmosUtil.Serializer,
                    trace: trace,
                    cancellationToken: cancellationToken);

                TransactionalBatchResponse batchresponse = await TransactionalBatchResponse.FromResponseMessageAsync(
                    new ResponseMessage(HttpStatusCode.OK) { Content = responseContent },
                    batchRequest,
                    MockCosmosUtil.Serializer,
                    true,
                    trace,
                    CancellationToken.None);

                return new PartitionKeyRangeBatchExecutionResult(request.PartitionKeyRangeId, request.Operations, batchresponse);
            };

        private readonly BatchAsyncBatcherExecuteDelegate ExecutorWithFailure
            = (PartitionKeyRangeServerBatchRequest request, ITrace trace, CancellationToken cancellationToken) => throw expectedException;

        private readonly BatchAsyncBatcherRetryDelegate Retrier = (ItemBatchOperation operation, CancellationToken cancellation) => Task.CompletedTask;

        [DataTestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        [DataRow(0)]
        [DataRow(-1)]
        public void ValidatesSize(int size)
        {
            _ = new BatchAsyncBatcher(size, 1, MockCosmosUtil.Serializer, this.Executor, this.Retrier, BatchAsyncBatcherTests.MockClientContext());
        }

        [DataTestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        [DataRow(0)]
        [DataRow(-1)]
        public void ValidatesByteSize(int size)
        {
            _ = new BatchAsyncBatcher(1, size, MockCosmosUtil.Serializer, this.Executor, this.Retrier, BatchAsyncBatcherTests.MockClientContext());
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ValidatesExecutor()
        {
            _ = new BatchAsyncBatcher(1, 1, MockCosmosUtil.Serializer, null, this.Retrier, BatchAsyncBatcherTests.MockClientContext());
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ValidatesRetrier()
        {
            _ = new BatchAsyncBatcher(1, 1, MockCosmosUtil.Serializer, this.Executor, null, BatchAsyncBatcherTests.MockClientContext());
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ValidatesSerializer()
        {
            _ = new BatchAsyncBatcher(1, 1, null, this.Executor, this.Retrier, BatchAsyncBatcherTests.MockClientContext());
        }

        [TestMethod]
        public void HasFixedSize()
        {
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(2, 1000, MockCosmosUtil.Serializer, this.Executor, this.Retrier, BatchAsyncBatcherTests.MockClientContext());
            Assert.IsTrue(batchAsyncBatcher.TryAdd(this.CreateItemBatchOperation(true)));
            Assert.IsTrue(batchAsyncBatcher.TryAdd(this.CreateItemBatchOperation(true)));
            Assert.IsFalse(batchAsyncBatcher.TryAdd(this.CreateItemBatchOperation(true)));
        }

        [TestMethod]
        public async Task HasFixedByteSize()
        {
            ItemBatchOperation itemBatchOperation = this.CreateItemBatchOperation(true);
            await itemBatchOperation.MaterializeResourceAsync(MockCosmosUtil.Serializer, default);
            // Each operation is 2 bytes
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(3, 4, MockCosmosUtil.Serializer, this.Executor, this.Retrier, BatchAsyncBatcherTests.MockClientContext());
            Assert.IsTrue(batchAsyncBatcher.TryAdd(itemBatchOperation));
            Assert.IsTrue(batchAsyncBatcher.TryAdd(itemBatchOperation));
            Assert.IsFalse(batchAsyncBatcher.TryAdd(itemBatchOperation));
        }

        [TestMethod]
        public async Task ExceptionsFailOperationsAsync()
        {
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(2, 1000, MockCosmosUtil.Serializer, this.ExecutorWithFailure, this.Retrier, BatchAsyncBatcherTests.MockClientContext());
            ItemBatchOperation operation1 = this.CreateItemBatchOperation();
            ItemBatchOperation operation2 = this.CreateItemBatchOperation();
            ItemBatchOperationContext context1 = new ItemBatchOperationContext(string.Empty, NoOpTrace.Singleton);
            operation1.AttachContext(context1);
            ItemBatchOperationContext context2 = new ItemBatchOperationContext(string.Empty, NoOpTrace.Singleton);
            operation2.AttachContext(context2);
            batchAsyncBatcher.TryAdd(operation1);
            batchAsyncBatcher.TryAdd(operation2);
            await batchAsyncBatcher.DispatchAsync(metric);

            Assert.AreEqual(TaskStatus.Faulted, context1.OperationTask.Status);
            Assert.AreEqual(TaskStatus.Faulted, context2.OperationTask.Status);
            Assert.AreEqual(expectedException, context1.OperationTask.Exception.InnerException);
            Assert.AreEqual(expectedException, context2.OperationTask.Exception.InnerException);
        }

        [TestMethod]
        public async Task DispatchProcessInOrderAsync()
        {
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(10, 1000, MockCosmosUtil.Serializer, this.Executor, this.Retrier, BatchAsyncBatcherTests.MockClientContext());
            List<ItemBatchOperation> operations = new List<ItemBatchOperation>(10);
            for (int i = 0; i < 10; i++)
            {
                ItemBatchOperation operation = new ItemBatchOperation(
                    operationType: OperationType.Create,
                    operationIndex: i,
                    partitionKey: new Cosmos.PartitionKey(i.ToString()),
                    id: i.ToString());

                ItemBatchOperationContext context = new ItemBatchOperationContext(string.Empty, NoOpTrace.Singleton);
                operation.AttachContext(context);
                operations.Add(operation);
                Assert.IsTrue(batchAsyncBatcher.TryAdd(operation));
            }

            await batchAsyncBatcher.DispatchAsync(metric);

            for (int i = 0; i < 10; i++)
            {
                ItemBatchOperation operation = operations[i];
                Assert.AreEqual(TaskStatus.RanToCompletion, operation.Context.OperationTask.Status);
                TransactionalBatchOperationResult result = await operation.Context.OperationTask;
                Assert.AreEqual(i.ToString(), result.ETag);
            }
        }

        [TestMethod]
        public async Task DispatchWithLessResponses()
        {
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(10, 1000, MockCosmosUtil.Serializer, this.ExecutorWithLessResponses, this.Retrier, BatchAsyncBatcherTests.MockClientContext());
            BatchAsyncBatcher secondAsyncBatcher = new BatchAsyncBatcher(10, 1000, MockCosmosUtil.Serializer, this.Executor, this.Retrier, BatchAsyncBatcherTests.MockClientContext());
            List<ItemBatchOperation> operations = new List<ItemBatchOperation>(10);
            for (int i = 0; i < 10; i++)
            {
                ItemBatchOperation operation = new ItemBatchOperation(OperationType.Create, i, Cosmos.PartitionKey.Null, i.ToString());
                ItemBatchOperationContext context = new ItemBatchOperationContext(string.Empty, NoOpTrace.Singleton);
                operation.AttachContext(context);
                operations.Add(operation);
                Assert.IsTrue(batchAsyncBatcher.TryAdd(operation));
            }

            await batchAsyncBatcher.DispatchAsync(metric);

            // Responses 1 and 10 should be missing
            for (int i = 0; i < 10; i++)
            {
                ItemBatchOperation operation = operations[i];
                // Some tasks should not be resolved
                if (i == 0 || i == 9)
                {
                    Assert.IsTrue(operation.Context.OperationTask.Status == TaskStatus.WaitingForActivation);
                }
                else
                {
                    Assert.IsTrue(operation.Context.OperationTask.Status == TaskStatus.RanToCompletion);
                }
                if (operation.Context.OperationTask.Status == TaskStatus.RanToCompletion)
                {
                    TransactionalBatchOperationResult result = await operation.Context.OperationTask;
                    Assert.AreEqual(i.ToString(), result.ETag);
                }
                else
                {
                    // Pass the pending one to another batcher
                    Assert.IsTrue(secondAsyncBatcher.TryAdd(operation));
                }
            }

            await secondAsyncBatcher.DispatchAsync(metric);

            // All tasks should be completed
            for (int i = 0; i < 10; i++)
            {
                ItemBatchOperation operation = operations[i];
                Assert.AreEqual(TaskStatus.RanToCompletion, operation.Context.OperationTask.Status);
                TransactionalBatchOperationResult result = await operation.Context.OperationTask;
                Assert.AreEqual(i.ToString(), result.ETag);
            }
        }

        [TestMethod]
        public void IsEmptyWithNoOperations()
        {
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(10, 1000, MockCosmosUtil.Serializer, this.Executor, this.Retrier, BatchAsyncBatcherTests.MockClientContext());
            Assert.IsTrue(batchAsyncBatcher.IsEmpty);
        }

        [TestMethod]
        public void IsNotEmptyWithOperations()
        {
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(1, 1000, MockCosmosUtil.Serializer, this.Executor, this.Retrier, BatchAsyncBatcherTests.MockClientContext());
            Assert.IsTrue(batchAsyncBatcher.TryAdd(this.CreateItemBatchOperation(true)));
            Assert.IsFalse(batchAsyncBatcher.IsEmpty);
        }

        [TestMethod]
        public async Task CannotAddToDispatchedBatch()
        {
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(1, 1000, MockCosmosUtil.Serializer, this.Executor, this.Retrier, BatchAsyncBatcherTests.MockClientContext());
            ItemBatchOperation operation = this.CreateItemBatchOperation();
            operation.AttachContext(new ItemBatchOperationContext(string.Empty, NoOpTrace.Singleton));
            Assert.IsTrue(batchAsyncBatcher.TryAdd(operation));
            await batchAsyncBatcher.DispatchAsync(metric);
            Assert.IsFalse(batchAsyncBatcher.TryAdd(this.CreateItemBatchOperation()));
        }

        [TestMethod]
        public async Task RetrierGetsCalledOnSplit()
        {
            IDocumentClientRetryPolicy retryPolicy1 = new BulkExecutionRetryPolicy(
                GetSplitEnabledContainer(),
                OperationType.Read,
                new ResourceThrottleRetryPolicy(1));

            IDocumentClientRetryPolicy retryPolicy2 = new BulkExecutionRetryPolicy(
                GetSplitEnabledContainer(),
                OperationType.Read,
                new ResourceThrottleRetryPolicy(1));

            ItemBatchOperation operation1 = this.CreateItemBatchOperation();
            ItemBatchOperation operation2 = this.CreateItemBatchOperation();
            operation1.AttachContext(new ItemBatchOperationContext(string.Empty, NoOpTrace.Singleton, retryPolicy1));
            operation2.AttachContext(new ItemBatchOperationContext(string.Empty, NoOpTrace.Singleton, retryPolicy2));

            Mock<BatchAsyncBatcherRetryDelegate> retryDelegate = new Mock<BatchAsyncBatcherRetryDelegate>();

            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(2, 1000, MockCosmosUtil.Serializer, this.ExecutorWithSplit, retryDelegate.Object, BatchAsyncBatcherTests.MockClientContext());
            Assert.IsTrue(batchAsyncBatcher.TryAdd(operation1));
            Assert.IsTrue(batchAsyncBatcher.TryAdd(operation2));
            await batchAsyncBatcher.DispatchAsync(metric);
            retryDelegate.Verify(a => a(It.Is<ItemBatchOperation>(o => o == operation1), It.IsAny<CancellationToken>()), Times.Once);
            retryDelegate.Verify(a => a(It.Is<ItemBatchOperation>(o => o == operation2), It.IsAny<CancellationToken>()), Times.Once);
            retryDelegate.Verify(a => a(It.IsAny<ItemBatchOperation>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [TestMethod]
        public async Task RetrierGetsCalledOnCompletingSplit()
        {
            IDocumentClientRetryPolicy retryPolicy1 = new BulkExecutionRetryPolicy(
                GetSplitEnabledContainer(),
                OperationType.Read,
                new ResourceThrottleRetryPolicy(1));

            IDocumentClientRetryPolicy retryPolicy2 = new BulkExecutionRetryPolicy(
                GetSplitEnabledContainer(),
                OperationType.Read,
                new ResourceThrottleRetryPolicy(1));

            ItemBatchOperation operation1 = this.CreateItemBatchOperation();
            ItemBatchOperation operation2 = this.CreateItemBatchOperation();
            operation1.AttachContext(new ItemBatchOperationContext(string.Empty, NoOpTrace.Singleton, retryPolicy1));
            operation2.AttachContext(new ItemBatchOperationContext(string.Empty, NoOpTrace.Singleton, retryPolicy2));

            Mock<BatchAsyncBatcherRetryDelegate> retryDelegate = new Mock<BatchAsyncBatcherRetryDelegate>();

            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(2, 1000, MockCosmosUtil.Serializer, this.ExecutorWithCompletingSplit, retryDelegate.Object, BatchAsyncBatcherTests.MockClientContext());
            Assert.IsTrue(batchAsyncBatcher.TryAdd(operation1));
            Assert.IsTrue(batchAsyncBatcher.TryAdd(operation2));
            await batchAsyncBatcher.DispatchAsync(metric);
            retryDelegate.Verify(a => a(It.Is<ItemBatchOperation>(o => o == operation1), It.IsAny<CancellationToken>()), Times.Once);
            retryDelegate.Verify(a => a(It.Is<ItemBatchOperation>(o => o == operation2), It.IsAny<CancellationToken>()), Times.Once);
            retryDelegate.Verify(a => a(It.IsAny<ItemBatchOperation>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [TestMethod]
        public async Task RetrierGetsCalledOnCompletingPartitionMigration()
        {
            IDocumentClientRetryPolicy retryPolicy1 = new BulkExecutionRetryPolicy(
                GetSplitEnabledContainer(),
                OperationType.Read,
                new ResourceThrottleRetryPolicy(1));

            IDocumentClientRetryPolicy retryPolicy2 = new BulkExecutionRetryPolicy(
                GetSplitEnabledContainer(),
                OperationType.Read,
                new ResourceThrottleRetryPolicy(1));

            ItemBatchOperation operation1 = this.CreateItemBatchOperation();
            ItemBatchOperation operation2 = this.CreateItemBatchOperation();
            operation1.AttachContext(new ItemBatchOperationContext(string.Empty, NoOpTrace.Singleton, retryPolicy1));
            operation2.AttachContext(new ItemBatchOperationContext(string.Empty, NoOpTrace.Singleton, retryPolicy2));

            Mock<BatchAsyncBatcherRetryDelegate> retryDelegate = new Mock<BatchAsyncBatcherRetryDelegate>();

            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(2, 1000, MockCosmosUtil.Serializer, this.ExecutorWithCompletingPartitionMigration, retryDelegate.Object, BatchAsyncBatcherTests.MockClientContext());
            Assert.IsTrue(batchAsyncBatcher.TryAdd(operation1));
            Assert.IsTrue(batchAsyncBatcher.TryAdd(operation2));
            await batchAsyncBatcher.DispatchAsync(metric);
            retryDelegate.Verify(a => a(It.Is<ItemBatchOperation>(o => o == operation1), It.IsAny<CancellationToken>()), Times.Once);
            retryDelegate.Verify(a => a(It.Is<ItemBatchOperation>(o => o == operation2), It.IsAny<CancellationToken>()), Times.Once);
            retryDelegate.Verify(a => a(It.IsAny<ItemBatchOperation>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [TestMethod]
        public async Task RetrierGetsCalledOnOverFlow()
        {
            ItemBatchOperation operation1 = this.CreateItemBatchOperation();
            ItemBatchOperation operation2 = this.CreateItemBatchOperation();
            operation1.AttachContext(new ItemBatchOperationContext(string.Empty, NoOpTrace.Singleton));
            operation2.AttachContext(new ItemBatchOperationContext(string.Empty, NoOpTrace.Singleton));

            Mock<BatchAsyncBatcherRetryDelegate> retryDelegate = new Mock<BatchAsyncBatcherRetryDelegate>();
            Mock<BatchAsyncBatcherExecuteDelegate> executeDelegate = new Mock<BatchAsyncBatcherExecuteDelegate>();

            BatchAsyncBatcherThatOverflows batchAsyncBatcher = new BatchAsyncBatcherThatOverflows(2, 1000, MockCosmosUtil.Serializer, executeDelegate.Object, retryDelegate.Object);
            Assert.IsTrue(batchAsyncBatcher.TryAdd(operation1));
            Assert.IsTrue(batchAsyncBatcher.TryAdd(operation2));
            await batchAsyncBatcher.DispatchAsync(metric);
            retryDelegate.Verify(a => a(It.Is<ItemBatchOperation>(o => o == operation1), It.IsAny<CancellationToken>()), Times.Never);
            retryDelegate.Verify(a => a(It.Is<ItemBatchOperation>(o => o == operation2), It.IsAny<CancellationToken>()), Times.Once);
            retryDelegate.Verify(a => a(It.IsAny<ItemBatchOperation>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task RetrierGetsCalledOn413_3402()
        {
            IDocumentClientRetryPolicy retryPolicy1 = new BulkExecutionRetryPolicy(
                GetSplitEnabledContainer(),
                OperationType.Read,
                new ResourceThrottleRetryPolicy(1));

            IDocumentClientRetryPolicy retryPolicy2 = new BulkExecutionRetryPolicy(
                GetSplitEnabledContainer(),
                OperationType.Read,
                new ResourceThrottleRetryPolicy(1));

            IDocumentClientRetryPolicy retryPolicy3 = new BulkExecutionRetryPolicy(
                GetSplitEnabledContainer(),
                OperationType.Create,
                new ResourceThrottleRetryPolicy(1));

            ItemBatchOperation operation1 = this.CreateItemBatchOperation();
            ItemBatchOperation operation2 = this.CreateItemBatchOperation();
            ItemBatchOperation operation3 = this.CreateItemBatchOperation();
            operation1.AttachContext(new ItemBatchOperationContext(string.Empty, NoOpTrace.Singleton, retryPolicy1));
            operation2.AttachContext(new ItemBatchOperationContext(string.Empty, NoOpTrace.Singleton, retryPolicy2));
            operation3.AttachContext(new ItemBatchOperationContext(string.Empty, NoOpTrace.Singleton, retryPolicy3));

            Mock<BatchAsyncBatcherRetryDelegate> retryDelegate = new Mock<BatchAsyncBatcherRetryDelegate>();

            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(3, 1000, MockCosmosUtil.Serializer, this.ExecutorWith413_3402, retryDelegate.Object, BatchAsyncBatcherTests.MockClientContext());
            Assert.IsTrue(batchAsyncBatcher.TryAdd(operation1));
            Assert.IsTrue(batchAsyncBatcher.TryAdd(operation2));
            Assert.IsTrue(batchAsyncBatcher.TryAdd(operation3));
            await batchAsyncBatcher.DispatchAsync(metric);
            retryDelegate.Verify(a => a(It.Is<ItemBatchOperation>(o => o == operation1), It.IsAny<CancellationToken>()), Times.Never);
            retryDelegate.Verify(a => a(It.Is<ItemBatchOperation>(o => o == operation2), It.IsAny<CancellationToken>()), Times.Once);
            retryDelegate.Verify(a => a(It.Is<ItemBatchOperation>(o => o == operation2), It.IsAny<CancellationToken>()), Times.Once);
            retryDelegate.Verify(a => a(It.IsAny<ItemBatchOperation>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [TestMethod]
        public async Task RetrierGetsCalledOn413_NoSubstatus()
        {
            IDocumentClientRetryPolicy retryPolicy1 = new BulkExecutionRetryPolicy(
                GetSplitEnabledContainer(),
                OperationType.Read,
                new ResourceThrottleRetryPolicy(1));

            IDocumentClientRetryPolicy retryPolicy2 = new BulkExecutionRetryPolicy(
                GetSplitEnabledContainer(),
                OperationType.Read,
                new ResourceThrottleRetryPolicy(1));

            IDocumentClientRetryPolicy retryPolicy3 = new BulkExecutionRetryPolicy(
                GetSplitEnabledContainer(),
                OperationType.Create,
                new ResourceThrottleRetryPolicy(1));

            ItemBatchOperation operation1 = this.CreateItemBatchOperation();
            ItemBatchOperation operation2 = this.CreateItemBatchOperation();
            ItemBatchOperation operation3 = this.CreateItemBatchOperation();
            operation1.AttachContext(new ItemBatchOperationContext(string.Empty, NoOpTrace.Singleton, retryPolicy1));
            operation2.AttachContext(new ItemBatchOperationContext(string.Empty, NoOpTrace.Singleton, retryPolicy2));
            operation3.AttachContext(new ItemBatchOperationContext(string.Empty, NoOpTrace.Singleton, retryPolicy3));

            Mock<BatchAsyncBatcherRetryDelegate> retryDelegate = new Mock<BatchAsyncBatcherRetryDelegate>();

            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(3, 1000, MockCosmosUtil.Serializer, this.ExecutorWith413, retryDelegate.Object, BatchAsyncBatcherTests.MockClientContext());
            Assert.IsTrue(batchAsyncBatcher.TryAdd(operation1));
            Assert.IsTrue(batchAsyncBatcher.TryAdd(operation2));
            Assert.IsTrue(batchAsyncBatcher.TryAdd(operation3));
            await batchAsyncBatcher.DispatchAsync(metric);
            retryDelegate.Verify(a => a(It.Is<ItemBatchOperation>(o => o == operation1), It.IsAny<CancellationToken>()), Times.Never);
            retryDelegate.Verify(a => a(It.Is<ItemBatchOperation>(o => o == operation2), It.IsAny<CancellationToken>()), Times.Never);
            retryDelegate.Verify(a => a(It.Is<ItemBatchOperation>(o => o == operation3), It.IsAny<CancellationToken>()), Times.Never);
            retryDelegate.Verify(a => a(It.IsAny<ItemBatchOperation>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        private static ContainerInternal GetSplitEnabledContainer()
        {
            Mock<ContainerInternal> container = new Mock<ContainerInternal>();
            container.Setup(c => c.GetCachedRIDAsync(It.IsAny<bool>(), It.IsAny<ITrace>(), It.IsAny<CancellationToken>())).ReturnsAsync(Guid.NewGuid().ToString());
            Mock<CosmosClientContext> context = new Mock<CosmosClientContext>();
            container.Setup(c => c.ClientContext).Returns(context.Object);
            context.Setup(c => c.DocumentClient).Returns(new ClientWithSplitDetection());
            return container.Object;
        }

        private static CosmosClientContext MockClientContext()
        {
            Mock<CosmosClientContext> mockContext = new Mock<CosmosClientContext>();
            mockContext.Setup(x => x.OperationHelperAsync<object>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<OperationType>(),
                It.IsAny<RequestOptions>(),
                It.IsAny<Func<ITrace, Task<object>>>(),
                It.IsAny<Tuple<string, Func<object, OpenTelemetryAttributes>>>(),
                It.IsAny<ResourceType?>(),
                It.IsAny<TraceComponent>(),
                It.IsAny<TraceLevel>()))
               .Returns<string, string, string, OperationType, RequestOptions, Func<ITrace, Task<object>>, Tuple<string, Func<object, OpenTelemetryAttributes>>, ResourceType?, TraceComponent, TraceLevel>(
                (operationName, containerName, databaseName, operationType, requestOptions, func, oTelFunc, resourceType, comp, level) => func(NoOpTrace.Singleton));

            return mockContext.Object;
        }

        private class BatchAsyncBatcherThatOverflows : BatchAsyncBatcher
        {
            public BatchAsyncBatcherThatOverflows(
                int maxBatchOperationCount,
                int maxBatchByteSize,
                CosmosSerializerCore serializerCore,
                BatchAsyncBatcherExecuteDelegate executor,
                BatchAsyncBatcherRetryDelegate retrier) : base(maxBatchOperationCount, maxBatchByteSize, serializerCore, executor, retrier, BatchAsyncBatcherTests.MockClientContext())
            {

            }

            internal override async Task<Tuple<PartitionKeyRangeServerBatchRequest, ArraySegment<ItemBatchOperation>>> CreateServerRequestAsync(CancellationToken cancellationToken)
            {
                (PartitionKeyRangeServerBatchRequest serverRequest, _) = await base.CreateServerRequestAsync(cancellationToken);

                // Returning a pending operation to retry
                return new Tuple<PartitionKeyRangeServerBatchRequest, ArraySegment<ItemBatchOperation>>(serverRequest, new ArraySegment<ItemBatchOperation>(serverRequest.Operations.ToArray(), 1, 1));
            }
        }

        private class ClientWithSplitDetection : MockDocumentClient
        {
            private readonly Mock<PartitionKeyRangeCache> partitionKeyRangeCache;

            public ClientWithSplitDetection()
            {
                this.partitionKeyRangeCache = new Mock<PartitionKeyRangeCache>(MockBehavior.Strict, null, null, null, null);
                this.partitionKeyRangeCache.Setup(
                        m => m.TryGetOverlappingRangesAsync(
                            It.IsAny<string>(),
                            It.IsAny<Documents.Routing.Range<string>>(),
                            It.IsAny<ITrace>(),
                            It.Is<bool>(b => b == true) // Mocking only the refresh, if it doesn't get called, the test fails
                        )
                ).Returns((string collectionRid, Documents.Routing.Range<string> range, ITrace trace, bool forceRefresh) => Task.FromResult<IReadOnlyList<PartitionKeyRange>>(this.ResolveOverlapingPartitionKeyRanges(collectionRid, range, forceRefresh)));
            }

            internal override Task<PartitionKeyRangeCache> GetPartitionKeyRangeCacheAsync(ITrace trace)
            {
                return Task.FromResult(this.partitionKeyRangeCache.Object);
            }

        }
    }
}