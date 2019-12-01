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
    using Microsoft.Azure.Cosmos.Common;
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
        private readonly ConcurrentBag<(int, bool, double, double)> countsAndLatencies = new ConcurrentBag<(int, bool, double, double)>();

        private PartitionKeyDefinition partitionKeyDefinition;
        private CollectionRoutingMap collectionRoutingMap;

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
            this.stopwatch.Start();
        }

        public virtual Task<ResponseMessage> AddAsync(
            ItemBatchOperation operation,
            ItemRequestOptions itemRequestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            Task<ResponseMessage> response = this.TryAddAsync(operation, itemRequestOptions, cancellationToken);
            if (response != null)
            {
                return response;
            }

            return this.AddImplAsync(operation, itemRequestOptions, cancellationToken);
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

            IEnumerable<IGrouping<int, (int, bool, double, double)>> gs = this.countsAndLatencies.GroupBy(i => i.Item1);
            double totalCharge = 0;
            foreach (IGrouping<int, (int, bool, double, double)> g in gs)
            {
                Console.WriteLine($"BatchItemCount: {g.Key} BatchCount: {g.Count()} RateLimitedBatches: {g.Count(h => h.Item2)} AvgLatency: {g.Average(h => h.Item4)}");
                totalCharge += g.Sum(h => h.Item3);
            }

            Console.WriteLine($"TotalCharge: {totalCharge}");
        }

        private Task<ResponseMessage> TryAddAsync(
            ItemBatchOperation operation,
            ItemRequestOptions itemRequestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            BatchAsyncContainerExecutor.ValidateOperation(operation, itemRequestOptions);

            if (this.collectionRoutingMap == null || this.partitionKeyDefinition == null)
            {
                return null;
            }

            // None partition key needs special resolution based on container partition key definition.
            if (object.ReferenceEquals(operation.PartitionKey, PartitionKey.None))
            {
                return null;
            }

            if (!operation.TryMaterializeResource(this.cosmosClientContext.CosmosSerializer))
            {
                return null;
            }

            string resolvedPartitionKeyRangeId = this.ResolvePartitionKeyRangeId(operation);
            return this.AddImplAsync(operation, resolvedPartitionKeyRangeId);
        }

        private async Task<ResponseMessage> AddImplAsync(
            ItemBatchOperation operation,
            ItemRequestOptions itemRequestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            await operation.MaterializeResourceAsync(this.cosmosClientContext.CosmosSerializer, cancellationToken);

            string resolvedPartitionKeyRangeId = await this.ResolvePartitionKeyRangeIdAsync(operation, cancellationToken).ConfigureAwait(false);
            return await this.AddImplAsync(operation, resolvedPartitionKeyRangeId);
        }

        private Task<ResponseMessage> AddImplAsync(
            ItemBatchOperation operation,
            string resolvedPartitionKeyRangeId)
        {
            BatchAsyncStreamer streamer = this.GetOrAddStreamerForPartitionKeyRange(resolvedPartitionKeyRangeId);
            ItemBatchOperationContext context = new ItemBatchOperationContext(resolvedPartitionKeyRangeId, BatchAsyncContainerExecutor.GetRetryPolicy(this.retryOptions));
            operation.AttachContext(context);
            streamer.Add(operation);
            return context.OperationTask;
        }

        private static void ValidateOperation(
            ItemBatchOperation operation,
            ItemRequestOptions itemRequestOptions = null)
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

        private string ResolvePartitionKeyRangeId(ItemBatchOperation operation)
        {
            Debug.Assert(operation.RequestOptions?.Properties?.TryGetValue(WFConstants.BackendHeaders.EffectivePartitionKeyString, out object epkObj) == null, "EPK is not supported");
            Debug.Assert(!object.ReferenceEquals(operation.PartitionKey, PartitionKey.None));
            Debug.Assert(this.partitionKeyDefinition != null);
            Debug.Assert(this.collectionRoutingMap != null);
            operation.PartitionKeyJson = operation.PartitionKey.Value.ToString();
            return BatchExecUtils.GetPartitionKeyRangeId(operation.PartitionKey.Value, this.partitionKeyDefinition, this.collectionRoutingMap);
        }

        private async Task<string> ResolvePartitionKeyRangeIdAsync(
            ItemBatchOperation operation,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Debug.Assert(operation.RequestOptions?.Properties?.TryGetValue(WFConstants.BackendHeaders.EffectivePartitionKeyString, out object epkObj) == null, "EPK is not supported");
            await this.FillOperationPropertiesAsync(operation, cancellationToken);
            this.partitionKeyDefinition = await this.cosmosContainer.GetPartitionKeyDefinitionAsync();
            this.collectionRoutingMap = await this.cosmosContainer.GetRoutingMapAsync(CancellationToken.None);
            return BatchExecUtils.GetPartitionKeyRangeId(operation.PartitionKey.Value, this.partitionKeyDefinition, this.collectionRoutingMap);
        }

        private async Task FillOperationPropertiesAsync(ItemBatchOperation operation, CancellationToken cancellationToken)
        {
            // Same logic from RequestInvokerHandler to manage partition key migration
            if (object.ReferenceEquals(operation.PartitionKey, PartitionKey.None))
            {
                Documents.Routing.PartitionKeyInternal partitionKeyInternal = await this.cosmosContainer.GetNonePartitionKeyValueAsync(cancellationToken).ConfigureAwait(false);
                operation.PartitionKeyJson = partitionKeyInternal.ToJsonString();
            }
            else
            {
                operation.PartitionKeyJson = operation.PartitionKey.Value.ToString();
            }
        }

        private async Task<PartitionKeyRangeBatchExecutionResult> ExecuteAsync(
            PartitionKeyRangeServerBatchRequest serverRequest,
            CancellationToken cancellationToken)
        {
            SemaphoreSlim limiter = this.GetOrAddLimiterForPartitionKeyRange(serverRequest.PartitionKeyRangeId);
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
                    //await Task.CompletedTask.ConfigureAwait(false);
                    TransactionalBatchResponse serverResponse = // new TransactionalBatchResponse(System.Net.HttpStatusCode.OK, SubStatusCodes.Unknown, null, serverRequest.Operations);
                    await TransactionalBatchResponse.FromResponseMessageAsync(responseMessage, serverRequest, this.cosmosClientContext.CosmosSerializer).ConfigureAwait(false);
                    this.countsAndLatencies.Add(
                        (serverRequest.Operations.Count,
                        serverResponse.Any(r => r.StatusCode == (System.Net.HttpStatusCode)429),
                        serverResponse.RequestCharge,
                        (this.stopwatch.Elapsed - start).TotalMilliseconds));
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

            BatchAsyncStreamer newStreamer = new BatchAsyncStreamer(this.maxServerRequestOperationCount, this.maxServerRequestBodyLength, this.dispatchTimerInSeconds, this.timerPool, this.cosmosClientContext.CosmosSerializer, this.ExecuteAsync, this.ReBatchAsync);
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

            SemaphoreSlim newLimiter = new SemaphoreSlim(1, 1);
            if (!this.limitersByPartitionkeyRange.TryAdd(partitionKeyRangeId, newLimiter))
            {
                newLimiter.Dispose();
            }

            return this.limitersByPartitionkeyRange[partitionKeyRangeId];
        }
    }
}
