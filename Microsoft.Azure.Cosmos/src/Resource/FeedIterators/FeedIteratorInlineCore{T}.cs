//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Tracing;

    internal sealed class FeedIteratorInlineCore<T> : FeedIteratorInternal<T>
    {
        private readonly FeedIteratorInternal<T> feedIteratorInternal;

        internal FeedIteratorInlineCore(
            FeedIterator<T> feedIterator)
        {
            if (!(feedIterator is FeedIteratorInternal<T> feedIteratorInternal))
            {
                throw new ArgumentNullException(nameof(feedIterator));
            }

            this.feedIteratorInternal = feedIteratorInternal;
        }

        internal FeedIteratorInlineCore(
            FeedIteratorInternal<T> feedIteratorInternal)
        {
            this.feedIteratorInternal = feedIteratorInternal ?? throw new ArgumentNullException(nameof(feedIteratorInternal));
        }

        public override bool HasMoreResults => this.feedIteratorInternal.HasMoreResults;

        public override Task<FeedResponse<T>> ReadNextAsync(ITrace trace, CancellationToken cancellationToken)
        {
            using (ITrace childTrace = trace.StartChild("Synchronization Context"))
            {
                return TaskHelper.RunInlineIfNeededAsync(() => this.feedIteratorInternal.ReadNextAsync(trace, cancellationToken));
            }
        }

        public override CosmosElement GetCosmosElementContinuationToken()
        {
            return this.feedIteratorInternal.GetCosmosElementContinuationToken();
        }

        protected override void Dispose(bool disposing)
        {
            this.feedIteratorInternal.Dispose();
            base.Dispose(disposing);
        }
    }
}
