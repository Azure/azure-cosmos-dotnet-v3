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
        private readonly TimerWheel timerWheel;
        private readonly RetryOptions retryOptions;
        private readonly int defaultMaxDegreeOfConcurrency = 50;

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
            this.timerWheel = TimerWheel.CreateTimerWheel(BatchAsyncContainerExecutor.TimerWheelResolution, BatchAsyncContainerExecutor.TimerWheelBucketCount);
            this.retryOptions = cosmosClientContext.ClientOptions.GetConnectionPolicy().RetryOptions;
        }

        public virtual async Task<TransactionalBatchOperationResult> AddAsync(
            ItemBatchOperation operation,
            ItemRequestOptions itemRequestOptions = null,
            CancellationToken cancellationToken = default)
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            await this.ValidateOperationAsync(operation, itemRequestOptions, cancellationToken);

            string resolvedPartitionKeyRangeId = await this.ResolvePartitionKeyRangeIdAsync(operation, cancellationToken).ConfigureAwait(false);
            BatchAsyncStreamer streamer = this.GetOrAddStreamerForPartitionKeyRange(resolvedPartitionKeyRangeId);
            ItemBatchOperationContext context = new ItemBatchOperationContext(resolvedPartitionKeyRangeId, BatchAsyncContainerExecutor.GetRetryPolicy(this.cosmosContainer, operation.OperationType, this.retryOptions));
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

            this.timerWheel.Dispose();
        }

        internal virtual async Task ValidateOperationAsync(
            ItemBatchOperation operation,
            ItemRequestOptions itemRequestOptions = null,
            CancellationToken cancellationToken = default)
        {
            if (itemRequestOptions != null)
            {
                if (itemRequestOptions.BaseConsistencyLevel.HasValue
                                || itemRequestOptions.PreTriggers != null
                                || itemRequestOptions.PostTriggers != null
                                || itemRequestOptions.SessionToken != null
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

        private static void AddHeadersToRequestMessage(RequestMessage requestMessage, string partitionKeyRangeId)
        {
            requestMessage.Headers.PartitionKeyRangeId = partitionKeyRangeId;
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.ShouldBatchContinueOnError, bool.TrueString);
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.IsBatchAtomic, bool.FalseString);
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.IsBatchRequest, bool.TrueString);
        }

        private async Task ReBatchAsync(
            ItemBatchOperation operation,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            using (ITrace retryTrace = trace.StartChild("Batch Retry Async", TraceComponent.Batch, Tracing.TraceLevel.Info))
            {
                string resolvedPartitionKeyRangeId = await this.ResolvePartitionKeyRangeIdAsync(operation, cancellationToken).ConfigureAwait(false);
                operation.Context.ReRouteOperation(resolvedPartitionKeyRangeId);
                BatchAsyncStreamer streamer = this.GetOrAddStreamerForPartitionKeyRange(resolvedPartitionKeyRangeId);
                streamer.Add(operation);
            }
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
                return await this.cosmosContainer.GetNonePartitionKeyValueAsync(NoOpTrace.Singleton, cancellationToken).ConfigureAwait(false);
            }

            return operation.PartitionKey.Value.InternalKey;
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
                    Debug.Assert(serverRequestPayload != null, "Server request payload expected to be non-null");
                    ResponseMessage responseMessage = await this.cosmosClientContext.ProcessResourceOperationStreamAsync(
                        this.cosmosContainer.LinkUri,
                        ResourceType.Document,
                        OperationType.Batch,
                        new RequestOptions(),
                        cosmosContainerCore: this.cosmosContainer,
                        feedRange: null,
                        streamPayload: serverRequestPayload,
                        requestEnricher: requestMessage => BatchAsyncContainerExecutor.AddHeadersToRequestMessage(requestMessage, serverRequest.PartitionKeyRangeId),
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

        private BatchAsyncStreamer GetOrAddStreamerForPartitionKeyRange(string partitionKeyRangeId)
        {
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
                this.cosmosClientContext);
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
