//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
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

        private ItemBatchOperation ItemBatchOperation = new ItemBatchOperation(OperationType.Create, 0, string.Empty, new MemoryStream(new byte[] { 0x41, 0x42 }, index: 0, count: 2, writable: false, publiclyVisible: true));

        private Func<IReadOnlyList<BatchAsyncOperationContext>, CancellationToken, Task<PartitionKeyBatchResponse>> Executor
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

                PartitionKeyBatchResponse response = new PartitionKeyBatchResponse(new List<BatchResponse> { batchresponse }, new CosmosJsonDotNetSerializer());
                return response;
            };

        private Func<IReadOnlyList<BatchAsyncOperationContext>, CancellationToken, Task<PartitionKeyBatchResponse>> ExecutorWithFailure
            = (IReadOnlyList<BatchAsyncOperationContext> operations, CancellationToken cancellation) =>
            {
                throw expectedException;
            };

        private Func<IReadOnlyList<BatchAsyncOperationContext>, CancellationToken, Task<PartitionKeyBatchResponse>> ExecutorWithSplit
            = async (IReadOnlyList<BatchAsyncOperationContext> operations, CancellationToken cancellation) =>
            {
                List<BatchOperationResult> results = new List<BatchOperationResult>();
                ItemBatchOperation[] arrayOperations = new ItemBatchOperation[operations.Count];
                int index = 0;
                foreach (BatchAsyncOperationContext operation in operations)
                {
                    results.Add(
                    new BatchOperationResult(HttpStatusCode.Gone)
                    {
                        ETag = operation.Operation.Id,
                        SubStatusCode = SubStatusCodes.PartitionKeyRangeGone
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

                ResponseMessage responseMessage = new ResponseMessage(HttpStatusCode.Gone) { Content = responseContent };
                responseMessage.Headers.SubStatusCode = SubStatusCodes.PartitionKeyRangeGone;

                BatchResponse batchresponse = await BatchResponse.PopulateFromContentAsync(
                    responseMessage,
                    batchRequest,
                    new CosmosJsonDotNetSerializer());

                PartitionKeyBatchResponse response = new PartitionKeyBatchResponse(new List<BatchResponse> { batchresponse }, new CosmosJsonDotNetSerializer());
                return response;
            };

        [DataTestMethod]
        [Owner("maquaran")]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        [DataRow(0)]
        [DataRow(-1)]
        public void ValidatesSize(int size)
        {
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(size, 1, new CosmosJsonDotNetSerializer(), this.Executor);
        }

        [DataTestMethod]
        [Owner("maquaran")]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        [DataRow(0)]
        [DataRow(-1)]
        public void ValidatesByteSize(int size)
        {
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(1, size, new CosmosJsonDotNetSerializer(), this.Executor);
        }

        [TestMethod]
        [Owner("maquaran")]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ValidatesExecutor()
        {
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(1, 1, new CosmosJsonDotNetSerializer(), null);
        }

        [TestMethod]
        [Owner("maquaran")]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ValidatesSerializer()
        {
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(1, 1, null, this.Executor);
        }

        [TestMethod]
        [Owner("maquaran")]
        public async Task HasFixedSize()
        {
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(2, 1000, new CosmosJsonDotNetSerializer(), this.Executor);
            Assert.IsTrue(await batchAsyncBatcher.TryAddAsync(new BatchAsyncOperationContext(string.Empty, this.ItemBatchOperation)));
            Assert.IsTrue(await batchAsyncBatcher.TryAddAsync(new BatchAsyncOperationContext(string.Empty, this.ItemBatchOperation)));
            Assert.IsFalse(await batchAsyncBatcher.TryAddAsync(new BatchAsyncOperationContext(string.Empty, this.ItemBatchOperation)));
        }

        [TestMethod]
        [Owner("maquaran")]
        public async Task HasFixedByteSize()
        {
            await ItemBatchOperation.MaterializeResourceAsync(new CosmosJsonDotNetSerializer(), CancellationToken.None);
            // Each operation is 2 bytes
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(3, 4, new CosmosJsonDotNetSerializer(), this.Executor);
            Assert.IsTrue(await batchAsyncBatcher.TryAddAsync(new BatchAsyncOperationContext(string.Empty, this.ItemBatchOperation)));
            Assert.IsTrue(await batchAsyncBatcher.TryAddAsync(new BatchAsyncOperationContext(string.Empty, this.ItemBatchOperation)));
            Assert.IsFalse(await batchAsyncBatcher.TryAddAsync(new BatchAsyncOperationContext(string.Empty, this.ItemBatchOperation)));
        }

        [TestMethod]
        [Owner("maquaran")]
        public void TryAddIsThreadSafe()
        {
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(2, 1000, new CosmosJsonDotNetSerializer(), this.Executor);
            Task<bool> firstOperation = batchAsyncBatcher.TryAddAsync(new BatchAsyncOperationContext(string.Empty, this.ItemBatchOperation));
            Task<bool> secondOperation = batchAsyncBatcher.TryAddAsync(new BatchAsyncOperationContext(string.Empty, this.ItemBatchOperation));
            Task<bool> thirdOperation = batchAsyncBatcher.TryAddAsync(new BatchAsyncOperationContext(string.Empty, this.ItemBatchOperation));

            Task.WhenAll(firstOperation, secondOperation, thirdOperation).GetAwaiter().GetResult();

            int countSucceded = (firstOperation.Result ? 1 : 0) + (secondOperation.Result ? 1 : 0) + (thirdOperation.Result ? 1 : 0);
            int countFailed = (!firstOperation.Result ? 1 : 0) + (!secondOperation.Result ? 1 : 0) + (!thirdOperation.Result ? 1 : 0);

            Assert.AreEqual(2, countSucceded);
            Assert.AreEqual(1, countFailed);
        }

        [TestMethod]
        [Owner("maquaran")]
        public async Task ExceptionsFailOperationsAsync()
        {
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(2, 1000, new CosmosJsonDotNetSerializer(), this.ExecutorWithFailure);
            BatchAsyncOperationContext context1 = new BatchAsyncOperationContext(string.Empty, this.ItemBatchOperation);
            BatchAsyncOperationContext context2 = new BatchAsyncOperationContext(string.Empty, this.ItemBatchOperation);
            await batchAsyncBatcher.TryAddAsync(context1);
            await batchAsyncBatcher.TryAddAsync(context2);
            await batchAsyncBatcher.DispatchAsync();

            Assert.AreEqual(TaskStatus.Faulted, context1.Task.Status);
            Assert.AreEqual(TaskStatus.Faulted, context2.Task.Status);
            Assert.AreEqual(expectedException, context1.Task.Exception.InnerException);
            Assert.AreEqual(expectedException, context2.Task.Exception.InnerException);
        }

        [TestMethod]
        [Owner("maquaran")]
        public async Task DispatchProcessInOrderAsync()
        {
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(10, 1000, new CosmosJsonDotNetSerializer(), this.Executor);
            List<BatchAsyncOperationContext> contexts = new List<BatchAsyncOperationContext>(10);
            for (int i = 0; i < 10; i++)
            {
                BatchAsyncOperationContext context = new BatchAsyncOperationContext(string.Empty, new ItemBatchOperation(OperationType.Create, i, i.ToString()));
                contexts.Add(context);
                Assert.IsTrue(await batchAsyncBatcher.TryAddAsync(context));
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
        [Owner("maquaran")]
        public void IsEmptyWithNoOperations()
        {
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(10, 1000, new CosmosJsonDotNetSerializer(), this.Executor);
            Assert.IsTrue(batchAsyncBatcher.IsEmpty);
        }

        [TestMethod]
        [Owner("maquaran")]
        public async Task IsNotEmptyWithOperations()
        {
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(1, 1000, new CosmosJsonDotNetSerializer(), this.Executor);
            Assert.IsTrue(await batchAsyncBatcher.TryAddAsync(new BatchAsyncOperationContext(string.Empty, this.ItemBatchOperation)));
            Assert.IsFalse(batchAsyncBatcher.IsEmpty);
        }

        [TestMethod]
        [Owner("maquaran")]
        public async Task CannotAddToDisposedBatch()
        {
            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(1, 1000, new CosmosJsonDotNetSerializer(), this.Executor);
            Assert.IsTrue(await batchAsyncBatcher.TryAddAsync(new BatchAsyncOperationContext(string.Empty, this.ItemBatchOperation)));
            batchAsyncBatcher.Dispose();
            await Assert.ThrowsExceptionAsync<ObjectDisposedException>(() => batchAsyncBatcher.TryAddAsync(new BatchAsyncOperationContext(string.Empty, this.ItemBatchOperation)));
        }

        [TestMethod]
        [Owner("maquaran")]
        public async Task RetryOnSplit()
        {
            int executeCount = 0;
            Func<IReadOnlyList<BatchAsyncOperationContext>, CancellationToken, Task<PartitionKeyBatchResponse>> executor = (IReadOnlyList<BatchAsyncOperationContext> operations, CancellationToken cancellationToken) =>
            {
                if (executeCount ++ == 0)
                {
                    return this.ExecutorWithSplit(operations, cancellationToken);
                }

                return this.Executor(operations, cancellationToken);
            };

            BatchAsyncBatcher batchAsyncBatcher = new BatchAsyncBatcher(1, 1000, new CosmosJsonDotNetSerializer(), executor);
            Assert.IsTrue(await batchAsyncBatcher.TryAddAsync(new BatchAsyncOperationContext(string.Empty, this.ItemBatchOperation)));
            await batchAsyncBatcher.DispatchAsync();
            Assert.AreEqual(2, executeCount);
        }
    }
}
