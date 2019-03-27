//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  Licensed under the MIT license.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor.PartitionManagement
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.LeaseManagement;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.Logging;

    internal sealed class PartitionLoadBalancerCore : PartitionLoadBalancer
    {
        private static readonly ILog Logger = LogProvider.GetCurrentClassLogger();
        private readonly PartitionController partitionController;
        private readonly DocumentServiceLeaseContainer leaseContainer;
        private readonly PartitionLoadBalancingStrategy partitionLoadBalancingStrategy;
        private readonly TimeSpan leaseAcquireInterval;
        private readonly CancellationTokenSource cancellationTokenSource;
        private Task runTask;

        public PartitionLoadBalancerCore(
            PartitionController partitionController,
            DocumentServiceLeaseContainer leaseContainer,
            PartitionLoadBalancingStrategy partitionLoadBalancingStrategy,
            TimeSpan leaseAcquireInterval)
        {
            if (partitionController == null) throw new ArgumentNullException(nameof(partitionController));
            if (leaseContainer == null) throw new ArgumentNullException(nameof(leaseContainer));
            if (partitionLoadBalancingStrategy == null) throw new ArgumentNullException(nameof(partitionLoadBalancingStrategy));

            this.partitionController = partitionController;
            this.leaseContainer = leaseContainer;
            this.partitionLoadBalancingStrategy = partitionLoadBalancingStrategy;
            this.leaseAcquireInterval = leaseAcquireInterval;
            this.cancellationTokenSource = new CancellationTokenSource();
        }

        public override void Start()
        {
            if (this.runTask != null)
            {
                throw new InvalidOperationException("Already started");
            }

            this.runTask = this.RunAsync();
        }

        public override async Task StopAsync()
        {
            if (this.runTask == null)
            {
                throw new InvalidOperationException("Start has to be called before stop");
            }

            this.cancellationTokenSource.Cancel();
            await this.runTask.ConfigureAwait(false);
        }

        private async Task RunAsync()
        {
            try
            {
                while (true)
                {
                    try
                    {
                        IEnumerable<DocumentServiceLease> allLeases = await this.leaseContainer.GetAllLeasesAsync().ConfigureAwait(false);
                        IEnumerable<DocumentServiceLease> leasesToTake = this.partitionLoadBalancingStrategy.SelectLeasesToTake(allLeases);

                        foreach (DocumentServiceLease lease in leasesToTake)
                        {
                            try
                            {
                                await this.partitionController.AddOrUpdateLeaseAsync(lease).ConfigureAwait(false);
                            }
                            catch (Exception e)
                            {
                                Logger.ErrorException("Partition load balancer lease add/update iteration failed", e);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.ErrorException("Partition load balancer iteration failed", e);
                    }

                    await Task.Delay(this.leaseAcquireInterval, this.cancellationTokenSource.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Info("Partition load balancer task stopped.");
            }
        }
    }
}