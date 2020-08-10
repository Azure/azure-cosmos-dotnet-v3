//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing
{
    using static Microsoft.Azure.Cosmos.Container;

    internal sealed class ChangeFeedObserverFactoryCore<T> : ChangeFeedObserverFactory<T>
    {
        private readonly ChangesHandler<T> onChanges;
        private readonly ChangesHandlerWithContext<T> onChangesWithContext;

        public ChangeFeedObserverFactoryCore(ChangesHandler<T> onChanges)
        {
            this.onChanges = onChanges;
        }

        public ChangeFeedObserverFactoryCore(ChangesHandlerWithContext<T> onChangesWithContext)
        {
            this.onChangesWithContext = onChangesWithContext;
        }

        public override ChangeFeedObserver<T> CreateObserver()
        {
            if (this.onChangesWithContext != null)
            {
                return new ChangeFeedObserverBase<T>(this.onChangesWithContext);
            }

            return new ChangeFeedObserverBase<T>(this.onChanges);
        }
    }
}