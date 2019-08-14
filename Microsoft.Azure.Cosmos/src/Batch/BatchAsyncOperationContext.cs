//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;

    /// <summary>
    /// Context for a particular Batch operation.
    /// </summary>
    internal class BatchAsyncOperationContext : IDisposable
    {
        public string PartitionKeyRangeId { get; }

        public ItemBatchOperation Operation { get; }

        public BatchAsyncBatcher CurrentBatcher { get; set; }

        public Task<BatchOperationResult> Task => this.taskCompletionSource.Task;

        private TaskCompletionSource<BatchOperationResult> taskCompletionSource = new TaskCompletionSource<BatchOperationResult>();

        public BatchAsyncOperationContext(
            string partitionKeyRangeId,
            ItemBatchOperation operation)
        {
            this.Operation = operation;
            this.PartitionKeyRangeId = partitionKeyRangeId;
        }

        public void Complete(
            BatchAsyncBatcher completer,
            BatchOperationResult result)
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
            if (this.AssertBatcher(completer))
            {
                this.taskCompletionSource.SetException(exception);
            }

            this.Dispose();
        }

        public void Dispose()
        {
            this.Operation.Dispose();
            this.CurrentBatcher = null;
        }

        private bool AssertBatcher(BatchAsyncBatcher completer)
        {
            if (!object.ReferenceEquals(completer, this.CurrentBatcher))
            {
                DefaultTrace.TraceCritical($"Operation {this.Operation.Id} was completed by incorrect batcher.");
                this.taskCompletionSource.SetException(new Exception($"Operation {this.Operation.Id} was completed by incorrect batcher."));
                return false;
            }

            return true;
        }
    }
}
