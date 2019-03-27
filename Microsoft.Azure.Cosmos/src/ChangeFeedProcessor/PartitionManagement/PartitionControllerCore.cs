//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  Licensed under the MIT license.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor.PartitionManagement
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.Exceptions;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.LeaseManagement;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.Logging;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.Utils;

    internal sealed class PartitionControllerCore : PartitionController
    {
        private static readonly ILog Logger = LogProvider.GetCurrentClassLogger();

        private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> currentlyOwnedPartitions = new ConcurrentDictionary<string, TaskCompletionSource<bool>>();

        private readonly DocumentServiceLeaseContainer leaseContainer;
        private readonly DocumentServiceLeaseManager leaseManager;
        private readonly PartitionSupervisorFactory partitionSupervisorFactory;
        private readonly PartitionSynchronizer synchronizer;
        private readonly CancellationTokenSource shutdownCts = new CancellationTokenSource();

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
            await this.LoadLeasesAsync().ConfigureAwait(false);
        }

        public override async Task AddOrUpdateLeaseAsync(DocumentServiceLease lease)
        {
            var tcs = new TaskCompletionSource<bool>();

            if (!this.currentlyOwnedPartitions.TryAdd(lease.CurrentLeaseToken, tcs))
            {
                await this.leaseManager.UpdatePropertiesAsync(lease).ConfigureAwait(false);
                Logger.DebugFormat("Lease with token {0}: updated", lease.CurrentLeaseToken);
                return;
            }

            try
            {
                var updatedLease = await this.leaseManager.AcquireAsync(lease).ConfigureAwait(false);
                if (updatedLease != null) lease = updatedLease;
                Logger.InfoFormat("Lease with token {0}: acquired", lease.CurrentLeaseToken);
            }
            catch (Exception)
            {
                await this.RemoveLeaseAsync(lease).ConfigureAwait(false);
                throw;
            }

            PartitionSupervisor supervisor = this.partitionSupervisorFactory.Create(lease);
            this.ProcessPartition(supervisor, lease).LogException();
        }

        public override async Task ShutdownAsync()
        {
            this.shutdownCts.Cancel();
            IEnumerable<Task> leases = this.currentlyOwnedPartitions.Select(pair => pair.Value.Task).ToList();
            await Task.WhenAll(leases).ConfigureAwait(false);
        }

        private async Task LoadLeasesAsync()
        {
            Logger.Debug("Starting renew leases assigned to this host on initialize.");
            var addLeaseTasks = new List<Task>();
            foreach (DocumentServiceLease lease in await this.leaseContainer.GetOwnedLeasesAsync().ConfigureAwait(false))
            {
                Logger.InfoFormat("Acquired lease with token '{0}' on startup.", lease.CurrentLeaseToken);
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

            Logger.InfoFormat("Lease with token {0}: released", lease.CurrentLeaseToken);

            try
            {
                await this.leaseManager.ReleaseAsync(lease).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Logger.WarnException("Lease with token {0}: failed to remove lease", e, lease.CurrentLeaseToken);
            }
            finally
            {
                worker.SetResult(false);
            }
        }

        private async Task ProcessPartition(PartitionSupervisor partitionSupervisor, DocumentServiceLease lease)
        {
            try
            {
                await partitionSupervisor.RunAsync(this.shutdownCts.Token).ConfigureAwait(false);
            }
            catch (PartitionSplitException ex)
            {
                await this.HandleSplitAsync(lease, ex.LastContinuation).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                Logger.DebugFormat("Lease with token {0}: processing canceled", lease.CurrentLeaseToken);
            }
            catch (Exception e)
            {
                Logger.WarnException("Lease with token {0}: processing failed", e, lease.CurrentLeaseToken);
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
                Logger.WarnException("Lease with token {0}: failed to split", e, lease.CurrentLeaseToken);
            }
        }
    }
}