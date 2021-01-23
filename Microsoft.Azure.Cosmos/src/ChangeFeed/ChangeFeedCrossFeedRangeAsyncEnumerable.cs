// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using Microsoft.Azure.Cosmos.ChangeFeed.Pagination;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    internal sealed class ChangeFeedCrossFeedRangeAsyncEnumerable : IAsyncEnumerable<TryCatch<ChangeFeedPage>>
    {
        private readonly IDocumentContainer documentContainer;
        private readonly ChangeFeedMode changeFeedMode;
        private readonly ChangeFeedRequestOptions changeFeedRequestOptions;
        private readonly ChangeFeedCrossFeedRangeState state;

        public ChangeFeedCrossFeedRangeAsyncEnumerable(
            IDocumentContainer documentContainer,
            ChangeFeedMode changeFeedMode,
            ChangeFeedRequestOptions changeFeedRequestOptions,
            ChangeFeedCrossFeedRangeState state)
        {
            this.documentContainer = documentContainer ?? throw new ArgumentNullException(nameof(documentContainer));
            this.changeFeedMode = changeFeedMode ?? throw new ArgumentNullException(nameof(changeFeedMode));
            this.changeFeedRequestOptions = changeFeedRequestOptions;
            this.state = state;
        }

        public IAsyncEnumerator<TryCatch<ChangeFeedPage>> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            CrossFeedRangeState<ChangeFeedState> innerState = new CrossFeedRangeState<ChangeFeedState>(this.state.FeedRangeStates);
            CrossPartitionChangeFeedAsyncEnumerator innerEnumerator = CrossPartitionChangeFeedAsyncEnumerator.Create(
                this.documentContainer,
                this.changeFeedMode,
                this.changeFeedRequestOptions,
                innerState,
                cancellationToken);

            return new ChangeFeedCrossFeedRangeAsyncEnumerator(
                innerEnumerator, 
                this.changeFeedRequestOptions?.JsonSerializationFormatOptions);
        }
    }
}
