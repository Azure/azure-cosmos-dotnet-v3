namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.ItemProducers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Documents;

    internal sealed class BackendQueryPartitionRangePageEnumerator : QueryPartitionRangePageEnumerator
    {
        private readonly CosmosQueryContext cosmosQueryContext;
        private readonly int pageSize;

        public BackendQueryPartitionRangePageEnumerator(
            CosmosQueryContext cosmosQueryContext,
            SqlQuerySpec sqlQuerySpec,
            FeedRange feedRange,
            int pageSize,
            State state = default)
            : base(sqlQuerySpec, feedRange, state)
        {
            this.cosmosQueryContext = cosmosQueryContext ?? throw new ArgumentNullException(nameof(cosmosQueryContext));
            this.pageSize = pageSize;

            if (state != default)
            {
                if (!(state is QueryState))
                {
                    throw new ArgumentOutOfRangeException(nameof(state));
                }
            }

            if (!(feedRange is FeedRangePartitionKeyRange))
            {
                throw new ArgumentOutOfRangeException(nameof(feedRange));
            }
        }

        public override ValueTask DisposeAsync()
        {
            return default;
        }

        public override async Task<(TryCatch<Page>, State)> GetNextPageAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            QueryResponseCore queryResponse = await this.cosmosQueryContext.ExecuteQueryAsync(
                querySpecForInit: this.sqlQuerySpec,
                continuationToken: ((QueryState)this.State).ContinuationToken,
                partitionKeyRange: new PartitionKeyRangeIdentity(
                        this.cosmosQueryContext.ContainerResourceId,
                        ((FeedRangePartitionKeyRange)this.Range).PartitionKeyRangeId),
                isContinuationExpected: this.cosmosQueryContext.IsContinuationExpected,
                pageSize: this.pageSize,
                cancellationToken: cancellationToken);


        }

        private sealed class QueryState : State
        {
            public QueryState(string continuationToken)
            {
                this.ContinuationToken = continuationToken;
            }

            public string ContinuationToken { get; }
        }
    }
}
