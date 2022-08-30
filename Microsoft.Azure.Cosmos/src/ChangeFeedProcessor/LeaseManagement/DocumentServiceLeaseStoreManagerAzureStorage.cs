//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement
{
    using System;
    using global::Azure.Storage.Blobs;

    /// <summary>
    /// Lease manager that is using Azure Blob Storage Service as lease storage.
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
        /// Initializes a new instance of the <see cref="DocumentServiceLeaseStoreManagerAzureStorage"/> class.
        /// </summary>
        public DocumentServiceLeaseStoreManagerAzureStorage(
            ContainerInternal monitoredContainer,
            BlobContainerClient leaseContainer,
            string hostName) // For testing purposes only.
        {
            if (monitoredContainer == null) throw new ArgumentNullException(nameof(monitoredContainer));
            if (leaseContainer == null) throw new ArgumentNullException(nameof(leaseContainer));
            if (string.IsNullOrEmpty(hostName)) throw new ArgumentNullException(nameof(hostName));
            
            DocumentServiceLeaseUpdaterAzureStorage leaseUpdater = new DocumentServiceLeaseUpdaterAzureStorage(leaseContainer); 
            
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