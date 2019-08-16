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
    internal class ItemBatchOperationContext : IDisposable
    {
        public string PartitionKeyRangeId { get; }

        public BatchAsyncBatcher CurrentBatcher { get; set; }

        public Task<BatchOperationResult> Task => this.taskCompletionSource.Task;

        private TaskCompletionSource<BatchOperationResult> taskCompletionSource = new TaskCompletionSource<BatchOperationResult>();

        public ItemBatchOperationContext(string partitionKeyRangeId)
        {
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
