//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Context for a particular Batch operation.
    /// </summary>
    internal class ItemBatchOperationContext : IDisposable
    {
        public string PartitionKeyRangeId { get; private set; }

        public BatchAsyncBatcher CurrentBatcher { get; set; }

        public Task<TransactionalBatchOperationResult> OperationTask => this.taskCompletionSource.Task;

        private readonly IDocumentClientRetryPolicy retryPolicy;

        private readonly TaskCompletionSource<TransactionalBatchOperationResult> taskCompletionSource = new TaskCompletionSource<TransactionalBatchOperationResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        public ItemBatchOperationContext(
            string partitionKeyRangeId,
            ITrace trace,
            IDocumentClientRetryPolicy retryPolicy = null)
        {
            this.PartitionKeyRangeId = partitionKeyRangeId;
            this.Trace = trace;
            this.retryPolicy = retryPolicy;
        }

        public ITrace Trace { get; private set; }

        /// <summary>
        /// Based on the Retry Policy, if a failed response should retry.
        /// </summary>
        public Task<ShouldRetryResult> ShouldRetryAsync(
            TransactionalBatchOperationResult batchOperationResult,
            CancellationToken cancellationToken)
        {
            // append Traces
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
            // append traces
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

        public void ReRouteOperation(string newPartitionKeyRangeId)
        {
            this.PartitionKeyRangeId = newPartitionKeyRangeId;
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
