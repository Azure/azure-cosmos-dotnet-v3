//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Newtonsoft.Json;

    /// <summary>
    /// Lease manager that is using In-Memory as lease storage.
    /// </summary>
    internal sealed class DocumentServiceLeaseStoreManagerInMemory : DocumentServiceLeaseStoreManager
    {
        private readonly DocumentServiceLeaseStore leaseStore;
        private readonly DocumentServiceLeaseManager leaseManager;
        private readonly DocumentServiceLeaseCheckpointer leaseCheckpointer;
        private readonly DocumentServiceLeaseContainerInMemory leaseContainer;

        public DocumentServiceLeaseStoreManagerInMemory()
            : this(new ConcurrentDictionary<string, DocumentServiceLease>())
        {
        }

        /// <summary>
        /// Initializes a new instance from a <see cref="MemoryStream"/> containing
        /// previously persisted lease state. Deserialization is co-located here so
        /// that the manager owns the lease JSON format for both read (restore) and
        /// write (ShutdownAsync → persist).
        /// </summary>
        internal DocumentServiceLeaseStoreManagerInMemory(MemoryStream leaseStateStream)
            : this(DocumentServiceLeaseStoreManagerInMemory.DeserializeLeaseState(leaseStateStream), leaseStateStream)
        {
        }

        internal DocumentServiceLeaseStoreManagerInMemory(ConcurrentDictionary<string, DocumentServiceLease> container)
            : this(new DocumentServiceLeaseUpdaterInMemory(container), container, leaseStateStream: null)
        {
        }

        internal DocumentServiceLeaseStoreManagerInMemory(
            ConcurrentDictionary<string, DocumentServiceLease> container,
            MemoryStream leaseStateStream)
            : this(new DocumentServiceLeaseUpdaterInMemory(container), container, leaseStateStream)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentServiceLeaseStoreManagerInMemory"/> class.
        /// </summary>
        /// <remarks>
        /// Internal only for testing purposes, otherwise would be private.
        /// </remarks>
        internal DocumentServiceLeaseStoreManagerInMemory(
            DocumentServiceLeaseUpdater leaseUpdater,
            ConcurrentDictionary<string, DocumentServiceLease> container,
            MemoryStream leaseStateStream = null)
        {
            if (leaseUpdater == null) throw new ArgumentException(nameof(leaseUpdater));

            this.leaseStore = new DocumentServiceLeaseStoreInMemory();

            this.leaseManager = new DocumentServiceLeaseManagerInMemory(leaseUpdater, container);

            this.leaseCheckpointer = new DocumentServiceLeaseCheckpointerCore(
                leaseUpdater,
                new PartitionedByIdCollectionRequestOptionsFactory());

            this.leaseContainer = new DocumentServiceLeaseContainerInMemory(container, leaseStateStream);
        }

        public override DocumentServiceLeaseStore LeaseStore => this.leaseStore;

        public override DocumentServiceLeaseManager LeaseManager => this.leaseManager;

        public override DocumentServiceLeaseCheckpointer LeaseCheckpointer => this.leaseCheckpointer;

        public override DocumentServiceLeaseContainer LeaseContainer => this.leaseContainer;

        public override Task ShutdownAsync()
        {
            return this.leaseContainer.ShutdownAsync();
        }

        /// <summary>
        /// Deserializes lease state from a <see cref="MemoryStream"/> into a dictionary.
        /// This is the counterpart of the serialization in
        /// <see cref="DocumentServiceLeaseContainerInMemory.ShutdownAsync"/>.
        /// </summary>
        private static ConcurrentDictionary<string, DocumentServiceLease> DeserializeLeaseState(
            MemoryStream leaseStateStream)
        {
            ConcurrentDictionary<string, DocumentServiceLease> container =
                new ConcurrentDictionary<string, DocumentServiceLease>();

            if (leaseStateStream == null || leaseStateStream.Length == 0)
            {
                return container;
            }

            leaseStateStream.Position = 0;

            List<DocumentServiceLease> leases;
            try
            {
                using (StreamReader sr = new StreamReader(
                    leaseStateStream,
                    encoding: System.Text.Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: true,
                    bufferSize: 1024,
                    leaveOpen: true))
                using (JsonTextReader jsonReader = new JsonTextReader(sr))
                {
                    JsonSerializer serializer = JsonSerializer.Create();
                    leases = serializer.Deserialize<List<DocumentServiceLease>>(jsonReader);
                }
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    "Failed to deserialize lease state from the provided MemoryStream. "
                    + "Ensure the stream contains valid lease state JSON previously persisted by the ChangeFeedProcessor.",
                    ex);
            }

            if (leases != null)
            {
                foreach (DocumentServiceLease lease in leases)
                {
                    if (string.IsNullOrEmpty(lease?.Id))
                    {
                        throw new InvalidOperationException("Lease state contains a null or invalid lease entry.");
                    }

                    container[lease.Id] = lease;
                }
            }

            return container;
        }
    }
}