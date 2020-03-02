//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.Parallel
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.ItemProducers;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;

    /// <summary>
    /// CosmosParallelItemQueryExecutionContext is a concrete implementation for CrossPartitionQueryExecutionContext.
    /// This class is responsible for draining cross partition queries that do not have order by conditions.
    /// The way parallel queries work is that it drains from the left most partition first.
    /// This class handles draining in the correct order and can also stop and resume the query 
    /// by generating a continuation token and resuming from said continuation token.
    /// </summary>
    internal sealed partial class CosmosParallelItemQueryExecutionContext : CosmosCrossPartitionQueryExecutionContext
    {
        public override async Task<QueryResponseCore> DrainAsync(int maxElements, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // In order to maintain the continuation token for the user we must drain with a few constraints
            // 1) We fully drain from the left most partition before moving on to the next partition
            // 2) We drain only full pages from the document producer so we aren't left with a partial page
            //  otherwise we would need to add to the continuation token how many items to skip over on that page.

            // Only drain from the leftmost (current) document producer tree
            ItemProducerTree currentItemProducerTree = this.PopCurrentItemProducerTree();
            List<CosmosElement> results = new List<CosmosElement>();
            try
            {
                (bool gotNextPage, QueryResponseCore? failureResponse) = await currentItemProducerTree.TryMoveNextPageAsync(cancellationToken);
                if (failureResponse != null)
                {
                    return failureResponse.Value;
                }

                if (gotNextPage)
                {
                    int itemsLeftInCurrentPage = currentItemProducerTree.ItemsLeftInCurrentPage;

                    // Only drain full pages or less if this is a top query.
                    currentItemProducerTree.TryMoveNextDocumentWithinPage();
                    int numberOfItemsToDrain = Math.Min(itemsLeftInCurrentPage, maxElements);
                    for (int i = 0; i < numberOfItemsToDrain; i++)
                    {
                        results.Add(currentItemProducerTree.Current);
                        currentItemProducerTree.TryMoveNextDocumentWithinPage();
                    }
                }
            }
            finally
            {
                this.PushCurrentItemProducerTree(currentItemProducerTree);
            }

            return QueryResponseCore.CreateSuccess(
                    result: results,
                    requestCharge: this.requestChargeTracker.GetAndResetCharge(),
                    activityId: null,
                    responseLengthBytes: this.GetAndResetResponseLengthBytes(),
                    disallowContinuationTokenMessage: null,
                    continuationToken: this.ContinuationToken,
                    diagnostics: this.GetAndResetDiagnostics());
        }
    }
}
