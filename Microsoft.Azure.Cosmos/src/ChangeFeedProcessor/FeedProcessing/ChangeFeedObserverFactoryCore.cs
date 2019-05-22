//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class ChangeFeedObserverFactoryCore<T> : ChangeFeedObserverFactory<T>
    {
        private readonly Func<IReadOnlyCollection<T>, CancellationToken, Task> onChanges;

        public ChangeFeedObserverFactoryCore(Func<IReadOnlyCollection<T>, CancellationToken, Task> onChanges)
        {
            this.onChanges = onChanges;
        }

        public override ChangeFeedObserver<T> CreateObserver()
        {
            return new ChangeFeedObserverBase<T>(this.onChanges);
        }
    }
}