﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Exceptions;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.Azure.Cosmos.ChangeFeed.Utils;
    using Microsoft.Azure.Cosmos.Core.Trace;

    internal sealed class PartitionControllerCore : PartitionController
    {
        private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> currentlyOwnedPartitions = new ConcurrentDictionary<string, TaskCompletionSource<bool>>();

        private readonly DocumentServiceLeaseContainer leaseContainer;
        private readonly DocumentServiceLeaseManager leaseManager;
        private readonly PartitionSupervisorFactory partitionSupervisorFactory;
        private readonly PartitionSynchronizer synchronizer;
        private readonly ChangeFeedProcessorHealthMonitor monitor;
        private CancellationTokenSource shutdownCts;

        public PartitionControllerCore(
            DocumentServiceLeaseContainer leaseContainer,
            DocumentServiceLeaseManager leaseManager,
            PartitionSupervisorFactory partitionSupervisorFactory,
            PartitionSynchronizer synchronizer,
            ChangeFeedProcessorHealthMonitor monitor)
        {
            this.leaseContainer = leaseContainer;
            this.leaseManager = leaseManager;
            this.partitionSupervisorFactory = partitionSupervisorFactory;
            this.synchronizer = synchronizer;
            this.monitor = monitor;
        }

        public override async Task InitializeAsync()
        {
            this.shutdownCts = new CancellationTokenSource();
            await this.LoadLeasesAsync().ConfigureAwait(false);
        }

        public override async Task AddOrUpdateLeaseAsync(DocumentServiceLease lease)
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            if (!this.currentlyOwnedPartitions.TryAdd(lease.CurrentLeaseToken, tcs))
            {
                await this.leaseManager.UpdatePropertiesAsync(lease).ConfigureAwait(false);
                DefaultTrace.TraceVerbose("Lease with token {0}: updated", lease.CurrentLeaseToken);
                return;
            }

            try
            {
                DocumentServiceLease updatedLease = await this.leaseManager.AcquireAsync(lease).ConfigureAwait(false);
                if (updatedLease != null)
                {
                    lease = updatedLease;
                }

                await this.monitor.NotifyLeaseAcquireAsync(lease.CurrentLeaseToken);
            }
            catch (Exception ex)
            {
                await this.RemoveLeaseAsync(lease: lease, wasAcquired: false).ConfigureAwait(false);
                switch (ex)
                {
                    case LeaseLostException leaseLostException:
                        // LeaseLostException by itself is not loggable, unless it contains a related inner exception
                        // For cases when the lease or container has been deleted or the lease has been stolen
                        if (leaseLostException.InnerException != null)
                        {
                            await this.monitor.NotifyErrorAsync(lease.CurrentLeaseToken, leaseLostException.InnerException);
                        }
                        break;

                    default:
                        await this.monitor.NotifyErrorAsync(lease.CurrentLeaseToken, ex);
                        break;
                }

                throw;
            }

            PartitionSupervisor supervisor = this.partitionSupervisorFactory.Create(lease);
            this.ProcessPartitionAsync(supervisor, lease).LogException();
        }

        public override async Task ShutdownAsync()
        {
            this.shutdownCts.Cancel();
            IEnumerable<Task> leases = this.currentlyOwnedPartitions.Select(pair => pair.Value.Task).ToList();
            await Task.WhenAll(leases).ConfigureAwait(false);
        }

        private async Task LoadLeasesAsync()
        {
            DefaultTrace.TraceVerbose("Starting renew leases assigned to this host on initialize.");
            List<Task> addLeaseTasks = new List<Task>();
            foreach (DocumentServiceLease lease in await this.leaseContainer.GetOwnedLeasesAsync().ConfigureAwait(false))
            {
                DefaultTrace.TraceInformation("Acquired lease with token '{0}' on startup.", lease.CurrentLeaseToken);
                addLeaseTasks.Add(this.AddOrUpdateLeaseAsync(lease));
            }

            await Task.WhenAll(addLeaseTasks.ToArray()).ConfigureAwait(false);
        }

        private async Task RemoveLeaseAsync(DocumentServiceLease lease, bool wasAcquired)
        {
            if (!this.currentlyOwnedPartitions.TryRemove(lease.CurrentLeaseToken, out TaskCompletionSource<bool> worker))
            {
                return;
            }

            try
            {
                await this.leaseManager.ReleaseAsync(lease).ConfigureAwait(false);

                await this.monitor.NotifyLeaseReleaseAsync(lease.CurrentLeaseToken);
            }
            catch (LeaseLostException)
            {
                if (wasAcquired)
                {
                    await this.monitor.NotifyLeaseReleaseAsync(lease.CurrentLeaseToken);
                }

                DefaultTrace.TraceVerbose("Lease with token {0}: taken by another host during release", lease.CurrentLeaseToken);
            }
            catch (Exception ex)
            {
                await this.monitor.NotifyErrorAsync(lease.CurrentLeaseToken, ex);
                DefaultTrace.TraceWarning("Lease with token {0}: failed to remove lease", lease.CurrentLeaseToken);
            }
            finally
            {
                worker.SetResult(false);
            }
        }

        private async Task ProcessPartitionAsync(PartitionSupervisor partitionSupervisor, DocumentServiceLease lease)
        {
            try
            {
                await partitionSupervisor.RunAsync(this.shutdownCts.Token).ConfigureAwait(false);
            }
            catch (FeedRangeGoneException ex)
            {
                await this.HandlePartitionGoneAsync(lease, ex.LastContinuation).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (this.shutdownCts.IsCancellationRequested)
            {
                DefaultTrace.TraceVerbose("Lease with token {0}: processing canceled", lease.CurrentLeaseToken);
            }
            catch (Exception ex)
            {
                await this.monitor.NotifyErrorAsync(lease.CurrentLeaseToken, ex);
                DefaultTrace.TraceWarning("Lease with token {0}: processing failed", lease.CurrentLeaseToken);
            }

            await this.RemoveLeaseAsync(lease: lease, wasAcquired: true).ConfigureAwait(false);
        }

        private async Task HandlePartitionGoneAsync(DocumentServiceLease lease, string lastContinuationToken)
        {
            try
            {
                lease.ContinuationToken = lastContinuationToken;
                (IEnumerable<DocumentServiceLease> addedLeases, bool shouldDeleteGoneLease) = await this.synchronizer.HandlePartitionGoneAsync(lease).ConfigureAwait(false);
                Task[] addLeaseTasks = addedLeases.Select(l =>
                    {
                        l.Properties = lease.Properties;
                        return this.AddOrUpdateLeaseAsync(l);
                    }).ToArray();

                if (shouldDeleteGoneLease)
                {
                    await this.leaseManager.DeleteAsync(lease).ConfigureAwait(false);
                }

                await Task.WhenAll(addLeaseTasks).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await this.monitor.NotifyErrorAsync(lease.CurrentLeaseToken, ex);
                DefaultTrace.TraceWarning("Lease with token {0}: failed to handle gone", ex, lease.CurrentLeaseToken);
            }
        }
    }
}