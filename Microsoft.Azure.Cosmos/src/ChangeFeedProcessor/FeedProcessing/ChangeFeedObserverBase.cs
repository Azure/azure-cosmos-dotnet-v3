//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing
{
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using static Microsoft.Azure.Cosmos.Container;

    internal sealed class ChangeFeedObserverBase : ChangeFeedObserver
    {
        private readonly ChangesStreamHandler onChanges;

        public ChangeFeedObserverBase(ChangesStreamHandler onChanges)
        {
            this.onChanges = onChanges;
        }

        public override Task CloseAsync(ChangeFeedObserverContext context, ChangeFeedObserverCloseReason reason)
        {
            return Task.CompletedTask;
        }

        public override Task OpenAsync(ChangeFeedObserverContext context)
        {
            return Task.CompletedTask;
        }

        public override Task ProcessChangesAsync(ChangeFeedObserverContext context, Stream stream, CancellationToken cancellationToken)
        {
            return this.onChanges(stream, cancellationToken);
        }
    }
}
