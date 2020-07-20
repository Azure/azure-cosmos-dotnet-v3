// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.Remote
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.ContinuationTokens;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    internal sealed class OrderByCrossPartitionQueryPipelineStage : IQueryPipelineStage
    {
        private readonly CrossPartitionRangePageAsyncEnumerator<OrderByQueryPage, QueryState> crossPartitionRangePageAsyncEnumerator;

        private OrderByCrossPartitionQueryPipelineStage(
            CrossPartitionRangePageAsyncEnumerator<OrderByQueryPage, QueryState> crossPartitionRangePageAsyncEnumerator)
        {
            this.crossPartitionRangePageAsyncEnumerator = crossPartitionRangePageAsyncEnumerator ?? throw new ArgumentNullException(nameof(crossPartitionRangePageAsyncEnumerator));
        }

        public TryCatch<QueryPage> Current => throw new NotImplementedException();

        public ValueTask DisposeAsync() => this.crossPartitionRangePageAsyncEnumerator.DisposeAsync();

        public ValueTask<bool> MoveNextAsync()
        {
            throw new NotImplementedException();
        }

        public static TryCatch<IQueryPipelineStage> MonadicCreate(
            IDocumentContainer documentContainer,
            SqlQuerySpec sqlQuerySpec,
            int pageSize,
            CosmosElement continuationToken)
        {
            if (pageSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pageSize));
            }

            TryCatch<CrossPartitionState<QueryState>> monadicExtractState = MonadicExtractState(continuationToken);
            if (monadicExtractState.Failed)
            {
                return TryCatch<IQueryPipelineStage>.FromException(monadicExtractState.Exception);
            }

            CrossPartitionState<QueryState> state = monadicExtractState.Result;


        }

        private static TryCatch<CrossPartitionState<QueryState>> MonadicExtractState(
            CosmosElement continuationToken,
            int numOrderByColumns)
        {
            if (continuationToken == null)
            {
                return TryCatch<CrossPartitionState<QueryState>>.FromResult(default);
            }

            if (!(continuationToken is CosmosArray cosmosArray))
            {
                return TryCatch<CrossPartitionState<QueryState>>.FromException(
                    new MalformedContinuationTokenException(
                        $"Order by continuation token must be an array: {continuationToken}."));
            }

            List<OrderByContinuationToken> orderByContinuationTokens = new List<OrderByContinuationToken>();
            foreach (CosmosElement arrayItem in cosmosArray)
            {
                TryCatch<OrderByContinuationToken> tryCreateOrderByContinuationToken = OrderByContinuationToken.TryCreateFromCosmosElement(arrayItem);
                if (!tryCreateOrderByContinuationToken.Succeeded)
                {
                    return TryCatch<CrossPartitionState<QueryState>>.FromException(tryCreateOrderByContinuationToken.Exception);
                }

                orderByContinuationTokens.Add(tryCreateOrderByContinuationToken.Result);
            }

            if (orderByContinuationTokens.Count == 0)
            {
                return TryCatch<CrossPartitionState<QueryState>>.FromException(
                    new MalformedContinuationTokenException(
                        $"Order by continuation token cannot be empty: {continuationToken}."));
            }

            foreach (OrderByContinuationToken suppliedOrderByContinuationToken in orderByContinuationTokens)
            {
                if (suppliedOrderByContinuationToken.OrderByItems.Count != numOrderByColumns)
                {
                    return TryCatch<CrossPartitionState<QueryState>>.FromException(
                        new MalformedContinuationTokenException(
                            $"Invalid order-by items in continuation token {continuationToken} for OrderBy~Context."));
                }
            }
        }

        private sealed class OrderByQueryPage : Page<QueryState>
        {
            public OrderByQueryPage(QueryPage queryPage)
                : base(queryPage.State)
            {
                this.Enumerator = queryPage.Documents.GetEnumerator();
            }

            public QueryPage Page { get; }

            public IEnumerator<CosmosElement> Enumerator { get; }
        }
    }
}
