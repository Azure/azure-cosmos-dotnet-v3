//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Exceptions;

    internal sealed class ObserverExceptionWrappingChangeFeedObserverDecorator : ChangeFeedObserver
    {
        private readonly ChangeFeedObserver changeFeedObserver;

        public ObserverExceptionWrappingChangeFeedObserverDecorator(ChangeFeedObserver changeFeedObserver)
        {
            this.changeFeedObserver = changeFeedObserver ?? throw new ArgumentNullException(nameof(changeFeedObserver));
        }

        public override Task CloseAsync(string leaseToken, ChangeFeedObserverCloseReason reason)
        {
            return this.changeFeedObserver.CloseAsync(leaseToken, reason);
        }

        public override Task OpenAsync(string leaseToken)
        {
            return this.OpenAsync(leaseToken);
        }

        public override async Task ProcessChangesAsync(ChangeFeedObserverContextCore context, Stream stream, CancellationToken cancellationToken)
        {
            try
            {
                await this.changeFeedObserver.ProcessChangesAsync(context, stream, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception userException)
            {
                throw new ChangeFeedProcessorUserException(userException);
            }
        }
    }
}
