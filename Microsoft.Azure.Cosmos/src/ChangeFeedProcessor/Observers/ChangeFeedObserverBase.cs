//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
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
            ChangeFeedProcessorContext context, 
            Stream stream, 
            CancellationToken cancellationToken)
        {
            return this.onChanges(context, stream, cancellationToken);
        }
    }
}
