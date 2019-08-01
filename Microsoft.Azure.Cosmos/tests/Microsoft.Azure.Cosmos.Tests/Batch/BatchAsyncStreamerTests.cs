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

    [TestClass]
    public class BatchAsyncStreamerTests
    {
        private const int DispatchTimerInSeconds = 5;
        private const int MaxBatchByteSize = 100000;
        private static Exception expectedException = new Exception();
        private ItemBatchOperation ItemBatchOperation = new ItemBatchOperation(OperationType.Create, 0, "0");
        private TimerPool TimerPool = new TimerPool(1);

        // Executor just returns a reponse matching the Id with Etag
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

        [DataTestMethod]
        [Owner("maquaran")]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        [DataRow(0)]
        [DataRow(-1)]
        public void ValidatesSize(int size)
        {
            BatchAsyncStreamer batchAsyncStreamer = new BatchAsyncStreamer(size, MaxBatchByteSize, DispatchTimerInSeconds, this.TimerPool, new CosmosJsonDotNetSerializer(), this.Executor);
        }

        [DataTestMethod]
        [Owner("maquaran")]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        [DataRow(0)]
        [DataRow(-1)]
        public void ValidatesDispatchTimer(int dispatchTimerInSeconds)
        {
            BatchAsyncStreamer batchAsyncStreamer = new BatchAsyncStreamer(1, MaxBatchByteSize, dispatchTimerInSeconds, this.TimerPool, new CosmosJsonDotNetSerializer(), this.Executor);
        }

        [TestMethod]
        [Owner("maquaran")]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ValidatesExecutor()
        {
            BatchAsyncStreamer batchAsyncStreamer = new BatchAsyncStreamer(1, MaxBatchByteSize, DispatchTimerInSeconds, this.TimerPool, new CosmosJsonDotNetSerializer(), null);
        }

        [TestMethod]
        [Owner("maquaran")]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ValidatesSerializer()
        {
            BatchAsyncStreamer batchAsyncStreamer = new BatchAsyncStreamer(1, MaxBatchByteSize, DispatchTimerInSeconds, this.TimerPool, null, this.Executor);
        }

        [TestMethod]
        [Owner("maquaran")]
        public async Task ExceptionsOnBatchBubbleUpAsync()
        {
            BatchAsyncStreamer batchAsyncStreamer = new BatchAsyncStreamer(2, MaxBatchByteSize, DispatchTimerInSeconds, this.TimerPool, new CosmosJsonDotNetSerializer(), this.ExecutorWithFailure);
            var context = CreateContext(this.ItemBatchOperation);
            await batchAsyncStreamer.AddAsync(context);
            Exception capturedException = await Assert.ThrowsExceptionAsync<Exception>(() => context.Task);
            Assert.AreEqual(expectedException, capturedException);
        }

        [TestMethod]
        [Owner("maquaran")]
        public async Task TimerDispatchesAsync()
        {
            // Bigger batch size than the amount of operations, timer should dispatch
            BatchAsyncStreamer batchAsyncStreamer = new BatchAsyncStreamer(2, MaxBatchByteSize, DispatchTimerInSeconds, this.TimerPool, new CosmosJsonDotNetSerializer(), this.Executor);
            var context = CreateContext(this.ItemBatchOperation);
            await batchAsyncStreamer.AddAsync(context);
            BatchOperationResult result = await context.Task;

            Assert.AreEqual(this.ItemBatchOperation.Id, result.ETag);
        }

        [TestMethod]
        [Owner("maquaran")]
        public async Task DispatchesAsync()
        {
            // Expect all operations to complete as their batches get dispached
            BatchAsyncStreamer batchAsyncStreamer = new BatchAsyncStreamer(2, MaxBatchByteSize, DispatchTimerInSeconds, this.TimerPool, new CosmosJsonDotNetSerializer(), this.Executor);
            List<Task<BatchOperationResult>> contexts = new List<Task<BatchOperationResult>>(10);
            for (int i = 0; i < 10; i++)
            {
                var context = CreateContext(new ItemBatchOperation(OperationType.Create, i, i.ToString()));
                await batchAsyncStreamer.AddAsync(context);
                contexts.Add(context.Task);
            }

            await Task.WhenAll(contexts);

            for (int i = 0; i < 10; i++)
            {
                Task<BatchOperationResult> context = contexts[i];
                Assert.AreEqual(TaskStatus.RanToCompletion, context.Status);
                BatchOperationResult result = await context;
                Assert.AreEqual(i.ToString(), result.ETag);
            }
        }

        [TestMethod]
        [Owner("maquaran")]
        public async Task DisposeAsyncShouldDisposeBatcher()
        {
            // Expect all operations to complete as their batches get dispached
            BatchAsyncStreamer batchAsyncStreamer = new BatchAsyncStreamer(2, MaxBatchByteSize, DispatchTimerInSeconds, this.TimerPool, new CosmosJsonDotNetSerializer(), this.Executor);
            List<Task<BatchOperationResult>> contexts = new List<Task<BatchOperationResult>>(10);
            for (int i = 0; i < 10; i++)
            {
                var context = CreateContext(new ItemBatchOperation(OperationType.Create, i, i.ToString()));
                await batchAsyncStreamer.AddAsync(context);
                contexts.Add(context.Task);
            }

            await Task.WhenAll(contexts);

            await batchAsyncStreamer.DisposeAsync();
            var newContext = CreateContext(new ItemBatchOperation(OperationType.Create, 0, "0"));
            // Disposed batcher's internal cancellation was signaled
            await Assert.ThrowsExceptionAsync<TaskCanceledException>(() => batchAsyncStreamer.AddAsync(newContext));
        }

        private static BatchAsyncOperationContext CreateContext(ItemBatchOperation operation) => new BatchAsyncOperationContext(string.Empty, operation);
    }
}
