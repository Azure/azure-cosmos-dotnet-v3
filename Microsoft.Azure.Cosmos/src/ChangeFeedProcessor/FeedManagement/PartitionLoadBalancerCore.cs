//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.Azure.Cosmos.Core.Trace;

    internal sealed class PartitionLoadBalancerCore : PartitionLoadBalancer
    {
        private readonly PartitionController partitionController;
        private readonly DocumentServiceLeaseContainer leaseContainer;
        private readonly LoadBalancingStrategy partitionLoadBalancingStrategy;
        private readonly TimeSpan leaseAcquireInterval;
        private CancellationTokenSource cancellationTokenSource;
        private Task runTask;

        public PartitionLoadBalancerCore(
            PartitionController partitionController,
            DocumentServiceLeaseContainer leaseContainer,
            LoadBalancingStrategy partitionLoadBalancingStrategy,
            TimeSpan leaseAcquireInterval)
        {
            if (partitionController == null)
            {
                throw new ArgumentNullException(nameof(partitionController));
            }

            if (leaseContainer == null)
            {
                throw new ArgumentNullException(nameof(leaseContainer));
            }

            if (partitionLoadBalancingStrategy == null)
            {
                throw new ArgumentNullException(nameof(partitionLoadBalancingStrategy));
            }

            this.partitionController = partitionController;
            this.leaseContainer = leaseContainer;
            this.partitionLoadBalancingStrategy = partitionLoadBalancingStrategy;
            this.leaseAcquireInterval = leaseAcquireInterval;
        }

        public override void Start()
        {
            if (this.runTask != null && !this.runTask.IsCompleted)
            {
                throw new InvalidOperationException("Already started");
            }

            this.cancellationTokenSource = new CancellationTokenSource();
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
                                Extensions.TraceException(e);
                                DefaultTrace.TraceError("Partition load balancer lease add/update iteration failed");
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Extensions.TraceException(e);
                        DefaultTrace.TraceError("Partition load balancer iteration failed");
                    }

                    await Task.Delay(this.leaseAcquireInterval, this.cancellationTokenSource.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                DefaultTrace.TraceInformation("Partition load balancer task stopped.");
            }
        }
    }
}