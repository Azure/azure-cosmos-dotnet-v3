// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Pagination;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.ReadFeed.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    internal sealed class NetworkAttachedDocumentContainer : IMonadicDocumentContainer
    {
        private readonly ContainerInternal container;
        private readonly CosmosQueryClient cosmosQueryClient;
        private readonly QueryRequestOptions queryRequestOptions;
        private readonly CosmosDiagnosticsContext diagnosticsContext;
        private readonly string resourceLink;
        private readonly ResourceType resourceType;

        public NetworkAttachedDocumentContainer(
            ContainerInternal container,
            CosmosQueryClient cosmosQueryClient,
            CosmosDiagnosticsContext diagnosticsContext,
            QueryRequestOptions queryRequestOptions = null,
            string resourceLink = null,
            ResourceType resourceType = ResourceType.Document)
        {
            this.container = container ?? throw new ArgumentNullException(nameof(container));
            this.cosmosQueryClient = cosmosQueryClient ?? throw new ArgumentNullException(nameof(cosmosQueryClient));
            this.diagnosticsContext = diagnosticsContext;
            this.queryRequestOptions = queryRequestOptions;
            this.resourceLink = resourceLink ?? this.container.LinkUri;
            this.resourceType = resourceType;
        }

        public Task<TryCatch> MonadicSplitAsync(
            FeedRangeInternal feedRange,
            CancellationToken cancellationToken) => Task.FromResult(TryCatch.FromException(new NotSupportedException()));

        public Task<TryCatch> MonadicMergeAsync(
            FeedRangeInternal feedRange1,
            FeedRangeInternal feedRange2,
            CancellationToken cancellationToken) => Task.FromResult(TryCatch.FromException(new NotSupportedException()));

        public async Task<TryCatch<Record>> MonadicCreateItemAsync(
            CosmosObject payload,
            CancellationToken cancellationToken)
        {
            ItemResponse<CosmosObject> tryInsertDocument = await this.container.CreateItemAsync(
                payload,
                cancellationToken: cancellationToken);
            if (tryInsertDocument.StatusCode != HttpStatusCode.Created)
            {
                return TryCatch<Record>.FromException(
                    new CosmosException(
                        message: "Failed to insert document",
                        statusCode: tryInsertDocument.StatusCode,
                        subStatusCode: default,
                        activityId: tryInsertDocument.ActivityId,
                        requestCharge: tryInsertDocument.RequestCharge));
            }

            CosmosObject insertedDocument = tryInsertDocument.Resource;
            string identifier = ((CosmosString)insertedDocument["id"]).Value;
            ResourceId resourceIdentifier = ResourceId.Parse(((CosmosString)insertedDocument["_rid"]).Value);
            long ticks = Number64.ToLong(((CosmosNumber)insertedDocument["_ts"]).Value);

            Record record = new Record(
                resourceIdentifier, 
                new DateTime(ticks: ticks, DateTimeKind.Utc),
                identifier, 
                insertedDocument);

            return TryCatch<Record>.FromResult(record);
        }

        public Task<TryCatch<Record>> MonadicReadItemAsync(
            CosmosElement partitionKey,
            string identifer,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<TryCatch<List<FeedRangeEpk>>> MonadicGetFeedRangesAsync(
            ITrace trace,
            CancellationToken cancellationToken) => this.MonadicGetChildRangeAsync(FeedRangeEpk.FullRange, trace, cancellationToken);

        public async Task<TryCatch<List<FeedRangeEpk>>> MonadicGetChildRangeAsync(
            FeedRangeInternal feedRange,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            try
            {
                ContainerProperties containerProperties = await this.container.ClientContext.GetCachedContainerPropertiesAsync(
                    this.container.LinkUri,
                    trace,
                    cancellationToken);
                List<PartitionKeyRange> overlappingRanges = await this.cosmosQueryClient.GetTargetPartitionKeyRangeByFeedRangeAsync(
                    this.container.LinkUri,
                    await this.container.GetCachedRIDAsync(cancellationToken: cancellationToken),
                    containerProperties.PartitionKey,
                    feedRange,
                    forceRefresh: false,
                    trace);
                return TryCatch<List<FeedRangeEpk>>.FromResult(
                    overlappingRanges.Select(range => new FeedRangeEpk(
                        new Documents.Routing.Range<string>(
                            min: range.MinInclusive,
                            max: range.MaxExclusive,
                            isMinInclusive: true,
                            isMaxInclusive: false))).ToList());
            }
            catch (Exception ex)
            {
                return TryCatch<List<FeedRangeEpk>>.FromException(ex);
            }
        }

        public async Task<TryCatch> MonadicRefreshProviderAsync(
            ITrace trace,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using (ITrace refreshTrace = trace.StartChild("Refresh FeedRangeProvider", TraceComponent.Routing, TraceLevel.Info))
            {
                try
                {
                    // We can refresh the cache by just getting all the ranges for this container using the force refresh flag
                    _ = await this.cosmosQueryClient.TryGetOverlappingRangesAsync(
                        this.container.LinkUri,
                        FeedRangeEpk.FullRange.Range,
                        forceRefresh: true);

                    return TryCatch.FromResult();
                }
                catch (Exception ex)
                {
                    return TryCatch.FromException(ex);
                }
            }
        }

        public async Task<TryCatch<ReadFeedPage>> MonadicReadFeedAsync(
            ReadFeedState readFeedState,
            FeedRangeInternal feedRange,
            QueryRequestOptions queryRequestOptions,
            int pageSize,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CosmosDiagnosticsContext cosmosDiagnosticsContext = CosmosDiagnosticsContext.Create(this.queryRequestOptions);
            using (cosmosDiagnosticsContext.GetOverallScope())
            {
                if (queryRequestOptions != null)
                {
                    queryRequestOptions.MaxItemCount = pageSize;
                }

                ResponseMessage responseMessage = await this.container.ClientContext.ProcessResourceOperationStreamAsync(
                   resourceUri: this.resourceLink,
                   resourceType: this.resourceType,
                   operationType: OperationType.ReadFeed,
                   requestOptions: queryRequestOptions,
                   cosmosContainerCore: this.container,
                   requestEnricher: request =>
                   {
                       if (readFeedState is ReadFeedContinuationState readFeedContinuationState)
                       {
                           request.Headers.ContinuationToken = ((CosmosString)readFeedContinuationState.ContinuationToken).Value;
                       }
                   },
                   feedRange: feedRange,
                   streamPayload: default,
                   diagnosticsContext: cosmosDiagnosticsContext,
                   trace: trace,
                   cancellationToken: cancellationToken);

                TryCatch<ReadFeedPage> monadicReadFeedPage;
                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    ReadFeedPage readFeedPage = new ReadFeedPage(
                        responseMessage.Content,
                        responseMessage.Headers.RequestCharge,
                        responseMessage.Headers.ActivityId,
                        responseMessage.DiagnosticsContext,
                        responseMessage.Headers.ContinuationToken != null ? ReadFeedState.Continuation(CosmosString.Create(responseMessage.Headers.ContinuationToken)) : null);

                    monadicReadFeedPage = TryCatch<ReadFeedPage>.FromResult(readFeedPage);
                }
                else
                {
                    CosmosException cosmosException = new CosmosException(
                        statusCode: responseMessage.StatusCode,
                        responseMessage.ErrorMessage,
                        (int)responseMessage.Headers.SubStatusCode,
                        stackTrace: null,
                        responseMessage.Headers.ActivityId,
                        responseMessage.Headers.RequestCharge,
                        responseMessage.Headers.RetryAfter,
                        responseMessage.Headers,
                        responseMessage.DiagnosticsContext,
                        error: null,
                        innerException: null);
                    cosmosException.Headers.ContinuationToken = responseMessage.Headers.ContinuationToken;

                    monadicReadFeedPage = TryCatch<ReadFeedPage>.FromException(cosmosException);
                }

                return monadicReadFeedPage;
            }
        }

        public async Task<TryCatch<QueryPage>> MonadicQueryAsync(
            SqlQuerySpec sqlQuerySpec,
            string continuationToken,
            FeedRangeInternal feedRange,
            int pageSize,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (sqlQuerySpec == null)
            {
                throw new ArgumentNullException(nameof(sqlQuerySpec));
            }

            if (feedRange == null)
            {
                throw new ArgumentNullException(nameof(feedRange));
            }

            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            QueryRequestOptions queryRequestOptions = this.queryRequestOptions == null ? new QueryRequestOptions() : this.queryRequestOptions.Clone();
            TryCatch<QueryPage> monadicQueryPage = await this.cosmosQueryClient.ExecuteItemQueryAsync(
                this.resourceLink,
                this.resourceType,
                Documents.OperationType.Query,
                Guid.NewGuid(),
                feedRange,
                queryRequestOptions,
                queryPageDiagnostics: this.AddQueryPageDiagnostic,
                sqlQuerySpec,
                continuationToken,
                isContinuationExpected: false,
                pageSize,
                trace,
                cancellationToken);

            return monadicQueryPage;
        }

        public async Task<TryCatch<ChangeFeedPage>> MonadicChangeFeedAsync(
            ChangeFeedState state,
            FeedRangeInternal feedRange,
            int pageSize,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ResponseMessage responseMessage = await this.container.ClientContext.ProcessResourceOperationStreamAsync(
                resourceUri: this.container.LinkUri,
                resourceType: ResourceType.Document,
                operationType: OperationType.ReadFeed,
                requestOptions: default,
                cosmosContainerCore: this.container,
                requestEnricher: (request) =>
                {
                    state.Accept(ChangeFeedStateRequestMessagePopulator.Singleton, request);

                    request.Headers.PageSize = pageSize.ToString();
                    request.Headers.Add(
                        HttpConstants.HttpHeaders.A_IM,
                        HttpConstants.A_IMHeaderValues.IncrementalFeed);
                },
                feedRange: feedRange,
                streamPayload: default,
                diagnosticsContext: this.diagnosticsContext,
                trace: trace,
                cancellationToken: cancellationToken);

            TryCatch<ChangeFeedPage> monadicChangeFeedPage;
            if (responseMessage.StatusCode == HttpStatusCode.OK)
            {
                ChangeFeedPage changeFeedPage = new ChangeFeedSuccessPage(
                    responseMessage.Content,
                    responseMessage.Headers.RequestCharge,
                    responseMessage.Headers.ActivityId,
                    ChangeFeedState.Continuation(CosmosString.Create(responseMessage.Headers.ETag)));

                monadicChangeFeedPage = TryCatch<ChangeFeedPage>.FromResult(changeFeedPage);
            }
            else if (responseMessage.StatusCode == HttpStatusCode.NotModified)
            {
                ChangeFeedPage changeFeedPage = new ChangeFeedNotModifiedPage(
                    responseMessage.Headers.RequestCharge,
                    responseMessage.Headers.ActivityId,
                    ChangeFeedState.Continuation(CosmosString.Create(responseMessage.Headers.ETag)));

                monadicChangeFeedPage = TryCatch<ChangeFeedPage>.FromResult(changeFeedPage);
            }
            else
            {
                CosmosException cosmosException = new CosmosException(
                    responseMessage.ErrorMessage,
                    statusCode: responseMessage.StatusCode,
                    (int)responseMessage.Headers.SubStatusCode,
                    responseMessage.Headers.ActivityId,
                    responseMessage.Headers.RequestCharge);
                cosmosException.Headers.ContinuationToken = responseMessage.Headers.ContinuationToken;

                monadicChangeFeedPage = TryCatch<ChangeFeedPage>.FromException(cosmosException);
            }

            return monadicChangeFeedPage;
        }

        private void AddQueryPageDiagnostic(QueryPageDiagnostics queryPageDiagnostics)
        {
            this.diagnosticsContext.AddDiagnosticsInternal(queryPageDiagnostics);
        }

        public async Task<TryCatch<string>> MonadicGetResourceIdentifierAsync(ITrace trace, CancellationToken cancellationToken)
        {
            using (ITrace getRidTrace = trace.StartChild("Get Container RID", TraceComponent.Routing, TraceLevel.Info))
            {
                try
                {
                    string resourceIdentifier = await this.container.GetCachedRIDAsync(forceRefresh: false, cancellationToken);
                    return TryCatch<string>.FromResult(resourceIdentifier);
                }
                catch (Exception ex)
                {
                    return TryCatch<string>.FromException(ex);
                }
            }
        }

        private sealed class ChangeFeedStateRequestMessagePopulator : IChangeFeedStateVisitor<RequestMessage>
        {
            public static readonly ChangeFeedStateRequestMessagePopulator Singleton = new ChangeFeedStateRequestMessagePopulator();

            private const string IfNoneMatchAllHeaderValue = "*";

            private static readonly DateTime StartFromBeginningTime = DateTime.MinValue.ToUniversalTime();

            private ChangeFeedStateRequestMessagePopulator()
            {
            }

            public void Visit(ChangeFeedStateBeginning changeFeedStateBeginning, RequestMessage message)
            {
                // We don't need to set any headers to start from the beginning
            }

            public void Visit(ChangeFeedStateTime changeFeedStateTime, RequestMessage message)
            {
                // Our current public contract for ChangeFeedProcessor uses DateTime.MinValue.ToUniversalTime as beginning.
                // We need to add a special case here, otherwise it would send it as normal StartTime.
                // The problem is Multi master accounts do not support StartTime header on ReadFeed, and thus,
                // it would break multi master Change Feed Processor users using Start From Beginning semantics.
                // It's also an optimization, since the backend won't have to binary search for the value.
                if (changeFeedStateTime.StartTime != ChangeFeedStateRequestMessagePopulator.StartFromBeginningTime)
                {
                    message.Headers.Add(
                        HttpConstants.HttpHeaders.IfModifiedSince,
                        changeFeedStateTime.StartTime.ToString("r", CultureInfo.InvariantCulture));
                }
            }

            public void Visit(ChangeFeedStateContinuation changeFeedStateContinuation, RequestMessage message)
            {
                // On REST level, change feed is using IfNoneMatch/ETag instead of continuation
                message.Headers.IfNoneMatch = (changeFeedStateContinuation.ContinuationToken as CosmosString).Value;
            }

            public void Visit(ChangeFeedStateNow changeFeedStateNow, RequestMessage message)
            {
                message.Headers.IfNoneMatch = ChangeFeedStateRequestMessagePopulator.IfNoneMatchAllHeaderValue;
            }
        }
    }
}
