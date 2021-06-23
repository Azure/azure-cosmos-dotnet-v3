//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;

    internal class BulkAsynEnumerator<TContext> : IAsyncEnumerator<BulkOperationResponse<TContext>>
    {
        private readonly IAsyncEnumerator<BulkItemOperation<TContext>> inputOperationsEnumerator;
        private readonly BulkRequestOptions bulkRequestOptions;
        private readonly ContainerInternal container;
        private readonly CancellationToken cancellationToken;
        private readonly ConcurrentDictionary<string, BatchAsyncStreamer> streamersByPartitionKeyRange = new ConcurrentDictionary<string, BatchAsyncStreamer>();
        private readonly ConcurrentQueue<BulkOperationResponse<TContext>> outputBuffer;

        private readonly int maxPipelinedOperationsPossible = 1000;
        private readonly int maxMicroBatchSize = 100;
        private readonly int maxConcurrentOperationsPerPartition = 1;
        private readonly int defaultMaxDegreeOfConcurrency = 50;
        private Task readInputTask;

        private int numberOfPipelinedOperations;

        public BulkAsynEnumerator(IAsyncEnumerable<BulkItemOperation<TContext>> inputOperations, 
                                  BulkRequestOptions bulkRequestOptions, 
                                  ContainerInternal container, 
                                  CancellationToken cancellationToken = default)
        {
            this.inputOperationsEnumerator = inputOperations.GetAsyncEnumerator(cancellationToken);
            this.bulkRequestOptions = bulkRequestOptions;
            this.container = container;
            this.cancellationToken = cancellationToken;
            this.numberOfPipelinedOperations = 0;
        }

        public BulkOperationResponse<TContext> Current => throw new System.NotImplementedException();

        public ValueTask DisposeAsync()
        {
            return default;
        }

        public ValueTask<bool> MoveNextAsync()
        {
            if (this.readInputTask == null || this.readInputTask.IsCompleted)
            {
                this.readInputTask = TaskHelper.InlineIfPossibleAsync(() => this.ReadInputOperationsAsync(this.inputOperationsEnumerator), null);
            }

            // TODO : Return task of when first element to queue

            Interlocked.Decrement(ref this.numberOfPipelinedOperations);
        }

        // To be called inside lock. Only one should be running at a time.
        private async Task ReadInputOperationsAsync(IAsyncEnumerator<BulkItemOperation<TContext>> inputOperationsEnumerator)
        {
            while (true)
            {
                Interlocked.Increment(ref this.numberOfPipelinedOperations);
                if (this.numberOfPipelinedOperations > this.maxPipelinedOperationsPossible)
                {
                    Interlocked.Decrement(ref this.numberOfPipelinedOperations);
                    return;
                }

                bool hasMoreOperations = await inputOperationsEnumerator.MoveNextAsync();
                if (hasMoreOperations)
                {
                    PartitionKeyRange partitionKeyRange = await this.ResolvePartitionKeyRangeAsync(inputOperationsEnumerator.Current);
                    BatchAsyncStreamer streamer = this.GetOrAddStreamerForPartitionKeyRange(partitionKeyRange.Id);
                    BulkItemOperation<TContext> currentOperation = inputOperationsEnumerator.Current;
                    ItemBatchOperation itemBatchOperation = new ItemBatchOperation(currentOperation.OperationType,
                                                                                    0,
                                                                                    currentOperation.PartitionKey,
                                                                                    resourceStream: currentOperation.StreamPayload);

                    ItemBatchOperationContext context = new ItemBatchOperationContext(partitionKeyRange.Id, Tracing.NoOpTrace.Singleton);
                    itemBatchOperation.AttachContext(context);
                    streamer.Add(itemBatchOperation);

                    _ = context.OperationTask.ContinueWith((operationTask) => 
                    this.outputBuffer.Enqueue(this.CreateBulkResponse(operationTask.Result, currentOperation.OperationContext)));
                }
                else
                {
                    foreach (BatchAsyncStreamer streamer in this.streamersByPartitionKeyRange.Values)
                    {
                        streamer.DispatchOnSignal();
                    }
                }
            }
        }

        private BulkOperationResponse<TContext> CreateBulkResponse(TransactionalBatchOperationResult transactionalBatchOperationResult, TContext operationContext)
        {
            return new BulkOperationResponse<TContext>(
                    transactionalBatchOperationResult,
                    operationContext);
        }

        private async Task<PartitionKeyRange> ResolvePartitionKeyRangeAsync(BulkItemOperation<TContext> operation)
        {
            CollectionRoutingMap collectionRoutingMap = await this.container.GetRoutingMapAsync(this.cancellationToken);
            PartitionKeyDefinition partitionKeyDefinition = await this.container.GetPartitionKeyDefinitionAsync(this.cancellationToken);
            string effectivePartitionKeyValue = operation.PartitionKey.InternalKey.GetEffectivePartitionKeyString(partitionKeyDefinition);
            return collectionRoutingMap.GetRangeByEffectivePartitionKey(effectivePartitionKeyValue);
        }

        private BatchAsyncStreamer GetOrAddStreamerForPartitionKeyRange(string partitionKeyRangeId)
        {
            if (this.streamersByPartitionKeyRange.TryGetValue(partitionKeyRangeId, out BatchAsyncStreamer streamer))
            {
                return streamer;
            }
            SemaphoreSlim limiter = this.GetOrAddLimiterForPartitionKeyRange(partitionKeyRangeId);
            BatchAsyncStreamer newStreamer = new BatchAsyncStreamer(
                Constants.MaxOperationsInDirectModeBatchRequest,
                Constants.MaxDirectModeBatchRequestBodySizeInBytes,
                this.timerWheel,
                limiter,
                this.defaultMaxDegreeOfConcurrency,
                this.container.ClientContext.SerializerCore,
                this.ExecuteAsync,
                this.ReBatchAsync,
                this.container.ClientContext);
            if (!this.streamersByPartitionKeyRange.TryAdd(partitionKeyRangeId, newStreamer))
            {
                newStreamer.Dispose();
            }

            return this.streamersByPartitionKeyRange[partitionKeyRangeId];
        }

        private SemaphoreSlim GetOrAddLimiterForPartitionKeyRange(string partitionKeyRangeId)
        {
            throw new NotImplementedException();
        }
    }
}