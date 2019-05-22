//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    internal sealed class DocumentServiceLeaseContainerInMemory : DocumentServiceLeaseContainer
    {
        private readonly ConcurrentDictionary<string, DocumentServiceLease> container;

        public DocumentServiceLeaseContainerInMemory(ConcurrentDictionary<string, DocumentServiceLease> container)
        {
            this.container = container;
        }

        public override Task<IReadOnlyList<DocumentServiceLease>> GetAllLeasesAsync()
        {
            return Task.FromResult<IReadOnlyList<DocumentServiceLease>>(this.container.Values.ToList().AsReadOnly());
        }

        public override Task<IEnumerable<DocumentServiceLease>> GetOwnedLeasesAsync()
        {
            return Task.FromResult<IEnumerable<DocumentServiceLease>>(this.container.Values.AsEnumerable());
        }
    }
}
