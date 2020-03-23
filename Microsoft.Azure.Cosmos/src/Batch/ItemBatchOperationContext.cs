//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Context for a particular Batch operation.
    /// </summary>
    internal sealed class ItemBatchOperationContext : IDisposable
    {
        public string PartitionKeyRangeId { get; }

        public BatchAsyncBatcher CurrentBatcher { get; set; }

        public Task<TransactionalBatchOperationResult> OperationTask => this.taskCompletionSource.Task;

        private readonly IDocumentClientRetryPolicy retryPolicy;

        private readonly TaskCompletionSource<TransactionalBatchOperationResult> taskCompletionSource = new TaskCompletionSource<TransactionalBatchOperationResult>();

        public ItemBatchOperationContext(
            string partitionKeyRangeId,
            IDocumentClientRetryPolicy retryPolicy = null)
        {
            this.PartitionKeyRangeId = partitionKeyRangeId;
            this.retryPolicy = retryPolicy;
        }

        /// <summary>
        /// Based on the Retry Policy, if a failed response should retry.
        /// </summary>
        public Task<ShouldRetryResult> ShouldRetryAsync(
            TransactionalBatchOperationResult batchOperationResult,
            CancellationToken cancellationToken)
        {
            if (this.retryPolicy == null
                || batchOperationResult.IsSuccessStatusCode)
            {
                return Task.FromResult(ShouldRetryResult.NoRetry());
            }

            ResponseMessage responseMessage = batchOperationResult.ToResponseMessage();
            return this.retryPolicy.ShouldRetryAsync(responseMessage, cancellationToken);
        }

        public void Complete(
            BatchAsyncBatcher completer,
            TransactionalBatchOperationResult result)
        {
            if (this.AssertBatcher(completer))
            {
                this.taskCompletionSource.SetResult(result);
            }

            this.Dispose();
        }

        public void Fail(
            BatchAsyncBatcher completer,
            Exception exception)
        {
            if (this.AssertBatcher(completer, exception))
            {
                this.taskCompletionSource.SetException(exception);
            }

            this.Dispose();
        }

        public void Dispose()
        {
            this.CurrentBatcher = null;
        }

        private bool AssertBatcher(
            BatchAsyncBatcher completer,
            Exception innerException = null)
        {
            if (!object.ReferenceEquals(completer, this.CurrentBatcher))
            {
                DefaultTrace.TraceCritical($"Operation was completed by incorrect batcher.");
                this.taskCompletionSource.SetException(new Exception($"Operation was completed by incorrect batcher.", innerException));
                return false;
            }

            return true;
        }
    }
}
