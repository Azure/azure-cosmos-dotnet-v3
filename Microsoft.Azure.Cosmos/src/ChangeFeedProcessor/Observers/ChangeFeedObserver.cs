//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// This interface is used to deliver change events to document feed observers.
    /// </summary>
    internal abstract class ChangeFeedObserver
    {
        /// <summary>
        /// This is called when change feed observer is opened.
        /// </summary>
        /// <param name="leaseToken">Token representing the lease that is starting processing.</param>
        /// <returns>A Task to allow asynchronous execution.</returns>
        public abstract Task OpenAsync(string leaseToken);

        /// <summary>
        /// This is called when change feed observer is closed.
        /// </summary>
        /// <param name="leaseToken">Token representing the lease that is finishing processing.</param>
        /// <param name="reason">Specifies the reason the observer is closed.</param>
        /// <returns>A Task to allow asynchronous execution.</returns>
        public abstract Task CloseAsync(string leaseToken, ChangeFeedObserverCloseReason reason);

        /// <summary>
        /// This is called when document changes are available on change feed.
        /// </summary>
        /// <param name="context">The context specifying partition for this change event, etc.</param>
        /// <param name="stream">The document streams that contain the change feed events.</param>
        /// <param name="cancellationToken">Token to signal that the partition processing is going to finish.</param>
        /// <returns>A Task to allow asynchronous execution.</returns>
        public abstract Task ProcessChangesAsync(
            ChangeFeedObserverContextCore context, 
            Stream stream,
            CancellationToken cancellationToken);
    }
}