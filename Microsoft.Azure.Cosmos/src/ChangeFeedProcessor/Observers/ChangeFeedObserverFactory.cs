//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    /// <summary>
    /// Factory class used to create instance(s) of <see cref="ChangeFeedObserver"/>.
    /// </summary>
    internal abstract class ChangeFeedObserverFactory
    {
        /// <summary>
        /// Creates an instance of a <see cref="ChangeFeedObserver"/>.
        /// </summary>
        /// <returns>An instance of a <see cref="ChangeFeedObserver"/>.</returns>
        public abstract ChangeFeedObserver CreateObserver();
    }
}