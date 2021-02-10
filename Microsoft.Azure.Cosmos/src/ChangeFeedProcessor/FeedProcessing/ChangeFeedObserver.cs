//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing
{
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing;

    internal abstract class ChangeFeedObserver
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
        /// <param name="stream">The document streams changed.</param>
        /// <param name="cancellationToken">Token to signal that the partition processing is going to finish.</param>
        /// <returns>A Task to allow asynchronous execution.</returns>
        public abstract Task ProcessChangesAsync(ChangeFeedObserverContext context, Stream stream, CancellationToken cancellationToken);
    }
}