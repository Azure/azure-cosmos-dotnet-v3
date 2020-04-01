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
    using System.IO.Compression;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Diagnostics;
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
        private readonly int defaultMaxDegreeOfConcurrency = 50;

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

                if (itemRequestOptions.DiagnosticContextFactory != null)
                {
                    throw new ArgumentException("DiagnosticContext is not allowed when AllowBulkExecution is set to true");
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
                            | itemRequestOptions.Properties.TryGetValue(WFConstants.BackendHeaders.EffectivePartitionKeyString, out object epkStrObj)
                            | itemRequestOptions.Properties.TryGetValue(HttpConstants.HttpHeaders.PartitionKey, out object pkStringObj)))
            {
                byte[] epk = epkObj as byte[];
                string epkStr = epkStrObj as string;
                string pkString = pkStringObj as string;
                if ((epk == null && pkString == null) || epkStr == null)
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
            //requestMessage.Headers.Add(HttpConstants.HttpHeaders.ContentEncoding, "gzip");
            //requestMessage.Headers.Add(HttpConstants.HttpHeaders.TransferEncoding, "gzip");
            //requestMessage.Headers.Add(HttpConstants.HttpHeaders.AcceptEncoding, "gzip");
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
            CosmosDiagnosticsContext diagnosticsContext = new CosmosDiagnosticsContextCore();
            CosmosDiagnosticScope limiterScope = diagnosticsContext.CreateScope("BatchAsyncContainerExecutor.Limiter");
            SemaphoreSlim limiter = this.GetOrAddLimiterForPartitionKeyRange(serverRequest.PartitionKeyRangeId);
            using (await limiter.UsingWaitAsync(cancellationToken))
            {
                limiterScope.Dispose();
                using (MemoryStream serverRequestPayload = serverRequest.TransferBodyStream())
                {
                    //Console.WriteLine(
                    //"Before compression Capacity = {0}, Length = {1}, Position = {2}\n",
                    //serverRequestPayload.Capacity.ToString(),
                    //serverRequestPayload.Length.ToString(),
                    //serverRequestPayload.Position.ToString());

                    //serverRequestPayload.Seek(0, SeekOrigin.Begin);

                    //// Read the first 20 bytes from the stream.
                    //byte[] byteArray = new byte[serverRequestPayload.Length];
                    //int count = serverRequestPayload.Read(byteArray, 0, 20);

                    //// Read the remaining bytes, byte by byte.
                    //while (count < serverRequestPayload.Length)
                    //{
                    //    byteArray[count++] =
                    //        Convert.ToByte(serverRequestPayload.ReadByte());
                    //}

                    //ASCIIEncoding encoding = new ASCIIEncoding();

                    //char[] charArray = new char[encoding.GetCharCount(
                    //         byteArray, 0, count)];
                    //encoding.GetDecoder().GetChars(
                    //    byteArray, 0, count, charArray, 0);
                    //Console.WriteLine(charArray);

                    //serverRequestPayload.Seek(0, SeekOrigin.Begin);

                    // Compress now
                    //MemoryStream outputStream = new MemoryStream();
                    //GZipStream gZipStream = new GZipStream(outputStream, CompressionMode.Compress, true);
                    //await serverRequestPayload.CopyToAsync(gZipStream);
                    //await gZipStream.FlushAsync();

                    //Console.WriteLine(
                    //"After compression Capacity = {0}, Length = {1}, Position = {2}\n",
                    //outputStream.Capacity.ToString(),
                    //outputStream.Length.ToString(),
                    //outputStream.Position.ToString());

                    //outputStream.Seek(0, SeekOrigin.Begin);

                    //// Read the first 20 bytes from the stream.
                    //byteArray = new byte[outputStream.Length];
                    //count = outputStream.Read(byteArray, 0, 20);

                    //// Read the remaining bytes, byte by byte.
                    //while (count < outputStream.Length)
                    //{
                    //    byteArray[count++] =
                    //        Convert.ToByte(outputStream.ReadByte());
                    //}

                    //charArray = new char[encoding.GetCharCount(
                    //         byteArray, 0, count)];
                    //encoding.GetDecoder().GetChars(
                    //    byteArray, 0, count, charArray, 0);
                    //Console.WriteLine(charArray);

                    //outputStream.Seek(0, SeekOrigin.Begin);

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
                        diagnosticsContext: diagnosticsContext,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    using (diagnosticsContext.CreateScope("BatchAsyncContainerExecutor.ToResponse"))
                    {
                        TransactionalBatchResponse serverResponse = await TransactionalBatchResponse.FromResponseMessageAsync(responseMessage, serverRequest, this.cosmosClientContext.SerializerCore).ConfigureAwait(false);

                        return new PartitionKeyRangeBatchExecutionResult(serverRequest.PartitionKeyRangeId, serverRequest.Operations, serverResponse);
                    }
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
                this.dispatchTimerInSeconds,
                this.timerPool,
                limiter,
                this.defaultMaxDegreeOfConcurrency,
                this.cosmosClientContext.SerializerCore,
                this.ExecuteAsync,
                this.ReBatchAsync);
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
