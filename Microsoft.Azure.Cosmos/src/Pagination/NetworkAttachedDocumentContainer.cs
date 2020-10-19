// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.ReadFeed.Pagination;
    using Microsoft.Azure.Documents;

    internal sealed class NetworkAttachedDocumentContainer : IMonadicDocumentContainer
    {
        private readonly ContainerCore container;
        private readonly CosmosQueryClient cosmosQueryClient;
        private readonly CosmosClientContext cosmosClientContext;
        private readonly QueryRequestOptions queryRequestOptions;
        private readonly CosmosDiagnosticsContext diagnosticsContext;

        public NetworkAttachedDocumentContainer(
            ContainerCore container,
            CosmosQueryClient cosmosQueryClient,
            CosmosClientContext cosmosClientContext,
            CosmosDiagnosticsContext diagnosticsContext,
            QueryRequestOptions queryRequestOptions = null)
        {
            this.container = container ?? throw new ArgumentNullException(nameof(container));
            this.cosmosQueryClient = cosmosQueryClient ?? throw new ArgumentNullException(nameof(cosmosQueryClient));
            this.cosmosClientContext = cosmosClientContext ?? throw new ArgumentNullException(nameof(cosmosClientContext));
            this.diagnosticsContext = diagnosticsContext;
            this.queryRequestOptions = queryRequestOptions;
        }

        public Task<TryCatch> MonadicSplitAsync(
            FeedRangeInternal feedRange,
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

        public Task<TryCatch<List<FeedRangeEpk>>> MonadicGetFeedRangesAsync(
            CancellationToken cancellationToken) => this.MonadicGetChildRangeAsync(FeedRangeEpk.FullRange, cancellationToken);

        public async Task<TryCatch<List<FeedRangeEpk>>> MonadicGetChildRangeAsync(
            FeedRangeInternal feedRange,
            CancellationToken cancellationToken)
        {
            try
            {
                ContainerProperties containerProperties = await this.cosmosClientContext.GetCachedContainerPropertiesAsync(
                    this.container.LinkUri,
                    cancellationToken);
                List<PartitionKeyRange> overlappingRanges = await this.cosmosQueryClient.GetTargetPartitionKeyRangeByFeedRangeAsync(
                    this.container.LinkUri,
                    await this.container.GetRIDAsync(cancellationToken),
                    containerProperties.PartitionKey,
                    feedRange);
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

        public async Task<TryCatch<ReadFeedPage>> MonadicReadFeedAsync(
            ReadFeedState readFeedState,
            FeedRangeInternal feedRange,
            int pageSize,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ResponseMessage responseMessage = await this.cosmosClientContext.ProcessResourceOperationStreamAsync(
               resourceUri: this.container.LinkUri,
               resourceType: ResourceType.Document,
               operationType: OperationType.ReadFeed,
               requestOptions: this.queryRequestOptions,
               cosmosContainerCore: this.container,
               requestEnricher: request =>
               {
                   if (readFeedState != null)
                   {
                       request.Headers.ContinuationToken = (readFeedState.ContinuationToken as CosmosString).Value;
                   }

                   FeedRangeRequestMessagePopulatorVisitor visitor = new FeedRangeRequestMessagePopulatorVisitor(request);
                   feedRange.Accept(visitor);
                   request.Headers.PageSize = pageSize.ToString();
               },
               partitionKey: default,
               streamPayload: default,
               diagnosticsContext: default,
               cancellationToken: cancellationToken);

            TryCatch<ReadFeedPage> monadicReadFeedPage;
            if (responseMessage.StatusCode == HttpStatusCode.OK)
            {
                ReadFeedPage readFeedPage = new ReadFeedPage(
                    responseMessage.Content,
                    responseMessage.Headers.RequestCharge,
                    responseMessage.Headers.ActivityId,
                    new ReadFeedState(CosmosString.Create(responseMessage.Headers.ETag)));

                monadicReadFeedPage = TryCatch<ReadFeedPage>.FromResult(readFeedPage);
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

                monadicReadFeedPage = TryCatch<ReadFeedPage>.FromException(cosmosException);
            }

            return monadicReadFeedPage;
        }

        public async Task<TryCatch<QueryPage>> MonadicQueryAsync(
            SqlQuerySpec sqlQuerySpec,
            string continuationToken,
            FeedRangeInternal feedRange,
            int pageSize,
            CancellationToken cancellationToken)
        {
            QueryRequestOptions queryRequestOptions = this.queryRequestOptions == null ? new QueryRequestOptions() : this.queryRequestOptions.Clone();
            TryCatch<QueryPage> monadicQueryPage;
            switch (feedRange)
            {
                case FeedRangePartitionKey feedRangePartitionKey:
                    {
                        ContainerProperties containerProperties = await this.cosmosClientContext.GetCachedContainerPropertiesAsync(
                            this.container.LinkUri,
                            cancellationToken);
                        PartitionKeyDefinition partitionKeyDefinition = await this.container.GetPartitionKeyDefinitionAsync(cancellationToken);

                        List<PartitionKeyRange> overlappingRanges;
                        if (feedRangePartitionKey.PartitionKey.IsNone)
                        {
                            overlappingRanges = new List<PartitionKeyRange>()
                            {
                                new PartitionKeyRange()
                                {
                                    Id = "0",
                                }
                            };
                        }
                        else
                        {
                            overlappingRanges = await this.cosmosQueryClient.GetTargetPartitionKeyRangeByFeedRangeAsync(
                                this.container.LinkUri,
                                await this.container.GetRIDAsync(cancellationToken),
                                containerProperties.PartitionKey,
                                feedRange);
                        }

                        queryRequestOptions.PartitionKey = feedRangePartitionKey.PartitionKey;

                        monadicQueryPage = await this.cosmosQueryClient.ExecuteItemQueryAsync(
                            this.container.LinkUri,
                            Documents.ResourceType.Document,
                            Documents.OperationType.Query,
                            Guid.NewGuid(),
                            queryRequestOptions,
                            queryPageDiagnostics: this.AddQueryPageDiagnostic,
                            sqlQuerySpec,
                            continuationToken,
                            partitionKeyRange: new PartitionKeyRangeIdentity(
                                await this.container.GetRIDAsync(cancellationToken),
                                overlappingRanges[0].Id),
                            isContinuationExpected: false,
                            pageSize,
                            cancellationToken);
                    }
                    break;

                case FeedRangePartitionKeyRange feedRangePartitionKeyRange:
                    {
                        monadicQueryPage = await this.cosmosQueryClient.ExecuteItemQueryAsync(
                            this.container.LinkUri,
                            Documents.ResourceType.Document,
                            Documents.OperationType.Query,
                            Guid.NewGuid(),
                            requestOptions: queryRequestOptions,
                            queryPageDiagnostics: this.AddQueryPageDiagnostic,
                            sqlQuerySpec,
                            continuationToken,
                            partitionKeyRange: new PartitionKeyRangeIdentity(
                                await this.container.GetRIDAsync(cancellationToken),
                                feedRangePartitionKeyRange.PartitionKeyRangeId),
                            isContinuationExpected: false,
                            pageSize,
                            cancellationToken);
                    }
                    break;

                case FeedRangeEpk feedRangeEpk:
                    {
                        ContainerProperties containerProperties = await this.cosmosClientContext.GetCachedContainerPropertiesAsync(
                            this.container.LinkUri,
                            cancellationToken);
                        List<PartitionKeyRange> overlappingRanges = await this.cosmosQueryClient.GetTargetPartitionKeyRangeByFeedRangeAsync(
                            this.container.LinkUri,
                            await this.container.GetRIDAsync(cancellationToken),
                            containerProperties.PartitionKey,
                            feedRange);

                        if ((overlappingRanges == null) || (overlappingRanges.Count != 1))
                        {
                            // Simulate a split exception, since we don't have a partition key range id to route to.
                            CosmosException goneException = new CosmosException(
                                message: $"Epk Range: {feedRangeEpk.Range} is gone.",
                                statusCode: System.Net.HttpStatusCode.Gone,
                                subStatusCode: (int)SubStatusCodes.PartitionKeyRangeGone,
                                activityId: Guid.NewGuid().ToString(),
                                requestCharge: default);

                            return TryCatch<QueryPage>.FromException(goneException);
                        }

                        monadicQueryPage = await this.cosmosQueryClient.ExecuteItemQueryAsync(
                            this.container.LinkUri,
                            Documents.ResourceType.Document,
                            Documents.OperationType.Query,
                            Guid.NewGuid(),
                            requestOptions: queryRequestOptions,
                            queryPageDiagnostics: this.AddQueryPageDiagnostic,
                            sqlQuerySpec,
                            continuationToken,
                            partitionKeyRange: new PartitionKeyRangeIdentity(
                                await this.container.GetRIDAsync(cancellationToken),
                                overlappingRanges[0].Id),
                            isContinuationExpected: false,
                            pageSize,
                            cancellationToken);
                    }
                    break;

                default:
                    throw new InvalidOperationException();
            }

            return monadicQueryPage;
        }

        private void AddQueryPageDiagnostic(QueryPageDiagnostics queryPageDiagnostics)
        {
            this.diagnosticsContext.AddDiagnosticsInternal(queryPageDiagnostics);
        }
    }
}
