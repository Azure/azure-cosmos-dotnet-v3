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
    /// Maintains a batch of operations and dispatches it as a unit of work.
    /// </summary>
    /// <remarks>
    /// The dispatch process consists of:
    /// 1. Creating a <see cref="PartitionKeyRangeServerBatchRequest"/>.
    /// 2. Verifying overflow that might happen due to HybridRow serialization. Any operations that did not fit, get sent to the <see cref="BatchAsyncBatcherRetryDelegate"/>.
    /// 3. Execution of the request gets delegated to <see cref="BatchAsyncBatcherExecuteDelegate"/>.
    /// 4. If there was a split detected, all operations in the request, are sent to the <see cref="BatchAsyncBatcherRetryDelegate"/> for re-queueing.
    /// 5. The result of the request is used to wire up all responses with the original Tasks for each operation.
    /// </remarks>
    /// <seealso cref="BatchAsyncOperationContext"/>
    internal class BatchAsyncBatcher
    {
        private readonly CosmosSerializer cosmosSerializer;
        private readonly List<BatchAsyncOperationContext> batchOperations;
        private readonly BatchAsyncBatcherExecuteDelegate executor;
        private readonly BatchAsyncBatcherRetryDelegate retrier;
        private readonly int maxBatchByteSize;
        private readonly int maxBatchOperationCount;
        private long currentSize = 0;
        private bool dispached = false;

        public bool IsEmpty => this.batchOperations.Count == 0;

        public BatchAsyncBatcher(
            int maxBatchOperationCount,
            int maxBatchByteSize,
            CosmosSerializer cosmosSerializer,
            BatchAsyncBatcherExecuteDelegate executor,
            BatchAsyncBatcherRetryDelegate retrier)
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

            if (retrier == null)
            {
                throw new ArgumentNullException(nameof(retrier));
            }

            if (cosmosSerializer == null)
            {
                throw new ArgumentNullException(nameof(cosmosSerializer));
            }

            this.batchOperations = new List<BatchAsyncOperationContext>(maxBatchOperationCount);
            this.executor = executor;
            this.retrier = retrier;
            this.maxBatchByteSize = maxBatchByteSize;
            this.maxBatchOperationCount = maxBatchOperationCount;
            this.cosmosSerializer = cosmosSerializer;
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
            PartitionKeyRangeServerBatchRequest serverRequest = await this.CreateServerRequestAsync(cancellationToken);
            // Any overflow goes to a new batch
            int overFlowOperations = this.GetOverflowOperations(serverRequest, this.batchOperations);
            while (overFlowOperations > 0)
            {
                await this.retrier(this.batchOperations[this.batchOperations.Count - overFlowOperations], cancellationToken);
                overFlowOperations--;
            }

            try
            {
                PartitionKeyRangeBatchExecutionResult result = await this.executor(serverRequest, cancellationToken);

                List<BatchResponse> responses = new List<BatchResponse>(result.ServerResponses.Count);
                if (!result.ContainsSplit())
                {
                    responses.AddRange(result.ServerResponses);
                }
                else
                {
                    foreach (ItemBatchOperation operationToRetry in result.Operations)
                    {
                        await this.retrier(this.batchOperations[operationToRetry.OperationIndex], cancellationToken);
                    }
                }

                using (PartitionKeyRangeBatchResponse batchResponse = new PartitionKeyRangeBatchResponse(serverRequest.Operations.Count, responses, this.cosmosSerializer))
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
                // Exceptions happening during execution fail all the Tasks part of the request (excluding overflow)
                foreach (ItemBatchOperation itemBatchOperation in serverRequest.Operations)
                {
                    BatchAsyncOperationContext context = this.batchOperations[itemBatchOperation.OperationIndex];
                    context.Fail(this, ex);
                }
            }
            finally
            {
                this.batchOperations.Clear();
                this.dispached = true;
            }
        }

        /// <summary>
        /// If because of HybridRow serialization overhead, not all operations fit in the request, we send those extra operations in a separate request.
        /// </summary>
        internal virtual int GetOverflowOperations(
            PartitionKeyRangeServerBatchRequest request,
            IReadOnlyList<BatchAsyncOperationContext> operationsSentToRequest)
        {
            int totalOperations = operationsSentToRequest.Count;
            return totalOperations - request.Operations.Count;
        }

        private async Task<PartitionKeyRangeServerBatchRequest> CreateServerRequestAsync(CancellationToken cancellationToken)
        {
            // All operations should be for the same PKRange
            string partitionKeyRangeId = this.batchOperations[0].PartitionKeyRangeId;

            ItemBatchOperation[] operations = new ItemBatchOperation[this.batchOperations.Count];
            for (int i = 0; i < this.batchOperations.Count; i++)
            {
                operations[i] = this.batchOperations[i].Operation;
            }

            ArraySegment<ItemBatchOperation> operationsArraySegment = new ArraySegment<ItemBatchOperation>(operations);
            PartitionKeyRangeServerBatchRequest request = await PartitionKeyRangeServerBatchRequest.CreateAsync(
                  partitionKeyRangeId,
                  operationsArraySegment,
                  this.maxBatchByteSize,
                  this.maxBatchOperationCount,
                  ensureContinuousOperationIndexes: false,
                  serializer: this.cosmosSerializer,
                  cancellationToken: cancellationToken).ConfigureAwait(false);

            return request;
        }
    }

    /// <summary>
    /// Executor implementation that processes a list of operations.
    /// </summary>
    /// <returns>An instance of <see cref="PartitionKeyRangeBatchResponse"/>.</returns>
    internal delegate Task<PartitionKeyRangeBatchExecutionResult> BatchAsyncBatcherExecuteDelegate(PartitionKeyRangeServerBatchRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Delegate to process a request for retry an operation
    /// </summary>
    /// <returns>An instance of <see cref="PartitionKeyRangeBatchResponse"/>.</returns>
    internal delegate Task BatchAsyncBatcherRetryDelegate(BatchAsyncOperationContext operationContext, CancellationToken cancellationToken);
}
