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
        private readonly ReadFeedCrossFeedRangeState state;
        private readonly ReadFeedPaginationOptions readFeedPaginationOptions;

        public ReadFeedCrossFeedRangeAsyncEnumerable(
            IDocumentContainer documentContainer,
            ReadFeedCrossFeedRangeState state,
            ReadFeedPaginationOptions readFeedPaginationOptions)
        {
            this.documentContainer = documentContainer ?? throw new ArgumentNullException(nameof(documentContainer));
            this.state = state;
            this.readFeedPaginationOptions = readFeedPaginationOptions;
        }

        public IAsyncEnumerator<TryCatch<ReadFeedPage>> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            CrossFeedRangeState<ReadFeedState> innerState = new CrossFeedRangeState<ReadFeedState>(this.state.FeedRangeStates);
            CrossPartitionReadFeedAsyncEnumerator innerEnumerator = CrossPartitionReadFeedAsyncEnumerator.Create(
                this.documentContainer,
                innerState,
                this.readFeedPaginationOptions,
                cancellationToken);

            return new ReadFeedCrossFeedRangeAsyncEnumerator(innerEnumerator);
        }
    }
}
