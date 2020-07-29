//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.OrderBy
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.ContinuationTokens;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionComponent;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.ItemProducers;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using PartitionKeyRange = Documents.PartitionKeyRange;
    using ResourceId = Documents.ResourceId;

    internal sealed partial class CosmosOrderByItemQueryExecutionContext : CosmosCrossPartitionQueryExecutionContext
    {
        public static async Task<TryCatch<IDocumentQueryExecutionComponent>> MonadicCreateAsync(
            CosmosQueryContext queryContext,
            CosmosCrossPartitionQueryExecutionContext.CrossPartitionInitParams initParams,
            CosmosElement requestContinuationToken,
            CancellationToken cancellationToken)
        {
            Debug.Assert(
                initParams.PartitionedQueryExecutionInfo.QueryInfo.HasOrderBy,
                "OrderBy~Context must have order by query info.");

            if (queryContext == null)
            {
                throw new ArgumentNullException(nameof(queryContext));
            }

            cancellationToken.ThrowIfCancellationRequested();

            OrderByItemProducerTreeComparer orderByItemProducerTreeComparer = new OrderByItemProducerTreeComparer(initParams.PartitionedQueryExecutionInfo.QueryInfo.OrderBy.ToArray());
            CosmosOrderByItemQueryExecutionContext context = new CosmosOrderByItemQueryExecutionContext(
                initPararms: queryContext,
                maxConcurrency: initParams.MaxConcurrency,
                maxItemCount: initParams.MaxItemCount,
                maxBufferedItemCount: initParams.MaxBufferedItemCount,
                consumeComparer: orderByItemProducerTreeComparer,
                testSettings: initParams.TestSettings);

            IReadOnlyList<string> orderByExpressions = initParams.PartitionedQueryExecutionInfo.QueryInfo.OrderByExpressions;
            IReadOnlyList<SortOrder> sortOrders = initParams.PartitionedQueryExecutionInfo.QueryInfo.OrderBy;
            if (orderByExpressions.Count != sortOrders.Count)
            {
                throw new ArgumentException("order by expressions count does not match sort order");
            }

            IReadOnlyList<OrderByColumn> columns = orderByExpressions
                .Zip(sortOrders, (expression, order) => new OrderByColumn(expression, order))
                .ToList();

            return (await context.TryInitializeAsync(
                sqlQuerySpec: initParams.SqlQuerySpec,
                requestContinuation: requestContinuationToken,
                collectionRid: initParams.CollectionRid,
                partitionKeyRanges: initParams.PartitionKeyRanges,
                initialPageSize: initParams.InitialPageSize,
                orderByColumns: columns,
                cancellationToken: cancellationToken))
                .Try<IDocumentQueryExecutionComponent>(() => context);
        }
    }
}
