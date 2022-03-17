// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.SinglePartition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.Parallel;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;
    using static Microsoft.Azure.Cosmos.Query.Core.Pipeline.PartitionMapper;

    internal sealed class SinglePartitionQueryPipelineStage : IQueryPipelineStage
    {
        private readonly CrossPartitionRangePageAsyncEnumerator<QueryPage, QueryState> partitionRangePageAsyncEnumerator;
        private CancellationToken cancellationToken;

        private SinglePartitionQueryPipelineStage(
            CrossPartitionRangePageAsyncEnumerator<QueryPage, QueryState> partitionRangePageAsyncEnumerator,
            CancellationToken cancellationToken)
        {
            this.partitionRangePageAsyncEnumerator = partitionRangePageAsyncEnumerator ?? throw new ArgumentNullException(nameof(partitionRangePageAsyncEnumerator));
            this.cancellationToken = cancellationToken;
        }
        public TryCatch<QueryPage> Current { get; private set; }

        public ValueTask DisposeAsync()
        {
            return this.partitionRangePageAsyncEnumerator.DisposeAsync();
        }
        private static CreatePartitionRangePageAsyncEnumerator<QueryPage, QueryState> MakeCreateFunction(
            IQueryDataSource queryDataSource,
            SqlQuerySpec sqlQuerySpec,
            QueryPaginationOptions queryPaginationOptions,
            Cosmos.PartitionKey? partitionKey,
            CancellationToken cancellationToken) => (FeedRangeState<QueryState> feedRangeState) => new QueryPartitionRangePageAsyncEnumerator(
                queryDataSource,
                sqlQuerySpec,
                feedRangeState,
                partitionKey,
                queryPaginationOptions,
                cancellationToken);
        public void SetCancellationToken(CancellationToken cancellationToken)
        {
            this.cancellationToken = cancellationToken;
            this.partitionRangePageAsyncEnumerator.SetCancellationToken(cancellationToken);
        }
        public ValueTask<bool> MoveNextAsync(ITrace trace)
        {
            return new ValueTask<bool>(true);
        }
        public static TryCatch<IQueryPipelineStage> MonadicCreate(
              IDocumentContainer documentContainer,
              SqlQuerySpec sqlQuerySpec,
              IReadOnlyList<FeedRangeEpk> targetRanges,
              Cosmos.PartitionKey? partitionKey,
              QueryPaginationOptions queryPaginationOptions,
              int maxConcurrency,
              CosmosElement continuationToken,
              CancellationToken cancellationToken)
        {
            if (targetRanges == null)
            {
                throw new ArgumentNullException(nameof(targetRanges));
            }

            if (targetRanges.Count == 0)
            {
                throw new ArgumentException($"{nameof(targetRanges)} must have some elements");
            }
            // there is a FeedRangeState which might be the right feed range to use
            TryCatch<CrossFeedRangeState<QueryState>> monadicExtractState = MonadicExtractState(continuationToken, targetRanges);
            if (monadicExtractState.Failed)
            {
                return TryCatch<IQueryPipelineStage>.FromException(monadicExtractState.Exception);
            }

            CrossFeedRangeState<QueryState> state = monadicExtractState.Result;

            CrossPartitionRangePageAsyncEnumerator<QueryPage, QueryState> partitionPageEnumerator = new CrossPartitionRangePageAsyncEnumerator<QueryPage, QueryState>(
                documentContainer,
                SinglePartitionQueryPipelineStage.MakeCreateFunction(documentContainer, sqlQuerySpec, queryPaginationOptions, partitionKey, cancellationToken),
                comparer: Comparer.Singleton,
                maxConcurrency,
                state: state,
                cancellationToken: cancellationToken);

            SinglePartitionQueryPipelineStage stage = new SinglePartitionQueryPipelineStage(partitionPageEnumerator, cancellationToken);
            return TryCatch<IQueryPipelineStage>.FromResult(stage);
        }
        private static TryCatch<CrossFeedRangeState<QueryState>> MonadicExtractState(
            CosmosElement continuationToken,
            IReadOnlyList<FeedRangeEpk> ranges)
        {
            if (continuationToken == null)
            {
                // Full fan out to the ranges with null continuations
                CrossFeedRangeState<QueryState> fullFanOutState = new CrossFeedRangeState<QueryState>(ranges.Select(range => new FeedRangeState<QueryState>(range, (QueryState)null)).ToArray());
                return TryCatch<CrossFeedRangeState<QueryState>>.FromResult(fullFanOutState);
            }

            if (!(continuationToken is CosmosArray parallelContinuationTokenListRaw))
            {
                return TryCatch<CrossFeedRangeState<QueryState>>.FromException(
                    new MalformedContinuationTokenException(
                        $"Invalid format for continuation token {continuationToken} for {nameof(SinglePartitionQueryPipelineStage)}"));
            }

            if (parallelContinuationTokenListRaw.Count == 0)
            {
                return TryCatch<CrossFeedRangeState<QueryState>>.FromException(
                    new MalformedContinuationTokenException(
                        $"Invalid format for continuation token {continuationToken} for {nameof(SinglePartitionQueryPipelineStage)}"));
            }

            List<SinglePartitionContinuationToken> parallelContinuationTokens = new List<SinglePartitionContinuationToken>();
            foreach (CosmosElement parallelContinuationTokenRaw in parallelContinuationTokenListRaw)
            {
                TryCatch<SinglePartitionContinuationToken> tryCreateParallelContinuationToken = SinglePartitionContinuationToken.TryCreateFromCosmosElement(parallelContinuationTokenRaw);
                if (tryCreateParallelContinuationToken.Failed)
                {
                    return TryCatch<CrossFeedRangeState<QueryState>>.FromException(
                        tryCreateParallelContinuationToken.Exception);
                }

                parallelContinuationTokens.Add(tryCreateParallelContinuationToken.Result);
            }

            TryCatch<PartitionMapping<SinglePartitionContinuationToken>> partitionMappingMonad = PartitionMapper.MonadicGetPartitionMapping(
                ranges,
                parallelContinuationTokens);
            //parallelContinuationTokens[0];
            if (partitionMappingMonad.Failed)
            {
                return TryCatch<CrossFeedRangeState<QueryState>>.FromException(
                    partitionMappingMonad.Exception);
            }

            PartitionMapping<SinglePartitionContinuationToken> partitionMapping = partitionMappingMonad.Result;
            List<FeedRangeState<QueryState>> feedRangeStates = new List<FeedRangeState<QueryState>>();

            List<IReadOnlyDictionary<FeedRangeEpk, SinglePartitionContinuationToken>> rangesToInitialize = new List<IReadOnlyDictionary<FeedRangeEpk, SinglePartitionContinuationToken>>()
            {
                // Skip all the partitions left of the target range, since they have already been drained fully.
                partitionMapping.TargetMapping,
                partitionMapping.MappingRightOfTarget,
            };

            foreach (IReadOnlyDictionary<FeedRangeEpk, SinglePartitionContinuationToken> rangeToInitalize in rangesToInitialize)
            {
                foreach (KeyValuePair<FeedRangeEpk, SinglePartitionContinuationToken> kvp in rangeToInitalize)
                {
                    FeedRangeState<QueryState> feedRangeState = new FeedRangeState<QueryState>(kvp.Key, kvp.Value?.Token != null ? new QueryState(CosmosString.Create(kvp.Value.Token)) : null);
                    feedRangeStates.Add(feedRangeState);
                }
            }

            CrossFeedRangeState<QueryState> crossPartitionState = new CrossFeedRangeState<QueryState>(feedRangeStates.ToArray());

            return TryCatch<CrossFeedRangeState<QueryState>>.FromResult(crossPartitionState);
        }
        private sealed class Comparer : IComparer<PartitionRangePageAsyncEnumerator<QueryPage, QueryState>>
        {
            public static readonly Comparer Singleton = new Comparer();

            public int Compare(
                PartitionRangePageAsyncEnumerator<QueryPage, QueryState> partitionRangePageEnumerator1,
                PartitionRangePageAsyncEnumerator<QueryPage, QueryState> partitionRangePageEnumerator2)
            {
                if (object.ReferenceEquals(partitionRangePageEnumerator1, partitionRangePageEnumerator2))
                {
                    return 0;
                }

                // Either both don't have results or both do.
                return string.CompareOrdinal(
                    ((FeedRangeEpk)partitionRangePageEnumerator1.FeedRangeState.FeedRange).Range.Min,
                    ((FeedRangeEpk)partitionRangePageEnumerator2.FeedRangeState.FeedRange).Range.Min);
            }
        }
    }
}