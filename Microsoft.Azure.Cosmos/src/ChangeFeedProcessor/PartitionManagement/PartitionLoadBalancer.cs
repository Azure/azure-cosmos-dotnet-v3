//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  Licensed under the MIT license.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor.PartitionManagement
{
    using System.Threading.Tasks;

    internal abstract class PartitionLoadBalancer
    {
        /// <summary>
        /// Starts the load balancer
        /// </summary>
        public abstract void Start();

        /// <summary>
        /// Stops the load balancer
        /// </summary>
        /// <returns>Task that completes once load balancer is fully stopped</returns>
        public abstract Task StopAsync();
    }
}