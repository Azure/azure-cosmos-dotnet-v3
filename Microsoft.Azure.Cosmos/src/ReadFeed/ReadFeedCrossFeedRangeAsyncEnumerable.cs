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
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.AsyncEnumerable;

    internal sealed class ReadFeedCrossFeedRangeAsyncEnumerable : ITraceableAsyncEnumerable<TryCatch<ReadFeedPage>>
    {
        private readonly IDocumentContainer documentContainer;
        private readonly ReadFeedCrossFeedRangeState state;
        private readonly ReadFeedPaginationOptions readFeedPaginationOptions;
        private readonly ITrace trace;

        public ReadFeedCrossFeedRangeAsyncEnumerable(
            IDocumentContainer documentContainer,
            ReadFeedCrossFeedRangeState state,
            ReadFeedPaginationOptions readFeedPaginationOptions,
            ITrace trace)
        {
            this.documentContainer = documentContainer ?? throw new ArgumentNullException(nameof(documentContainer));
            this.state = state;
            this.readFeedPaginationOptions = readFeedPaginationOptions;
            this.trace = trace ?? throw new ArgumentNullException(nameof(trace));
        }

        public IAsyncEnumerator<TryCatch<ReadFeedPage>> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return this.GetAsyncEnumerator(this.trace, cancellationToken);
        }

        public ITraceableAsyncEnumerator<TryCatch<ReadFeedPage>> GetAsyncEnumerator(ITrace trace, CancellationToken cancellationToken)
        {
            CrossFeedRangeState<ReadFeedState> innerState = new CrossFeedRangeState<ReadFeedState>(this.state.FeedRangeStates);
            CrossPartitionReadFeedAsyncEnumerator innerEnumerator = CrossPartitionReadFeedAsyncEnumerator.Create(
                this.documentContainer,
                innerState,
                this.readFeedPaginationOptions,
                trace,
                cancellationToken);

            return new ReadFeedCrossFeedRangeAsyncEnumerator(innerEnumerator, trace);
        }
    }
}
