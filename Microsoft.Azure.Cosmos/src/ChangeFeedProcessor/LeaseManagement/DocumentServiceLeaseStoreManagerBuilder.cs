//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Tracing;

    /// <summary>
    /// Provides flexible way to build lease manager constructor parameters.
    /// For the actual creation of lease manager instance, delegates to lease manager factory.
    /// </summary>
    internal class DocumentServiceLeaseStoreManagerBuilder
    {
        public static async Task<DocumentServiceLeaseStoreManager> InitializeAsync(
            ContainerInternal monitoredContainer,
            ContainerInternal leaseContainer,
            string leaseContainerPrefix,
            string instanceName)
        {
            ContainerProperties containerProperties = await leaseContainer.GetCachedContainerPropertiesAsync(forceRefresh: false, NoOpTrace.Singleton, cancellationToken: default);

            bool isPartitioned =
                containerProperties.PartitionKey != null &&
                containerProperties.PartitionKey.Paths != null &&
                containerProperties.PartitionKey.Paths.Count > 0;
            bool isMigratedFixed = containerProperties.PartitionKey?.IsSystemKey == true;
            if (isPartitioned
                && !isMigratedFixed
                && (containerProperties.PartitionKey.Paths.Count != 1 || containerProperties.PartitionKey.Paths[0] != "/id"))
            {
                throw new ArgumentException("The lease collection, if partitioned, must have partition key equal to id.");
            }

            RequestOptionsFactory requestOptionsFactory = isPartitioned && !isMigratedFixed ?
                (RequestOptionsFactory)new PartitionedByIdCollectionRequestOptionsFactory() :
                (RequestOptionsFactory)new SinglePartitionRequestOptionsFactory();

            DocumentServiceLeaseStoreManagerBuilder leaseStoreManagerBuilder = new DocumentServiceLeaseStoreManagerBuilder()
                .WithLeasePrefix(leaseContainerPrefix)
                .WithMonitoredContainer(monitoredContainer)
                .WithLeaseContainer(leaseContainer)
                .WithRequestOptionsFactory(requestOptionsFactory)
                .WithHostName(instanceName);

            return leaseStoreManagerBuilder.Build();
        }

        private readonly DocumentServiceLeaseStoreManagerOptions options = new DocumentServiceLeaseStoreManagerOptions();
        private ContainerInternal monitoredContainer;
        private ContainerInternal leaseContainer;
        private RequestOptionsFactory requestOptionsFactory;

        private DocumentServiceLeaseStoreManagerBuilder WithMonitoredContainer(ContainerInternal monitoredContainer)
        {
            this.monitoredContainer = monitoredContainer ?? throw new ArgumentNullException(nameof(leaseContainer));
            return this;
        }

        private DocumentServiceLeaseStoreManagerBuilder WithLeaseContainer(ContainerInternal leaseContainer)
        {
            this.leaseContainer = leaseContainer ?? throw new ArgumentNullException(nameof(leaseContainer));
            return this;
        }

        private DocumentServiceLeaseStoreManagerBuilder WithLeasePrefix(string leasePrefix)
        {
            this.options.ContainerNamePrefix = leasePrefix ?? throw new ArgumentNullException(nameof(leasePrefix));
            return this;
        }

        private DocumentServiceLeaseStoreManagerBuilder WithRequestOptionsFactory(RequestOptionsFactory requestOptionsFactory)
        {
            this.requestOptionsFactory = requestOptionsFactory ?? throw new ArgumentNullException(nameof(requestOptionsFactory));
            return this;
        }

        private DocumentServiceLeaseStoreManagerBuilder WithHostName(string hostName)
        {
            this.options.HostName = hostName ?? throw new ArgumentNullException(nameof(hostName));
            return this;
        }

        private DocumentServiceLeaseStoreManager Build()
        {
            if (this.monitoredContainer == null)
            {
                throw new InvalidOperationException(nameof(this.monitoredContainer) + " was not specified");
            }

            if (this.leaseContainer == null)
            {
                throw new InvalidOperationException(nameof(this.leaseContainer) + " was not specified");
            }

            if (this.requestOptionsFactory == null)
            {
                throw new InvalidOperationException(nameof(this.requestOptionsFactory) + " was not specified");
            }

            return new DocumentServiceLeaseStoreManagerCosmos(this.options, this.monitoredContainer, this.leaseContainer, this.requestOptionsFactory);
        }
    }
}
