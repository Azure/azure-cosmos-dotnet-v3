// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ReadFeed
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.ReadFeed.Pagination;

    internal sealed class ReadFeedCrossFeedRangeAsyncEnumerable : IAsyncEnumerable<TryCatch<ReadFeedPage>>
    {
        private readonly IDocumentContainer documentContainer;
        private readonly QueryRequestOptions requestOptions;
        private readonly ReadFeedCrossFeedRangeState state;

        public ReadFeedCrossFeedRangeAsyncEnumerable(
            IDocumentContainer documentContainer,
            QueryRequestOptions requestOptions,
            ReadFeedCrossFeedRangeState state)
        {
            this.documentContainer = documentContainer ?? throw new ArgumentNullException(nameof(documentContainer));
            this.requestOptions = requestOptions;
            this.state = state;
        }

        public IAsyncEnumerator<TryCatch<ReadFeedPage>> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            CrossFeedRangeState<ReadFeedState> innerState = new CrossFeedRangeState<ReadFeedState>(this.state.FeedRangeStates);
            CrossPartitionReadFeedAsyncEnumerator innerEnumerator = CrossPartitionReadFeedAsyncEnumerator.Create(
                this.documentContainer,
                this.requestOptions,
                innerState,
                this.requestOptions.MaxItemCount ?? int.MaxValue,
                cancellationToken);

            return new ReadFeedCrossFeedRangeAsyncEnumerator(innerEnumerator);
        }
    }
}
