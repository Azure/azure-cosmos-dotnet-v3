//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing
{
    /// <summary>
    /// Factory class used to create instance(s) of <see cref="ChangeFeedObserver{T}"/>.
    /// </summary>
    internal abstract class ChangeFeedObserverFactory<T>
    {
        /// <summary>
        /// Creates an instance of a <see cref="ChangeFeedObserver{T}"/>.
        /// </summary>
        /// <returns>An instance of a <see cref="ChangeFeedObserver{T}"/>.</returns>
        public abstract ChangeFeedObserver<T> CreateObserver();
    }
}