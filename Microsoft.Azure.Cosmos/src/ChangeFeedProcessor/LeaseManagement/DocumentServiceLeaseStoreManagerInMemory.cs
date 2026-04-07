//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;

    /// <summary>
    /// Lease manager that is using In-Memory as lease storage.
    /// </summary>
    internal sealed class DocumentServiceLeaseStoreManagerInMemory : DocumentServiceLeaseStoreManager
    {
        private readonly DocumentServiceLeaseStore leaseStore;
        private readonly DocumentServiceLeaseManager leaseManager;
        private readonly DocumentServiceLeaseCheckpointer leaseCheckpointer;
        private readonly DocumentServiceLeaseContainer leaseContainer;

        public DocumentServiceLeaseStoreManagerInMemory()
            : this(new ConcurrentDictionary<string, DocumentServiceLease>())
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
    }
}