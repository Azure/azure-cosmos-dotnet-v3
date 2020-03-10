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
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class BatchAsyncBatcherTests
    {
        private static Exception expectedException = new Exception();
        private static BatchPartitionMetric metric = new BatchPartitionMetric();

        private ItemBatchOperation CreateItemBatchOperation(bool withContext = false)
        {
            ItemBatchOperation operation = new ItemBatchOperation(
                operationType: OperationType.Create,
                operationIndex: 0,
                id: string.Empty,
                resourceStream: new MemoryStream(new byte[] { 0x41, 0x42 }, index: 0, count: 2, writable: false, publiclyVisible: true));
            if (withContext)
            {
                operation.AttachContext(new ItemBatchOperationContext(string.Empty));
            }

            return operation;
        }

        private BatchAsyncBatcherExecuteDelegate Executor
            = async (PartitionKeyRangeServerBatchRequest request, CancellationToken cancellationToken) =>
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
                cancellationToken: cancellationToken);

                TransactionalBatchResponse batchresponse = await TransactionalBatchResponse.FromResponseMessageAsync(
                    new ResponseMessage(HttpStatusCode.OK)
                    {
                        Content = responseContent
                    },
                    batchRequest,
                    MockCosmosUtil.Serializer);

                return new PartitionKeyRangeBatchExecutionResult(request.PartitionKeyRangeId, request.Operations, batchresponse);
            };

        private BatchAsyncBatcherExecuteDelegate ExecutorWithSplit
            = async (PartitionKeyRangeServerBatchRequest request, CancellationToken cancellationToken) =>
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
                cancellationToken: cancellationToken);

                ResponseMessage responseMessage = new ResponseMessage(HttpStatusCode.Gone)
                {
                    Content = responseContent
                };
                responseMessage.Headers.SubStatusCode = SubStatusCodes.PartitionKeyRangeGone;

                TransactionalBatchResponse batchresponse = await TransactionalBatchResponse.FromResponseMessageAsync(
                    responseMessage,
                    batchRequest,
                    MockCosmosUtil.Serializer);

                return new PartitionKeyRangeBatchExecutionResult(request.PartitionKeyRangeId, request.Operations, batchresponse);
            };

        // The response will include all but 2 operation responses
        private BatchAsyncBatcherExecuteDelegate ExecutorWithLessResponses
            = async (PartitionKeyRangeServerBatchRequest request, CancellationToken cancellationToken) =>
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
                cancellationToken: cancellationToken);

                TransactionalBatchResponse batchresponse = await TransactionalBatchResponse.FromResponseMessageAsync(
                    new ResponseMessage(HttpStatusCode.OK) { Content = responseContent },
                    batchRequest,
                    MockCosmosUtil.Serializer);

                return new PartitionKeyRangeBatchExecutionResult(request.PartitionKeyRangeId, request.Operations, batchresponse);
            };

        private BatchAsyncBatcherExecuteDelegate ExecutorWithFailure
            = (PartitionKeyRangeServerBatchRequest request, CancellationToken cancellationToken) =>
            {
                throw expectedException;
            };

        private BatchAsyncBatcherRetryDelegate Retrier = (ItemBatchOperation operation, CancellationToken cancellation) =>
        {
            return Task.CompletedTask;
        };

        [DataTestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        [DataRow(0)]
        [DataRow(-1)]
        public void ValidatesSize(int size)
        {
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(size, 1, MockCosmosUtil.Serializer, this.Executor, this.Retrier);
        }

        [DataTestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        [DataRow(0)]
        [DataRow(-1)]
        public void ValidatesByteSize(int size)
        {
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(1, size, MockCosmosUtil.Serializer, this.Executor, this.Retrier);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ValidatesExecutor()
        {
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(1, 1, MockCosmosUtil.Serializer, null, this.Retrier);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ValidatesRetrier()
        {
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(1, 1, MockCosmosUtil.Serializer, this.Executor, null);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ValidatesSerializer()
        {
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(1, 1, null, this.Executor, this.Retrier);
        }

        [TestMethod]
        public void HasFixedSize()
        {
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(2, 1000, MockCosmosUtil.Serializer, this.Executor, this.Retrier);
            Assert.IsTrue(batchAsyncBatcher.TryAdd(this.CreateItemBatchOperation(true)));
            Assert.IsTrue(batchAsyncBatcher.TryAdd(this.CreateItemBatchOperation(true)));
            Assert.IsFalse(batchAsyncBatcher.TryAdd(this.CreateItemBatchOperation(true)));
        }

        [TestMethod]
        public async Task HasFixedByteSize()
        {
            ItemBatchOperation itemBatchOperation = this.CreateItemBatchOperation(true);
            await itemBatchOperation.MaterializeResourceAsync(MockCosmosUtil.Serializer, default(CancellationToken));
            // Each operation is 2 bytes
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(3, 4, MockCosmosUtil.Serializer, this.Executor, this.Retrier);
            Assert.IsTrue(batchAsyncBatcher.TryAdd(itemBatchOperation));
            Assert.IsTrue(batchAsyncBatcher.TryAdd(itemBatchOperation));
            Assert.IsFalse(batchAsyncBatcher.TryAdd(itemBatchOperation));
        }

        [TestMethod]
        public async Task ExceptionsFailOperationsAsync()
        {
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(2, 1000, MockCosmosUtil.Serializer, this.ExecutorWithFailure, this.Retrier);
            ItemBatchOperation operation1 = this.CreateItemBatchOperation();
            ItemBatchOperation operation2 = this.CreateItemBatchOperation();
            ItemBatchOperationContext context1 = new ItemBatchOperationContext(string.Empty);
            operation1.AttachContext(context1);
            ItemBatchOperationContext context2 = new ItemBatchOperationContext(string.Empty);
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
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(10, 1000, MockCosmosUtil.Serializer, this.Executor, this.Retrier);
            List<ItemBatchOperation> operations = new List<ItemBatchOperation>(10);
            for (int i = 0; i < 10; i++)
            {
                ItemBatchOperation operation = new ItemBatchOperation(
                    operationType: OperationType.Create,
                    operationIndex: i,
                    partitionKey: new Cosmos.PartitionKey(i.ToString()),
                    id: i.ToString(),
                    diagnosticsContext: new CosmosDiagnosticsContextCore());

                ItemBatchOperationContext context = new ItemBatchOperationContext(string.Empty);
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

                Assert.IsNotNull(operation.DiagnosticsContext);
                Assert.AreEqual(operation.DiagnosticsContext.ToString(), result.DiagnosticsContext.ToString());
                Assert.IsFalse(string.IsNullOrEmpty(operation.DiagnosticsContext.ToString()));
            }
        }

        [TestMethod]
        public async Task DispatchWithLessResponses()
        {
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(10, 1000, MockCosmosUtil.Serializer, this.ExecutorWithLessResponses, this.Retrier);
            BatchAsyncBatcher secondAsyncBatcher = new BatchAsyncBatcher(10, 1000, MockCosmosUtil.Serializer, this.Executor, this.Retrier);
            List<ItemBatchOperation> operations = new List<ItemBatchOperation>(10);
            for (int i = 0; i < 10; i++)
            {
                ItemBatchOperation operation = new ItemBatchOperation(OperationType.Create, i, i.ToString());
                ItemBatchOperationContext context = new ItemBatchOperationContext(string.Empty);
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
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(10, 1000, MockCosmosUtil.Serializer, this.Executor, this.Retrier);
            Assert.IsTrue(batchAsyncBatcher.IsEmpty);
        }

        [TestMethod]
        public void IsNotEmptyWithOperations()
        {
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(1, 1000, MockCosmosUtil.Serializer, this.Executor, this.Retrier);
            Assert.IsTrue(batchAsyncBatcher.TryAdd(this.CreateItemBatchOperation(true)));
            Assert.IsFalse(batchAsyncBatcher.IsEmpty);
        }

        [TestMethod]
        public async Task CannotAddToDispatchedBatch()
        {
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(1, 1000, MockCosmosUtil.Serializer, this.Executor, this.Retrier);
            ItemBatchOperation operation = this.CreateItemBatchOperation();
            operation.AttachContext(new ItemBatchOperationContext(string.Empty));
            Assert.IsTrue(batchAsyncBatcher.TryAdd(operation));
            await batchAsyncBatcher.DispatchAsync(metric);
            Assert.IsFalse(batchAsyncBatcher.TryAdd(this.CreateItemBatchOperation()));
        }

        [TestMethod]
        public async Task RetrierGetsCalledOnSplit()
        {
            IDocumentClientRetryPolicy retryPolicy1 = new BulkPartitionKeyRangeGoneRetryPolicy(
                new ResourceThrottleRetryPolicy(1));

            IDocumentClientRetryPolicy retryPolicy2 = new BulkPartitionKeyRangeGoneRetryPolicy(
                new ResourceThrottleRetryPolicy(1));

            ItemBatchOperation operation1 = this.CreateItemBatchOperation();
            ItemBatchOperation operation2 = this.CreateItemBatchOperation();
            operation1.AttachContext(new ItemBatchOperationContext(string.Empty, retryPolicy1));
            operation2.AttachContext(new ItemBatchOperationContext(string.Empty, retryPolicy2));

            Mock<BatchAsyncBatcherRetryDelegate> retryDelegate = new Mock<BatchAsyncBatcherRetryDelegate>();

            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(2, 1000, MockCosmosUtil.Serializer, this.ExecutorWithSplit, retryDelegate.Object);
            Assert.IsTrue(batchAsyncBatcher.TryAdd(operation1));
            Assert.IsTrue(batchAsyncBatcher.TryAdd(operation2));
            await batchAsyncBatcher.DispatchAsync(metric);
            retryDelegate.Verify(a => a(It.Is<ItemBatchOperation>(o => o == operation1), It.IsAny<CancellationToken>()), Times.Once);
            retryDelegate.Verify(a => a(It.Is<ItemBatchOperation>(o => o == operation2), It.IsAny<CancellationToken>()), Times.Once);
            retryDelegate.Verify(a => a(It.IsAny<ItemBatchOperation>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
            Assert.IsNull(operation1.DiagnosticsContext, "Batch operations do not have diagnostics per operation");
            Assert.IsNull(operation2.DiagnosticsContext, "Batch operations do not have diagnostics per operation");
        }

        [TestMethod]
        public async Task RetrierGetsCalledOnOverFlow()
        {
            ItemBatchOperation operation1 = this.CreateItemBatchOperation();
            ItemBatchOperation operation2 = this.CreateItemBatchOperation();
            operation1.AttachContext(new ItemBatchOperationContext(string.Empty));
            operation2.AttachContext(new ItemBatchOperationContext(string.Empty));

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

        private class BatchAsyncBatcherThatOverflows : BatchAsyncBatcher
        {
            public BatchAsyncBatcherThatOverflows(
                int maxBatchOperationCount,
                int maxBatchByteSize,
                CosmosSerializerCore serializerCore,
                BatchAsyncBatcherExecuteDelegate executor,
                BatchAsyncBatcherRetryDelegate retrier) : base (maxBatchOperationCount, maxBatchByteSize, serializerCore, executor, retrier)
            {

            }

            internal override async Task<Tuple<PartitionKeyRangeServerBatchRequest, ArraySegment<ItemBatchOperation>>> CreateServerRequestAsync(CancellationToken cancellationToken)
            {
                (PartitionKeyRangeServerBatchRequest serverRequest, ArraySegment<ItemBatchOperation> pendingOperations) = await base.CreateServerRequestAsync(cancellationToken);

                // Returning a pending operation to retry
                return new Tuple<PartitionKeyRangeServerBatchRequest, ArraySegment<ItemBatchOperation>>(serverRequest, new ArraySegment<ItemBatchOperation>(serverRequest.Operations.ToArray(), 1, 1));
            }
        }
    }
}
