//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  Licensed under the MIT license.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor.PartitionManagement
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.LeaseManagement;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.Logging;

    internal sealed class PartitionCheckpointerCore : PartitionCheckpointer
    {
        private static readonly ILog Logger = LogProvider.GetCurrentClassLogger();
        private readonly DocumentServiceLeaseCheckpointer leaseCheckpointer;
        private DocumentServiceLease lease;

        public PartitionCheckpointerCore(DocumentServiceLeaseCheckpointer leaseCheckpointer, DocumentServiceLease lease)
        {
            this.leaseCheckpointer = leaseCheckpointer;
            this.lease = lease;
        }

        public override async Task CheckpointPartitionAsync(string сontinuationToken)
        {
            this.lease = await this.leaseCheckpointer.CheckpointAsync(this.lease, сontinuationToken).ConfigureAwait(false);
            Logger.InfoFormat("Checkpoint: lease token {0}, new continuation {1}", this.lease.CurrentLeaseToken, this.lease.ContinuationToken);
        }
    }
}