//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  Licensed under the MIT license.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor.PartitionManagement
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