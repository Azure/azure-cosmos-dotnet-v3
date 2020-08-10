//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// This interface is used to deliver change events to document feed observers.
    /// </summary>
    internal abstract class ChangeFeedObserver<T>
    {
        /// <summary>
        /// This is called when change feed observer is opened.
        /// </summary>
        /// <param name="context">The context specifying partition for this observer, etc.</param>
        /// <returns>A Task to allow asynchronous execution.</returns>
        public virtual Task OpenAsync(ChangeFeedProcessorContext context)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// This is called when change feed observer is closed.
        /// </summary>
        /// <param name="context">The context specifying partition for this observer, etc.</param>
        /// <param name="reason">Specifies the reason the observer is closed.</param>
        /// <returns>A Task to allow asynchronous execution.</returns>
        public virtual Task CloseAsync(ChangeFeedProcessorContext context, ChangeFeedObserverCloseReason reason)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// This is called when document changes are available on change feed.
        /// </summary>
        /// <param name="context">The context specifying partition for this change event, etc.</param>
        /// <param name="docs">The documents changed.</param>
        /// <param name="cancellationToken">Token to signal that the partition processing is going to finish.</param>
        /// <returns>A Task to allow asynchronous execution.</returns>
        public abstract Task ProcessChangesAsync(ChangeFeedProcessorContext context, IReadOnlyCollection<T> docs, CancellationToken cancellationToken);
    }
}