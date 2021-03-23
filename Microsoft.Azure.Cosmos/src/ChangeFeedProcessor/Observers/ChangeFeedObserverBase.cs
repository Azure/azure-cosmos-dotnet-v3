//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class ChangeFeedObserverBase : ChangeFeedObserver
    {
        internal delegate Task ChangeFeedObserverBaseHandler(
            ChangeFeedObserverContextCore context,
            Stream stream,
            CancellationToken cancellationToken);

        private readonly ChangeFeedObserverBaseHandler onChanges;

        public ChangeFeedObserverBase(ChangeFeedObserverBaseHandler onChanges)
        {
            this.onChanges = onChanges;
        }

        public override Task OpenAsync(string leaseToken)
        {
            return Task.CompletedTask;
        }

        public override Task CloseAsync(
            string leaseToken, 
            ChangeFeedObserverCloseReason reason)
        {
            return Task.CompletedTask;
        }

        public override Task ProcessChangesAsync(
            ChangeFeedObserverContextCore context, 
            Stream stream, 
            CancellationToken cancellationToken)
        {
            return this.onChanges(context, stream, cancellationToken);
        }
    }
}
