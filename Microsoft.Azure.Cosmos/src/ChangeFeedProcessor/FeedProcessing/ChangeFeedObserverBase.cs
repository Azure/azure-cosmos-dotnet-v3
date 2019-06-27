//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using static Microsoft.Azure.Cosmos.Container;

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
