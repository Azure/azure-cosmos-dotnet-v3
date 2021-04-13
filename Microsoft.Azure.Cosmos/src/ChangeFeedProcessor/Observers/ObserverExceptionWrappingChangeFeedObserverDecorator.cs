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
    using Microsoft.Azure.Cosmos.Core.Trace;

    internal sealed class ObserverExceptionWrappingChangeFeedObserverDecorator : ChangeFeedObserver
    {
        private readonly ChangeFeedObserver changeFeedObserver;

        public ObserverExceptionWrappingChangeFeedObserverDecorator(ChangeFeedObserver changeFeedObserver)
        {
            this.changeFeedObserver = changeFeedObserver ?? throw new ArgumentNullException(nameof(changeFeedObserver));
        }

        public override async Task CloseAsync(string leaseToken, ChangeFeedObserverCloseReason reason)
        {
            try
            {
                await this.changeFeedObserver.CloseAsync(leaseToken, reason).ConfigureAwait(false);
            }
            catch (Exception userException)
            {
                Extensions.TraceException(userException);
                DefaultTrace.TraceWarning("Exception happened on Observer.CloseAsync");
                throw new ObserverException(userException);
            }
        }

        public override async Task OpenAsync(string leaseToken)
        {
            try
            {
                await this.changeFeedObserver.OpenAsync(leaseToken).ConfigureAwait(false);
            }
            catch (Exception userException)
            {
                Extensions.TraceException(userException);
                DefaultTrace.TraceWarning("Exception happened on Observer.OpenAsync");
                throw new ObserverException(userException);
            }
        }

        public override async Task ProcessChangesAsync(ChangeFeedObserverContextCore context, Stream stream, CancellationToken cancellationToken)
        {
            try
            {
                await this.changeFeedObserver.ProcessChangesAsync(context, stream, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception userException)
            {
                Extensions.TraceException(userException);
                DefaultTrace.TraceWarning("Exception happened on Observer.ProcessChangesAsync");
                throw new ObserverException(userException);
            }
        }
    }
}
