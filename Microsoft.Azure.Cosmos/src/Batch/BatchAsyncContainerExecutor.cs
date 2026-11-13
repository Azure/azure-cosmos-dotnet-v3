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
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
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
        private const int TimerWheelBucketCount = 20;
        private static readonly TimeSpan TimerWheelResolution = TimeSpan.FromMilliseconds(50);

        private readonly ContainerInternal cosmosContainer;
        private readonly CosmosClientContext cosmosClientContext;
        private readonly int maxServerRequestBodyLength;
        private readonly int maxServerRequestOperationCount;
        private readonly ConcurrentDictionary<string, BatchAsyncStreamer> streamersByPartitionKeyRange = new ConcurrentDictionary<string, BatchAsyncStreamer>();
        private readonly ConcurrentDictionary<string, SemaphoreSlim> limitersByPartitionkeyRange = new ConcurrentDictionary<string, SemaphoreSlim>();
        private readonly ConcurrentDictionary<long, Task> delayedRetryTasks = new ConcurrentDictionary<long, Task>();
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly CancellationToken executorCancellationToken;
        private readonly object streamerLock = new object();
        private readonly TimerWheel timerWheel;
        private readonly RetryOptions retryOptions;
        private readonly int defaultMaxDegreeOfConcurrency = 50;
        private long nextDelayedRetryId;
        private bool isDisposed;

        /// <summary>
        /// For unit testing.
        /// </summary>
        internal BatchAsyncContainerExecutor()
        {
        }

        public BatchAsyncContainerExecutor(
            ContainerInternal cosmosContainer,
            CosmosClientContext cosmosClientContext,
            int maxServerRequestOperationCount,
            int maxServerRequestBodyLength)
        {
            if (maxServerRequestOperationCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxServerRequestOperationCount));
            }

            if (maxServerRequestBodyLength < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxServerRequestBodyLength));
            }

            this.cosmosContainer = cosmosContainer ?? throw new ArgumentNullException(nameof(cosmosContainer));
            this.cosmosClientContext = cosmosClientContext;
            this.maxServerRequestBodyLength = maxServerRequestBodyLength;
            this.maxServerRequestOperationCount = maxServerRequestOperationCount;
            this.executorCancellationToken = this.cancellationTokenSource.Token;
            this.timerWheel = TimerWheel.CreateTimerWheel(BatchAsyncContainerExecutor.TimerWheelResolution, BatchAsyncContainerExecutor.TimerWheelBucketCount);
            this.retryOptions = cosmosClientContext.ClientOptions.GetConnectionPolicy(cosmosClientContext.Client.ClientId).RetryOptions;
        }

        public virtual async Task<TransactionalBatchOperationResult> AddAsync(
            ItemBatchOperation operation,
            ITrace trace,
            ItemRequestOptions itemRequestOptions = null,
            CancellationToken cancellationToken = default)
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            await this.ValidateOperationAsync(operation, itemRequestOptions, cancellationToken);

            string resolvedPartitionKeyRangeId = await this.ResolvePartitionKeyRangeIdAsync(
                operation, 
                trace, 
                cancellationToken).ConfigureAwait(false);
            BatchAsyncStreamer streamer = this.GetOrAddStreamerForPartitionKeyRange(resolvedPartitionKeyRangeId, itemRequestOptions);

            ItemBatchOperationContext context = new ItemBatchOperationContext(
                resolvedPartitionKeyRangeId,
                trace,
                BatchAsyncContainerExecutor.GetRetryPolicy(this.cosmosContainer, operation.OperationType, this.retryOptions),
                cancellationToken);

            if (itemRequestOptions != null && itemRequestOptions.AddRequestHeaders != null)
            {
                // get the header value if any, passed by the encryption package.
                Headers encryptionHeaders = new Headers();
                itemRequestOptions.AddRequestHeaders?.Invoke(encryptionHeaders);

                // make sure we set the Intended Collection Rid header when we have encrypted payload.
                // This primarily would allow CosmosDB Encryption package to detect change in container referenced by a Client
                // and prevent creating data with wrong Encryption Policy.
                if (encryptionHeaders.TryGetValue(HttpConstants.HttpHeaders.IsClientEncrypted, out string encrypted))
                {
                    context.IsClientEncrypted = bool.Parse(encrypted);

                    if (context.IsClientEncrypted && encryptionHeaders.TryGetValue(WFConstants.BackendHeaders.IntendedCollectionRid, out string ridValue))
                    {
                        context.IntendedCollectionRidValue = ridValue;
                    }
                }
            }
            
            operation.AttachContext(context);
            try
            {
                streamer.Add(operation);
            }
            catch (Exception exception)
            {
                context.Fail(context.CurrentBatcher, exception);
            }

            return await context.OperationTask;
        }

        public void Dispose()
        {
            lock (this.streamerLock)
            {
                if (this.isDisposed)
                {
                    return;
                }

                this.isDisposed = true;
                this.cancellationTokenSource.Cancel();

                foreach (KeyValuePair<string, BatchAsyncStreamer> streamer in this.streamersByPartitionKeyRange)
                {
                    streamer.Value.Dispose();
                }
            }

            foreach (KeyValuePair<string, SemaphoreSlim> limiter in this.limitersByPartitionkeyRange)
            {
                limiter.Value.Dispose();
            }

            this.timerWheel.Dispose();
            this.cancellationTokenSource.Dispose();
        }

        internal virtual async Task ValidateOperationAsync(
            ItemBatchOperation operation,
            ItemRequestOptions itemRequestOptions = null,
            CancellationToken cancellationToken = default)
        {
            if (itemRequestOptions != null)
            {
                if (itemRequestOptions.BaseConsistencyLevel.HasValue
                                || itemRequestOptions.BaseReadConsistencyStrategy.HasValue
                                || itemRequestOptions.PreTriggers != null
                                || itemRequestOptions.PostTriggers != null
                                || itemRequestOptions.SessionToken != null
                                || itemRequestOptions.Properties != null
                                || itemRequestOptions.ThroughputBucket.HasValue
                                || itemRequestOptions.DedicatedGatewayRequestOptions?.MaxIntegratedCacheStaleness != null)
                {
                    throw new InvalidOperationException(ClientResources.UnsupportedBulkRequestOptions);
                }

                Debug.Assert(BatchAsyncContainerExecutor.ValidateOperationEPK(operation, itemRequestOptions));
            }

            await operation.MaterializeResourceAsync(this.cosmosClientContext.SerializerCore, cancellationToken);
        }

        private static IDocumentClientRetryPolicy GetRetryPolicy(
            ContainerInternal containerInternal,
            OperationType operationType,
            RetryOptions retryOptions)
        {
            return new BulkExecutionRetryPolicy(
               containerInternal,
               operationType,
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
                            | itemRequestOptions.Properties.TryGetValue(WFConstants.BackendHeaders.EffectivePartitionKeyString, out object epkStrObj)
                            | itemRequestOptions.Properties.TryGetValue(HttpConstants.HttpHeaders.PartitionKey, out object pkStringObj)))
            {
                byte[] epk = epkObj as byte[];
                string pkString = pkStringObj as string;
                if ((epk == null && pkString == null) || !(epkStrObj is string _))
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

        private static void AddHeadersToRequestMessage(RequestMessage requestMessage, PartitionKeyRangeServerBatchRequest partitionKeyRangeServerBatchRequest)
        {
            requestMessage.Headers.PartitionKeyRangeId = partitionKeyRangeServerBatchRequest.PartitionKeyRangeId;

            if (partitionKeyRangeServerBatchRequest.IsClientEncrypted)
            {
                requestMessage.Headers.Add(HttpConstants.HttpHeaders.IsClientEncrypted, partitionKeyRangeServerBatchRequest.IsClientEncrypted.ToString());
                requestMessage.Headers.Add(WFConstants.BackendHeaders.IntendedCollectionRid, partitionKeyRangeServerBatchRequest.IntendedCollectionRidValue);
            }

            requestMessage.Headers.Add(HttpConstants.HttpHeaders.ShouldBatchContinueOnError, bool.TrueString);
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.IsBatchAtomic, bool.FalseString);
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.IsBatchRequest, bool.TrueString);
        }

        private Task ReBatchAsync(
            ItemBatchOperation operation,
            TimeSpan retryDelay,
            CancellationToken cancellationToken)
        {
            if (retryDelay <= TimeSpan.Zero)
            {
                return this.ReBatchOperationAndHandleFailureAsync(operation, cancellationToken);
            }

            long delayedRetryId = Interlocked.Increment(ref this.nextDelayedRetryId);
            Task delayedRetryTask = this.ReBatchAfterDelayAsync(operation, retryDelay, cancellationToken);
            if (!this.delayedRetryTasks.TryAdd(delayedRetryId, delayedRetryTask))
            {
                throw new InvalidOperationException("Failed to track delayed batch retry.");
            }

            _ = delayedRetryTask.ContinueWith(
                completedTask => this.delayedRetryTasks.TryRemove(delayedRetryId, out Task _),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

            return Task.CompletedTask;
        }

        private async Task ReBatchAfterDelayAsync(
            ItemBatchOperation operation,
            TimeSpan retryDelay,
            CancellationToken cancellationToken)
        {
            try
            {
                using (CancellationTokenSource linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    this.executorCancellationToken,
                    operation.Context.CallerCancellationToken))
                {
                    await Task.Delay(retryDelay, linkedCancellationTokenSource.Token).ConfigureAwait(false);
                    await this.ReBatchOperationAsync(operation, linkedCancellationTokenSource.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                CancellationToken cancellationTokenToReport = operation.Context.CallerCancellationToken.IsCancellationRequested
                    ? operation.Context.CallerCancellationToken
                    : cancellationToken;
                operation.Context.Cancel(operation.Context.CurrentBatcher, cancellationTokenToReport);
            }
            catch (Exception exception)
            {
                operation.Context.Fail(operation.Context.CurrentBatcher, exception);
            }
        }

        private async Task ReBatchOperationAndHandleFailureAsync(
            ItemBatchOperation operation,
            CancellationToken cancellationToken)
        {
            try
            {
                await this.ReBatchOperationAsync(operation, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                operation.Context.Cancel(operation.Context.CurrentBatcher, cancellationToken);
            }
            catch (Exception exception)
            {
                operation.Context.Fail(operation.Context.CurrentBatcher, exception);
            }
        }

        private async Task ReBatchOperationAsync(
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

        private async Task<string> ResolvePartitionKeyRangeIdAsync(
            ItemBatchOperation operation,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ContainerProperties cachedContainerPropertiesAsync = await this.cosmosContainer.GetCachedContainerPropertiesAsync(
                forceRefresh: false,
                trace: trace,
                cancellationToken: cancellationToken);
            PartitionKeyDefinition partitionKeyDefinition = cachedContainerPropertiesAsync?.PartitionKey;

            (operation.PartitionKey, _) = await this.cosmosContainer.EnsureIdGetsAppendedToPartitionKeyIfNeededAsync(
                operation.PartitionKey, operation.Id, null, cancellationToken);

            CollectionRoutingMap collectionRoutingMap = await this.cosmosContainer.GetRoutingMapAsync(cancellationToken);

            Debug.Assert(operation.RequestOptions?.Properties?.TryGetValue(WFConstants.BackendHeaders.EffectivePartitionKeyString, out object _) == null, "EPK is not supported");
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
                return await this.cosmosContainer.GetNonePartitionKeyValueAsync(NoOpTrace.Singleton, cancellationToken).ConfigureAwait(false);
            }

            return operation.PartitionKey.Value.InternalKey;
        }

        private async Task<PartitionKeyRangeBatchExecutionResult> ExecuteAsync(
            PartitionKeyRangeServerBatchRequest serverRequest,
            ITrace trace,
            ItemRequestOptions itemRequestOptions,
            CancellationToken cancellationToken)
        {
            SemaphoreSlim limiter = this.GetOrAddLimiterForPartitionKeyRange(serverRequest.PartitionKeyRangeId);
            using (await limiter.UsingWaitAsync(trace, cancellationToken))
            {
                using (Stream serverRequestPayload = serverRequest.TransferBodyStream())
                {
                    RequestOptions requestOptions = new RequestOptions();
                    if (itemRequestOptions != null && itemRequestOptions.AvailabilityStrategy != null)
                    {
                        requestOptions.AvailabilityStrategy = itemRequestOptions.AvailabilityStrategy;
                    }
                    Debug.Assert(serverRequestPayload != null, "Server request payload expected to be non-null");
                    ResponseMessage responseMessage = await this.cosmosClientContext.ProcessResourceOperationStreamAsync(
                        this.cosmosContainer.LinkUri,
                        ResourceType.Document,
                        OperationType.Batch,
                        requestOptions,
                        cosmosContainerCore: this.cosmosContainer,
                        feedRange: null,
                        streamPayload: serverRequestPayload,
                        requestEnricher: requestMessage => BatchAsyncContainerExecutor.AddHeadersToRequestMessage(requestMessage, serverRequest),
                        trace: trace,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    TransactionalBatchResponse serverResponse = await TransactionalBatchResponse.FromResponseMessageAsync(
                        responseMessage,
                        serverRequest,
                        this.cosmosClientContext.SerializerCore,
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

        internal virtual BatchAsyncStreamer GetOrAddStreamerForPartitionKeyRange(string partitionKeyRangeId, ItemRequestOptions options = null)
        {
            lock (this.streamerLock)
            {
                if (this.isDisposed)
                {
                    throw new ObjectDisposedException(nameof(BatchAsyncContainerExecutor));
                }

                if (this.streamersByPartitionKeyRange.TryGetValue(partitionKeyRangeId, out BatchAsyncStreamer streamer))
                {
                    return streamer;
                }

                SemaphoreSlim limiter = this.GetOrAddLimiterForPartitionKeyRange(partitionKeyRangeId);
                BatchAsyncStreamer newStreamer = new BatchAsyncStreamer(
                    this.maxServerRequestOperationCount,
                    this.maxServerRequestBodyLength,
                    this.timerWheel,
                    limiter,
                    this.defaultMaxDegreeOfConcurrency,
                    this.cosmosClientContext.SerializerCore,
                    this.ExecuteAsync,
                    this.ReBatchAsync,
                    this.cosmosClientContext,
                    options);
                if (!this.streamersByPartitionKeyRange.TryAdd(partitionKeyRangeId, newStreamer))
                {
                    newStreamer.Dispose();
                }

                return this.streamersByPartitionKeyRange[partitionKeyRangeId];
            }
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
