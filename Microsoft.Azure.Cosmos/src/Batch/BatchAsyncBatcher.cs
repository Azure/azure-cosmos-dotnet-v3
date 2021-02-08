//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

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
        private readonly CosmosSerializerCore serializerCore;
        private readonly List<ItemBatchOperation> batchOperations;
        private readonly BatchAsyncBatcherExecuteDelegate executor;
        private readonly BatchAsyncBatcherRetryDelegate retrier;
        private readonly int maxBatchByteSize;
        private readonly int maxBatchOperationCount;
        private readonly InterlockIncrementCheck interlockIncrementCheck = new InterlockIncrementCheck();
        private long currentSize = 0;
        private bool dispatched = false;

        public bool IsEmpty => this.batchOperations.Count == 0;

        public BatchAsyncBatcher(
            int maxBatchOperationCount,
            int maxBatchByteSize,
            CosmosSerializerCore serializerCore,
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

            this.batchOperations = new List<ItemBatchOperation>(maxBatchOperationCount);
            this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
            this.retrier = retrier ?? throw new ArgumentNullException(nameof(retrier));
            this.maxBatchByteSize = maxBatchByteSize;
            this.maxBatchOperationCount = maxBatchOperationCount;
            this.serializerCore = serializerCore ?? throw new ArgumentNullException(nameof(serializerCore));
        }

        public virtual bool TryAdd(ItemBatchOperation operation)
        {
            if (this.dispatched)
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
                DefaultTrace.TraceInformation($"Batch is full - Max operation count {this.maxBatchOperationCount} reached.");
                return false;
            }

            int itemByteSize = operation.GetApproximateSerializedLength();

            if (this.batchOperations.Count > 0 && itemByteSize + this.currentSize > this.maxBatchByteSize)
            {
                DefaultTrace.TraceInformation($"Batch is full - Max byte size {this.maxBatchByteSize} reached.");
                return false;
            }

            this.currentSize += itemByteSize;

            // Operation index is in the scope of the current batch
            operation.OperationIndex = this.batchOperations.Count;
            operation.Context.CurrentBatcher = this;
            this.batchOperations.Add(operation);
            return true;
        }

        public virtual async Task DispatchAsync(
            BatchPartitionMetric partitionMetric,
            CancellationToken cancellationToken = default)
        {
            using (ITrace trace = Tracing.Trace.GetRootTrace("Batch Dispatch Async", TraceComponent.Batch, Tracing.TraceLevel.Info))
            {
                this.interlockIncrementCheck.EnterLockCheck();

                PartitionKeyRangeServerBatchRequest serverRequest = null;
                ArraySegment<ItemBatchOperation> pendingOperations;

                try
                {
                    try
                    {
                        // HybridRow serialization might leave some pending operations out of the batch
                        Tuple<PartitionKeyRangeServerBatchRequest, ArraySegment<ItemBatchOperation>> createRequestResponse = await this.CreateServerRequestAsync(cancellationToken);
                        serverRequest = createRequestResponse.Item1;
                        pendingOperations = createRequestResponse.Item2;
                        // Any overflow goes to a new batch
                        foreach (ItemBatchOperation operation in pendingOperations)
                        {
                            await this.retrier(operation, trace, cancellationToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Exceptions happening during request creation, fail the entire list
                        foreach (ItemBatchOperation itemBatchOperation in this.batchOperations)
                        {
                            itemBatchOperation.Context.Fail(this, ex);
                        }

                        throw;
                    }

                    try
                    {
                        Stopwatch stopwatch = Stopwatch.StartNew();

                        PartitionKeyRangeBatchExecutionResult result = await this.executor(serverRequest, trace, cancellationToken);

                        int numThrottle = result.ServerResponse.Any(r => r.StatusCode == (System.Net.HttpStatusCode)StatusCodes.TooManyRequests) ? 1 : 0;
                        partitionMetric.Add(
                            numberOfDocumentsOperatedOn: result.ServerResponse.Count,
                            timeTakenInMilliseconds: stopwatch.ElapsedMilliseconds,
                            numberOfThrottles: numThrottle);

                        using (PartitionKeyRangeBatchResponse batchResponse = new PartitionKeyRangeBatchResponse(serverRequest.Operations.Count, result.ServerResponse, this.serializerCore))
                        {
                            foreach (ItemBatchOperation itemBatchOperation in batchResponse.Operations)
                            {
                                TransactionalBatchOperationResult response = batchResponse[itemBatchOperation.OperationIndex];

                                if (!response.IsSuccessStatusCode)
                                {
                                    Documents.ShouldRetryResult shouldRetry = await itemBatchOperation.Context.ShouldRetryAsync(response, cancellationToken);
                                    if (shouldRetry.ShouldRetry)
                                    {
                                        await this.retrier(itemBatchOperation, trace, cancellationToken);
                                        continue;
                                    }
                                }

                                itemBatchOperation.Context.Complete(this, response);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Exceptions happening during execution fail all the Tasks part of the request (excluding overflow)
                        foreach (ItemBatchOperation itemBatchOperation in serverRequest.Operations)
                        {
                            itemBatchOperation.Context.Fail(this, ex);
                        }

                        throw;
                    }

                }
                catch (Exception ex)
                {
                    DefaultTrace.TraceError("Exception during BatchAsyncBatcher: {0}", ex);
                }
                finally
                {
                    this.batchOperations.Clear();
                    this.dispatched = true;
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
                  serializerCore: this.serializerCore,
                  cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Executor implementation that processes a list of operations.
    /// </summary>
    /// <returns>An instance of <see cref="PartitionKeyRangeBatchResponse"/>.</returns>
    internal delegate Task<PartitionKeyRangeBatchExecutionResult> BatchAsyncBatcherExecuteDelegate(
        PartitionKeyRangeServerBatchRequest request,
        ITrace trace,
        CancellationToken cancellationToken);

    /// <summary>
    /// Delegate to process a request for retry an operation
    /// </summary>
    /// <returns>An instance of <see cref="PartitionKeyRangeBatchResponse"/>.</returns>
    internal delegate Task BatchAsyncBatcherRetryDelegate(
        ItemBatchOperation operation,
        ITrace trace,
        CancellationToken cancellationToken);
}
