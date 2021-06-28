//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    internal class BulkAsynEnumerator<TContext> : IAsyncEnumerator<BulkOperationResponse<TContext>>
    {
        private readonly IAsyncEnumerator<BulkItemOperation<TContext>> inputOperationsEnumerator;
        private readonly BulkRequestOptions bulkRequestOptions;
        private readonly ContainerInternal container;
        private readonly CancellationToken cancellationToken;
        private readonly ConcurrentDictionary<string, BatchAsyncStreamer> streamersByPartitionKeyRange = new ConcurrentDictionary<string, BatchAsyncStreamer>();
        private readonly ConcurrentDictionary<string, SemaphoreSlim> limitersByPartitionkeyRange = new ConcurrentDictionary<string, SemaphoreSlim>();
        private readonly ConcurrentQueue<BulkOperationResponse<TContext>> outputBuffer = new ConcurrentQueue<BulkOperationResponse<TContext>>();
        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(0);
        private readonly object readTaskLock = new object();
        private readonly TimerWheel timerWheel;
        private readonly RetryOptions retryOptions;

        private readonly int maxPipelinedOperationsPossible = 1000;
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
            this.timerWheel = TimerWheel.CreateTimerWheel(TimeSpan.FromSeconds(1), 10);
            this.retryOptions = container.ClientContext.ClientOptions.GetConnectionPolicy().RetryOptions;
        }

        public BulkOperationResponse<TContext> Current => this.current;

        public ValueTask DisposeAsync()
        {
            foreach (KeyValuePair<string, BatchAsyncStreamer> streamer in this.streamersByPartitionKeyRange)
            {
                streamer.Value.Dispose();
            }

            foreach (KeyValuePair<string, SemaphoreSlim> limiter in this.limitersByPartitionkeyRange)
            {
                limiter.Value.Dispose();
            }

            this.timerWheel.Dispose();
            return default;
        }

        public async ValueTask<bool> MoveNextAsync()
        {
            lock (this.readTaskLock)
            {
                if (this.readInputTask == null || (this.readInputTask.IsCompleted && this.readInputTask.Result))
                {
                    this.readInputTask = (Task<bool>)TaskHelper.InlineIfPossibleAsync(() => this.ReadInputOperationsAsync(this.inputOperationsEnumerator), null);
                }
            }

            Task taskForQueueNotEmpty = this.semaphore.WaitAsync(this.cancellationToken);
            Task completedTask = await Task.WhenAny(taskForQueueNotEmpty, this.readInputTask);
            int decrementedValue = Interlocked.Decrement(ref this.numberOfPipelinedOperations);
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
                    await taskForQueueNotEmpty;
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
                    BulkItemOperation<TContext> currentOperation = inputOperationsEnumerator.Current;
                    ItemBatchOperation itemBatchOperation = new ItemBatchOperation(currentOperation.OperationType,
                                                                                    0,
                                                                                    currentOperation.PartitionKey,
                                                                                    resourceStream: currentOperation.StreamPayload);

                    string partitionKeyRangeId = await this.ResolvePartitionKeyRangeIdAsync(itemBatchOperation, NoOpTrace.Singleton, this.cancellationToken);
                    BatchAsyncStreamer streamer = this.GetOrAddStreamerForPartitionKeyRange(partitionKeyRangeId);

                    ItemBatchOperationContext context = new ItemBatchOperationContext(partitionKeyRangeId, 
                                                                                      Tracing.NoOpTrace.Singleton,
                                                                                      BatchAsyncContainerExecutor.GetRetryPolicy(this.container, currentOperation.OperationType, this.retryOptions));
                    itemBatchOperation.AttachContext(context);
                    streamer.Add(itemBatchOperation);

                    _ = context.OperationTask.ContinueWith((operationTask) =>
                    {
                        try
                        {
                            this.outputBuffer.Enqueue(this.CreateBulkResponse(operationTask.Result, currentOperation.OperationContext, operationTask.Exception));
                        }
                        finally
                        {
                            this.semaphore.Release();
                        }
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

        private BulkOperationResponse<TContext> CreateBulkResponse(TransactionalBatchOperationResult transactionalBatchOperationResult, 
                                                                   TContext operationContext,
                                                                   Exception ex = null)
        {
            return new BulkOperationResponse<TContext>(
                    transactionalBatchOperationResult,
                    operationContext,
                    this.container,
                    ex);
        }

        private async Task<string> ResolvePartitionKeyRangeIdAsync(
            ItemBatchOperation operation,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ContainerProperties cachedContainerPropertiesAsync = await this.container.GetCachedContainerPropertiesAsync(
                forceRefresh: false,
                trace: trace,
                cancellationToken: cancellationToken);
            PartitionKeyDefinition partitionKeyDefinition = cachedContainerPropertiesAsync?.PartitionKey;
            CollectionRoutingMap collectionRoutingMap = await this.container.GetRoutingMapAsync(cancellationToken);

            Documents.Routing.PartitionKeyInternal partitionKeyInternal = await this.GetPartitionKeyInternalAsync(operation, cancellationToken);
            operation.PartitionKeyJson = partitionKeyInternal.ToJsonString();
            string effectivePartitionKeyString = partitionKeyInternal.GetEffectivePartitionKeyString(partitionKeyDefinition);
            return collectionRoutingMap.GetRangeByEffectivePartitionKey(effectivePartitionKeyString).Id;
        }

        private async Task<Documents.Routing.PartitionKeyInternal> GetPartitionKeyInternalAsync(ItemBatchOperation operation, CancellationToken cancellationToken)
        {
            if (operation.PartitionKey.Value.IsNone)
            {
                return await this.container.GetNonePartitionKeyValueAsync(NoOpTrace.Singleton, cancellationToken).ConfigureAwait(false);
            }

            return operation.PartitionKey.Value.InternalKey;
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
                this.container.ClientContext,
                TimeSpan.FromSeconds(1));
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

            SemaphoreSlim newLimiter = new SemaphoreSlim(1);
            if (!this.limitersByPartitionkeyRange.TryAdd(partitionKeyRangeId, newLimiter))
            {
                newLimiter.Dispose();
            }

            return this.limitersByPartitionkeyRange[partitionKeyRangeId];
        }

        private async Task ReBatchAsync(
                        ItemBatchOperation operation,
                        CancellationToken cancellationToken)
        {
            using (ITrace trace = Tracing.Trace.GetRootTrace("Batch Retry Async", TraceComponent.Batch, Tracing.TraceLevel.Verbose))
            {
                string resolvedPartitionKeyRangeId = await this.ResolvePartitionKeyRangeIdAsync(operation, trace, cancellationToken).ConfigureAwait(false);
                operation.Context.ReRouteOperation(resolvedPartitionKeyRangeId, trace);
                BatchAsyncStreamer streamer = this.GetOrAddStreamerForPartitionKeyRange(resolvedPartitionKeyRangeId);
                streamer.Add(operation);
            }
        }

        private async Task<PartitionKeyRangeBatchExecutionResult> ExecuteAsync(
            PartitionKeyRangeServerBatchRequest serverRequest,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            SemaphoreSlim limiter = this.GetOrAddLimiterForPartitionKeyRange(serverRequest.PartitionKeyRangeId);
            using (await limiter.UsingWaitAsync(trace, cancellationToken))
            {
                using (Stream serverRequestPayload = serverRequest.TransferBodyStream())
                {
                    ResponseMessage responseMessage = await this.container.ClientContext.ProcessResourceOperationStreamAsync(
                        this.container.LinkUri,
                        ResourceType.Document,
                        OperationType.Batch,
                        new RequestOptions(),
                        cosmosContainerCore: this.container,
                        feedRange: null,
                        streamPayload: serverRequestPayload,
                        requestEnricher: requestMessage => BulkAsynEnumerator<TContext>.AddHeadersToRequestMessage(requestMessage, serverRequest.PartitionKeyRangeId),
                        trace: trace,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    TransactionalBatchResponse serverResponse = await TransactionalBatchResponse.FromResponseMessageAsync(
                        responseMessage,
                        serverRequest,
                        this.container.ClientContext.SerializerCore,
                        shouldPromoteOperationStatus: true,
                        trace,
                        cancellationToken).ConfigureAwait(false);

                    return new PartitionKeyRangeBatchExecutionResult(
                        serverRequest.PartitionKeyRangeId,
                        serverRequest.Operations,
                        serverResponse);
                }
            }
        }

        private static void AddHeadersToRequestMessage(RequestMessage requestMessage, string partitionKeyRangeId)
        {
            requestMessage.Headers.PartitionKeyRangeId = partitionKeyRangeId;
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.ShouldBatchContinueOnError, bool.TrueString);
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.IsBatchAtomic, bool.FalseString);
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.IsBatchRequest, bool.TrueString);
        }
    }
}