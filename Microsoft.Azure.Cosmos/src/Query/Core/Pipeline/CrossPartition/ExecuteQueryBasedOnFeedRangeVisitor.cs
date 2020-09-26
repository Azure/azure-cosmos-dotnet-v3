// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Documents;

    internal sealed class ExecuteQueryBasedOnFeedRangeVisitor : IFeedRangeAsyncVisitor<TryCatch<QueryPage>, ExecuteQueryBasedOnFeedRangeVisitor.Arguments>
    {
        private readonly IMonadicDocumentContainer documentContainer;

        public ExecuteQueryBasedOnFeedRangeVisitor(IMonadicDocumentContainer documentContainer)
        {
            this.documentContainer = documentContainer ?? throw new ArgumentNullException(nameof(documentContainer));
        }

        public Task<TryCatch<QueryPage>> VisitAsync(
            FeedRangePartitionKey feedRange,
            Arguments argument,
            CancellationToken cancellationToken) => this.documentContainer.MonadicQueryAsync(
                argument.SqlQuerySpec,
                argument.ContinuationToken,
                feedRange.PartitionKey,
                argument.PageSize,
                cancellationToken);

        public Task<TryCatch<QueryPage>> VisitAsync(
            FeedRangePartitionKeyRange feedRange,
            Arguments argument,
            CancellationToken cancellationToken) => this.documentContainer.MonadicQueryAsync(
                argument.SqlQuerySpec,
                argument.ContinuationToken,
                int.Parse(feedRange.PartitionKeyRangeId),
                argument.PageSize,
                cancellationToken);

        public async Task<TryCatch<QueryPage>> VisitAsync(
            FeedRangeEpk feedRange,
            Arguments argument,
            CancellationToken cancellationToken)
        {
            // Check to see if it lines up exactly with one physical partition
            TryCatch<List<PartitionKeyRange>> tryGetFeedRanges = await this.documentContainer.MonadicGetChildRangeAsync(
                new PartitionKeyRange()
                {
                    MinInclusive = feedRange.Range.Min,
                    MaxExclusive = feedRange.Range.Max,
                },
                cancellationToken);
            if (tryGetFeedRanges.Failed)
            {
                return TryCatch<QueryPage>.FromException(tryGetFeedRanges.Exception);
            }

            List<PartitionKeyRange> feedRanges = tryGetFeedRanges.Result;

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

            return await this.documentContainer.MonadicQueryAsync(
                argument.SqlQuerySpec,
                argument.ContinuationToken,
                int.Parse(singleRange.Id),
                argument.PageSize,
                cancellationToken);
        }

        public readonly struct Arguments
        {
            public Arguments(SqlQuerySpec sqlQuerySpec, string continuationToken, int pageSize)
            {
                this.SqlQuerySpec = sqlQuerySpec;
                this.ContinuationToken = continuationToken;
                this.PageSize = pageSize;
            }

            public SqlQuerySpec SqlQuerySpec { get; }

            public string ContinuationToken { get; }

            public int PageSize { get; }
        }
    }
}
