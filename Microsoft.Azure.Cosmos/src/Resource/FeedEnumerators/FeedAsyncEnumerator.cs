//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Threading;

    /// <summary>
    /// This class encapsulates the asynchronous enumerator for an instance of <see cref="FeedIterator{T}"/>.
    /// </summary>
    /// <typeparam name="T">The generic type to deserialize items to.</typeparam>
    public sealed class FeedAsyncEnumerator<T> : IAsyncEnumerable<FeedResponse<T>>, IDisposable
    {
        private readonly FeedIterator<T> feedIterator;

        /// <summary>
        /// This constructor creates an instance of <see cref="FeedAsyncEnumerator{T}"/>.
        /// </summary>
        /// <param name="feedIterator">
        /// The target feed iterator instance.
        /// </param>
        public FeedAsyncEnumerator(FeedIterator<T> feedIterator)
        {
            this.feedIterator = feedIterator;
        }

        /// <summary>
        /// This method is used to get the asynchronous enumerator for an instance of <see cref="FeedIterator{T}"/>.
        /// </summary>
        /// <param name="cancellationToken">
        /// (optional) The cancellation token that could be used to cancel the operation.
        /// </param>
        /// <returns>
        /// The asynchronous enumerator for an instance of <see cref="FeedIterator{T}"/>.
        /// </returns>
        public IAsyncEnumerator<FeedResponse<T>> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return this.feedIterator.BuildFeedAsyncEnumerator<T>(cancellationToken);
        }

        /// <summary>
        /// Releases the unmanaged resources used by the <see cref="FeedIterator{T}"/> and optionally releases the managed resources.
        /// </summary>
        public void Dispose()
        {
            this.feedIterator.Dispose();
        }
    }
}