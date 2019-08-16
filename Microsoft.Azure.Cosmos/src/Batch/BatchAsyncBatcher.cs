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
    /// <seealso cref="ItemBatchOperation"/>
    internal class BatchAsyncBatcher
    {
        private readonly CosmosSerializer cosmosSerializer;
        private readonly List<ItemBatchOperation> batchOperations;
        private readonly BatchAsyncBatcherExecuteDelegate executor;
        private readonly BatchAsyncBatcherRetryDelegate retrier;
        private readonly int maxBatchByteSize;
        private readonly int maxBatchOperationCount;
        private readonly InterlockIncrementCheck interlockIncrementCheck = new InterlockIncrementCheck();
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

            this.batchOperations = new List<ItemBatchOperation>(maxBatchOperationCount);
            this.executor = executor;
            this.retrier = retrier;
            this.maxBatchByteSize = maxBatchByteSize;
            this.maxBatchOperationCount = maxBatchOperationCount;
            this.cosmosSerializer = cosmosSerializer;
        }

        public virtual bool TryAdd(ItemBatchOperation operation)
        {
            if (this.dispached)
            {
                DefaultTrace.TraceCritical($"Add operation attempted on dispatched batch.");
                return false;
            }

            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            if (operation.Context == null)
            {
                throw new ArgumentNullException(nameof(operation.Context));
            }

            if (this.batchOperations.Count == this.maxBatchOperationCount)
            {
                DefaultTrace.TraceVerbose($"Batch is full - Max operation count {this.maxBatchOperationCount} reached.");
                return false;
            }

            int itemByteSize = operation.GetApproximateSerializedLength();

            if (itemByteSize + this.currentSize > this.maxBatchByteSize)
            {
                DefaultTrace.TraceVerbose($"Batch is full - Max byte size {this.maxBatchByteSize} reached.");
                return false;
            }

            this.currentSize += itemByteSize;

            // Operation index is in the scope of the current batch
            operation.OperationIndex = this.batchOperations.Count;
            operation.Context.CurrentBatcher = this;
            this.batchOperations.Add(operation);
            return true;
        }

        public virtual async Task DispatchAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            using (this.interlockIncrementCheck.EnterLockCheck())
            {
                // HybridRow serialization might leave some pending operations out of the batch
                (PartitionKeyRangeServerBatchRequest serverRequest, ArraySegment<ItemBatchOperation> pendingOperations) = await this.CreateServerRequestAsync(cancellationToken);
                // Any overflow goes to a new batch
                foreach (ItemBatchOperation operation in pendingOperations)
                {
                    await this.retrier(operation, cancellationToken);
                }

                try
                {
                    PartitionKeyRangeBatchExecutionResult result = await this.executor(serverRequest, cancellationToken);

                    if (result.IsSplit())
                    {
                        foreach (ItemBatchOperation operationToRetry in result.Operations)
                        {
                            await this.retrier(this.batchOperations[operationToRetry.OperationIndex], cancellationToken);
                        }

                        return;
                    }

                    using (PartitionKeyRangeBatchResponse batchResponse = new PartitionKeyRangeBatchResponse(serverRequest.Operations.Count, result.ServerResponse, this.cosmosSerializer))
                    {
                        for (int index = 0; index < this.batchOperations.Count; index++)
                        {
                            ItemBatchOperation operation = this.batchOperations[index];
                            BatchOperationResult response = batchResponse[operation.OperationIndex];
                            if (response != null)
                            {
                                operation.Context.Complete(this, response);
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
                        ItemBatchOperation operation = this.batchOperations[itemBatchOperation.OperationIndex];
                        operation.Context.Fail(this, ex);
                    }
                }
                finally
                {
                    this.batchOperations.Clear();
                    this.dispached = true;
                }
            }
        }

        internal virtual async Task<Tuple<PartitionKeyRangeServerBatchRequest, ArraySegment<ItemBatchOperation>>> CreateServerRequestAsync(CancellationToken cancellationToken)
        {
            // All operations should be for the same PKRange
            string partitionKeyRangeId = this.batchOperations[0].Context.PartitionKeyRangeId;

            ArraySegment<ItemBatchOperation> operationsArraySegment = new ArraySegment<ItemBatchOperation>(this.batchOperations.ToArray());
            return await PartitionKeyRangeServerBatchRequest.CreateAsync(
                  partitionKeyRangeId,
                  operationsArraySegment,
                  this.maxBatchByteSize,
                  this.maxBatchOperationCount,
                  ensureContinuousOperationIndexes: false,
                  serializer: this.cosmosSerializer,
                  cancellationToken: cancellationToken).ConfigureAwait(false);
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
    internal delegate Task BatchAsyncBatcherRetryDelegate(ItemBatchOperation operation, CancellationToken cancellationToken);
}
