//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Newtonsoft.Json;

    internal sealed class DocumentServiceLeaseContainerInMemory : DocumentServiceLeaseContainer
    {
        private readonly ConcurrentDictionary<string, DocumentServiceLease> container;
        private readonly MemoryStream leaseStateStream;

        public DocumentServiceLeaseContainerInMemory(ConcurrentDictionary<string, DocumentServiceLease> container)
            : this(container, leaseStateStream: null)
        {
        }

        public DocumentServiceLeaseContainerInMemory(
            ConcurrentDictionary<string, DocumentServiceLease> container,
            MemoryStream leaseStateStream)
        {
            this.container = container;
            this.leaseStateStream = leaseStateStream;
        }

        public override Task<IReadOnlyList<DocumentServiceLease>> GetAllLeasesAsync()
        {
            return Task.FromResult<IReadOnlyList<DocumentServiceLease>>(this.container.Values.ToList().AsReadOnly());
        }

        public override Task<IEnumerable<DocumentServiceLease>> GetOwnedLeasesAsync()
        {
            return Task.FromResult<IEnumerable<DocumentServiceLease>>(this.container.Values.AsEnumerable());
        }

        /// <summary>
        /// Initiates shutdown of in-memory lease container. 
        /// Exports all processor leases from current in-memory lease container.
        /// </summary>
        /// <returns>A task that represents the asynchronous shutdown operation.</returns>
        public override Task ShutdownAsync()
        {
            if (this.leaseStateStream == null)
            {
                return Task.CompletedTask;
            }

            List<DocumentServiceLease> epkLeases = new List<DocumentServiceLease>();

            foreach (DocumentServiceLease lease in this.container.Values)
            {
                if (!(lease.FeedRange is FeedRangeEpk))
                {
                    continue;
                }

                epkLeases.Add(lease);
            }

            this.leaseStateStream.SetLength(0);

            using (StreamWriter writer = new StreamWriter(this.leaseStateStream, encoding: System.Text.Encoding.UTF8, bufferSize: 1024, leaveOpen: true))
            using (JsonTextWriter jsonWriter = new JsonTextWriter(writer))
            {
                JsonSerializer serializer = JsonSerializer.Create();
                serializer.Serialize(jsonWriter, epkLeases);
            }

            this.leaseStateStream.Position = 0;

            return Task.CompletedTask;
        }
    }
}
