//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  Licensed under the MIT license.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor.FeedProcessing
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// This interface is used to deliver change events to document feed observers.
    /// </summary>
    public abstract class ChangeFeedObserver<T>
    {
        /// <summary>
        /// This is called when change feed observer is opened.
        /// </summary>
        /// <param name="context">The context specifying partition for this observer, etc.</param>
        /// <returns>A Task to allow asynchronous execution.</returns>
        public abstract Task OpenAsync(ChangeFeedObserverContext context);

        /// <summary>
        /// This is called when change feed observer is closed.
        /// </summary>
        /// <param name="context">The context specifying partition for this observer, etc.</param>
        /// <param name="reason">Specifies the reason the observer is closed.</param>
        /// <returns>A Task to allow asynchronous execution.</returns>
        public abstract Task CloseAsync(ChangeFeedObserverContext context, ChangeFeedObserverCloseReason reason);

        /// <summary>
        /// This is called when document changes are available on change feed.
        /// </summary>
        /// <param name="context">The context specifying partition for this change event, etc.</param>
        /// <param name="docs">The documents changed.</param>
        /// <param name="cancellationToken">Token to signal that the parition processing is going to finish.</param>
        /// <returns>A Task to allow asynchronous execution.</returns>
        public abstract Task ProcessChangesAsync(ChangeFeedObserverContext context, IReadOnlyList<T> docs, CancellationToken cancellationToken);
    }
}