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
    using Microsoft.Azure.Documents;

    internal sealed class CrossPartitionReadFeedAsyncEnumerator : IAsyncEnumerator<TryCatch<CrossFeedRangePage<ReadFeedPage, ReadFeedState>>>
    {
        private readonly CrossPartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState> crossPartitionEnumerator;
        private CancellationToken cancellationToken;

        private CrossPartitionReadFeedAsyncEnumerator(
            CrossPartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState> crossPartitionEnumerator,
            CancellationToken cancellationToken)
        {
            this.crossPartitionEnumerator = crossPartitionEnumerator ?? throw new ArgumentNullException(nameof(crossPartitionEnumerator));
            this.cancellationToken = cancellationToken;
        }

        public TryCatch<CrossFeedRangePage<ReadFeedPage, ReadFeedState>> Current { get; set; }

        public async ValueTask<bool> MoveNextAsync()
        {
            this.cancellationToken.ThrowIfCancellationRequested();

            if (!await this.crossPartitionEnumerator.MoveNextAsync())
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

        public ValueTask DisposeAsync() => this.crossPartitionEnumerator.DisposeAsync();

        public static CrossPartitionReadFeedAsyncEnumerator Create(
            IDocumentContainer documentContainer,
            QueryRequestOptions queryRequestOptions,
            CrossFeedRangeState<ReadFeedState> crossFeedRangeState,
            int pageSize,
            CancellationToken cancellationToken)
        {
            if (documentContainer == null)
            {
                throw new ArgumentNullException(nameof(documentContainer));
            }

            if (crossFeedRangeState == null)
            {
                throw new ArgumentNullException(nameof(crossFeedRangeState));
            }

            RntbdConstants.RntdbEnumerationDirection rntdbEnumerationDirection = RntbdConstants.RntdbEnumerationDirection.Forward;
            if ((queryRequestOptions?.Properties != null) && queryRequestOptions.Properties.TryGetValue(HttpConstants.HttpHeaders.EnumerationDirection, out object direction))
            {
                rntdbEnumerationDirection = (byte)direction == (byte)RntbdConstants.RntdbEnumerationDirection.Reverse ? RntbdConstants.RntdbEnumerationDirection.Reverse : RntbdConstants.RntdbEnumerationDirection.Forward;
            }

            IComparer<PartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState>> comparer;
            if (rntdbEnumerationDirection == RntbdConstants.RntdbEnumerationDirection.Forward)
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
                    queryRequestOptions,
                    pageSize,
                    cancellationToken),
                comparer: comparer,
                maxConcurrency: default,
                cancellationToken,
                crossFeedRangeState);

            CrossPartitionReadFeedAsyncEnumerator enumerator = new CrossPartitionReadFeedAsyncEnumerator(
                crossPartitionEnumerator,
                cancellationToken);

            return enumerator;
        }

        private static CreatePartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState> MakeCreateFunction(
            IReadFeedDataSource readFeedDataSource,
            QueryRequestOptions queryRequestOptions,
            int pageSize,
            CancellationToken cancellationToken) => (FeedRangeInternal range, ReadFeedState state) => new ReadFeedPartitionRangeEnumerator(
                readFeedDataSource,
                range,
                queryRequestOptions,
                pageSize,
                cancellationToken,
                state);

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
                if (partitionRangePageEnumerator1.Range is FeedRangePartitionKey)
                {
                    return -1;
                }

                // Order does not matter for logical partition keys, since they are vacously split proof.
                if (partitionRangePageEnumerator2.Range is FeedRangePartitionKey)
                {
                    return -1;
                }

                // Either both don't have results or both do.
                return string.CompareOrdinal(
                    ((FeedRangeEpk)partitionRangePageEnumerator1.Range).Range.Min,
                    ((FeedRangeEpk)partitionRangePageEnumerator2.Range).Range.Min);
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
