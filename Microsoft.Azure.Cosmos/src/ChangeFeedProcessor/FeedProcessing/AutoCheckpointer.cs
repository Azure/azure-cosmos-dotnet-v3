//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class AutoCheckpointer : ChangeFeedObserver
    {
        private readonly ChangeFeedObserver observer;

        public AutoCheckpointer(ChangeFeedObserver observer)
        {
            if (observer == null)
            {
                throw new ArgumentNullException(nameof(observer));
            }

            this.observer = observer;
        }

        public override Task OpenAsync(ChangeFeedObserverContext context)
        {
            return this.observer.OpenAsync(context);
        }

        public override Task CloseAsync(ChangeFeedObserverContext context, ChangeFeedObserverCloseReason reason)
        {
            return this.observer.CloseAsync(context, reason);
        }

        public override async Task ProcessChangesAsync(
            ChangeFeedObserverContext context,
            Stream stream,
            CancellationToken cancellationToken)
        {
            await this.observer.ProcessChangesAsync(context, stream, cancellationToken).ConfigureAwait(false);

            await context.CheckpointAsync().ConfigureAwait(false);
        }
    }
}