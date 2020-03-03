// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.Parallel
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
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
            if (queryContext == null)
            {
                throw new ArgumentNullException(nameof(queryContext));
            }

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
            IReadOnlyList<PartitionKeyRange> partitionKeyRanges,
            int initialPageSize,
            CosmosElement requestContinuation,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            TryCatch<IReadOnlyDictionary<PartitionKeyRange, CompositeContinuationToken>> tryGetPartitionKeyRangeToCompositeContinuationToken = CosmosParallelItemQueryExecutionContext.TryGetPartitionKeyRangeToCompositeContinuationToken(
                partitionKeyRanges,
                requestContinuation);
            if (!tryGetPartitionKeyRangeToCompositeContinuationToken.Succeeded)
            {
                return TryCatch<CosmosParallelItemQueryExecutionContext>.FromException(tryGetPartitionKeyRangeToCompositeContinuationToken.Exception);
            }

            IReadOnlyDictionary<PartitionKeyRange, string> partitionKeyRangeToContinuationToken = tryGetPartitionKeyRangeToCompositeContinuationToken
                .Result
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Token);
            TryCatch tryInitialize = await base.TryInitializeAsync(
                collectionRid,
                initialPageSize,
                sqlQuerySpec,
                partitionKeyRangeToContinuationToken,
                deferFirstPage: true,
                filter: null,
                tryFilterAsync: null,
                cancellationToken);
            if (!tryInitialize.Succeeded)
            {
                return TryCatch<CosmosParallelItemQueryExecutionContext>.FromException(tryInitialize.Exception);
            }

            return TryCatch<CosmosParallelItemQueryExecutionContext>.FromResult(this);
        }

        private static TryCatch<IReadOnlyDictionary<PartitionKeyRange, CompositeContinuationToken>> TryGetPartitionKeyRangeToCompositeContinuationToken(
            IReadOnlyList<PartitionKeyRange> partitionKeyRanges,
            CosmosElement continuationToken)
        {
            if (continuationToken == null)
            {
                Dictionary<PartitionKeyRange, CompositeContinuationToken> dictionary = new Dictionary<PartitionKeyRange, CompositeContinuationToken>();
                foreach (PartitionKeyRange partitionKeyRange in partitionKeyRanges)
                {
                    dictionary.Add(key: partitionKeyRange, value: null);
                }

                return TryCatch<IReadOnlyDictionary<PartitionKeyRange, CompositeContinuationToken>>.FromResult(dictionary);
            }

            TryCatch<IReadOnlyList<CompositeContinuationToken>> tryParseCompositeContinuationTokens = TryParseCompositeContinuationList(continuationToken);
            if (!tryParseCompositeContinuationTokens.Succeeded)
            {
                return TryCatch<IReadOnlyDictionary<PartitionKeyRange, CompositeContinuationToken>>.FromException(tryParseCompositeContinuationTokens.Exception);
            }

            TryCatch<IReadOnlyDictionary<PartitionKeyRange, CompositeContinuationToken>> tryMatchContinuationTokensToRanges = CosmosCrossPartitionQueryExecutionContext.TryMatchRangesToContinuationTokens(
                partitionKeyRanges,
                tryParseCompositeContinuationTokens.Result);
            if (!tryMatchContinuationTokensToRanges.Succeeded)
            {
                return TryCatch<IReadOnlyDictionary<PartitionKeyRange, CompositeContinuationToken>>.FromException(
                    tryMatchContinuationTokensToRanges.Exception);
            }

            return tryMatchContinuationTokensToRanges;
        }

        private static TryCatch<IReadOnlyList<CompositeContinuationToken>> TryParseCompositeContinuationList(
            CosmosElement requestContinuationToken)
        {
            if (requestContinuationToken == null)
            {
                throw new ArgumentNullException(nameof(requestContinuationToken));
            }

            if (!(requestContinuationToken is CosmosArray compositeContinuationTokenListRaw))
            {
                return TryCatch<IReadOnlyList<CompositeContinuationToken>>.FromException(
                    new MalformedContinuationTokenException(
                        $"Invalid format for continuation token {requestContinuationToken} for {nameof(CosmosParallelItemQueryExecutionContext)}"));
            }

            List<CompositeContinuationToken> compositeContinuationTokens = new List<CompositeContinuationToken>();
            foreach (CosmosElement compositeContinuationTokenRaw in compositeContinuationTokenListRaw)
            {
                TryCatch<CompositeContinuationToken> tryCreateCompositeContinuationToken = CompositeContinuationToken.TryCreateFromCosmosElement(compositeContinuationTokenRaw);
                if (!tryCreateCompositeContinuationToken.Succeeded)
                {
                    return TryCatch<IReadOnlyList<CompositeContinuationToken>>.FromException(tryCreateCompositeContinuationToken.Exception);
                }

                compositeContinuationTokens.Add(tryCreateCompositeContinuationToken.Result);
            }

            return TryCatch<IReadOnlyList<CompositeContinuationToken>>.FromResult(compositeContinuationTokens);
        }
    }
}
