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
        /// Persists the current in-memory lease state into the user-supplied <see cref="MemoryStream"/>.
        /// </summary>
        /// <remarks>
        /// Must only be invoked from the single <c>ChangeFeedProcessor.StopAsync</c> call path;
        /// concurrent invocation is not supported and may corrupt the stream.
        /// </remarks>
        /// <returns>A completed task once the stream has been populated, or a no-op if no stream was supplied.</returns>
        public Task ShutdownAsync()
        {
            if (this.leaseStateStream == null)
            {
                return Task.CompletedTask;
            }

            byte[] serializedBytes = InMemoryLeaseJsonFormat.Serialize(this.container.Values.ToList());

            // Resize the target stream BEFORE writing. If the stream is not expandable and
            // cannot hold the new payload, SetLength throws NotSupportedException and the
            // user's stream is left untouched (no partial-write corruption). If SetLength
            // succeeds, the subsequent Write is guaranteed to fit.
            try
            {
                this.leaseStateStream.SetLength(serializedBytes.Length);
            }
            catch (NotSupportedException ex)
            {
                throw new InvalidOperationException(
                    "Failed to persist lease state because the MemoryStream is not expandable and the serialized "
                    + "state exceeds its capacity. Use 'new MemoryStream()' or a MemoryStream with sufficient "
                    + "capacity instead of 'new MemoryStream(byte[])' to create a resizable stream.",
                    ex);
            }

            this.leaseStateStream.Position = 0;
            this.leaseStateStream.Write(serializedBytes, 0, serializedBytes.Length);
            this.leaseStateStream.Position = 0;

            return Task.CompletedTask;
        }
    }
}
