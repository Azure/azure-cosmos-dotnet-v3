//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if AZURECORE
namespace Azure.Cosmos.ChangeFeed
#else
namespace Microsoft.Azure.Cosmos.ChangeFeed
#endif
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