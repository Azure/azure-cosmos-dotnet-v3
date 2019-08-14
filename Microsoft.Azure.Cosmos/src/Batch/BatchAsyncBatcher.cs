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

    /// <summary>
    /// Maintains a batch of operations and dispatches the batch through an executor. Maps results into the original operation contexts.
    /// </summary>
    /// <seealso cref="BatchAsyncOperationContext"/>
    internal class BatchAsyncBatcher
    {
        private readonly CosmosSerializer CosmosSerializer;
        private readonly List<BatchAsyncOperationContext> batchOperations;
        private readonly BatchAsyncBatcherExecuteDelegate executor;
        private readonly int maxBatchByteSize;
        private readonly int maxBatchOperationCount;
        private long currentSize = 0;
        private bool dispached = false;

        public bool IsEmpty => this.batchOperations.Count == 0;

        public BatchAsyncBatcher(
            int maxBatchOperationCount,
            int maxBatchByteSize,
            CosmosSerializer cosmosSerializer,
            BatchAsyncBatcherExecuteDelegate executor)
        {
            if (maxBatchOperationCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxBatchOperationCount));
            }

            if (maxBatchByteSize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxBatchByteSize));
            }

            if (executor == null)
            {
                throw new ArgumentNullException(nameof(executor));
            }

            if (cosmosSerializer == null)
            {
                throw new ArgumentNullException(nameof(cosmosSerializer));
            }

            this.batchOperations = new List<BatchAsyncOperationContext>(maxBatchOperationCount);
            this.executor = executor;
            this.maxBatchByteSize = maxBatchByteSize;
            this.maxBatchOperationCount = maxBatchOperationCount;
            this.CosmosSerializer = cosmosSerializer;
        }

        public virtual bool TryAdd(BatchAsyncOperationContext operationContext)
        {
            if (this.dispached)
            {
                DefaultTrace.TraceCritical($"Add operation attempted on dispatched batch.");
                return false;
            }

            if (operationContext == null)
            {
                throw new ArgumentNullException(nameof(operationContext));
            }

            if (this.batchOperations.Count == this.maxBatchOperationCount)
            {
                DefaultTrace.TraceVerbose($"Batch is full - Max operation count {this.maxBatchOperationCount} reached.");
                return false;
            }

            int itemByteSize = operationContext.Operation.GetApproximateSerializedLength();

            if (itemByteSize + this.currentSize > this.maxBatchByteSize)
            {
                DefaultTrace.TraceVerbose($"Batch is full - Max byte size {this.maxBatchByteSize} reached.");
                return false;
            }

            this.currentSize += itemByteSize;

            // Operation index is in the scope of the current batch
            operationContext.Operation.OperationIndex = this.batchOperations.Count;
            operationContext.CurrentBatcher = this;
            this.batchOperations.Add(operationContext);
            return true;
        }

        public virtual async Task DispatchAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                using (PartitionKeyRangeBatchResponse batchResponse = await this.executor(this.batchOperations, cancellationToken))
                {
                    for (int index = 0; index < this.batchOperations.Count; index++)
                    {
                        BatchAsyncOperationContext context = this.batchOperations[index];
                        BatchOperationResult response = batchResponse[context.Operation.OperationIndex];
                        if (response != null)
                        {
                            context.Complete(this, response);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceError("Exception during BatchAsyncBatcher: {0}", ex);
                // Exceptions happening during execution fail all the Tasks
                foreach (BatchAsyncOperationContext context in this.batchOperations)
                {
                    context.Fail(this, ex);
                }
            }
            finally
            {
                this.batchOperations.Clear();
                this.dispached = true;
            }
        }
    }

    /// <summary>
    /// Executor implementation that processes a list of operations.
    /// </summary>
    /// <returns>An instance of <see cref="PartitionKeyRangeBatchResponse"/>.</returns>
    internal delegate Task<PartitionKeyRangeBatchResponse> BatchAsyncBatcherExecuteDelegate(IReadOnlyList<BatchAsyncOperationContext> operationContexts, CancellationToken cancellationToken);
}
