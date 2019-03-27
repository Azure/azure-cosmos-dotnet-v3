//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  Licensed under the MIT license.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor.FeedProcessing
{
    /// <summary>
    /// Factory class used to create instance(s) of <see cref="ChangeFeedObserver{T}"/>.
    /// </summary>

    public abstract class ChangeFeedObserverFactory<T>
    {
        /// <summary>
        /// Creates an instance of a <see cref="ChangeFeedObserver{T}"/>.
        /// </summary>
        /// <returns>An instance of a <see cref="ChangeFeedObserver{T}"/>.</returns>
        public abstract ChangeFeedObserver<T> CreateObserver();
    }
}