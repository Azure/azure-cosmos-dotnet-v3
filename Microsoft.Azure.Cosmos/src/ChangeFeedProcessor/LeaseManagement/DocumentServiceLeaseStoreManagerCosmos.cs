//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement
{
    using System;
    using Microsoft.Azure.Cosmos;

    /// <summary>
    /// Lease manager that is using Azure Document Service as lease storage.
    /// Documents in lease collection are organized as this:
    /// ChangeFeed.federation|database_rid|collection_rid.info            -- container
    /// ChangeFeed.federation|database_rid|collection_rid..partitionId1   -- each partition
    /// ChangeFeed.federation|database_rid|collection_rid..partitionId2
    ///                                         ...
    /// </summary>
    internal sealed class DocumentServiceLeaseStoreManagerCosmos : DocumentServiceLeaseStoreManager
    {
        private readonly DocumentServiceLeaseStore leaseStore;
        private readonly DocumentServiceLeaseManager leaseManager;
        private readonly DocumentServiceLeaseCheckpointer leaseCheckpointer;
        private readonly DocumentServiceLeaseContainer leaseContainer;

        public DocumentServiceLeaseStoreManagerCosmos(
            DocumentServiceLeaseStoreManagerOptions options,
            Container leaseContainer,
            RequestOptionsFactory requestOptionsFactory)
            : this(options, leaseContainer, requestOptionsFactory, new DocumentServiceLeaseUpdaterCosmos(leaseContainer))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentServiceLeaseStoreManagerCosmos"/> class.
        /// </summary>
        /// <remarks>
        /// Internal only for testing purposes, otherwise would be private.
        /// </remarks>
        internal DocumentServiceLeaseStoreManagerCosmos(
            DocumentServiceLeaseStoreManagerOptions options,
            Container container,
            RequestOptionsFactory requestOptionsFactory,
            DocumentServiceLeaseUpdater leaseUpdater) // For testing purposes only.
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (options.ContainerNamePrefix == null)
            {
                throw new ArgumentNullException(nameof(options.ContainerNamePrefix));
            }

            if (string.IsNullOrEmpty(options.HostName))
            {
                throw new ArgumentNullException(nameof(options.HostName));
            }

            if (container == null)
            {
                throw new ArgumentNullException(nameof(container));
            }

            if (requestOptionsFactory == null)
            {
                throw new ArgumentException(nameof(requestOptionsFactory));
            }

            if (leaseUpdater == null)
            {
                throw new ArgumentException(nameof(leaseUpdater));
            }

            this.leaseStore = new DocumentServiceLeaseStoreCosmos(
                container,
                options.ContainerNamePrefix,
                requestOptionsFactory);

            this.leaseManager = new DocumentServiceLeaseManagerCosmos(
                container,
                leaseUpdater,
                options,
                requestOptionsFactory);

            this.leaseCheckpointer = new DocumentServiceLeaseCheckpointerCore(
                leaseUpdater,
                requestOptionsFactory);

            this.leaseContainer = new DocumentServiceLeaseContainerCosmos(
                container,
                options);
        }

        public override DocumentServiceLeaseStore LeaseStore => this.leaseStore;

        public override DocumentServiceLeaseManager LeaseManager => this.leaseManager;

        public override DocumentServiceLeaseCheckpointer LeaseCheckpointer => this.leaseCheckpointer;

        public override DocumentServiceLeaseContainer LeaseContainer => this.leaseContainer;
    }
}