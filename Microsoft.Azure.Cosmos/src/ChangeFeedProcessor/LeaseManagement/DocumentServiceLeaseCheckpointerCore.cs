//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Exceptions;
    using Microsoft.Azure.Cosmos.Core.Trace;

    internal sealed class DocumentServiceLeaseCheckpointerCore : DocumentServiceLeaseCheckpointer
    {
        private readonly DocumentServiceLeaseUpdater leaseUpdater;
        private readonly RequestOptionsFactory requestOptionsFactory;

        public DocumentServiceLeaseCheckpointerCore(
            DocumentServiceLeaseUpdater leaseUpdater,
            RequestOptionsFactory requestOptionsFactory)
        {
            this.leaseUpdater = leaseUpdater;
            this.requestOptionsFactory = requestOptionsFactory;
        }

        public override Task<DocumentServiceLease> CheckpointAsync(DocumentServiceLease lease, string continuationToken)
        {
            if (lease == null)
            {
                throw new ArgumentNullException(nameof(lease));
            }

            if (string.IsNullOrEmpty(continuationToken))
            {
                throw new ArgumentException("continuationToken must be a non-empty string", nameof(continuationToken));
            }

            return this.leaseUpdater.UpdateLeaseAsync(
                lease,
                lease.Id,
                this.requestOptionsFactory.GetPartitionKey(lease.Id),
                serverLease =>
                {
                    if (serverLease.Owner != lease.Owner)
                    {
                        DefaultTrace.TraceInformation("{0} lease token was taken over by owner '{1}'", lease.CurrentLeaseToken, serverLease.Owner);
                        throw new LeaseLostException(lease);
                    }
                    serverLease.ContinuationToken = continuationToken;
                    return serverLease;
                });
        }
    }
}
