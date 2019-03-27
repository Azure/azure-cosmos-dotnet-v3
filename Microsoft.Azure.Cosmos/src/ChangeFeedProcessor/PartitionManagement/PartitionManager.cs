//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  Licensed under the MIT license.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor.PartitionManagement
{
    using System.Threading.Tasks;

    internal abstract class PartitionManager
    {
        public abstract Task StartAsync();

        public abstract Task StopAsync();
    }
}
