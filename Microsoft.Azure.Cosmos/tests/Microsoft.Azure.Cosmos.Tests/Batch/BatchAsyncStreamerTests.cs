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
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class BatchAsyncStreamerTests
    {
        private const int MaxBatchByteSize = 100000;
        private const int defaultMaxDegreeOfConcurrency = 10;
        private static readonly Exception expectedException = new Exception();
        private readonly ItemBatchOperation ItemBatchOperation = new ItemBatchOperation(OperationType.Create, 0, Cosmos.PartitionKey.Null, "0");
        private readonly TimerWheel TimerWheel = TimerWheel.CreateTimerWheel(TimeSpan.FromMilliseconds(50), 20);
        private readonly SemaphoreSlim limiter = new SemaphoreSlim(1, defaultMaxDegreeOfConcurrency);

        // Executor just returns a reponse matching the Id with Etag
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
                    trace: NoOpTrace.Singleton,
                    cancellationToken: cancellationToken);

                await Task.Delay(20);

                TransactionalBatchResponse batchresponse = await TransactionalBatchResponse.FromResponseMessageAsync(
                    new ResponseMessage(HttpStatusCode.OK) { Content = responseContent },
                    batchRequest,
                    MockCosmosUtil.Serializer,
                    true,
                    NoOpTrace.Singleton,
                    CancellationToken.None);

                return new PartitionKeyRangeBatchExecutionResult(request.PartitionKeyRangeId, request.Operations, batchresponse);
            };

        private readonly BatchAsyncBatcherExecuteDelegate ExecutorWithFailure = (PartitionKeyRangeServerBatchRequest request, ITrace trace, CancellationToken cancellationToken) => throw expectedException;

        private readonly BatchAsyncBatcherRetryDelegate Retrier = (ItemBatchOperation operation, CancellationToken cancellation) => Task.CompletedTask;

        [DataTestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        [DataRow(0)]
        [DataRow(-1)]
        public void ValidatesSize(int size)
        {
            _ = new BatchAsyncStreamer(size, MaxBatchByteSize, this.TimerWheel, this.limiter, 1, MockCosmosUtil.Serializer, this.Executor, this.Retrier, this.GetMockClientContext());
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ValidatesExecutor()
        {
            _ = new BatchAsyncStreamer(1, MaxBatchByteSize, this.TimerWheel, this.limiter, 1, MockCosmosUtil.Serializer, null, this.Retrier, this.GetMockClientContext());
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ValidatesRetrier()
        {
            _ = new BatchAsyncStreamer(1, MaxBatchByteSize, this.TimerWheel, this.limiter, 1, MockCosmosUtil.Serializer, this.Executor, null, this.GetMockClientContext());
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ValidatesSerializer()
        {
            _ = new BatchAsyncStreamer(1, MaxBatchByteSize, this.TimerWheel, this.limiter, 1, null, this.Executor, this.Retrier, this.GetMockClientContext());
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ValidatesLimiter()
        {
            _ = new BatchAsyncStreamer(1, MaxBatchByteSize, this.TimerWheel, null, 1, null, this.Executor, this.Retrier, this.GetMockClientContext());
        }

        [TestMethod]
        public async Task ExceptionsOnBatchBubbleUpAsync()
        {
            BatchAsyncStreamer batchAsyncStreamer = new BatchAsyncStreamer(2, MaxBatchByteSize, this.TimerWheel, this.limiter, 1, MockCosmosUtil.Serializer, this.ExecutorWithFailure, this.Retrier, this.GetMockClientContext());
            ItemBatchOperationContext context = AttachContext(this.ItemBatchOperation);
            batchAsyncStreamer.Add(this.ItemBatchOperation);
            Exception capturedException = await Assert.ThrowsExceptionAsync<Exception>(() => context.OperationTask);
            Assert.AreEqual(expectedException, capturedException);
        }

        [TestMethod]
        public async Task TimerDispatchesAsync()
        {
            // Bigger batch size than the amount of operations, timer should dispatch
            BatchAsyncStreamer batchAsyncStreamer = new BatchAsyncStreamer(2, MaxBatchByteSize, this.TimerWheel, this.limiter, 1, MockCosmosUtil.Serializer, this.Executor, this.Retrier, this.GetMockClientContext());
            ItemBatchOperationContext context = AttachContext(this.ItemBatchOperation);
            batchAsyncStreamer.Add(this.ItemBatchOperation);
            TransactionalBatchOperationResult result = await context.OperationTask;

            Assert.AreEqual(this.ItemBatchOperation.Id, result.ETag);
        }

        [TestMethod]
        public async Task ValidatesCongestionControlAsync()
        {
            SemaphoreSlim newLimiter = new SemaphoreSlim(1, defaultMaxDegreeOfConcurrency);
            BatchAsyncStreamer batchAsyncStreamer = new BatchAsyncStreamer(2, MaxBatchByteSize, this.TimerWheel, newLimiter, defaultMaxDegreeOfConcurrency, MockCosmosUtil.Serializer, this.Executor, this.Retrier, this.GetMockClientContext());

            Assert.AreEqual(newLimiter.CurrentCount, 1);

            List<Task<TransactionalBatchOperationResult>> contexts = new List<Task<TransactionalBatchOperationResult>>(100);
            for (int i = 0; i < 600; i++)
            {
                ItemBatchOperation operation = new ItemBatchOperation(OperationType.Create, i, Cosmos.PartitionKey.Null, i.ToString());
                ItemBatchOperationContext context = AttachContext(operation);
                batchAsyncStreamer.Add(operation);
                contexts.Add(context.OperationTask);
            }

            // 300 batch request should atleast sum up to 1000 ms barrier with wait time of 20ms in executor
            await Task.WhenAll(contexts);

            await Task.Delay(2000);

            Assert.IsTrue(newLimiter.CurrentCount >= 2, "Count of threads that can enter into semaphore should increase atleast by 1");
        }

        [TestMethod]
        public async Task DispatchesAsync()
        {
            // Expect all operations to complete as their batches get dispached
            BatchAsyncStreamer batchAsyncStreamer = new BatchAsyncStreamer(
                2,
                MaxBatchByteSize,
                this.TimerWheel,
                this.limiter,
                1,
                MockCosmosUtil.Serializer,
                this.Executor,
                this.Retrier,
                this.GetMockClientContext());
            List<Task<TransactionalBatchOperationResult>> contexts = new List<Task<TransactionalBatchOperationResult>>(10);
            for (int i = 0; i < 10; i++)
            {
                ItemBatchOperation operation = new ItemBatchOperation(OperationType.Create, i, Cosmos.PartitionKey.Null, i.ToString());
                ItemBatchOperationContext context = AttachContext(operation);
                batchAsyncStreamer.Add(operation);
                contexts.Add(context.OperationTask);
            }

            await Task.WhenAll(contexts);

            for (int i = 0; i < 10; i++)
            {
                Task<TransactionalBatchOperationResult> context = contexts[i];
                Assert.AreEqual(TaskStatus.RanToCompletion, context.Status);
                TransactionalBatchOperationResult result = await context;
                Assert.AreEqual(i.ToString(), result.ETag);
            }
        }

        private static ItemBatchOperationContext AttachContext(ItemBatchOperation operation)
        {
            ItemBatchOperationContext context = new ItemBatchOperationContext(string.Empty, NoOpTrace.Singleton);
            operation.AttachContext(context);
            return context;
        }

        private CosmosClientContext GetMockClientContext()
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
    }
}