//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if AZURECORE
namespace Azure.Cosmos.ChangeFeed
#else
namespace Microsoft.Azure.Cosmos.ChangeFeed
#endif
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
#if AZURECORE
    using static Azure.Cosmos.CosmosContainer;
#else
    using static Microsoft.Azure.Cosmos.Container;
#endif

    internal sealed class ChangeFeedObserverBase<T> : ChangeFeedObserver<T>
    {
        private readonly ChangesHandler<T> onChanges;

        public ChangeFeedObserverBase(ChangesHandler<T> onChanges)
        {
            this.onChanges = onChanges;
        }

        public override Task CloseAsync(ChangeFeedObserverContext context, ChangeFeedObserverCloseReason reason)
        {
            return Task.CompletedTask;
        }

        public override Task OpenAsync(ChangeFeedObserverContext context)
        {
            return Task.CompletedTask;
        }

        public override Task ProcessChangesAsync(ChangeFeedObserverContext context, IReadOnlyCollection<T> docs, CancellationToken cancellationToken)
        {
            return this.onChanges(docs, cancellationToken);
        }
    }
}
