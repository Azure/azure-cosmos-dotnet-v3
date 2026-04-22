//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement
{
    using System;
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

            // Serialize to a temporary stream first to avoid data loss if serialization fails
            byte[] serializedBytes;
            using (MemoryStream temp = new MemoryStream())
            {
                using (StreamWriter writer = new StreamWriter(temp, encoding: System.Text.Encoding.UTF8, bufferSize: 1024, leaveOpen: true))
                using (JsonTextWriter jsonWriter = new JsonTextWriter(writer))
                {
                    JsonSerializer serializer = JsonSerializer.Create();
                    serializer.Serialize(jsonWriter, this.container.Values.ToList());
                }

                serializedBytes = temp.ToArray();
            }

            // Write serialized state to the user's stream. Write first, then trim
            // excess via SetLength so that a failed Write leaves prior data intact
            // rather than an empty stream.
            try
            {
                this.leaseStateStream.Position = 0;
                this.leaseStateStream.Write(serializedBytes, 0, serializedBytes.Length);
                this.leaseStateStream.SetLength(serializedBytes.Length);
                this.leaseStateStream.Position = 0;
            }
            catch (NotSupportedException ex)
            {
                throw new InvalidOperationException(
                    "Failed to persist lease state because the MemoryStream is not expandable. "
                    + "Use 'new MemoryStream()' instead of 'new MemoryStream(byte[])' to create a resizable stream.",
                    ex);
            }

            return Task.CompletedTask;
        }
    }
}
