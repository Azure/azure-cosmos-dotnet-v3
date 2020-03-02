// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.Parallel
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.Collections;
    using Microsoft.Azure.Cosmos.Query.Core.ContinuationTokens;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionComponent;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.ItemProducers;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using PartitionKeyRange = Documents.PartitionKeyRange;

    internal sealed partial class CosmosParallelItemQueryExecutionContext : CosmosCrossPartitionQueryExecutionContext
    {
        public static async Task<TryCatch<IDocumentQueryExecutionComponent>> TryCreateAsync(
            CosmosQueryContext queryContext,
            CosmosCrossPartitionQueryExecutionContext.CrossPartitionInitParams initParams,
            CosmosElement requestContinuationToken,
            CancellationToken cancellationToken)
        {
            Debug.Assert(
                !initParams.PartitionedQueryExecutionInfo.QueryInfo.HasOrderBy,
                "Parallel~Context must not have order by query info.");

            cancellationToken.ThrowIfCancellationRequested();

            IComparer<ItemProducerTree> moveNextComparer;
            if (initParams.ReturnResultsInDeterministicOrder)
            {
                moveNextComparer = DeterministicParallelItemProducerTreeComparer.Singleton;
            }
            else
            {
                moveNextComparer = NonDeterministicParallelItemProducerTreeComparer.Singleton;
            }

            CosmosParallelItemQueryExecutionContext context = new CosmosParallelItemQueryExecutionContext(
                queryContext: queryContext,
                maxConcurrency: initParams.MaxConcurrency,
                maxItemCount: initParams.MaxItemCount,
                maxBufferedItemCount: initParams.MaxBufferedItemCount,
                moveNextComparer: moveNextComparer,
                returnResultsInDeterministicOrder: initParams.ReturnResultsInDeterministicOrder,
                testSettings: initParams.TestSettings);

            return (await context.TryInitializeAsync(
                sqlQuerySpec: initParams.SqlQuerySpec,
                collectionRid: initParams.CollectionRid,
                partitionKeyRanges: initParams.PartitionKeyRanges,
                initialPageSize: initParams.InitialPageSize,
                requestContinuation: requestContinuationToken,
                cancellationToken: cancellationToken)).Try<IDocumentQueryExecutionComponent>(x => x);
        }

        /// <summary>
        /// Initialize the execution context.
        /// </summary>
        /// <param name="sqlQuerySpec">SQL query spec.</param>
        /// <param name="collectionRid">The collection rid.</param>
        /// <param name="partitionKeyRanges">The partition key ranges to drain documents from.</param>
        /// <param name="initialPageSize">The initial page size.</param>
        /// <param name="requestContinuation">The continuation token to resume from.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task to await on.</returns>
        private async Task<TryCatch<CosmosParallelItemQueryExecutionContext>> TryInitializeAsync(
            SqlQuerySpec sqlQuerySpec,
            string collectionRid,
            List<PartitionKeyRange> partitionKeyRanges,
            int initialPageSize,
            CosmosElement requestContinuation,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            TryCatch<ParallelInitInfo> tryGetInitInfo = TryGetInitializationInfoFromContinuationToken(
                partitionKeyRanges,
                requestContinuation);
            if (!tryGetInitInfo.Succeeded)
            {
                return TryCatch<CosmosParallelItemQueryExecutionContext>.FromException(tryGetInitInfo.Exception);
            }

            ParallelInitInfo initializationInfo = tryGetInitInfo.Result;
            IReadOnlyList<PartitionKeyRange> filteredPartitionKeyRanges = initializationInfo.PartialRanges;
            IReadOnlyDictionary<string, CompositeContinuationToken> targetIndicesForFullContinuation = initializationInfo.ContinuationTokens;
            TryCatch tryInitialize = await base.TryInitializeAsync(
                collectionRid,
                filteredPartitionKeyRanges,
                initialPageSize,
                sqlQuerySpec,
                targetIndicesForFullContinuation?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Token),
                true,
                null,
                null,
                cancellationToken);
            if (!tryInitialize.Succeeded)
            {
                return TryCatch<CosmosParallelItemQueryExecutionContext>.FromException(tryInitialize.Exception);
            }

            return TryCatch<CosmosParallelItemQueryExecutionContext>.FromResult(this);
        }

        /// <summary>
        /// Given a continuation token and a list of partitionKeyRanges this function will return a list of partition key ranges you should resume with.
        /// Note that the output list is just a right hand slice of the input list, since we know that for any continuation of a parallel query it is just
        /// resuming from the partition that the query left off that.
        /// </summary>
        /// <param name="partitionKeyRanges">The partition key ranges.</param>
        /// <param name="continuationToken">The continuation tokens that the user has supplied.</param>
        /// <returns>The subset of partition to actually target and continuation tokens.</returns>
        private static TryCatch<ParallelInitInfo> TryGetInitializationInfoFromContinuationToken(
            List<PartitionKeyRange> partitionKeyRanges,
            CosmosElement continuationToken)
        {
            if (continuationToken == null)
            {
                return TryCatch<ParallelInitInfo>.FromResult(
                    new ParallelInitInfo(
                        partitionKeyRanges,
                        null));
            }

            if (!(continuationToken is CosmosArray compositeContinuationTokenListRaw))
            {
                return TryCatch<ParallelInitInfo>.FromException(
                    new MalformedContinuationTokenException($"Invalid format for continuation token {continuationToken} for {nameof(CosmosParallelItemQueryExecutionContext)}"));
            }

            List<CompositeContinuationToken> compositeContinuationTokens = new List<CompositeContinuationToken>();
            foreach (CosmosElement compositeContinuationTokenRaw in compositeContinuationTokenListRaw)
            {
                TryCatch<CompositeContinuationToken> tryCreateCompositeContinuationToken = CompositeContinuationToken.TryCreateFromCosmosElement(compositeContinuationTokenRaw);
                if (!tryCreateCompositeContinuationToken.Succeeded)
                {
                    return TryCatch<ParallelInitInfo>.FromException(tryCreateCompositeContinuationToken.Exception);
                }

                compositeContinuationTokens.Add(tryCreateCompositeContinuationToken.Result);
            }

            return CosmosCrossPartitionQueryExecutionContext.TryFindTargetRangeAndExtractContinuationTokens(
                partitionKeyRanges,
                compositeContinuationTokens.Select(token => Tuple.Create(token, token.Range)))
                .Try<ParallelInitInfo>((indexAndTokens) =>
                {
                    int minIndex = indexAndTokens.TargetIndex;
                    IReadOnlyDictionary<string, CompositeContinuationToken> rangeToToken = indexAndTokens.ContinuationTokens;

                    // We know that all partitions to the left of the continuation token are fully drained so we can filter them out
                    IReadOnlyList<PartitionKeyRange> filteredRanges = new PartialReadOnlyList<PartitionKeyRange>(
                    partitionKeyRanges,
                    minIndex,
                    partitionKeyRanges.Count - minIndex);

                    return new ParallelInitInfo(
                        filteredRanges,
                        rangeToToken);
                });
        }

        private readonly struct ParallelInitInfo
        {
            public ParallelInitInfo(IReadOnlyList<PartitionKeyRange> partialRanges, IReadOnlyDictionary<string, CompositeContinuationToken> continuationTokens)
            {
                this.PartialRanges = partialRanges;
                this.ContinuationTokens = continuationTokens;
            }

            public IReadOnlyList<PartitionKeyRange> PartialRanges { get; }

            public IReadOnlyDictionary<string, CompositeContinuationToken> ContinuationTokens { get; }
        }
    }
}
