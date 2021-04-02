//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
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
            this.observer = observer ?? throw new ArgumentNullException(nameof(observer));
        }

        public override Task OpenAsync(string leaseToken)
        {
            return this.observer.OpenAsync(leaseToken);
        }

        public override Task CloseAsync(string leaseToken, ChangeFeedObserverCloseReason reason)
        {
            return this.observer.CloseAsync(leaseToken, reason);
        }

        public override async Task ProcessChangesAsync(
            ChangeFeedObserverContextCore context,
            Stream stream,
            CancellationToken cancellationToken)
        {
            await this.observer.ProcessChangesAsync(context, stream, cancellationToken).ConfigureAwait(false);

            (bool isSuccess, Exception exception) = await context.TryCheckpointAsync();
            if (!isSuccess)
            {
                throw exception;
            }
        }
    }
}