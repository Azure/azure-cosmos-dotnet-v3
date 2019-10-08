//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing.Streams
{
    using static Microsoft.Azure.Cosmos.Container;

    internal sealed class ChangeFeedObserverFactoryCore : ChangeFeedObserverFactory
    {
        private readonly ChangesStreamHandler onChanges;

        public ChangeFeedObserverFactoryCore(ChangesStreamHandler onChanges)
        {
            this.onChanges = onChanges;
        }

        public override ChangeFeedObserver CreateObserver()
        {
            return new ChangeFeedObserverBase(this.onChanges);
        }
    }
}