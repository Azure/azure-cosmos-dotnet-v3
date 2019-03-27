//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  Licensed under the MIT license.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor.PartitionManagement
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.Bootstrapping;

    internal sealed class PartitionManagerCore : PartitionManager
    {
        private readonly Bootstrapper bootstrapper;
        private readonly PartitionController partitionController;
        private readonly PartitionLoadBalancer partitionLoadBalancer;

        public PartitionManagerCore(Bootstrapper bootstrapper, PartitionController partitionController, PartitionLoadBalancer partitionLoadBalancer)
        {
            this.bootstrapper = bootstrapper;
            this.partitionController = partitionController;
            this.partitionLoadBalancer = partitionLoadBalancer;
        }

        public override async Task StartAsync()
        {
            await this.bootstrapper.InitializeAsync().ConfigureAwait(false);
            await this.partitionController.InitializeAsync().ConfigureAwait(false);
            this.partitionLoadBalancer.Start();
        }

        public override async Task StopAsync()
        {
            await this.partitionLoadBalancer.StopAsync().ConfigureAwait(false);
            await this.partitionController.ShutdownAsync().ConfigureAwait(false);
        }
    }
}