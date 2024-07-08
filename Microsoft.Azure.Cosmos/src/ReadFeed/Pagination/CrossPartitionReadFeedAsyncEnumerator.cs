// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ReadFeed.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Tests.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    internal sealed class CrossPartitionReadFeedAsyncEnumerator : ITracingAsyncEnumerator<TryCatch<CrossFeedRangePage<ReadFeedPage, ReadFeedState>>>
    {
        private readonly CrossPartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState> crossPartitionEnumerator;

        private CrossPartitionReadFeedAsyncEnumerator(
            CrossPartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState> crossPartitionEnumerator)
        {
            this.crossPartitionEnumerator = crossPartitionEnumerator ?? throw new ArgumentNullException(nameof(crossPartitionEnumerator));
        }

        public TryCatch<CrossFeedRangePage<ReadFeedPage, ReadFeedState>> Current { get; set; }

        public async ValueTask<bool> MoveNextAsync(ITrace trace, CancellationToken cancellationToken)
        {
            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            using (ITrace moveNextAsyncTrace = trace.StartChild(name: nameof(MoveNextAsync), component: TraceComponent.ReadFeed, level: TraceLevel.Info))
            {
                if (!await this.crossPartitionEnumerator.MoveNextAsync(moveNextAsyncTrace, cancellationToken))
                {
                    this.Current = default;
                    return false;
                }

                TryCatch<CrossFeedRangePage<ReadFeedPage, ReadFeedState>> monadicCrossPartitionPage = this.crossPartitionEnumerator.Current;
                if (monadicCrossPartitionPage.Failed)
                {
                    this.Current = TryCatch<CrossFeedRangePage<ReadFeedPage, ReadFeedState>>.FromException(monadicCrossPartitionPage.Exception);
                    return true;
                }

                CrossFeedRangePage<ReadFeedPage, ReadFeedState> crossPartitionPage = monadicCrossPartitionPage.Result;
                this.Current = TryCatch<CrossFeedRangePage<ReadFeedPage, ReadFeedState>>.FromResult(crossPartitionPage);
                return true;
            }
        }

        public ValueTask DisposeAsync() => this.crossPartitionEnumerator.DisposeAsync();

        public static CrossPartitionReadFeedAsyncEnumerator Create(
            IDocumentContainer documentContainer,
            CrossFeedRangeState<ReadFeedState> crossFeedRangeState,
            ReadFeedExecutionOptions readFeedPaginationOptions)
        {
            if (documentContainer == null)
            {
                throw new ArgumentNullException(nameof(documentContainer));
            }

            if (crossFeedRangeState == null)
            {
                throw new ArgumentNullException(nameof(crossFeedRangeState));
            }

            readFeedPaginationOptions ??= ReadFeedExecutionOptions.Default;

            ReadFeedExecutionOptions.PaginationDirection paginationDirection = readFeedPaginationOptions.Direction.GetValueOrDefault(ReadFeedExecutionOptions.PaginationDirection.Forward);

            IComparer<PartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState>> comparer;
            if (paginationDirection == ReadFeedExecutionOptions.PaginationDirection.Forward)
            {
                comparer = PartitionRangePageAsyncEnumeratorComparerForward.Singleton;
            }
            else
            {
                comparer = PartitionRangePageAsyncEnumeratorComparerReverse.Singleton;
            }

            CrossPartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState> crossPartitionEnumerator = new CrossPartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState>(
                documentContainer,
                CrossPartitionReadFeedAsyncEnumerator.MakeCreateFunction(
                    documentContainer,
                    readFeedPaginationOptions),
                comparer: comparer,
                maxConcurrency: default,
                prefetchPolicy: PrefetchPolicy.PrefetchSinglePage,
                crossFeedRangeState);

            CrossPartitionReadFeedAsyncEnumerator enumerator = new CrossPartitionReadFeedAsyncEnumerator(
                crossPartitionEnumerator);

            return enumerator;
        }

        private static CreatePartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState> MakeCreateFunction(
            IReadFeedDataSource readFeedDataSource,
            ReadFeedExecutionOptions readFeedPaginationOptions)
        {
            return (FeedRangeState<ReadFeedState> feedRangeState) => new ReadFeedPartitionRangeEnumerator(
                readFeedDataSource,
                feedRangeState,
                readFeedPaginationOptions);
        }

        private sealed class PartitionRangePageAsyncEnumeratorComparerForward : IComparer<PartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState>>
        {
            public static readonly PartitionRangePageAsyncEnumeratorComparerForward Singleton = new PartitionRangePageAsyncEnumeratorComparerForward();

            public int Compare(
                PartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState> partitionRangePageEnumerator1,
                PartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState> partitionRangePageEnumerator2)
            {
                if (object.ReferenceEquals(partitionRangePageEnumerator1, partitionRangePageEnumerator2))
                {
                    return 0;
                }

                // Order does not matter for logical partition keys, since they are vacously split proof.
                if (partitionRangePageEnumerator1.FeedRangeState.FeedRange is FeedRangePartitionKey)
                {
                    return -1;
                }

                // Order does not matter for logical partition keys, since they are vacously split proof.
                if (partitionRangePageEnumerator2.FeedRangeState.FeedRange is FeedRangePartitionKey)
                {
                    return -1;
                }

                // Either both don't have results or both do.
                return string.CompareOrdinal(
                    ((FeedRangeEpk)partitionRangePageEnumerator1.FeedRangeState.FeedRange).Range.Min,
                    ((FeedRangeEpk)partitionRangePageEnumerator2.FeedRangeState.FeedRange).Range.Min);
            }
        }

        private sealed class PartitionRangePageAsyncEnumeratorComparerReverse : IComparer<PartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState>>
        {
            public static readonly PartitionRangePageAsyncEnumeratorComparerReverse Singleton = new PartitionRangePageAsyncEnumeratorComparerReverse();

            public int Compare(
                PartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState> partitionRangePageEnumerator1,
                PartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState> partitionRangePageEnumerator2)
            {
                return -1 * PartitionRangePageAsyncEnumeratorComparerForward.Singleton.Compare(
                    partitionRangePageEnumerator1,
                    partitionRangePageEnumerator2);
            }
        }
    }
}
