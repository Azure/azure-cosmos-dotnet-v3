// ----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  Licensed under the MIT license.
// ----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor.FeedProcessing
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.Exceptions;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.Logging;

    internal sealed class ObserverExceptionWrappingChangeFeedObserverDecorator<T>: ChangeFeedObserver<T>
    {
        private static readonly ILog Logger = LogProvider.GetCurrentClassLogger();
        private ChangeFeedObserver<T> changeFeedObserver;

        public ObserverExceptionWrappingChangeFeedObserverDecorator(ChangeFeedObserver<T> changeFeedObserver)
        {
            this.changeFeedObserver = changeFeedObserver;
        }

        public override async Task CloseAsync(ChangeFeedObserverContext context, ChangeFeedObserverCloseReason reason)
        {
            try
            {
                await this.changeFeedObserver.CloseAsync(context, reason).ConfigureAwait(false);
            }
            catch (Exception userException)
            {
                Logger.WarnException("Exception happened on Observer.CloseAsync", userException);
                throw new ObserverException(userException);
            }
        }

        public override async Task OpenAsync(ChangeFeedObserverContext context)
        {
            try
            {
                await this.changeFeedObserver.OpenAsync(context).ConfigureAwait(false);
            }
            catch (Exception userException)
            {
                Logger.WarnException("Exception happened on Observer.OpenAsync", userException);
                throw new ObserverException(userException);
            }
        }

        public override async Task ProcessChangesAsync(ChangeFeedObserverContext context, IReadOnlyList<T> docs, CancellationToken cancellationToken)
        {
            try
            {
                await this.changeFeedObserver.ProcessChangesAsync(context, docs, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception userException)
            {
                Logger.WarnException("Exception happened on Observer.ProcessChangesAsync", userException);
                throw new ObserverException(userException);
            }
        }
    }
}
