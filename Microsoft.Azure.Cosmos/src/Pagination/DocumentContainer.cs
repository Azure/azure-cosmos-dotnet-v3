// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Remote;
    using Microsoft.Azure.Documents;

    internal abstract class DocumentContainer : IDocumentContainer
    {
        private static readonly CosmosException RequestRateTooLargeException = new CosmosException(
            message: "Request Rate Too Large",
            statusCode: (System.Net.HttpStatusCode)429,
            subStatusCode: default,
            activityId: Guid.NewGuid().ToString(),
            requestCharge: default);

        private static readonly Task<TryCatch<Record>> ThrottleForCreateItem = Task.FromResult(
            TryCatch<Record>.FromException(
                RequestRateTooLargeException));

        private static readonly Task<TryCatch<DocumentContainerPage>> ThrottleForFeedOperation = Task.FromResult(
            TryCatch<DocumentContainerPage>.FromException(
                RequestRateTooLargeException));

        private static readonly Task<TryCatch<QueryPage>> ThrottleForQuery = Task.FromResult(
            TryCatch<QueryPage>.FromException(
                RequestRateTooLargeException));

        private static readonly PartitionKeyRange FullRange = new PartitionKeyRange()
        {
            MinInclusive = Documents.Routing.PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
            MaxExclusive = Documents.Routing.PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
        };

        private readonly FailureConfigs failureConfigs;
        private readonly Random random;
        private readonly ExecuteQueryBasedOnFeedRangeVisitor visitor;

        protected DocumentContainer(FailureConfigs failureConfigs = null)
        {
            this.failureConfigs = failureConfigs;
            this.random = new Random();
            this.visitor = new ExecuteQueryBasedOnFeedRangeVisitor(this);
        }

        protected abstract Task<TryCatch<List<PartitionKeyRange>>> MonadicGetChildRangeImplementationAsync(
            PartitionKeyRange partitionKeyRange,
            CancellationToken cancellationToken);

        public Task<TryCatch<List<PartitionKeyRange>>> MonadicGetChildRangeAsync(
            PartitionKeyRange partitionKeyRange,
            CancellationToken cancellationToken) => this.MonadicGetChildRangeImplementationAsync(
                partitionKeyRange,
                cancellationToken);

        public Task<List<PartitionKeyRange>> GetChildRangeAsync(
            PartitionKeyRange partitionKeyRange,
            CancellationToken cancellationToken) => TryCatch<List<PartitionKeyRange>>.UnsafeGetResultAsync(
                this.MonadicGetChildRangeAsync(
                    partitionKeyRange,
                    cancellationToken),
                cancellationToken);

        public Task<TryCatch<List<PartitionKeyRange>>> MonadicGetFeedRangesAsync(
            CancellationToken cancellationToken) => this.MonadicGetChildRangeAsync(
                DocumentContainer.FullRange,
                cancellationToken);

        public Task<List<PartitionKeyRange>> GetFeedRangesAsync(
            CancellationToken cancellationToken) => TryCatch<List<PartitionKeyRange>>.UnsafeGetResultAsync(
                this.MonadicGetFeedRangesAsync(
                    cancellationToken),
                cancellationToken);

        protected abstract Task<TryCatch<Record>> MonadicCreateItemImplementationAsync(
            CosmosObject payload,
            CancellationToken cancellationToken);

        public Task<TryCatch<Record>> MonadicCreateItemAsync(
            CosmosObject payload,
            CancellationToken cancellationToken)
        {
            if (this.ShouldReturn429())
            {
                return ThrottleForCreateItem;
            }

            return this.MonadicCreateItemImplementationAsync(
                payload,
                cancellationToken);
        }

        public Task<Record> CreateItemAsync(
            CosmosObject payload,
            CancellationToken cancellationToken) => TryCatch<List<PartitionKeyRange>>.UnsafeGetResultAsync(
                this.MonadicCreateItemAsync(
                    payload,
                    cancellationToken),
                cancellationToken);

        protected abstract Task<TryCatch<Record>> MonadicReadItemImplementationAsync(
            CosmosElement partitionKey,
            string identifer,
            CancellationToken cancellationToken);

        public Task<TryCatch<Record>> MonadicReadItemAsync(
            CosmosElement partitionKey,
            string identifer,
            CancellationToken cancellationToken)
        {
            if (this.ShouldReturn429())
            {
                return ThrottleForCreateItem;
            }

            return this.MonadicReadItemImplementationAsync(
                partitionKey,
                identifer,
                cancellationToken);
        }

        public Task<Record> ReadItemAsync(
            CosmosElement partitionKey,
            string identifier,
            CancellationToken cancellationToken) => TryCatch<Record>.UnsafeGetResultAsync(
                this.MonadicReadItemAsync(
                    partitionKey,
                    identifier,
                    cancellationToken),
                cancellationToken);

        protected abstract Task<TryCatch<DocumentContainerPage>> MonadicReadFeedImplementationAsync(
            int partitionKeyRangeId,
            long resourceIdentifer,
            int pageSize,
            CancellationToken cancellationToken);

        public Task<TryCatch<DocumentContainerPage>> MonadicReadFeedAsync(
            int partitionKeyRangeId,
            long resourceIdentifer,
            int pageSize,
            CancellationToken cancellationToken)
        {
            if (this.ShouldReturn429())
            {
                return ThrottleForFeedOperation;
            }

            if (this.ShouldReturnEmptyPage())
            {
                return Task.FromResult(
                    TryCatch<DocumentContainerPage>.FromResult(
                        new DocumentContainerPage(
                            new List<Record>(),
                            new DocumentContainerState(resourceIdentifer))));
            }

            return this.MonadicReadFeedImplementationAsync(
                partitionKeyRangeId,
                resourceIdentifer,
                pageSize,
                cancellationToken);
        }

        public Task<DocumentContainerPage> ReadFeedAsync(
            int partitionKeyRangeId,
            long resourceIdentifier,
            int pageSize,
            CancellationToken cancellationToken) => TryCatch<DocumentContainerPage>.UnsafeGetResultAsync(
                this.MonadicReadFeedAsync(
                    partitionKeyRangeId,
                    resourceIdentifier,
                    pageSize,
                    cancellationToken),
                cancellationToken);

        protected abstract Task<TryCatch<QueryPage>> MonadicQueryWithLogicalPartitionKeyAsync(
            SqlQuerySpec sqlQuerySpec,
            string continuationToken,
            Cosmos.PartitionKey partitionKey,
            int pageSize,
            CancellationToken cancellationToken);

        protected abstract Task<TryCatch<QueryPage>> MonadicQueryWithRangeIdAsync(
            SqlQuerySpec sqlQuerySpec,
            string continuationToken,
            int partitionKeyRangeId,
            int pageSize,
            CancellationToken cancellationToken);

        public Task<TryCatch<QueryPage>> MonadicQueryAsync(
            SqlQuerySpec sqlQuerySpec,
            string continuationToken,
            FeedRangeInternal feedRange,
            int pageSize,
            CancellationToken cancellationToken)
        {
            if (this.ShouldReturn429())
            {
                return ThrottleForQuery;
            }

            if (this.ShouldReturnEmptyPage())
            {
                return Task.FromResult(
                    TryCatch<QueryPage>.FromResult(
                        new QueryPage(
                            documents: new List<CosmosElement>(),
                            requestCharge: 42,
                            activityId: Guid.NewGuid().ToString(),
                            responseLengthInBytes: "[]".Length,
                            cosmosQueryExecutionInfo: default,
                            disallowContinuationTokenMessage: default,
                            state: new QueryState(CosmosString.Create(continuationToken)))));
            }

            return feedRange.AcceptAsync(
                this.visitor,
                new VisitorArguments(sqlQuerySpec, continuationToken, pageSize),
                cancellationToken);
        }

        public Task<QueryPage> QueryAsync(
            SqlQuerySpec sqlQuerySpec,
            string continuationToken,
            FeedRangeInternal feedRange,
            int pageSize,
            CancellationToken cancellationToken) => TryCatch<QueryPage>.UnsafeGetResultAsync(
                this.MonadicQueryAsync(
                    sqlQuerySpec,
                    continuationToken,
                    feedRange,
                    pageSize,
                    cancellationToken),
                cancellationToken);

        public abstract Task<TryCatch> MonadicSplitAsync(
            int partitionKeyRangeId,
            CancellationToken cancellationToken);

        public Task SplitAsync(
            int partitionKeyRangeId,
            CancellationToken cancellationToken) => TryCatch.UnsafeWaitAsync(
                this.MonadicSplitAsync(
                    partitionKeyRangeId,
                    cancellationToken),
                cancellationToken);
        private bool ShouldReturn429() => (this.failureConfigs != null) && this.failureConfigs.Inject429s && ((this.random.Next() % 2) == 0);

        private bool ShouldReturnEmptyPage() => (this.failureConfigs != null) && this.failureConfigs.InjectEmptyPages && ((this.random.Next() % 2) == 0);

        public sealed class FailureConfigs
        {
            public FailureConfigs(bool inject429s, bool injectEmptyPages)
            {
                this.Inject429s = inject429s;
                this.InjectEmptyPages = injectEmptyPages;
            }

            public bool Inject429s { get; }

            public bool InjectEmptyPages { get; }
        }

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
            private readonly DocumentContainer documentContainer;

            public ExecuteQueryBasedOnFeedRangeVisitor(DocumentContainer documentContainer)
            {
                this.documentContainer = documentContainer ?? throw new ArgumentNullException(nameof(documentContainer));
            }

            public Task<TryCatch<QueryPage>> VisitAsync(
                FeedRangePartitionKey feedRange,
                VisitorArguments argument,
                CancellationToken cancellationToken) => this.documentContainer.MonadicQueryWithLogicalPartitionKeyAsync(
                    argument.SqlQuerySpec,
                    argument.ContinuationToken,
                    feedRange.PartitionKey,
                    argument.PageSize,
                    cancellationToken);

            public Task<TryCatch<QueryPage>> VisitAsync(
                FeedRangePartitionKeyRange feedRange,
                VisitorArguments argument,
                CancellationToken cancellationToken) => this.documentContainer.MonadicQueryWithRangeIdAsync(
                    argument.SqlQuerySpec,
                    argument.ContinuationToken,
                    int.Parse(feedRange.PartitionKeyRangeId),
                    argument.PageSize,
                    cancellationToken);

            public async Task<TryCatch<QueryPage>> VisitAsync(
                FeedRangeEpk feedRange,
                VisitorArguments argument,
                CancellationToken cancellationToken)
            {
                // Check to see if it lines up exactly with one physical partition
                List<PartitionKeyRange> feedRanges = await this.documentContainer.GetChildRangeAsync(
                    new PartitionKeyRange()
                    {
                        MinInclusive = feedRange.Range.Min,
                        MaxExclusive = feedRange.Range.Max,
                    },
                    cancellationToken);

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

                return await this.documentContainer.MonadicQueryWithRangeIdAsync(
                    argument.SqlQuerySpec,
                    argument.ContinuationToken,
                    int.Parse(singleRange.Id),
                    argument.PageSize,
                    cancellationToken);
            }
        }
    }
}
