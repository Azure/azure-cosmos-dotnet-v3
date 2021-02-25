// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ReadFeed
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.ReadFeed.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.AsyncEnumerable;

    internal sealed class ReadFeedCrossFeedRangeAsyncEnumerator : ITraceableAsyncEnumerator<TryCatch<ReadFeedPage>>
    {
        private readonly CrossPartitionReadFeedAsyncEnumerator enumerator;
        private readonly ITrace trace;

        public ReadFeedCrossFeedRangeAsyncEnumerator(
            CrossPartitionReadFeedAsyncEnumerator enumerator,
            ITrace trace)
        {
            this.enumerator = enumerator ?? throw new ArgumentNullException(nameof(enumerator));
            this.trace = trace ?? throw new ArgumentNullException(nameof(trace));
        }

        public TryCatch<ReadFeedPage> Current { get; private set; }

        public ValueTask DisposeAsync() => this.enumerator.DisposeAsync();

        public ValueTask<bool> MoveNextAsync()
        {
            return this.MoveNextAsync(this.trace);
        }

        public async ValueTask<bool> MoveNextAsync(ITrace trace)
        {
            if (!await this.enumerator.MoveNextAsync(trace))
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

            CosmosArray documents = CosmosQueryClientCore.ParseElementsFromRestStream(
                innerReadFeedPage.Page.Content,
                Documents.ResourceType.Document,
                cosmosSerializationOptions: null);
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
