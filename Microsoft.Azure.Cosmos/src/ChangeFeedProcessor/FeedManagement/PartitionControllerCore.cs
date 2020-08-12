//------------------------------------------------------------
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
        private CancellationTokenSource shutdownCts;

        public PartitionControllerCore(
            DocumentServiceLeaseContainer leaseContainer,
            DocumentServiceLeaseManager leaseManager,
            PartitionSupervisorFactory partitionSupervisorFactory,
            PartitionSynchronizer synchronizer)
        {
            this.leaseContainer = leaseContainer;
            this.leaseManager = leaseManager;
            this.partitionSupervisorFactory = partitionSupervisorFactory;
            this.synchronizer = synchronizer;
        }

        public override async Task InitializeAsync()
        {
            this.shutdownCts = new CancellationTokenSource();
            await this.LoadLeasesAsync().ConfigureAwait(false);
        }

        public override async Task AddOrUpdateLeaseAsync(DocumentServiceLease lease)
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

            if (!this.currentlyOwnedPartitions.TryAdd(lease.CurrentLeaseToken, tcs))
            {
                await this.leaseManager.UpdatePropertiesAsync(lease).ConfigureAwait(false);
                DefaultTrace.TraceVerbose("Lease with token {0}: updated", lease.CurrentLeaseToken);
                return;
            }

            try
            {
                DocumentServiceLease updatedLease = await this.leaseManager.AcquireAsync(lease).ConfigureAwait(false);
                if (updatedLease != null) lease = updatedLease;
                DefaultTrace.TraceInformation("Lease with token {0}: acquired", lease.CurrentLeaseToken);
            }
            catch (Exception)
            {
                await this.RemoveLeaseAsync(lease).ConfigureAwait(false);
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

        private async Task RemoveLeaseAsync(DocumentServiceLease lease)
        {
            TaskCompletionSource<bool> worker;
            if (!this.currentlyOwnedPartitions.TryRemove(lease.CurrentLeaseToken, out worker))
            {
                return;
            }

            DefaultTrace.TraceInformation("Lease with token {0}: released", lease.CurrentLeaseToken);

            try
            {
                await this.leaseManager.ReleaseAsync(lease).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Extensions.TraceException(e);
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
            catch (FeedSplitException ex)
            {
                await this.HandleSplitAsync(lease, ex.LastContinuation).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                DefaultTrace.TraceVerbose("Lease with token {0}: processing canceled", lease.CurrentLeaseToken);
            }
            catch (Exception e)
            {
                Extensions.TraceException(e);
                DefaultTrace.TraceWarning("Lease with token {0}: processing failed", lease.CurrentLeaseToken);
            }

            await this.RemoveLeaseAsync(lease).ConfigureAwait(false);
        }

        private async Task HandleSplitAsync(DocumentServiceLease lease, string lastContinuationToken)
        {
            try
            {
                lease.ContinuationToken = lastContinuationToken;
                IEnumerable<DocumentServiceLease> addedLeases = await this.synchronizer.SplitPartitionAsync(lease).ConfigureAwait(false);
                Task[] addLeaseTasks = addedLeases.Select(l =>
                    {
                        l.Properties = lease.Properties;
                        return this.AddOrUpdateLeaseAsync(l);
                    }).ToArray();

                await this.leaseManager.DeleteAsync(lease).ConfigureAwait(false);
                await Task.WhenAll(addLeaseTasks).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Extensions.TraceException(e);
                DefaultTrace.TraceWarning("Lease with token {0}: failed to split", e, lease.CurrentLeaseToken);
            }
        }
    }
}