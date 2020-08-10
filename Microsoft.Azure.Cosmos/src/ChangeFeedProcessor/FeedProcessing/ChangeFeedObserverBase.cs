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

        private readonly ChangesHandlerWithContext<T> onChangesWithContext;

        public ChangeFeedObserverBase(ChangesHandler<T> onChanges)
        {
            this.onChanges = onChanges;
        }

        public ChangeFeedObserverBase(ChangesHandlerWithContext<T> onChanges)
        {
            this.onChangesWithContext = onChanges;
        }

        public override Task ProcessChangesAsync(
            ChangeFeedProcessorContext context,
            IReadOnlyCollection<T> docs,
            CancellationToken cancellationToken)
        {
            if (this.onChangesWithContext != null)
            {
                return this.onChangesWithContext(context, docs, cancellationToken);
            }

            return this.onChanges(docs, cancellationToken);
        }
    }
}
