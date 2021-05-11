//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
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

        private readonly List<ITrace> traces;

        public ItemBatchOperationContext(
            string partitionKeyRangeId,
            ITrace trace,
            IDocumentClientRetryPolicy retryPolicy = null)
        {
            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            this.PartitionKeyRangeId = partitionKeyRangeId ?? throw new ArgumentNullException(nameof(partitionKeyRangeId));
            this.traces = new List<ITrace>
            {
                trace
            };
            this.retryPolicy = retryPolicy;
        }

        /// <summary>
        /// Based on the Retry Policy, if a failed response should retry.
        /// </summary>
        public async Task<ShouldRetryResult> ShouldRetryAsync(
            TransactionalBatchOperationResult batchOperationResult,
            CancellationToken cancellationToken)
        {
            if (this.retryPolicy == null
                || batchOperationResult.IsSuccessStatusCode)
            {
                return ShouldRetryResult.NoRetry();
            }

            ResponseMessage responseMessage = batchOperationResult.ToResponseMessage();
            ShouldRetryResult shouldRetry = await this.retryPolicy.ShouldRetryAsync(responseMessage, cancellationToken);
            if (shouldRetry.ShouldRetry)
            {
                this.traces.Add(batchOperationResult.Trace);
            }

            return shouldRetry;
        }

        public void Complete(
            BatchAsyncBatcher completer,
            TransactionalBatchOperationResult result)
        {
            if (this.AssertBatcher(completer))
            {
                this.traces.Add(result.Trace);
                result.Trace = TraceJoiner.JoinTraces(this.traces);
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

        public void ReRouteOperation(
            string newPartitionKeyRangeId,
            ITrace trace)
        {
            this.PartitionKeyRangeId = newPartitionKeyRangeId;
            this.traces.Add(trace);
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
