//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement
{
    using System;
    using global::Azure.Storage.Blobs;
    using Microsoft.Azure.Cosmos;

    /// <summary>
    /// Lease manager that is using Azure Document Service as lease storage.
    /// Documents in lease collection are organized as this:
    /// ChangeFeed.federation|database_rid|collection_rid.info            -- container
    /// ChangeFeed.federation|database_rid|collection_rid..partitionId1   -- each partition
    /// ChangeFeed.federation|database_rid|collection_rid..partitionId2
    ///                                         ...
    /// </summary>
    internal sealed class DocumentServiceLeaseStoreManagerAzureStorage : DocumentServiceLeaseStoreManager
    {
        private readonly DocumentServiceLeaseStore leaseStore;
        private readonly DocumentServiceLeaseManager leaseManager;
        private readonly DocumentServiceLeaseCheckpointer leaseCheckpointer;
        private readonly DocumentServiceLeaseContainer leaseContainer;

        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentServiceLeaseStoreManagerCosmos"/> class.
        /// </summary>
        public DocumentServiceLeaseStoreManagerAzureStorage(
            ContainerInternal monitoredContainer,
            string containerUri,
            string hostName) // For testing purposes only.
        {
            if (string.IsNullOrEmpty(hostName)) throw new ArgumentNullException(nameof(hostName));
            if (monitoredContainer == null) throw new ArgumentNullException(nameof(monitoredContainer));
            if (string.IsNullOrEmpty(containerUri)) throw new ArgumentNullException(nameof(containerUri));
            
            var leaseContainer = new BlobContainerClient(new Uri(containerUri));
            var leaseUpdater = new DocumentServiceLeaseUpdaterAzureStorage(leaseContainer); 
            
            this.leaseStore = new DocumentServiceLeaseStoreAzureStorage(
                leaseContainer);

            this.leaseManager = new DocumentServiceLeaseManagerAzureStorage(
                monitoredContainer,
                leaseContainer,
                leaseUpdater,
                hostName);

            this.leaseCheckpointer = new DocumentServiceLeaseCheckpointerCore(
                leaseUpdater,
                new NoneRequestOptionsFactory());

            this.leaseContainer = new DocumentServiceLeaseContainerAzureStorage(
                leaseContainer,
                hostName);
        }

        public override DocumentServiceLeaseStore LeaseStore => this.leaseStore;

        public override DocumentServiceLeaseManager LeaseManager => this.leaseManager;

        public override DocumentServiceLeaseCheckpointer LeaseCheckpointer => this.leaseCheckpointer;

        public override DocumentServiceLeaseContainer LeaseContainer => this.leaseContainer;
    }
    
    internal class NoneRequestOptionsFactory : RequestOptionsFactory
    {
        public override PartitionKey GetPartitionKey(string itemId, string partitionKey)
        {
            return PartitionKey.None;
        }

        public override void AddPartitionKeyIfNeeded(Action<string> partitionKeySetter, string partitionKey)
        {
        }
    }
}