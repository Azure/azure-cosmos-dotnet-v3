//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Bulk batch executor for operations in the same container.
    /// </summary>
    /// <remarks>
    /// It maintains one <see cref="BatchAsyncStreamer"/> for each Partition Key Range, which allows independent execution of requests.
    /// Semaphores are in place to rate limit the operations at the Streamer / Partition Key Range level, this means that we can send parallel and independent requests to different Partition Key Ranges, but for the same Range, requests will be limited.
    /// Two delegate implementations define how a particular request should be executed, and how operations should be retried. When the <see cref="BatchAsyncStreamer"/> dispatches a batch, the batch will create a request and call the execute delegate, if conditions are met, it might call the retry delegate.
    /// </remarks>
    /// <seealso cref="BatchAsyncStreamer"/>
    internal class BatchAsyncContainerExecutor : IDisposable
    {
        private const int DefaultDispatchTimerInSeconds = 1;
        private const int MinimumDispatchTimerInSeconds = 1;

        private readonly ContainerCore cosmosContainer;
        private readonly CosmosClientContext cosmosClientContext;
        private readonly int maxServerRequestBodyLength;
        private readonly int maxServerRequestOperationCount;
        private readonly int dispatchTimerInSeconds;
        private readonly ConcurrentDictionary<string, BatchAsyncStreamer> streamersByPartitionKeyRange = new ConcurrentDictionary<string, BatchAsyncStreamer>();
        private readonly ConcurrentDictionary<string, SemaphoreSlim> limitersByPartitionkeyRange = new ConcurrentDictionary<string, SemaphoreSlim>();
        private readonly TimerPool timerPool;
        private readonly RetryOptions retryOptions;

        private readonly Stopwatch stopwatch = new Stopwatch();
        private readonly ConcurrentDictionary<string, int> docsPartitionId = new ConcurrentDictionary<string, int>();
        private readonly ConcurrentDictionary<string, int> throttlePartitionId = new ConcurrentDictionary<string, int>();
        private readonly ConcurrentDictionary<string, long> timePartitionid = new ConcurrentDictionary<string, long>();
        private readonly int startingDegreeOfConcurrency = 1;
        private readonly int defaultMaxDegreeOfConcurrency = 80;
        private readonly int additiveIncreaseFactor = 1;
        private readonly int congestionControllerDelayInMs = 2;
        private readonly double multiplicativeDecreaseFactor = 2.0;
        private readonly bool enableCongestionControl;

        /// <summary>
        /// For unit testing.
        /// </summary>
        internal BatchAsyncContainerExecutor()
        {
        }

        public BatchAsyncContainerExecutor(
            ContainerCore cosmosContainer,
            CosmosClientContext cosmosClientContext,
            int maxServerRequestOperationCount,
            int maxServerRequestBodyLength,
            int dispatchTimerInSeconds = BatchAsyncContainerExecutor.DefaultDispatchTimerInSeconds)
        {
            if (cosmosContainer == null)
            {
                throw new ArgumentNullException(nameof(cosmosContainer));
            }

            if (maxServerRequestOperationCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxServerRequestOperationCount));
            }

            if (maxServerRequestBodyLength < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxServerRequestBodyLength));
            }

            if (dispatchTimerInSeconds < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(dispatchTimerInSeconds));
            }

            this.cosmosContainer = cosmosContainer;
            this.cosmosClientContext = cosmosClientContext;
            this.maxServerRequestBodyLength = maxServerRequestBodyLength;
            this.maxServerRequestOperationCount = maxServerRequestOperationCount;
            this.dispatchTimerInSeconds = dispatchTimerInSeconds;
            this.timerPool = new TimerPool(BatchAsyncContainerExecutor.MinimumDispatchTimerInSeconds);
            this.retryOptions = cosmosClientContext.ClientOptions.GetConnectionPolicy().RetryOptions;
            this.enableCongestionControl = cosmosClientContext.ClientOptions.EnableCongestionControlForBulkExecution;

            this.stopwatch.Start();
        }

        public virtual async Task<TransactionalBatchOperationResult> AddAsync(
            ItemBatchOperation operation,
            ItemRequestOptions itemRequestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            await this.ValidateOperationAsync(operation, itemRequestOptions, cancellationToken);

            string resolvedPartitionKeyRangeId = await this.ResolvePartitionKeyRangeIdAsync(operation, cancellationToken).ConfigureAwait(false);
            BatchAsyncStreamer streamer = this.GetOrAddStreamerForPartitionKeyRange(resolvedPartitionKeyRangeId);
            ItemBatchOperationContext context = new ItemBatchOperationContext(resolvedPartitionKeyRangeId, BatchAsyncContainerExecutor.GetRetryPolicy(this.retryOptions));
            operation.AttachContext(context);
            streamer.Add(operation);
            return await context.OperationTask;
        }

        public void Dispose()
        {
            foreach (KeyValuePair<string, BatchAsyncStreamer> streamer in this.streamersByPartitionKeyRange)
            {
                streamer.Value.Dispose();
            }

            foreach (KeyValuePair<string, SemaphoreSlim> limiter in this.limitersByPartitionkeyRange)
            {
                limiter.Value.Dispose();
            }

            this.timerPool.Dispose();
        }

        internal virtual async Task ValidateOperationAsync(
            ItemBatchOperation operation,
            ItemRequestOptions itemRequestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (itemRequestOptions != null)
            {
                if (itemRequestOptions.BaseConsistencyLevel.HasValue
                                || itemRequestOptions.PreTriggers != null
                                || itemRequestOptions.PostTriggers != null
                                || itemRequestOptions.SessionToken != null)
                {
                    throw new InvalidOperationException(ClientResources.UnsupportedBulkRequestOptions);
                }

                Debug.Assert(BatchAsyncContainerExecutor.ValidateOperationEPK(operation, itemRequestOptions));
            }

            await operation.MaterializeResourceAsync(this.cosmosClientContext.SerializerCore, cancellationToken);
        }

        private static IDocumentClientRetryPolicy GetRetryPolicy(RetryOptions retryOptions)
        {
            return new BulkPartitionKeyRangeGoneRetryPolicy(
                new ResourceThrottleRetryPolicy(
                retryOptions.MaxRetryAttemptsOnThrottledRequests,
                retryOptions.MaxRetryWaitTimeInSeconds));
        }

        private static bool ValidateOperationEPK(
            ItemBatchOperation operation,
            ItemRequestOptions itemRequestOptions)
        {
            if (itemRequestOptions.Properties != null
                            && (itemRequestOptions.Properties.TryGetValue(WFConstants.BackendHeaders.EffectivePartitionKey, out object epkObj)
                            | itemRequestOptions.Properties.TryGetValue(WFConstants.BackendHeaders.EffectivePartitionKeyString, out object epkStrObj)))
            {
                byte[] epk = epkObj as byte[];
                string epkStr = epkStrObj as string;
                if (epk == null || epkStr == null)
                {
                    throw new InvalidOperationException(string.Format(
                        ClientResources.EpkPropertiesPairingExpected,
                        WFConstants.BackendHeaders.EffectivePartitionKey,
                        WFConstants.BackendHeaders.EffectivePartitionKeyString));
                }

                if (operation.PartitionKey != null)
                {
                    throw new InvalidOperationException(ClientResources.PKAndEpkSetTogether);
                }
            }

            return true;
        }

        private static void AddHeadersToRequestMessage(RequestMessage requestMessage, string partitionKeyRangeId)
        {
            requestMessage.Headers.PartitionKeyRangeId = partitionKeyRangeId;
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.ShouldBatchContinueOnError, bool.TrueString);
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.IsBatchRequest, bool.TrueString);
        }

        private async Task ReBatchAsync(
            ItemBatchOperation operation,
            CancellationToken cancellationToken)
        {
            string resolvedPartitionKeyRangeId = await this.ResolvePartitionKeyRangeIdAsync(operation, cancellationToken).ConfigureAwait(false);
            BatchAsyncStreamer streamer = this.GetOrAddStreamerForPartitionKeyRange(resolvedPartitionKeyRangeId);
            streamer.Add(operation);
        }

        private async Task<string> ResolvePartitionKeyRangeIdAsync(
            ItemBatchOperation operation,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PartitionKeyDefinition partitionKeyDefinition = await this.cosmosContainer.GetPartitionKeyDefinitionAsync(cancellationToken);
            CollectionRoutingMap collectionRoutingMap = await this.cosmosContainer.GetRoutingMapAsync(cancellationToken);

            Debug.Assert(operation.RequestOptions?.Properties?.TryGetValue(WFConstants.BackendHeaders.EffectivePartitionKeyString, out object epkObj) == null, "EPK is not supported");
            Documents.Routing.PartitionKeyInternal partitionKeyInternal = await this.GetPartitionKeyInternalAsync(operation, cancellationToken);
            operation.PartitionKeyJson = partitionKeyInternal.ToJsonString();
            string effectivePartitionKeyString = partitionKeyInternal.GetEffectivePartitionKeyString(partitionKeyDefinition);
            return collectionRoutingMap.GetRangeByEffectivePartitionKey(effectivePartitionKeyString).Id;
        }

        private async Task<Documents.Routing.PartitionKeyInternal> GetPartitionKeyInternalAsync(ItemBatchOperation operation, CancellationToken cancellationToken)
        {
            Debug.Assert(operation.PartitionKey.HasValue, "PartitionKey should be set on the operation");
            if (operation.PartitionKey.Value.IsNone)
            {
                return await this.cosmosContainer.GetNonePartitionKeyValueAsync(cancellationToken).ConfigureAwait(false);
            }

            return operation.PartitionKey.Value.InternalKey;
        }

        private async Task<PartitionKeyRangeBatchExecutionResult> ExecuteAsync(
            PartitionKeyRangeServerBatchRequest serverRequest,
            CancellationToken cancellationToken)
        {
            SemaphoreSlim limiter = this.GetOrAddLimiterForPartitionKeyRange(serverRequest.PartitionKeyRangeId, cancellationToken);

            using (await limiter.UsingWaitAsync(cancellationToken))
            {
                using (Stream serverRequestPayload = serverRequest.TransferBodyStream())
                {
                    Debug.Assert(serverRequestPayload != null, "Server request payload expected to be non-null");

                    TimeSpan start = this.stopwatch.Elapsed;
                    ResponseMessage responseMessage = await this.cosmosClientContext.ProcessResourceOperationStreamAsync(
                        this.cosmosContainer.LinkUri,
                        ResourceType.Document,
                        OperationType.Batch,
                        new RequestOptions(),
                        cosmosContainerCore: this.cosmosContainer,
                        partitionKey: null,
                        streamPayload: serverRequestPayload,
                        requestEnricher: requestMessage => BatchAsyncContainerExecutor.AddHeadersToRequestMessage(requestMessage, serverRequest.PartitionKeyRangeId),
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    TransactionalBatchResponse serverResponse = await TransactionalBatchResponse.FromResponseMessageAsync(responseMessage, serverRequest, this.cosmosClientContext.SerializerCore).ConfigureAwait(false);

                    int numThrottle = serverResponse.Any(r => r.StatusCode == (System.Net.HttpStatusCode)StatusCodes.TooManyRequests) ? 1 : 0;
                    long milliSecondsElapsed = (this.stopwatch.Elapsed - start).Milliseconds;
                    this.throttlePartitionId.AddOrUpdate(serverRequest.PartitionKeyRangeId, numThrottle, (_, old) => old + numThrottle);
                    this.docsPartitionId.AddOrUpdate(serverRequest.PartitionKeyRangeId, serverResponse.Count, (_, old) => old + serverResponse.Count);
                    this.timePartitionid.AddOrUpdate(serverRequest.PartitionKeyRangeId, milliSecondsElapsed, (_, old) => old + milliSecondsElapsed);

                    return new PartitionKeyRangeBatchExecutionResult(serverRequest.PartitionKeyRangeId, serverRequest.Operations, serverResponse);
                }
            }
        }

        private BatchAsyncStreamer GetOrAddStreamerForPartitionKeyRange(string partitionKeyRangeId)
        {
            if (this.streamersByPartitionKeyRange.TryGetValue(partitionKeyRangeId, out BatchAsyncStreamer streamer))
            {
                return streamer;
            }

            BatchAsyncStreamer newStreamer = new BatchAsyncStreamer(this.maxServerRequestOperationCount, this.maxServerRequestBodyLength, this.dispatchTimerInSeconds, this.timerPool, this.cosmosClientContext.SerializerCore, this.ExecuteAsync, this.ReBatchAsync);
            if (!this.streamersByPartitionKeyRange.TryAdd(partitionKeyRangeId, newStreamer))
            {
                newStreamer.Dispose();
            }

            return this.streamersByPartitionKeyRange[partitionKeyRangeId];
        }

        private SemaphoreSlim GetOrAddLimiterForPartitionKeyRange(string partitionKeyRangeId, CancellationToken cancellationToken)
        {
            if (this.limitersByPartitionkeyRange.TryGetValue(partitionKeyRangeId, out SemaphoreSlim limiter))
            {
                return limiter;
            }

            SemaphoreSlim newLimiter = new SemaphoreSlim(this.startingDegreeOfConcurrency, this.defaultMaxDegreeOfConcurrency);
            if (!this.limitersByPartitionkeyRange.TryAdd(partitionKeyRangeId, newLimiter))
            {
                newLimiter.Dispose();
            }
            else if (this.enableCongestionControl)
            {
                // New limiter was created, so create congestion control on top of it.
                this.CongestionControlTask(partitionKeyRangeId, newLimiter, cancellationToken);
            }

            return this.limitersByPartitionkeyRange[partitionKeyRangeId];
        }

        private void CongestionControlTask(string partitionKeyRangeId, SemaphoreSlim limiter, CancellationToken cancellationToken)
        {
            Task congestionControllerTask = Task.Run(async () =>
            {
                long lastElapsedTimeInMs = 0;
                int degreeOfConcurrency = this.startingDegreeOfConcurrency;
                int maxDegreeOfConcurrency = this.defaultMaxDegreeOfConcurrency;
                int oldThrottleCount = 0;
                int oldDocCount = 0;

                while (!cancellationToken.IsCancellationRequested)
                {
                    this.timePartitionid.TryGetValue(partitionKeyRangeId, out long currentElapsedTimeInMs);
                    long elapsedTime = currentElapsedTimeInMs - lastElapsedTimeInMs;
                    lastElapsedTimeInMs = currentElapsedTimeInMs;

                    if (elapsedTime > 100)
                    {
                        this.docsPartitionId.TryGetValue(partitionKeyRangeId, out int newDocCount);
                        this.throttlePartitionId.TryGetValue(partitionKeyRangeId, out int newThrottleCount);

                        int diffThrottle = newThrottleCount - oldThrottleCount;
                        oldThrottleCount = newThrottleCount;

                        int changeDocCount = newDocCount - oldDocCount;
                        oldDocCount = newDocCount;

                        if (diffThrottle > 0)
                        {
                            int decreaseCount = (int)(degreeOfConcurrency * 1.0 / this.multiplicativeDecreaseFactor);

                            // We got a throttle so we need to back off on the degree of concurrency.
                            // Get the current degree of concurrency and decrease that (AIMD).
                            for (int i = 0; i < decreaseCount; i++)
                            {
                                await limiter.WaitAsync(cancellationToken);
                            }
                            degreeOfConcurrency -= decreaseCount;
                        }

                        if (changeDocCount > 0 && diffThrottle == 0)
                        {
                            if (degreeOfConcurrency + this.additiveIncreaseFactor <= maxDegreeOfConcurrency)
                            {
                                // We aren't getting throttles, so we should bump up the degree of concurrency (AIMD).
                                limiter.Release(this.additiveIncreaseFactor);
                                degreeOfConcurrency = degreeOfConcurrency + this.additiveIncreaseFactor;
                            }
                        }
                    }
                    else
                    {
                        await Task.Delay(this.congestionControllerDelayInMs);
                    }
                }
            }, cancellationToken);
        }
    }
}
