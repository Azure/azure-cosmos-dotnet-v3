//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class BatchAsyncOperationContextTests
    {
        [TestMethod]
        public async Task TraceIsJoinedOnCompletionWithRetry()
        {
            IDocumentClientRetryPolicy retryPolicy = new BulkExecutionRetryPolicy(
                Mock.Of<ContainerInternal>(),
                OperationType.Read,
                new ResourceThrottleRetryPolicy(1));

            Trace rootTrace = Trace.GetRootTrace(name: "RootTrace");

            ItemBatchOperation operation = new ItemBatchOperation(OperationType.Create, 0, Cosmos.PartitionKey.Null);

            // Start with the base trace
            ItemBatchOperationContext batchAsyncOperationContext = new ItemBatchOperationContext(Guid.NewGuid().ToString(), rootTrace, retryPolicy);
            operation.AttachContext(batchAsyncOperationContext);

            // Simulate a retry scenario that should append to the context traces
            Trace retryTrace = Trace.GetRootTrace(name: "TransportTrace");
            TransactionalBatchOperationResult retryResult = new TransactionalBatchOperationResult(HttpStatusCode.TooManyRequests)
            {
                Trace = retryTrace
            };
            ShouldRetryResult shouldRetryResult = await batchAsyncOperationContext.ShouldRetryAsync(retryResult, default);
            Assert.IsTrue(shouldRetryResult.ShouldRetry);

            // Simulate the completion that should append to the context traces
            Trace transportTrace = Trace.GetRootTrace(name: "TransportTrace");
            TransactionalBatchOperationResult result = new TransactionalBatchOperationResult(HttpStatusCode.OK)
            {
                Trace = transportTrace
            };

            batchAsyncOperationContext.Complete(null, result);

            Assert.AreEqual(result, await batchAsyncOperationContext.OperationTask);
            Assert.AreEqual(2, result.Trace.Children.Count, "The final trace should have the initial trace, plus the retries, plus the final trace");
            Assert.AreEqual(rootTrace, result.Trace, "The first trace child should be the initial root");
            Assert.AreEqual(retryTrace, result.Trace.Children[0], "The second trace child should be the one from the retry");
            Assert.AreEqual(transportTrace, result.Trace.Children[1], "The third trace child should be the one from the final result");
        }

        [TestMethod]
        public async Task TraceIsJoinedOnCompletionWithoutRetry()
        {
            IDocumentClientRetryPolicy retryPolicy = new BulkExecutionRetryPolicy(
                Mock.Of<ContainerInternal>(),
                OperationType.Read,
                new ResourceThrottleRetryPolicy(1));

            Trace rootTrace = Trace.GetRootTrace(name: "RootTrace");

            ItemBatchOperation operation = new ItemBatchOperation(OperationType.Create, 0, Cosmos.PartitionKey.Null);

            // Start with the base trace
            ItemBatchOperationContext batchAsyncOperationContext = new ItemBatchOperationContext(Guid.NewGuid().ToString(), rootTrace, retryPolicy);
            operation.AttachContext(batchAsyncOperationContext);

            // Simulate a retry scenario that should not append to the context traces
            Trace retryTrace = Trace.GetRootTrace(name: "TransportTrace");
            TransactionalBatchOperationResult retryResult = new TransactionalBatchOperationResult(HttpStatusCode.Forbidden)
            {
                Trace = retryTrace
            };
            ShouldRetryResult shouldRetryResult = await batchAsyncOperationContext.ShouldRetryAsync(retryResult, default);
            Assert.IsFalse(shouldRetryResult.ShouldRetry);

            // Simulate the completion that should append to the context traces
            Trace transportTrace = Trace.GetRootTrace(name: "TransportTrace");
            TransactionalBatchOperationResult result = new TransactionalBatchOperationResult(HttpStatusCode.OK)
            {
                Trace = transportTrace
            };

            batchAsyncOperationContext.Complete(null, result);

            Assert.AreEqual(result, await batchAsyncOperationContext.OperationTask);
            Assert.AreEqual(1, result.Trace.Children.Count, "The final trace should have the initial trace, plus the final trace, since the result is not retried, it should not capture it");
            Assert.AreEqual(rootTrace, result.Trace, "The first trace child should be the initial root");
            Assert.AreEqual(transportTrace, result.Trace.Children[0], "The second trace child should be the one from the final result");
        }

        [TestMethod]
        public void PartitionKeyRangeIdIsSetOnInitialization()
        {
            string expectedPkRangeId = Guid.NewGuid().ToString();
            ItemBatchOperation operation = new ItemBatchOperation(OperationType.Create, 0, Cosmos.PartitionKey.Null);
            ItemBatchOperationContext batchAsyncOperationContext = new ItemBatchOperationContext(expectedPkRangeId, NoOpTrace.Singleton);
            operation.AttachContext(batchAsyncOperationContext);

            Assert.IsNotNull(batchAsyncOperationContext.OperationTask);
            Assert.AreEqual(batchAsyncOperationContext, operation.Context);
            Assert.AreEqual(expectedPkRangeId, batchAsyncOperationContext.PartitionKeyRangeId);
            Assert.AreEqual(TaskStatus.WaitingForActivation, batchAsyncOperationContext.OperationTask.Status);
        }

        [TestMethod]
        public void TaskIsCreatedOnInitialization()
        {
            ItemBatchOperation operation = new ItemBatchOperation(OperationType.Create, 0, Cosmos.PartitionKey.Null);
            ItemBatchOperationContext batchAsyncOperationContext = new ItemBatchOperationContext(string.Empty, NoOpTrace.Singleton);
            operation.AttachContext(batchAsyncOperationContext);

            Assert.IsNotNull(batchAsyncOperationContext.OperationTask);
            Assert.AreEqual(batchAsyncOperationContext, operation.Context);
            Assert.AreEqual(TaskStatus.WaitingForActivation, batchAsyncOperationContext.OperationTask.Status);
        }

        [TestMethod]
        public async Task TaskResultIsSetOnCompleteAsync()
        {
            ItemBatchOperation operation = new ItemBatchOperation(OperationType.Create, 0, Cosmos.PartitionKey.Null);
            ItemBatchOperationContext batchAsyncOperationContext = new ItemBatchOperationContext(string.Empty, NoOpTrace.Singleton);
            operation.AttachContext(batchAsyncOperationContext);

            TransactionalBatchOperationResult expected = new TransactionalBatchOperationResult(HttpStatusCode.OK);

            batchAsyncOperationContext.Complete(null, expected);

            Assert.AreEqual(expected, await batchAsyncOperationContext.OperationTask);
            Assert.AreEqual(TaskStatus.RanToCompletion, batchAsyncOperationContext.OperationTask.Status);
        }

        [TestMethod]
        public async Task ExceptionIsSetOnFailAsync()
        {
            Exception failure = new Exception("It failed");
            ItemBatchOperation operation = new ItemBatchOperation(OperationType.Create, 0, Cosmos.PartitionKey.Null);
            ItemBatchOperationContext batchAsyncOperationContext = new ItemBatchOperationContext(string.Empty, NoOpTrace.Singleton);
            operation.AttachContext(batchAsyncOperationContext);

            batchAsyncOperationContext.Fail(null, failure);

            Exception capturedException = await Assert.ThrowsExceptionAsync<Exception>(() => batchAsyncOperationContext.OperationTask);
            Assert.AreEqual(failure, capturedException);
            Assert.AreEqual(TaskStatus.Faulted, batchAsyncOperationContext.OperationTask.Status);
        }

        [TestMethod]
        public void CannotAttachMoreThanOnce()
        {
            ItemBatchOperation operation = new ItemBatchOperation(OperationType.Create, 0, Cosmos.PartitionKey.Null);
            operation.AttachContext(new ItemBatchOperationContext(string.Empty, NoOpTrace.Singleton));
            Assert.ThrowsException<InvalidOperationException>(() => operation.AttachContext(new ItemBatchOperationContext(string.Empty, NoOpTrace.Singleton)));
        }

        [TestMethod]
        public async Task ShouldRetry_NoPolicy()
        {
            TransactionalBatchOperationResult result = new TransactionalBatchOperationResult(HttpStatusCode.OK);
            ItemBatchOperation operation = new ItemBatchOperation(OperationType.Create, 0, Cosmos.PartitionKey.Null);
            operation.AttachContext(new ItemBatchOperationContext(string.Empty, NoOpTrace.Singleton));
            ShouldRetryResult shouldRetryResult = await operation.Context.ShouldRetryAsync(result, default);
            Assert.IsFalse(shouldRetryResult.ShouldRetry);
        }

        [TestMethod]
        public async Task ShouldRetry_WithPolicy_OnSuccess()
        {
            IDocumentClientRetryPolicy retryPolicy = new BulkExecutionRetryPolicy(
                Mock.Of<ContainerInternal>(),
                OperationType.Read,
                new ResourceThrottleRetryPolicy(1));
            TransactionalBatchOperationResult result = new TransactionalBatchOperationResult(HttpStatusCode.OK);
            ItemBatchOperation operation = new ItemBatchOperation(OperationType.Create, 0, Cosmos.PartitionKey.Null);
            operation.AttachContext(new ItemBatchOperationContext(string.Empty, NoOpTrace.Singleton, retryPolicy));
            ShouldRetryResult shouldRetryResult = await operation.Context.ShouldRetryAsync(result, default);
            Assert.IsFalse(shouldRetryResult.ShouldRetry);
        }

        [TestMethod]
        public async Task ShouldRetry_WithPolicy_On429()
        {
            IDocumentClientRetryPolicy retryPolicy = new BulkExecutionRetryPolicy(
                Mock.Of<ContainerInternal>(),
                OperationType.Read,
                new ResourceThrottleRetryPolicy(1));
            TransactionalBatchOperationResult result = new TransactionalBatchOperationResult((HttpStatusCode)StatusCodes.TooManyRequests);
            ItemBatchOperation operation = new ItemBatchOperation(OperationType.Create, 0, Cosmos.PartitionKey.Null);
            operation.AttachContext(new ItemBatchOperationContext(string.Empty, NoOpTrace.Singleton, retryPolicy));
            ShouldRetryResult shouldRetryResult = await operation.Context.ShouldRetryAsync(result, default);
            Assert.IsTrue(shouldRetryResult.ShouldRetry);
        }

        [TestMethod]
        public async Task ShouldRetry_WithPolicy_On413_3402()
        {
            IDocumentClientRetryPolicy retryPolicy = new BulkExecutionRetryPolicy(
                Mock.Of<ContainerInternal>(),
                OperationType.Read,
                new ResourceThrottleRetryPolicy(1));
            TransactionalBatchOperationResult result = new TransactionalBatchOperationResult(HttpStatusCode.RequestEntityTooLarge) { SubStatusCode = (SubStatusCodes)3402 };
            ItemBatchOperation operation = new ItemBatchOperation(OperationType.Create, 0, Cosmos.PartitionKey.Null);
            operation.AttachContext(new ItemBatchOperationContext(string.Empty, NoOpTrace.Singleton, retryPolicy));
            ShouldRetryResult shouldRetryResult = await operation.Context.ShouldRetryAsync(result, default);
            Assert.IsTrue(shouldRetryResult.ShouldRetry);
        }

        [TestMethod]
        public async Task ShouldRetry_WithPolicy_On413_0()
        {
            IDocumentClientRetryPolicy retryPolicy = new BulkExecutionRetryPolicy(
                Mock.Of<ContainerInternal>(),
                OperationType.Create,
                new ResourceThrottleRetryPolicy(1));
            TransactionalBatchOperationResult result = new TransactionalBatchOperationResult(HttpStatusCode.RequestEntityTooLarge);
            ItemBatchOperation operation = new ItemBatchOperation(OperationType.Create, 0, Cosmos.PartitionKey.Null);
            operation.AttachContext(new ItemBatchOperationContext(string.Empty, NoOpTrace.Singleton, retryPolicy));
            ShouldRetryResult shouldRetryResult = await operation.Context.ShouldRetryAsync(result, default);
            Assert.IsFalse(shouldRetryResult.ShouldRetry);
        }

        [TestMethod]
        public async Task ShouldRetry_WithPolicy_OnSplit()
        {
            IDocumentClientRetryPolicy retryPolicy = new BulkExecutionRetryPolicy(
                GetSplitEnabledContainer(),
                OperationType.Read,
                new ResourceThrottleRetryPolicy(1));
            TransactionalBatchOperationResult result = new TransactionalBatchOperationResult(HttpStatusCode.Gone) { SubStatusCode = SubStatusCodes.PartitionKeyRangeGone };
            ItemBatchOperation operation = new ItemBatchOperation(OperationType.Create, 0, Cosmos.PartitionKey.Null);
            operation.AttachContext(new ItemBatchOperationContext(string.Empty, NoOpTrace.Singleton, retryPolicy));
            ShouldRetryResult shouldRetryResult = await operation.Context.ShouldRetryAsync(result, default);
            Assert.IsTrue(shouldRetryResult.ShouldRetry);
        }

        [TestMethod]
        public async Task ShouldRetry_WithPolicy_OnCompletingSplit()
        {
            IDocumentClientRetryPolicy retryPolicy = new BulkExecutionRetryPolicy(
                GetSplitEnabledContainer(),
                OperationType.Read,
                new ResourceThrottleRetryPolicy(1));
            TransactionalBatchOperationResult result = new TransactionalBatchOperationResult(HttpStatusCode.Gone) { SubStatusCode = SubStatusCodes.CompletingSplit };
            ItemBatchOperation operation = new ItemBatchOperation(OperationType.Create, 0, Cosmos.PartitionKey.Null);
            operation.AttachContext(new ItemBatchOperationContext(string.Empty, NoOpTrace.Singleton, retryPolicy));
            ShouldRetryResult shouldRetryResult = await operation.Context.ShouldRetryAsync(result, default);
            Assert.IsTrue(shouldRetryResult.ShouldRetry);
        }

        [TestMethod]
        public async Task ShouldRetry_WithPolicy_OnCompletingPartitionMigration()
        {
            IDocumentClientRetryPolicy retryPolicy = new BulkExecutionRetryPolicy(
                GetSplitEnabledContainer(),
                OperationType.Read,
                new ResourceThrottleRetryPolicy(1));
            TransactionalBatchOperationResult result = new TransactionalBatchOperationResult(HttpStatusCode.Gone) { SubStatusCode = SubStatusCodes.CompletingPartitionMigration };
            ItemBatchOperation operation = new ItemBatchOperation(OperationType.Create, 0, Cosmos.PartitionKey.Null);
            operation.AttachContext(new ItemBatchOperationContext(string.Empty, NoOpTrace.Singleton, retryPolicy));
            ShouldRetryResult shouldRetryResult = await operation.Context.ShouldRetryAsync(result, default);
            Assert.IsTrue(shouldRetryResult.ShouldRetry);
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