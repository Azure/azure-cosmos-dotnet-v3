//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Context for a particular Batch operation.
    /// </summary>
    internal class BatchAsyncOperationContext
    {
        public string PartitionKeyRangeId { get; }

        public ItemBatchOperation Operation { get; }

        public Task<BatchOperationResult> Task => this.taskCompletionSource.Task;

        private TaskCompletionSource<BatchOperationResult> taskCompletionSource = new TaskCompletionSource<BatchOperationResult>();

        public BatchAsyncOperationContext(
            string partitionKeyRangeId,
            ItemBatchOperation operation)
        {
            this.Operation = operation;
            this.PartitionKeyRangeId = partitionKeyRangeId;
        }

        public void Complete(BatchOperationResult result)
        {
            this.taskCompletionSource.SetResult(result);
        }

        public void Fail(Exception exception)
        {
            this.taskCompletionSource.SetException(exception);
        }
    }
}
