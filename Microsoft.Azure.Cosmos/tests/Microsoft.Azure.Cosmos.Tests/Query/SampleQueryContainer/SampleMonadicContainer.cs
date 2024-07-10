namespace Microsoft.Azure.Cosmos.Tests.Query.SampleQueryContainer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.ChangeFeed.Pagination;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.ReadFeed.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;

    internal class SampleMonadicContainer : IMonadicDocumentContainer
    {
        private readonly SampleContainer sourceContainer;

        public SampleMonadicContainer(SampleContainer sourceContainer)
        {
            this.sourceContainer = sourceContainer;
        }

        public Task<TryCatch<ChangeFeedPage>> MonadicChangeFeedAsync(
            FeedRangeState<ChangeFeedState> feedRangeState, 
            ChangeFeedExecutionOptions changeFeedOptions, 
            ITrace trace, 
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<TryCatch<Record>> MonadicCreateItemAsync(CosmosObject payload, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<TryCatch<List<FeedRangeEpk>>> MonadicGetChildRangeAsync(FeedRangeInternal feedRange, ITrace trace, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<TryCatch<List<FeedRangeEpk>>> MonadicGetFeedRangesAsync(ITrace trace, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<TryCatch<string>> MonadicGetResourceIdentifierAsync(ITrace trace, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<TryCatch> MonadicMergeAsync(FeedRangeInternal feedRange1, FeedRangeInternal feedRange2, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<TryCatch<QueryPage>> MonadicQueryAsync(
            SqlQuerySpec sqlQuerySpec,
            FeedRangeState<QueryState> feedRangeState,
            QueryExecutionOptions queryOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            FeedRangePartitionKey feedRangePartitionKey = feedRangeState.FeedRange as FeedRangePartitionKey;
            if (feedRangePartitionKey != null)
            {
                //
            }

            FeedRangeEpk feedRangeEpk = feedRangeState.FeedRange as FeedRangeEpk;
            if(feedRangeEpk != null)
            {
                List<Partition> partitions = this.sourceContainer.Partitions
                    .Where(partition => (partition.LogicalPartitionRange.Min == null) ||
                        (StringComparer.Ordinal.Compare(partition.LogicalPartitionRange.Min.PhysicalPartitionKey.Hash, feedRangeEpk.Range.Min) <= 0))
                    .Where(partition => (partition.LogicalPartitionRange.Max == null) || (StringComparer.Ordinal.Compare(partition.LogicalPartitionRange.Max.PhysicalPartitionKey.Hash, feedRangeEpk.Range.Max) > 0))
                    .ToList();
                if(partitions.Count != 1)
                {
                    return Task.FromResult(TryCatch<QueryPage>.FromException(
                        new CosmosException(
                            message: $"PartitionKeyRangeId {0} is gone",
                            statusCode: System.Net.HttpStatusCode.Gone,
                            subStatusCode: (int)Microsoft.Azure.Documents.SubStatusCodes.PartitionKeyRangeGone,
                            activityId: Guid.NewGuid().ToString(),
                            requestCharge: 42)));
                }

                List<CosmosElement> result = new List<CosmosElement>();
                foreach (SampleDocument document in partitions.Single().Documents)
                {
                    result.Add(
                        CosmosObject.Create(
                            new Dictionary<string, CosmosElement>
                            {
                                { nameof(document.TenantId), CosmosString.Create(document.TenantId) },
                                { nameof(document.UserId), CosmosString.Create(document.UserId) },
                                { nameof(document.SessionId), CosmosString.Create(document.SessionId) },
                                { nameof(document.Id).ToLowerInvariant(), CosmosString.Create(document.Id) }
                            }));
                }

                return Task.FromResult(
                    TryCatch<QueryPage>.FromResult(
                        new QueryPage(
                            result,
                            requestCharge: 42,
                            activityId: Guid.NewGuid().ToString(),
                            cosmosQueryExecutionInfo: default,
                            distributionPlanSpec: default,
                            disallowContinuationTokenMessage: default,
                            additionalHeaders: new Dictionary<string, string>(),
                            state: default,
                            streaming: default)));
            }

            throw new InvalidOperationException("Unsupported scenario!");
        }

        public Task<TryCatch<ReadFeedPage>> MonadicReadFeedAsync(
            FeedRangeState<ReadFeedState> feedRangeState, 
            ReadFeedExecutionOptions readFeedOptions, 
            ITrace trace, 
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<TryCatch<Record>> MonadicReadItemAsync(CosmosElement partitionKey, string identifer, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<TryCatch> MonadicRefreshProviderAsync(ITrace trace, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<TryCatch> MonadicSplitAsync(FeedRangeInternal feedRange, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
