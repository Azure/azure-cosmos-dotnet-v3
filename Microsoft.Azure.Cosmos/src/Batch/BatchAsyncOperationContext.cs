//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;

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
            Debug.Assert(this.CurrentBatcher == null || completer == this.CurrentBatcher);
            this.taskCompletionSource.SetResult(result);
            this.Dispose();
        }

        public void Fail(
            BatchAsyncBatcher completer,
            Exception exception)
        {
            Debug.Assert(this.CurrentBatcher == null || completer == this.CurrentBatcher);
            this.taskCompletionSource.SetException(exception);
            this.Dispose();
        }

        public void Dispose()
        {
            this.Operation.Dispose();
            this.CurrentBatcher = null;
        }
    }
}
