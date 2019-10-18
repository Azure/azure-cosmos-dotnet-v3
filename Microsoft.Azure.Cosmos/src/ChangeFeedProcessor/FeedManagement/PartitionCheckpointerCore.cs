//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if AZURECORE
namespace Azure.Cosmos.ChangeFeed
#else
namespace Microsoft.Azure.Cosmos.ChangeFeed
#endif
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;

    internal sealed class PartitionCheckpointerCore : PartitionCheckpointer
    {
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
            DefaultTrace.TraceInformation("Checkpoint: lease token {0}, new continuation {1}", this.lease.CurrentLeaseToken, this.lease.ContinuationToken);
        }
    }
}