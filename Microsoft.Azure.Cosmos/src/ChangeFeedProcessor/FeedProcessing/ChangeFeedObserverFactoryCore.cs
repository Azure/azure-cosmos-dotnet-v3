//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing
{
    using static Microsoft.Azure.Cosmos.Container;

    internal sealed class ChangeFeedObserverFactoryCore<T> : ChangeFeedObserverFactory<T>
    {
        private readonly ChangesHandler<T> onChanges;

        public ChangeFeedObserverFactoryCore(ChangesHandler<T> onChanges)
        {
            this.onChanges = onChanges;
        }

        public override ChangeFeedObserver<T> CreateObserver()
        {
            return new ChangeFeedObserverBase<T>(this.onChanges);
        }
    }
}