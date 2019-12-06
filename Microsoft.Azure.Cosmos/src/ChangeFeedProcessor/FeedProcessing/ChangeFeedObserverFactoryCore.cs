//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if AZURECORE
namespace Azure.Cosmos.ChangeFeed
#else
namespace Microsoft.Azure.Cosmos.ChangeFeed
#endif
{
#if AZURECORE
    using static Azure.Cosmos.CosmosContainer;
#else
    using static Microsoft.Azure.Cosmos.Container;
#endif

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