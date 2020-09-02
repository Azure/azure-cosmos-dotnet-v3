//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;

    /// <summary>
    /// Provides flexible way to build lease manager constructor parameters.
    /// For the actual creation of lease manager instance, delegates to lease manager factory.
    /// </summary>
    internal class DocumentServiceLeaseStoreManagerBuilder
    {
        public static async Task<DocumentServiceLeaseStoreManager> InitializeAsync(
            ContainerInternal leaseContainer,
            string leaseContainerPrefix,
            string instanceName)
        {
            ContainerResponse cosmosContainerResponse = await leaseContainer.ReadContainerAsync().ConfigureAwait(false);
            ContainerProperties containerProperties = cosmosContainerResponse.Resource;

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
                .WithLeaseContainer(leaseContainer)
                .WithRequestOptionsFactory(requestOptionsFactory)
                .WithHostName(instanceName);

            return await leaseStoreManagerBuilder.BuildAsync().ConfigureAwait(false);
        }

        private DocumentServiceLeaseStoreManagerOptions options = new DocumentServiceLeaseStoreManagerOptions();
        private Container container;
        private RequestOptionsFactory requestOptionsFactory;

        private DocumentServiceLeaseStoreManagerBuilder WithLeaseContainer(Container leaseContainer)
        {
            if (leaseContainer == null) throw new ArgumentNullException(nameof(leaseContainer));

            this.container = leaseContainer;
            return this;
        }

        private DocumentServiceLeaseStoreManagerBuilder WithLeasePrefix(string leasePrefix)
        {
            if (leasePrefix == null) throw new ArgumentNullException(nameof(leasePrefix));

            this.options.ContainerNamePrefix = leasePrefix;
            return this;
        }

        private DocumentServiceLeaseStoreManagerBuilder WithRequestOptionsFactory(RequestOptionsFactory requestOptionsFactory)
        {
            if (requestOptionsFactory == null) throw new ArgumentNullException(nameof(requestOptionsFactory));

            this.requestOptionsFactory = requestOptionsFactory;
            return this;
        }

        private DocumentServiceLeaseStoreManagerBuilder WithHostName(string hostName)
        {
            if (hostName == null) throw new ArgumentNullException(nameof(hostName));

            this.options.HostName = hostName;
            return this;
        }

        private Task<DocumentServiceLeaseStoreManager> BuildAsync()
        {
            if (this.container == null)
            {
                throw new InvalidOperationException(nameof(this.container) + " was not specified");
            }

            if (this.requestOptionsFactory == null)
            {
                throw new InvalidOperationException(nameof(this.requestOptionsFactory) + " was not specified");
            }

            DocumentServiceLeaseStoreManagerCosmos leaseStoreManager = new DocumentServiceLeaseStoreManagerCosmos(this.options, this.container, this.requestOptionsFactory);
            return Task.FromResult<DocumentServiceLeaseStoreManager>(leaseStoreManager);
        }
    }
}
