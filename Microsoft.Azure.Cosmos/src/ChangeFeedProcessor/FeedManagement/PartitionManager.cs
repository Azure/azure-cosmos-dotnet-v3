//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement
{
    using System.Threading.Tasks;

    internal abstract class PartitionManager
    {
        public abstract Task StartAsync();

        public abstract Task StopAsync();
    }
}
