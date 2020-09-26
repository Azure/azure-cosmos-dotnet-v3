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
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
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
        private readonly ExecuteQueryBasedOnFeedRangeVisitor executeQueryBasedOnFeedRangeVisitor;

        public NetworkAttachedDocumentContainer(
            ContainerCore container,
            CosmosQueryContext cosmosQueryContext)
        {
            this.container = container ?? throw new ArgumentNullException(nameof(container));
            this.cosmosQueryContext = cosmosQueryContext ?? throw new ArgumentNullException(nameof(cosmosQueryContext));
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

        public Task<TryCatch<List<PartitionKeyRange>>> MonadicGetChildRangeAsync(
            PartitionKeyRange partitionKeyRange,
            CancellationToken cancellationToken) => this.cosmosQueryContext.QueryClient.TryGetOverlappingRangesAsync(
                this.cosmosQueryContext.ContainerResourceId,
                partitionKeyRange.ToRange(),
                forceRefresh: true)
                .ContinueWith(antecedent => TryCatch<List<PartitionKeyRange>>.FromResult(antecedent.Result.ToList()));

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
    }
}
