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
    /// It groups operations by Partition Key Range and sends them to the Batch API and then unifies results as they become available. It uses <see cref="BatchAsyncStreamer"/> as batch processor and <see cref="ExecuteAsync(IReadOnlyList{BatchAsyncOperationContext}, System.Threading.CancellationToken)"/> as batch executing handler.
    /// </remarks>
    /// <seealso cref="BatchAsyncStreamer"/>
    internal class BatchAsyncContainerExecutor
    {
        private const int DefaultDispatchTimer = 10;
        private const int MinimumDispatchTimerInSeconds = 1;

        private readonly ContainerCore cosmosContainer;
        private readonly CosmosClientContext cosmosClientContext;
        private readonly int maxServerRequestBodyLength;
        private readonly int maxServerRequestOperationCount;
        private readonly int dispatchTimerInSeconds;
        private readonly ConcurrentDictionary<string, BatchAsyncStreamer> streamersByPartitionKeyRange = new ConcurrentDictionary<string, BatchAsyncStreamer>();
        private readonly ConcurrentDictionary<string, SemaphoreSlim> limitersByPartitionkeyRange = new ConcurrentDictionary<string, SemaphoreSlim>();
        private readonly TimerPool timerPool;

        public BatchAsyncContainerExecutor(
            ContainerCore cosmosContainer,
            CosmosClientContext cosmosClientContext,
            int maxServerRequestOperationCount,
            int maxServerRequestBodyLength,
            int dispatchTimerInSeconds = BatchAsyncContainerExecutor.DefaultDispatchTimer)
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
        }

        public async Task<BatchOperationResult> AddAsync(
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
            BatchAsyncStreamer streamer = await this.GetOrAddStreamerForPartitionKeyRangeAsync(resolvedPartitionKeyRangeId).ConfigureAwait(false);
            BatchAsyncOperationContext context = new BatchAsyncOperationContext(resolvedPartitionKeyRangeId, operation);
            await streamer.AddAsync(context).ConfigureAwait(false);
            return await context.Task;
        }

        public async Task DisposeAsync()
        {
            foreach (KeyValuePair<string, BatchAsyncStreamer> streamer in this.streamersByPartitionKeyRange)
            {
                await streamer.Value.DisposeAsync();
            }

            foreach (KeyValuePair<string, SemaphoreSlim> limiter in this.limitersByPartitionkeyRange)
            {
                limiter.Value.Dispose();
            }

            this.timerPool.Dispose();
        }

        internal async Task ValidateOperationAsync(
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
                    throw new InvalidOperationException(ClientResources.UnsupportedBatchRequestOptions);
                }

                Debug.Assert(BatchAsyncContainerExecutor.ValidateOperationEPK(operation, itemRequestOptions));
            }

            await operation.MaterializeResourceAsync(this.cosmosClientContext.CosmosSerializer, cancellationToken).ConfigureAwait(false);

            int itemByteSize = operation.GetApproximateSerializedLength();

            if (itemByteSize > this.maxServerRequestBodyLength)
            {
                throw new ArgumentException(RMResources.RequestTooLarge);
            }
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

        /// <summary>
        /// If because of HybridRow serialization overhead, not all operations fit in the request, we send those extra operations in a separate request.
        /// </summary>
        private static IEnumerable<BatchAsyncOperationContext> GetOverflowOperations(
            PartitionKeyServerBatchRequest request,
            IEnumerable<BatchAsyncOperationContext> operationsSentToRequest)
        {
            int totalOperations = operationsSentToRequest.Count();
            int operationsThatOverflowed = totalOperations - request.Operations.Count;
            if (operationsThatOverflowed == 0)
            {
                return Enumerable.Empty<BatchAsyncOperationContext>();
            }

            return operationsSentToRequest.Skip(totalOperations - operationsThatOverflowed);
        }

        private static IReadOnlyList<BatchAsyncOperationContext> GetOperationsToRetry(
            IReadOnlyList<BatchAsyncOperationContext> baseOperations,
            IEnumerable<int> indexes)
        {
            List<BatchAsyncOperationContext> operations = new List<BatchAsyncOperationContext>();
            foreach (int index in indexes)
            {
                operations.Add(baseOperations[index]);
            }

            return operations;
        }

        private async Task ReBatchAsync(
            BatchAsyncOperationContext context,
            CancellationToken cancellationToken)
        {
            string resolvedPartitionKeyRangeId = await this.ResolvePartitionKeyRangeIdAsync(context.Operation, cancellationToken).ConfigureAwait(false);
            BatchAsyncStreamer streamer = await this.GetOrAddStreamerForPartitionKeyRangeAsync(resolvedPartitionKeyRangeId).ConfigureAwait(false);
            await streamer.AddAsync(context).ConfigureAwait(false);
        }

        private async Task<PartitionKeyBatchResponse> ExecuteAsync(
            IReadOnlyList<BatchAsyncOperationContext> operations,
            CancellationToken cancellationToken)
        {
            // All operations should be for the same PKRange
            string partitionKeyRangeId = operations[0].PartitionKeyRangeId;
            PartitionKeyServerBatchRequest serverRequest = await this.CreateServerRequestAsync(partitionKeyRangeId, operations.Select(o => o.Operation), cancellationToken);
            // Any overflow goes to a new batch
            IEnumerable<BatchAsyncOperationContext> overFlowOperations = BatchAsyncContainerExecutor.GetOverflowOperations(serverRequest, operations);
            foreach (BatchAsyncOperationContext overflowedContext in overFlowOperations)
            {
                await this.ReBatchAsync(overflowedContext, cancellationToken);
            }

            PartitionKeyRangeBatchExecutionResult result = await this.ExecuteServerRequestAsync(serverRequest, cancellationToken);

            List<BatchResponse> responses = new List<BatchResponse>(serverRequest.Operations.Count);
            if (!result.ContainsSplit())
            {
                responses.AddRange(result.ServerResponses);
            }
            else
            {
                IReadOnlyList<BatchAsyncOperationContext> retryContexts = BatchAsyncContainerExecutor.GetOperationsToRetry(operations, result.Operations.Select(o => o.OperationIndex));
                foreach (BatchAsyncOperationContext retryContext in retryContexts)
                {
                    await this.ReBatchAsync(retryContext, cancellationToken);
                }
            }

            return new PartitionKeyBatchResponse(responses, this.cosmosClientContext.CosmosSerializer);
        }

        private async Task<string> ResolvePartitionKeyRangeIdAsync(
            ItemBatchOperation operation,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PartitionKeyDefinition partitionKeyDefinition = await this.cosmosContainer.GetPartitionKeyDefinitionAsync(cancellationToken);
            CollectionRoutingMap collectionRoutingMap = await this.cosmosContainer.GetRoutingMapAsync(cancellationToken);

            Debug.Assert(operation.RequestOptions?.Properties?.TryGetValue(WFConstants.BackendHeaders.EffectivePartitionKeyString, out object epkObj) == null, "EPK is not supported");
            await this.FillOperationPropertiesAsync(operation, cancellationToken);
            return BatchExecUtils.GetPartitionKeyRangeId(operation.PartitionKey.Value, partitionKeyDefinition, collectionRoutingMap);
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

        private async Task<PartitionKeyServerBatchRequest> CreateServerRequestAsync(
            string partitionKeyRangeId,
            IEnumerable<ItemBatchOperation> operations,
            CancellationToken cancellationToken)
        {
            ArraySegment<ItemBatchOperation> operationsArraySegment = new ArraySegment<ItemBatchOperation>(operations.ToArray());

            PartitionKeyServerBatchRequest request = await PartitionKeyServerBatchRequest.CreateAsync(
                  partitionKeyRangeId,
                  operationsArraySegment,
                  this.maxServerRequestBodyLength,
                  this.maxServerRequestOperationCount,
                  ensureContinuousOperationIndexes: false,
                  serializer: this.cosmosClientContext.CosmosSerializer,
                  cancellationToken: cancellationToken).ConfigureAwait(false);

            return request;
        }

        private async Task<PartitionKeyRangeBatchExecutionResult> ExecuteServerRequestAsync(
            PartitionKeyServerBatchRequest serverRequest,
            CancellationToken cancellationToken)
        {
            SemaphoreSlim limiter = this.GetOrAddLimiterForPartitionKeyRange(serverRequest.PartitionKeyRangeId);
            using (await limiter.UsingWaitAsync(cancellationToken))
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
                        partitionKey: null,
                        streamPayload: serverRequestPayload,
                        requestEnricher: requestMessage => BatchAsyncContainerExecutor.AddHeadersToRequestMessage(requestMessage, serverRequest.PartitionKeyRangeId),
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                    
                    BatchResponse serverResponse = await BatchResponse.FromResponseMessageAsync(responseMessage, serverRequest, this.cosmosClientContext.CosmosSerializer).ConfigureAwait(false);

                    return new PartitionKeyRangeBatchExecutionResult(serverRequest.PartitionKeyRangeId, serverRequest.Operations, new List<BatchResponse>() { serverResponse });
                }
            }
        }

        private async Task<BatchAsyncStreamer> GetOrAddStreamerForPartitionKeyRangeAsync(string partitionKeyRangeId)
        {
            if (this.streamersByPartitionKeyRange.TryGetValue(partitionKeyRangeId, out BatchAsyncStreamer streamer))
            {
                return streamer;
            }

            BatchAsyncStreamer newStreamer = new BatchAsyncStreamer(this.maxServerRequestOperationCount, this.maxServerRequestBodyLength, this.dispatchTimerInSeconds, this.timerPool, this.cosmosClientContext.CosmosSerializer, this.ExecuteAsync);
            if (!this.streamersByPartitionKeyRange.TryAdd(partitionKeyRangeId, newStreamer))
            {
                await newStreamer.DisposeAsync();
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
