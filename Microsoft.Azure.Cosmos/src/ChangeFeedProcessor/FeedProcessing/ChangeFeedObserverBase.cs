//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  Licensed under the MIT license.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor.FeedProcessing
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class ChangeFeedObserverBase<T> : ChangeFeedObserver<T>
    {
        private readonly Func<IReadOnlyList<T>, CancellationToken, Task> onChanges;

        public ChangeFeedObserverBase(Func<IReadOnlyList<T>, CancellationToken, Task> onChanges)
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

        public override Task ProcessChangesAsync(ChangeFeedObserverContext context, IReadOnlyList<T> docs, CancellationToken cancellationToken)
        {
            return this.onChanges(docs, cancellationToken);
        }
    }
}
