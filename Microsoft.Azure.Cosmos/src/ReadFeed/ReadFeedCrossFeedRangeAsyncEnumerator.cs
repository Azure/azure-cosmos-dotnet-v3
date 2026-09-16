// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ReadFeed
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.ReadFeed.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;

    internal sealed class ReadFeedCrossFeedRangeAsyncEnumerator : ITracingAsyncEnumerator<TryCatch<ReadFeedPage>>
    {
        private readonly CrossPartitionReadFeedAsyncEnumerator enumerator;

        public ReadFeedCrossFeedRangeAsyncEnumerator(CrossPartitionReadFeedAsyncEnumerator enumerator)
        {
            this.enumerator = enumerator ?? throw new ArgumentNullException(nameof(enumerator));
        }

        public TryCatch<ReadFeedPage> Current { get; private set; }

        public ValueTask DisposeAsync() => this.enumerator.DisposeAsync();

        public async ValueTask<bool> MoveNextAsync(ITrace trace, CancellationToken cancellationToken)
        {
            if (!await this.enumerator.MoveNextAsync(trace, cancellationToken))
            {
                return false;
            }

            TryCatch<CrossFeedRangePage<Pagination.ReadFeedPage, ReadFeedState>> monadicInnerReadFeedPage = this.enumerator.Current;
            if (monadicInnerReadFeedPage.Failed)
            {
                this.Current = TryCatch<ReadFeedPage>.FromException(monadicInnerReadFeedPage.Exception);
                return true;
            }

            CrossFeedRangePage<Pagination.ReadFeedPage, ReadFeedState> innerReadFeedPage = monadicInnerReadFeedPage.Result;
            CrossFeedRangeState<ReadFeedState> crossFeedRangeState = innerReadFeedPage.State;
            ReadFeedCrossFeedRangeState? state = crossFeedRangeState != null ? new ReadFeedCrossFeedRangeState(crossFeedRangeState.Value) : (ReadFeedCrossFeedRangeState?)null;

            CosmosQueryClientCore.ParseRestStream(
                innerReadFeedPage.Page.Content,
                Documents.ResourceType.Document,
                out CosmosArray documents,
                out CosmosObject distributionPlan,
                out bool? ignored);
            ReadFeedPage page = new ReadFeedPage(
                documents,
                innerReadFeedPage.Page.RequestCharge,
                innerReadFeedPage.Page.ActivityId,
                state,
                innerReadFeedPage.Page.AdditionalHeaders);
            this.Current = TryCatch<ReadFeedPage>.FromResult(page);
            return true;
        }
    }
}
