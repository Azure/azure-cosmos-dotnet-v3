//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class BatchAsyncOperationContextTests
    {
        [TestMethod]
        public void PartitionKeyRangeIdIsSetOnInitialization()
        {
            string expectedPkRangeId = Guid.NewGuid().ToString();
            ItemBatchOperation operation = new ItemBatchOperation(OperationType.Create, 0, Cosmos.PartitionKey.Null);
            ItemBatchOperationContext batchAsyncOperationContext = new ItemBatchOperationContext(expectedPkRangeId);
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
            ItemBatchOperationContext batchAsyncOperationContext = new ItemBatchOperationContext(string.Empty);
            operation.AttachContext(batchAsyncOperationContext);

            Assert.IsNotNull(batchAsyncOperationContext.OperationTask);
            Assert.AreEqual(batchAsyncOperationContext, operation.Context);
            Assert.AreEqual(TaskStatus.WaitingForActivation, batchAsyncOperationContext.OperationTask.Status);
        }

        [TestMethod]
        public async Task TaskResultIsSetOnCompleteAsync()
        {
            ItemBatchOperation operation = new ItemBatchOperation(OperationType.Create, 0, Cosmos.PartitionKey.Null);
            ItemBatchOperationContext batchAsyncOperationContext = new ItemBatchOperationContext(string.Empty);
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
            ItemBatchOperationContext batchAsyncOperationContext = new ItemBatchOperationContext(string.Empty);
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
            operation.AttachContext(new ItemBatchOperationContext(string.Empty));
            Assert.ThrowsException<InvalidOperationException>(() => operation.AttachContext(new ItemBatchOperationContext(string.Empty)));
        }

        [TestMethod]
        public async Task ShouldRetry_NoPolicy()
        {
            TransactionalBatchOperationResult result = new TransactionalBatchOperationResult(HttpStatusCode.OK);
            ItemBatchOperation operation = new ItemBatchOperation(OperationType.Create, 0, Cosmos.PartitionKey.Null);
            operation.AttachContext(new ItemBatchOperationContext(string.Empty));
            ShouldRetryResult shouldRetryResult = await operation.Context.ShouldRetryAsync(result, default(CancellationToken));
            Assert.IsFalse(shouldRetryResult.ShouldRetry);
        }

        [TestMethod]
        public async Task ShouldRetry_WithPolicy_OnSuccess()
        {
            IDocumentClientRetryPolicy retryPolicy = new BulkPartitionKeyRangeGoneRetryPolicy(
                new ResourceThrottleRetryPolicy(1));
            TransactionalBatchOperationResult result = new TransactionalBatchOperationResult(HttpStatusCode.OK);
            ItemBatchOperation operation = new ItemBatchOperation(OperationType.Create, 0, Cosmos.PartitionKey.Null);
            operation.AttachContext(new ItemBatchOperationContext(string.Empty, retryPolicy));
            ShouldRetryResult shouldRetryResult = await operation.Context.ShouldRetryAsync(result, default(CancellationToken));
            Assert.IsFalse(shouldRetryResult.ShouldRetry);
        }

        [TestMethod]
        public async Task ShouldRetry_WithPolicy_On429()
        {
            IDocumentClientRetryPolicy retryPolicy = new BulkPartitionKeyRangeGoneRetryPolicy(
                new ResourceThrottleRetryPolicy(1));
            TransactionalBatchOperationResult result = new TransactionalBatchOperationResult((HttpStatusCode)StatusCodes.TooManyRequests);
            ItemBatchOperation operation = new ItemBatchOperation(OperationType.Create, 0, Cosmos.PartitionKey.Null);
            operation.AttachContext(new ItemBatchOperationContext(string.Empty, retryPolicy));
            ShouldRetryResult shouldRetryResult = await operation.Context.ShouldRetryAsync(result, default(CancellationToken));
            Assert.IsTrue(shouldRetryResult.ShouldRetry);
        }

        [TestMethod]
        public async Task ShouldRetry_WithPolicy_OnSplit()
        {
            IDocumentClientRetryPolicy retryPolicy = new BulkPartitionKeyRangeGoneRetryPolicy(
                new ResourceThrottleRetryPolicy(1));
            TransactionalBatchOperationResult result = new TransactionalBatchOperationResult(HttpStatusCode.Gone) { SubStatusCode = SubStatusCodes.PartitionKeyRangeGone };
            ItemBatchOperation operation = new ItemBatchOperation(OperationType.Create, 0, Cosmos.PartitionKey.Null);
            operation.AttachContext(new ItemBatchOperationContext(string.Empty, retryPolicy));
            ShouldRetryResult shouldRetryResult = await operation.Context.ShouldRetryAsync(result, default(CancellationToken));
            Assert.IsTrue(shouldRetryResult.ShouldRetry);
        }

        [TestMethod]
        public async Task ShouldRetry_WithPolicy_OnCompletingSplit()
        {
            IDocumentClientRetryPolicy retryPolicy = new BulkPartitionKeyRangeGoneRetryPolicy(
                new ResourceThrottleRetryPolicy(1));
            TransactionalBatchOperationResult result = new TransactionalBatchOperationResult(HttpStatusCode.Gone) { SubStatusCode = SubStatusCodes.CompletingSplit };
            ItemBatchOperation operation = new ItemBatchOperation(OperationType.Create, 0, Cosmos.PartitionKey.Null);
            operation.AttachContext(new ItemBatchOperationContext(string.Empty, retryPolicy));
            ShouldRetryResult shouldRetryResult = await operation.Context.ShouldRetryAsync(result, default(CancellationToken));
            Assert.IsTrue(shouldRetryResult.ShouldRetry);
        }
    }
}
