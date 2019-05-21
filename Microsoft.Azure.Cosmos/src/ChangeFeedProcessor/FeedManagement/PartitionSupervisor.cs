//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    internal abstract class PartitionSupervisor : IDisposable
    {
        public abstract Task RunAsync(CancellationToken shutdownToken);

        public abstract void Dispose();
    }
}