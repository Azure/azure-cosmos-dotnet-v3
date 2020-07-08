// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.Remote
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Documents;

    internal sealed class BackendQueryDataSource : IQueryDataSource
    {
        private readonly ExecuteQueryBasedOnFeedRangeVisitor visitor;

        public BackendQueryDataSource(CosmosQueryContext cosmosQueryContext)
        {
            this.visitor = new ExecuteQueryBasedOnFeedRangeVisitor(cosmosQueryContext);
        }

        public Task<TryCatch<QueryPage>> ExecuteQueryAsync(
            SqlQuerySpec sqlQuerySpec,
            string continuationToken,
            FeedRangeInternal feedRange,
            int pageSize,
            CancellationToken cancellationToken) => feedRange.AcceptAsync(
                this.visitor,
                new VisitorArguments(sqlQuerySpec, continuationToken, pageSize),
                cancellationToken);

        private readonly struct VisitorArguments
        {
            public VisitorArguments(SqlQuerySpec sqlQuerySpec, string continuationToken, int pageSize)
            {
                this.SqlQuerySpec = sqlQuerySpec;
                this.ContinuationToken = continuationToken;
                this.PageSize = pageSize;
            }

            public SqlQuerySpec SqlQuerySpec { get; }

            public string ContinuationToken { get; }

            public int PageSize { get; }
        }

        private sealed class ExecuteQueryBasedOnFeedRangeVisitor : IFeedRangeAsyncVisitor<TryCatch<QueryPage>, VisitorArguments>
        {
            private readonly CosmosQueryContext cosmosQueryContext;

            public ExecuteQueryBasedOnFeedRangeVisitor(CosmosQueryContext cosmosQueryContext)
            {
                this.cosmosQueryContext = cosmosQueryContext ?? throw new ArgumentNullException(nameof(cosmosQueryContext));
            }

            public Task<TryCatch<QueryPage>> VisitAsync(
                FeedRangePartitionKey feedRange,
                VisitorArguments argument,
                CancellationToken cancellationToken) => this.cosmosQueryContext.QueryClient.ExecuteItemQueryAsync(
                    this.cosmosQueryContext.ResourceLink,
                    this.cosmosQueryContext.ResourceTypeEnum,
                    this.cosmosQueryContext.OperationTypeEnum,
                    this.cosmosQueryContext.CorrelatedActivityId,
                    new QueryRequestOptions()
                    {
                        PartitionKey = feedRange.PartitionKey,
                    },
                    queryPageDiagnostics: default,
                    argument.SqlQuerySpec,
                    argument.ContinuationToken,
                    partitionKeyRange: default,
                    isContinuationExpected: true,
                    argument.PageSize,
                    cancellationToken);

            public Task<TryCatch<QueryPage>> VisitAsync(
                FeedRangePartitionKeyRange feedRange,
                VisitorArguments argument,
                CancellationToken cancellationToken) => this.cosmosQueryContext.ExecuteQueryAsync(
                    querySpecForInit: argument.SqlQuerySpec,
                    continuationToken: argument.ContinuationToken,
                    partitionKeyRange: new PartitionKeyRangeIdentity(
                        this.cosmosQueryContext.ContainerResourceId,
                        feedRange.PartitionKeyRangeId),
                    isContinuationExpected: this.cosmosQueryContext.IsContinuationExpected,
                    pageSize: argument.PageSize,
                    cancellationToken: cancellationToken);

            public async Task<TryCatch<QueryPage>> VisitAsync(
                FeedRangeEpk feedRange,
                VisitorArguments argument,
                CancellationToken cancellationToken)
            {
                // Check to see if it lines up exactly with one physical partition
                IReadOnlyList<PartitionKeyRange> feedRanges = await this.cosmosQueryContext.QueryClient.TryGetOverlappingRangesAsync(
                    this.cosmosQueryContext.ContainerResourceId,
                    feedRange.Range,
                    forceRefresh: true);

                if (feedRanges.Count != 1)
                {
                    // Simulate a split exception, since we don't have a partition key range id to route to.
                    CosmosException goneException = new CosmosException(
                        message: $"Epk Range: {feedRange.Range} is gone.",
                        statusCode: System.Net.HttpStatusCode.Gone,
                        subStatusCode: (int)SubStatusCodes.PartitionKeyRangeGone,
                        activityId: Guid.NewGuid().ToString(),
                        requestCharge: default);

                    return TryCatch<QueryPage>.FromException(goneException);
                }

                // If the epk range aligns exactly to a physical partition, then continue as if PKRangeId was given
                PartitionKeyRange singleRange = feedRanges[0];
                Documents.Routing.Range<string> range = singleRange.ToRange();
                if (!feedRange.Range.Equals(range))
                {
                    // User want's use to query on a sub partition, which is currently not possible.
                    throw new NotImplementedException("Can not query on a sub partition.");
                }

                FeedRangeInternal partitionKeyRangeFeedRange = new FeedRangePartitionKeyRange(singleRange.Id);
                return await partitionKeyRangeFeedRange.AcceptAsync(this, argument, cancellationToken);
            }
        }
    }
}
