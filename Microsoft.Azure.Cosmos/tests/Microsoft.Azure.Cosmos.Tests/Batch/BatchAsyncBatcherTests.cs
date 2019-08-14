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

    [TestClass]
    public class BatchAsyncBatcherTests
    {
        private static Exception expectedException = new Exception();

        private ItemBatchOperation ItemBatchOperation = new ItemBatchOperation(OperationType.Create, 0, string.Empty, new MemoryStream(new byte[] { 0x41, 0x42 }, index: 0, count: 2, writable: false, publiclyVisible: true));

        private BatchAsyncBatcherExecuteDelegate Executor
            = async (IReadOnlyList<BatchAsyncOperationContext> operations, CancellationToken cancellation) =>
            {
                List<BatchOperationResult> results = new List<BatchOperationResult>();
                ItemBatchOperation[] arrayOperations = new ItemBatchOperation[operations.Count];
                int index = 0;
                foreach (BatchAsyncOperationContext operation in operations)
                {
                    results.Add(
                    new BatchOperationResult(HttpStatusCode.OK)
                    {
                        ResourceStream = new MemoryStream(new byte[] { 0x41, 0x42 }, index: 0, count: 2, writable: false, publiclyVisible: true),
                        ETag = operation.Operation.Id
                    });

                    arrayOperations[index++] = operation.Operation;
                }

                MemoryStream responseContent = await new BatchResponsePayloadWriter(results).GeneratePayloadAsync();

                SinglePartitionKeyServerBatchRequest batchRequest = await SinglePartitionKeyServerBatchRequest.CreateAsync(
                    partitionKey: null,
                    operations: new ArraySegment<ItemBatchOperation>(arrayOperations),
                    maxBodyLength: (int)responseContent.Length * operations.Count,
                    maxOperationCount: operations.Count,
                    serializer: new CosmosJsonDotNetSerializer(),
                cancellationToken: cancellation);

                BatchResponse batchresponse = await BatchResponse.PopulateFromContentAsync(
                    new ResponseMessage(HttpStatusCode.OK) { Content = responseContent },
                    batchRequest,
                    new CosmosJsonDotNetSerializer());

                return new PartitionKeyRangeBatchResponse(operations.Count, new List <BatchResponse> { batchresponse }, new CosmosJsonDotNetSerializer());
            };

        // The response will include all but 2 operation responses
        private BatchAsyncBatcherExecuteDelegate ExecutorWithLessResponses
            = async (IReadOnlyList<BatchAsyncOperationContext> operations, CancellationToken cancellation) =>
            {
                int operationCount = operations.Count - 2;
                List<BatchOperationResult> results = new List<BatchOperationResult>();
                ItemBatchOperation[] arrayOperations = new ItemBatchOperation[operationCount];
                int index = 0;
                foreach (BatchAsyncOperationContext operation in operations.Skip(1).Take(operationCount))
                {
                    results.Add(
                    new BatchOperationResult(HttpStatusCode.OK)
                    {
                        ResourceStream = new MemoryStream(new byte[] { 0x41, 0x42 }, index: 0, count: 2, writable: false, publiclyVisible: true),
                        ETag = operation.Operation.Id
                    });

                    arrayOperations[index++] = operation.Operation;
                }

                MemoryStream responseContent = await new BatchResponsePayloadWriter(results).GeneratePayloadAsync();

                SinglePartitionKeyServerBatchRequest batchRequest = await SinglePartitionKeyServerBatchRequest.CreateAsync(
                    partitionKey: null,
                    operations: new ArraySegment<ItemBatchOperation>(arrayOperations),
                    maxBodyLength: (int)responseContent.Length * operationCount,
                    maxOperationCount: operationCount,
                    serializer: new CosmosJsonDotNetSerializer(),
                cancellationToken: cancellation);

                BatchResponse batchresponse = await BatchResponse.PopulateFromContentAsync(
                    new ResponseMessage(HttpStatusCode.OK) { Content = responseContent },
                    batchRequest,
                    new CosmosJsonDotNetSerializer());

                return new PartitionKeyRangeBatchResponse(operations.Count, new List <BatchResponse> { batchresponse }, new CosmosJsonDotNetSerializer());
            };

        private BatchAsyncBatcherExecuteDelegate ExecutorWithFailure
            = (IReadOnlyList<BatchAsyncOperationContext> operations, CancellationToken cancellation) =>
            {
                throw expectedException;
            };

        [DataTestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        [DataRow(0)]
        [DataRow(-1)]
        public void ValidatesSize(int size)
        {
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(size, 1, new CosmosJsonDotNetSerializer(), this.Executor);
        }

        [DataTestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        [DataRow(0)]
        [DataRow(-1)]
        public void ValidatesByteSize(int size)
        {
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(1, size, new CosmosJsonDotNetSerializer(), this.Executor);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ValidatesExecutor()
        {
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(1, 1, new CosmosJsonDotNetSerializer(), null);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ValidatesSerializer()
        {
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(1, 1, null, this.Executor);
        }

        [TestMethod]
        public void HasFixedSize()
        {
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(2, 1000, new CosmosJsonDotNetSerializer(), this.Executor);
            Assert.IsTrue(batchAsyncBatcher.TryAdd(new BatchAsyncOperationContext(string.Empty, this.ItemBatchOperation)));
            Assert.IsTrue(batchAsyncBatcher.TryAdd(new BatchAsyncOperationContext(string.Empty, this.ItemBatchOperation)));
            Assert.IsFalse(batchAsyncBatcher.TryAdd(new BatchAsyncOperationContext(string.Empty, this.ItemBatchOperation)));
        }

        [TestMethod]
        public async Task HasFixedByteSize()
        {
            await this.ItemBatchOperation.MaterializeResourceAsync(new CosmosJsonDotNetSerializer(), CancellationToken.None);
            // Each operation is 2 bytes
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(3, 4, new CosmosJsonDotNetSerializer(), this.Executor);
            Assert.IsTrue(batchAsyncBatcher.TryAdd(new BatchAsyncOperationContext(string.Empty, this.ItemBatchOperation)));
            Assert.IsTrue(batchAsyncBatcher.TryAdd(new BatchAsyncOperationContext(string.Empty, this.ItemBatchOperation)));
            Assert.IsFalse(batchAsyncBatcher.TryAdd(new BatchAsyncOperationContext(string.Empty, this.ItemBatchOperation)));
        }

        [TestMethod]
        public async Task ExceptionsFailOperationsAsync()
        {
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(2, 1000, new CosmosJsonDotNetSerializer(), this.ExecutorWithFailure);
            BatchAsyncOperationContext context1 = new BatchAsyncOperationContext(string.Empty, this.ItemBatchOperation);
            BatchAsyncOperationContext context2 = new BatchAsyncOperationContext(string.Empty, this.ItemBatchOperation);
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
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(10, 1000, new CosmosJsonDotNetSerializer(), this.Executor);
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
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(10, 1000, new CosmosJsonDotNetSerializer(), this.ExecutorWithLessResponses);
            BatchAsyncBatcher secondAsyncBatcher = new BatchAsyncBatcher(10, 1000, new CosmosJsonDotNetSerializer(), this.Executor);
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
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(10, 1000, new CosmosJsonDotNetSerializer(), this.Executor);
            Assert.IsTrue(batchAsyncBatcher.IsEmpty);
        }

        [TestMethod]
        public void IsNotEmptyWithOperations()
        {
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(1, 1000, new CosmosJsonDotNetSerializer(), this.Executor);
            Assert.IsTrue(batchAsyncBatcher.TryAdd(new BatchAsyncOperationContext(string.Empty, this.ItemBatchOperation)));
            Assert.IsFalse(batchAsyncBatcher.IsEmpty);
        }

        [TestMethod]
        public async Task CannotAddToDispatchedBatch()
        {
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(1, 1000, new CosmosJsonDotNetSerializer(), this.Executor);
            Assert.IsTrue(batchAsyncBatcher.TryAdd(new BatchAsyncOperationContext(string.Empty, this.ItemBatchOperation)));
            await batchAsyncBatcher.DispatchAsync();
            Assert.IsFalse(batchAsyncBatcher.TryAdd(new BatchAsyncOperationContext(string.Empty, this.ItemBatchOperation)));
        }
    }
}
