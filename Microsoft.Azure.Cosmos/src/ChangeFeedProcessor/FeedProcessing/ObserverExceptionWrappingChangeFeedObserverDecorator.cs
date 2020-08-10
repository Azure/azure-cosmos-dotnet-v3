//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Exceptions;
    using Microsoft.Azure.Cosmos.Core.Trace;

    internal sealed class ObserverExceptionWrappingChangeFeedObserverDecorator<T> : ChangeFeedObserver<T>
    {
        private ChangeFeedObserver<T> changeFeedObserver;

        public ObserverExceptionWrappingChangeFeedObserverDecorator(ChangeFeedObserver<T> changeFeedObserver)
        {
            this.changeFeedObserver = changeFeedObserver;
        }

        public override async Task CloseAsync(ChangeFeedProcessorContext context, ChangeFeedObserverCloseReason reason)
        {
            try
            {
                await this.changeFeedObserver.CloseAsync(context, reason).ConfigureAwait(false);
            }
            catch (Exception userException)
            {
                Extensions.TraceException(userException);
                DefaultTrace.TraceWarning("Exception happened on Observer.CloseAsync");
                throw new ObserverException(userException);
            }
        }

        public override async Task OpenAsync(ChangeFeedProcessorContext context)
        {
            try
            {
                await this.changeFeedObserver.OpenAsync(context).ConfigureAwait(false);
            }
            catch (Exception userException)
            {
                Extensions.TraceException(userException);
                DefaultTrace.TraceWarning("Exception happened on Observer.OpenAsync");
                throw new ObserverException(userException);
            }
        }

        public override async Task ProcessChangesAsync(ChangeFeedProcessorContext context, IReadOnlyCollection<T> docs, CancellationToken cancellationToken)
        {
            try
            {
                await this.changeFeedObserver.ProcessChangesAsync(context, docs, cancellationToken).ConfigureAwait(false);
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
