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
    using Microsoft.Azure.Cosmos.Serializer;
    using Microsoft.Azure.Cosmos.Tracing;

    internal sealed class ChangeFeedCrossFeedRangeAsyncEnumerable : IAsyncEnumerable<TryCatch<ChangeFeedPage>>
    {
        private readonly IDocumentContainer documentContainer;
        private readonly ChangeFeedExecutionOptions changeFeedPaginationOptions;
        private readonly ChangeFeedCrossFeedRangeState state;
        private readonly JsonSerializationFormatOptions jsonSerializationFormatOptions;

        public ChangeFeedCrossFeedRangeAsyncEnumerable(
            IDocumentContainer documentContainer,
            ChangeFeedCrossFeedRangeState state,
            ChangeFeedExecutionOptions changeFeedPaginationOptions,
            JsonSerializationFormatOptions jsonSerializationFormatOptions = null)
        {
            this.documentContainer = documentContainer ?? throw new ArgumentNullException(nameof(documentContainer));
            this.changeFeedPaginationOptions = changeFeedPaginationOptions ?? ChangeFeedExecutionOptions.Default;
            this.state = state;
            this.jsonSerializationFormatOptions = jsonSerializationFormatOptions;
        }

        public IAsyncEnumerator<TryCatch<ChangeFeedPage>> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            CrossFeedRangeState<ChangeFeedState> innerState = new CrossFeedRangeState<ChangeFeedState>(this.state.FeedRangeStates);
            CrossPartitionChangeFeedAsyncEnumerator innerEnumerator = CrossPartitionChangeFeedAsyncEnumerator.Create(
                this.documentContainer,
                innerState,
                this.changeFeedPaginationOptions);

            ChangeFeedCrossFeedRangeAsyncEnumerator changeFeedEnumerator = new ChangeFeedCrossFeedRangeAsyncEnumerator(
                innerEnumerator,
                this.jsonSerializationFormatOptions);

            return new TracingAsyncEnumerator<TryCatch<ChangeFeedPage>>(changeFeedEnumerator, NoOpTrace.Singleton, cancellationToken);
        }
    }
}
