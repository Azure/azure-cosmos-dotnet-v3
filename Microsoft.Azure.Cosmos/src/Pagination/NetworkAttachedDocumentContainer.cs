// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Pagination;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Documents;

    internal sealed class NetworkAttachedDocumentContainer : IMonadicDocumentContainer
    {
        private static readonly PartitionKeyRange FullRange = new PartitionKeyRange()
        {
            MinInclusive = Documents.Routing.PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
            MaxExclusive = Documents.Routing.PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
        };

        private readonly ContainerCore container;
        private readonly CosmosQueryContext cosmosQueryContext;
        private readonly CosmosClientContext cosmosClientContext;
        private readonly ExecuteQueryBasedOnFeedRangeVisitor executeQueryBasedOnFeedRangeVisitor;

        public NetworkAttachedDocumentContainer(
            ContainerCore container,
            CosmosQueryContext cosmosQueryContext,
            CosmosClientContext cosmosClientContext)
        {
            this.container = container ?? throw new ArgumentNullException(nameof(container));
            this.cosmosQueryContext = cosmosQueryContext ?? throw new ArgumentNullException(nameof(cosmosQueryContext));
            this.cosmosClientContext = cosmosClientContext ?? throw new ArgumentNullException(nameof(cosmosClientContext));
            this.executeQueryBasedOnFeedRangeVisitor = new ExecuteQueryBasedOnFeedRangeVisitor(this);
        }

        public Task<TryCatch> MonadicSplitAsync(
            int partitionKeyRangeId,
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
            long timestamp = Number64.ToLong(((CosmosNumber)insertedDocument["_ts"]).Value);

            Record record = new Record(resourceIdentifier, timestamp, identifier, insertedDocument);

            return TryCatch<Record>.FromResult(record);
        }

        public Task<TryCatch<Record>> MonadicReadItemAsync(
            CosmosElement partitionKey,
            string identifer,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<TryCatch<List<PartitionKeyRange>>> MonadicGetFeedRangesAsync(
            CancellationToken cancellationToken) => this.MonadicGetChildRangeAsync(FullRange, cancellationToken);

        public async Task<TryCatch<List<PartitionKeyRange>>> MonadicGetChildRangeAsync(
            PartitionKeyRange partitionKeyRange,
            CancellationToken cancellationToken)
        {
            try
            {
                List<PartitionKeyRange> overlappingRanges = await this.cosmosQueryContext.QueryClient.GetTargetPartitionKeyRangesAsync(
                this.cosmosQueryContext.ResourceLink,
                this.cosmosQueryContext.ContainerResourceId,
                new List<Documents.Routing.Range<string>>() { partitionKeyRange.ToRange() });
                return TryCatch<List<PartitionKeyRange>>.FromResult(overlappingRanges);
            }
            catch (Exception ex)
            {
                return TryCatch<List<PartitionKeyRange>>.FromException(ex);
            }
        }

        public Task<TryCatch<DocumentContainerPage>> MonadicReadFeedAsync(
            int partitionKeyRangeId,
            ResourceId resourceIdentifer,
            int pageSize,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<TryCatch<QueryPage>> MonadicQueryAsync(
            SqlQuerySpec sqlQuerySpec,
            string continuationToken,
            Cosmos.PartitionKey partitionKey,
            int pageSize,
            CancellationToken cancellationToken) => this.cosmosQueryContext.QueryClient.ExecuteItemQueryAsync(
                this.cosmosQueryContext.ResourceLink,
                this.cosmosQueryContext.ResourceTypeEnum,
                this.cosmosQueryContext.OperationTypeEnum,
                this.cosmosQueryContext.CorrelatedActivityId,
                new QueryRequestOptions()
                {
                    PartitionKey = partitionKey,
                },
                queryPageDiagnostics: default,
                sqlQuerySpec,
                continuationToken,
                partitionKeyRange: default,
                isContinuationExpected: true,
                pageSize,
                cancellationToken);

        public Task<TryCatch<QueryPage>> MonadicQueryAsync(
            SqlQuerySpec sqlQuerySpec,
            string continuationToken,
            int partitionKeyRangeId,
            int pageSize,
            CancellationToken cancellationToken) => this.cosmosQueryContext.ExecuteQueryAsync(
                querySpecForInit: sqlQuerySpec,
                continuationToken: continuationToken,
                partitionKeyRange: new PartitionKeyRangeIdentity(
                    this.cosmosQueryContext.ContainerResourceId,
                    partitionKeyRangeId.ToString()),
                isContinuationExpected: this.cosmosQueryContext.IsContinuationExpected,
                pageSize: pageSize,
                cancellationToken: cancellationToken);

        public Task<TryCatch<QueryPage>> MonadicQueryAsync(
            SqlQuerySpec sqlQuerySpec,
            string continuationToken,
            FeedRangeInternal feedRange,
            int pageSize,
            CancellationToken cancellationToken) => feedRange.AcceptAsync(
                this.executeQueryBasedOnFeedRangeVisitor,
                new ExecuteQueryBasedOnFeedRangeVisitor.Arguments(
                    sqlQuerySpec,
                    continuationToken,
                    pageSize),
                cancellationToken);

        public async Task<TryCatch<ChangeFeedPage>> MonadicChangeFeedAsync(
            ChangeFeedState state,
            FeedRangeInternal feedRange,
            int pageSize,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ResponseMessage responseMessage = await this.cosmosClientContext.ProcessResourceOperationStreamAsync(
                resourceUri: this.container.LinkUri,
                resourceType: ResourceType.Document,
                operationType: OperationType.ReadFeed,
                requestOptions: default,
                cosmosContainerCore: this.container,
                requestEnricher: (request) =>
                {
                    state.Accept(ChangeFeedStateRequestMessagePopulator.Singleton, request);
                    feedRange.Accept(FeedRangeRequestMessagePopulatorVisitor.Singleton, request);

                    request.Headers.PageSize = pageSize.ToString();
                    request.Headers.Add(
                        HttpConstants.HttpHeaders.A_IM,
                        HttpConstants.A_IMHeaderValues.IncrementalFeed);
                },
                partitionKey: default,
                streamPayload: default,
                diagnosticsContext: default,
                cancellationToken: cancellationToken);

            if (!responseMessage.IsSuccessStatusCode)
            {
                CosmosException cosmosException = new CosmosException(
                    responseMessage.ErrorMessage,
                    statusCode: responseMessage.StatusCode,
                    (int)responseMessage.Headers.SubStatusCode,
                    responseMessage.Headers.ActivityId,
                    responseMessage.Headers.RequestCharge);

                return TryCatch<ChangeFeedPage>.FromException(cosmosException);
            }

            ChangeFeedPage changeFeedPage = new ChangeFeedPage(
                responseMessage.Content,
                responseMessage.Headers.RequestCharge,
                responseMessage.Headers.ActivityId,
                ChangeFeedState.Continuation(responseMessage.ContinuationToken));
            return TryCatch<ChangeFeedPage>.FromResult(changeFeedPage);
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
                message.Headers.IfNoneMatch = changeFeedStateContinuation.ContinuationToken;
            }

            public void Visit(ChangeFeedStateNow changeFeedStateNow, RequestMessage message)
            {
                message.Headers.IfNoneMatch = ChangeFeedStateRequestMessagePopulator.IfNoneMatchAllHeaderValue;
            }
        }
    }
}
