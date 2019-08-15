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
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class BatchAsyncBatcherTests
    {
        private static Exception expectedException = new Exception();

        private ItemBatchOperation ItemBatchOperation() => new ItemBatchOperation(OperationType.Create, 0, string.Empty, new MemoryStream(new byte[] { 0x41, 0x42 }, index: 0, count: 2, writable: false, publiclyVisible: true));

        private BatchAsyncBatcherExecuteDelegate Executor
            = async (PartitionKeyRangeServerBatchRequest request, CancellationToken cancellationToken) =>
            {
                List<BatchOperationResult> results = new List<BatchOperationResult>();
                ItemBatchOperation[] arrayOperations = new ItemBatchOperation[request.Operations.Count];
                int index = 0;
                foreach (ItemBatchOperation operation in request.Operations)
                {
                    results.Add(
                    new BatchOperationResult(HttpStatusCode.OK)
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
                    maxBodyLength: (int)responseContent.Length * request.Operations.Count,
                    maxOperationCount: request.Operations.Count,
                    serializer: new CosmosJsonDotNetSerializer(),
                cancellationToken: cancellationToken);

                BatchResponse batchresponse = await BatchResponse.PopulateFromContentAsync(
                    new ResponseMessage(HttpStatusCode.OK) { Content = responseContent },
                    batchRequest,
                    new CosmosJsonDotNetSerializer());

                return new PartitionKeyRangeBatchExecutionResult(request.PartitionKeyRangeId, request.Operations, new List<BatchResponse>() { batchresponse });
            };

        private BatchAsyncBatcherExecuteDelegate ExecutorWithSplit
            = async (PartitionKeyRangeServerBatchRequest request, CancellationToken cancellationToken) =>
            {
                List<BatchOperationResult> results = new List<BatchOperationResult>();
                ItemBatchOperation[] arrayOperations = new ItemBatchOperation[request.Operations.Count];
                int index = 0;
                foreach (ItemBatchOperation operation in request.Operations)
                {
                    results.Add(
                    new BatchOperationResult(HttpStatusCode.Gone)
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
                    maxBodyLength: (int)responseContent.Length * request.Operations.Count,
                    maxOperationCount: request.Operations.Count,
                    serializer: new CosmosJsonDotNetSerializer(),
                cancellationToken: cancellationToken);

                ResponseMessage responseMessage = new ResponseMessage(HttpStatusCode.Gone) { Content = responseContent };
                responseMessage.Headers.SubStatusCode = SubStatusCodes.PartitionKeyRangeGone;

                BatchResponse batchresponse = await BatchResponse.PopulateFromContentAsync(
                    responseMessage,
                    batchRequest,
                    new CosmosJsonDotNetSerializer());

                return new PartitionKeyRangeBatchExecutionResult(request.PartitionKeyRangeId, request.Operations, new List<BatchResponse>() { batchresponse });
            };

        // The response will include all but 2 operation responses
        private BatchAsyncBatcherExecuteDelegate ExecutorWithLessResponses
            = async (PartitionKeyRangeServerBatchRequest request, CancellationToken cancellationToken) =>
            {
                int operationCount = request.Operations.Count - 2;
                List<BatchOperationResult> results = new List<BatchOperationResult>();
                ItemBatchOperation[] arrayOperations = new ItemBatchOperation[operationCount];
                int index = 0;
                foreach (ItemBatchOperation operation in request.Operations.Skip(1).Take(operationCount))
                {
                    results.Add(
                    new BatchOperationResult(HttpStatusCode.OK)
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
                    maxBodyLength: (int)responseContent.Length * operationCount,
                    maxOperationCount: operationCount,
                    serializer: new CosmosJsonDotNetSerializer(),
                cancellationToken: cancellationToken);

                BatchResponse batchresponse = await BatchResponse.PopulateFromContentAsync(
                    new ResponseMessage(HttpStatusCode.OK) { Content = responseContent },
                    batchRequest,
                    new CosmosJsonDotNetSerializer());

                return new PartitionKeyRangeBatchExecutionResult(request.PartitionKeyRangeId, request.Operations, new List<BatchResponse>() { batchresponse });
            };

        private BatchAsyncBatcherExecuteDelegate ExecutorWithFailure
            = (PartitionKeyRangeServerBatchRequest request, CancellationToken cancellationToken) =>
            {
                throw expectedException;
            };

        private BatchAsyncBatcherRetryDelegate Retrier = (BatchAsyncOperationContext operation, CancellationToken cancellation) =>
        {
            return Task.CompletedTask;
        };

        [DataTestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        [DataRow(0)]
        [DataRow(-1)]
        public void ValidatesSize(int size)
        {
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(size, 1, new CosmosJsonDotNetSerializer(), this.Executor, this.Retrier);
        }

        [DataTestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        [DataRow(0)]
        [DataRow(-1)]
        public void ValidatesByteSize(int size)
        {
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(1, size, new CosmosJsonDotNetSerializer(), this.Executor, this.Retrier);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ValidatesExecutor()
        {
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(1, 1, new CosmosJsonDotNetSerializer(), null, this.Retrier);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ValidatesRetrier()
        {
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(1, 1, new CosmosJsonDotNetSerializer(), this.Executor, null);
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
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(2, 1000, new CosmosJsonDotNetSerializer(), this.Executor, this.Retrier);
            Assert.IsTrue(batchAsyncBatcher.TryAdd(new BatchAsyncOperationContext(string.Empty, this.ItemBatchOperation())));
            Assert.IsTrue(batchAsyncBatcher.TryAdd(new BatchAsyncOperationContext(string.Empty, this.ItemBatchOperation())));
            Assert.IsFalse(batchAsyncBatcher.TryAdd(new BatchAsyncOperationContext(string.Empty, this.ItemBatchOperation())));
        }

        [TestMethod]
        public async Task HasFixedByteSize()
        {
            ItemBatchOperation itemBatchOperation = this.ItemBatchOperation();
            await itemBatchOperation.MaterializeResourceAsync(new CosmosJsonDotNetSerializer(), default(CancellationToken));
            // Each operation is 2 bytes
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(3, 4, new CosmosJsonDotNetSerializer(), this.Executor, this.Retrier);
            Assert.IsTrue(batchAsyncBatcher.TryAdd(new BatchAsyncOperationContext(string.Empty, itemBatchOperation)));
            Assert.IsTrue(batchAsyncBatcher.TryAdd(new BatchAsyncOperationContext(string.Empty, itemBatchOperation)));
            Assert.IsFalse(batchAsyncBatcher.TryAdd(new BatchAsyncOperationContext(string.Empty, itemBatchOperation)));
        }

        [TestMethod]
        public async Task ExceptionsFailOperationsAsync()
        {
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(2, 1000, new CosmosJsonDotNetSerializer(), this.ExecutorWithFailure, this.Retrier);
            BatchAsyncOperationContext context1 = new BatchAsyncOperationContext(string.Empty, this.ItemBatchOperation());
            BatchAsyncOperationContext context2 = new BatchAsyncOperationContext(string.Empty, this.ItemBatchOperation());
            batchAsyncBatcher.TryAdd(context1);
            batchAsyncBatcher.TryAdd(context2);
            await batchAsyncBatcher.DispatchAsync();

            Assert.AreEqual(TaskStatus.Faulted, context1.Task.Status);
            Assert.AreEqual(TaskStatus.Faulted, context2.Task.Status);
            Assert.AreEqual(expectedException, context1.Task.Exception.InnerException);
            Assert.AreEqual(expectedException, context2.Task.Exception.InnerException);
        }

        [TestMethod]
        public async Task DispatchProcessInOrderAsync()
        {
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(10, 1000, new CosmosJsonDotNetSerializer(), this.Executor, this.Retrier);
            List<BatchAsyncOperationContext> contexts = new List<BatchAsyncOperationContext>(10);
            for (int i = 0; i < 10; i++)
            {
                BatchAsyncOperationContext context = new BatchAsyncOperationContext(string.Empty, new ItemBatchOperation(OperationType.Create, i, i.ToString()));
                contexts.Add(context);
                Assert.IsTrue(batchAsyncBatcher.TryAdd(context));
            }

            await batchAsyncBatcher.DispatchAsync();

            for (int i = 0; i < 10; i++)
            {
                BatchAsyncOperationContext context = contexts[i];
                Assert.AreEqual(TaskStatus.RanToCompletion, context.Task.Status);
                BatchOperationResult result = await context.Task;
                Assert.AreEqual(i.ToString(), result.ETag);
            }
        }

        [TestMethod]
        public async Task DispatchWithLessResponses()
        {
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(10, 1000, new CosmosJsonDotNetSerializer(), this.ExecutorWithLessResponses, this.Retrier);
            BatchAsyncBatcher secondAsyncBatcher = new BatchAsyncBatcher(10, 1000, new CosmosJsonDotNetSerializer(), this.Executor, this.Retrier);
            List<BatchAsyncOperationContext> contexts = new List<BatchAsyncOperationContext>(10);
            for (int i = 0; i < 10; i++)
            {
                BatchAsyncOperationContext context = new BatchAsyncOperationContext(string.Empty, new ItemBatchOperation(OperationType.Create, i, i.ToString()));
                contexts.Add(context);
                Assert.IsTrue(batchAsyncBatcher.TryAdd(context));
            }

            await batchAsyncBatcher.DispatchAsync();

            // Responses 1 and 10 should be missing
            for (int i = 0; i < 10; i++)
            {
                BatchAsyncOperationContext context = contexts[i];
                // Some tasks should not be resolved
                if(i == 0 || i == 9)
                {
                    Assert.IsTrue(context.Task.Status == TaskStatus.WaitingForActivation);
                }
                else
                {
                    Assert.IsTrue(context.Task.Status == TaskStatus.RanToCompletion);
                }
                if (context.Task.Status == TaskStatus.RanToCompletion)
                {
                    BatchOperationResult result = await context.Task;
                    Assert.AreEqual(i.ToString(), result.ETag);
                }
                else
                {
                    // Pass the pending one to another batcher
                    Assert.IsTrue(secondAsyncBatcher.TryAdd(context));
                }
            }

            await secondAsyncBatcher.DispatchAsync();
            // All tasks should be completed
            for (int i = 0; i < 10; i++)
            {
                BatchAsyncOperationContext context = contexts[i];
                Assert.AreEqual(TaskStatus.RanToCompletion, context.Task.Status);
                BatchOperationResult result = await context.Task;
                Assert.AreEqual(i.ToString(), result.ETag);
            }
        }

        [TestMethod]
        public void IsEmptyWithNoOperations()
        {
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(10, 1000, new CosmosJsonDotNetSerializer(), this.Executor, this.Retrier);
            Assert.IsTrue(batchAsyncBatcher.IsEmpty);
        }

        [TestMethod]
        public void IsNotEmptyWithOperations()
        {
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(1, 1000, new CosmosJsonDotNetSerializer(), this.Executor, this.Retrier);
            Assert.IsTrue(batchAsyncBatcher.TryAdd(new BatchAsyncOperationContext(string.Empty, this.ItemBatchOperation())));
            Assert.IsFalse(batchAsyncBatcher.IsEmpty);
        }

        [TestMethod]
        public async Task CannotAddToDispatchedBatch()
        {
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(1, 1000, new CosmosJsonDotNetSerializer(), this.Executor, this.Retrier);
            Assert.IsTrue(batchAsyncBatcher.TryAdd(new BatchAsyncOperationContext(string.Empty, this.ItemBatchOperation())));
            await batchAsyncBatcher.DispatchAsync();
            Assert.IsFalse(batchAsyncBatcher.TryAdd(new BatchAsyncOperationContext(string.Empty, this.ItemBatchOperation())));
        }

        [TestMethod]
        public async Task RetrierGetsCalledOnSplit()
        {
            BatchAsyncOperationContext context1 = new BatchAsyncOperationContext(string.Empty, this.ItemBatchOperation());
            BatchAsyncOperationContext context2 = new BatchAsyncOperationContext(string.Empty, this.ItemBatchOperation());

            Mock<BatchAsyncBatcherRetryDelegate> retryDelegate = new Mock<BatchAsyncBatcherRetryDelegate>();

            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(2, 1000, new CosmosJsonDotNetSerializer(), this.ExecutorWithSplit, retryDelegate.Object);
            Assert.IsTrue(batchAsyncBatcher.TryAdd(context1));
            Assert.IsTrue(batchAsyncBatcher.TryAdd(context2));
            await batchAsyncBatcher.DispatchAsync();
            retryDelegate.Verify(a => a(It.Is<BatchAsyncOperationContext>(o => o == context1), It.IsAny<CancellationToken>()), Times.Once);
            retryDelegate.Verify(a => a(It.Is<BatchAsyncOperationContext>(o => o == context2), It.IsAny<CancellationToken>()), Times.Once);
            retryDelegate.Verify(a => a(It.IsAny<BatchAsyncOperationContext>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [TestMethod]
        public async Task RetrierGetsCalledOnOverFlow()
        {
            BatchAsyncOperationContext context1 = new BatchAsyncOperationContext(string.Empty, this.ItemBatchOperation());
            BatchAsyncOperationContext context2 = new BatchAsyncOperationContext(string.Empty, this.ItemBatchOperation());

            Mock<BatchAsyncBatcherRetryDelegate> retryDelegate = new Mock<BatchAsyncBatcherRetryDelegate>();
            Mock<BatchAsyncBatcherExecuteDelegate> executeDelegate = new Mock<BatchAsyncBatcherExecuteDelegate>();

            BatchAsyncBatcherThatOverflows batchAsyncBatcher = new BatchAsyncBatcherThatOverflows(2, 1000, new CosmosJsonDotNetSerializer(), executeDelegate.Object, retryDelegate.Object);
            Assert.IsTrue(batchAsyncBatcher.TryAdd(context1));
            Assert.IsTrue(batchAsyncBatcher.TryAdd(context2));
            await batchAsyncBatcher.DispatchAsync();
            retryDelegate.Verify(a => a(It.Is<BatchAsyncOperationContext>(o => o == context1), It.IsAny<CancellationToken>()), Times.Never);
            retryDelegate.Verify(a => a(It.Is<BatchAsyncOperationContext>(o => o == context2), It.IsAny<CancellationToken>()), Times.Once);
            retryDelegate.Verify(a => a(It.IsAny<BatchAsyncOperationContext>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        private class BatchAsyncBatcherThatOverflows : BatchAsyncBatcher
        {
            public BatchAsyncBatcherThatOverflows(
                int maxBatchOperationCount,
                int maxBatchByteSize,
                CosmosSerializer cosmosSerializer,
                BatchAsyncBatcherExecuteDelegate executor,
                BatchAsyncBatcherRetryDelegate retrier) : base (maxBatchOperationCount, maxBatchByteSize, cosmosSerializer, executor, retrier)
            {

            }

            internal override int GetOverflowOperations(
                PartitionKeyRangeServerBatchRequest request,
                IReadOnlyList<BatchAsyncOperationContext> operationsSentToRequest)
            {
                return 1;
            }
        }
    }
}
