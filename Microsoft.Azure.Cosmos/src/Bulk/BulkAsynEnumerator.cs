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
        private readonly ConcurrentDictionary<string, SemaphoreSlim> limitersByPartitionkeyRange = new ConcurrentDictionary<string, SemaphoreSlim>();
        private readonly ConcurrentQueue<BulkOperationResponse<TContext>> outputBuffer;
        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(0);

        private readonly int maxPipelinedOperationsPossible = 1000;
        private readonly int maxMicroBatchSize = 100;
        private readonly int maxConcurrentOperationsPerPartition = 1;
        private readonly int defaultMaxDegreeOfConcurrency = 50;
        private Task<bool> readInputTask;
        private BulkOperationResponse<TContext> current;

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

        public BulkOperationResponse<TContext> Current => this.current;

        public ValueTask DisposeAsync()
        {
            return default;
        }

        public async ValueTask<bool> MoveNextAsync()
        {
            if (this.readInputTask == null || this.readInputTask.IsCompleted)
            {
                this.readInputTask = (Task<bool>)TaskHelper.InlineIfPossibleAsync(() => this.ReadInputOperationsAsync(this.inputOperationsEnumerator), null);
            }

            int decrementedValue = Interlocked.Decrement(ref this.numberOfPipelinedOperations);
            Task taskForQueueNotEmpty = this.semaphore.WaitAsync(this.cancellationToken);
            Task completedTask = await Task.WhenAny(taskForQueueNotEmpty, this.readInputTask);
            if (completedTask == taskForQueueNotEmpty)
            {
                this.outputBuffer.TryDequeue(out this.current);
                return true;
            }
            else
            {
                bool hasMoreOperations = this.readInputTask.Result;
                if (!hasMoreOperations && decrementedValue == -1)
                {
                    this.current = null;
                    return false;
                }
                else
                {
                    await this.semaphore.WaitAsync(this.cancellationToken);
                    this.outputBuffer.TryDequeue(out this.current);
                    return true;
                }
            }
            
        }

        // To be called inside lock. Only one should be running at a time.
        private async Task<bool> ReadInputOperationsAsync(IAsyncEnumerator<BulkItemOperation<TContext>> inputOperationsEnumerator)
        {
            while (true)
            {
                if (this.numberOfPipelinedOperations > this.maxPipelinedOperationsPossible)
                {
                    return true;
                }

                bool hasMoreOperations = await inputOperationsEnumerator.MoveNextAsync();
                if (hasMoreOperations)
                {
                    Interlocked.Increment(ref this.numberOfPipelinedOperations);
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
                    {
                        this.outputBuffer.Enqueue(this.CreateBulkResponse(operationTask.Result, currentOperation.OperationContext));
                        this.semaphore.Release();
                    });
                }
                else
                {
                    foreach (BatchAsyncStreamer streamer in this.streamersByPartitionKeyRange.Values)
                    {
                        streamer.DispatchOnSignal();
                    }

                    return false;
                }
            }
        }

        private BulkOperationResponse<TContext> CreateBulkResponse(TransactionalBatchOperationResult transactionalBatchOperationResult, TContext operationContext)
        {
            return new BulkOperationResponse<TContext>(
                    transactionalBatchOperationResult,
                    operationContext,
                    this.container);
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
            if (this.limitersByPartitionkeyRange.TryGetValue(partitionKeyRangeId, out SemaphoreSlim limiter))
            {
                return limiter;
            }

            SemaphoreSlim newLimiter = new SemaphoreSlim(1, this.defaultMaxDegreeOfConcurrency);
            if (!this.limitersByPartitionkeyRange.TryAdd(partitionKeyRangeId, newLimiter))
            {
                newLimiter.Dispose();
            }

            return this.limitersByPartitionkeyRange[partitionKeyRangeId];
        }
    }
}