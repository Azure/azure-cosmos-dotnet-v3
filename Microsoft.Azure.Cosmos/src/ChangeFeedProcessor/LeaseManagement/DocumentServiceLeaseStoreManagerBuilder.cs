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
        private readonly DocumentServiceLeaseStoreManagerOptions options = new DocumentServiceLeaseStoreManagerOptions();
        private Container container;
        private RequestOptionsFactory requestOptionsFactory;

        public DocumentServiceLeaseStoreManagerBuilder WithLeaseContainer(Container leaseContainer)
        {
            this.container = leaseContainer ?? throw new ArgumentNullException(nameof(leaseContainer));
            return this;
        }

        public DocumentServiceLeaseStoreManagerBuilder WithLeasePrefix(string leasePrefix)
        {
            this.options.ContainerNamePrefix = leasePrefix ?? throw new ArgumentNullException(nameof(leasePrefix));
            return this;
        }

        public DocumentServiceLeaseStoreManagerBuilder WithRequestOptionsFactory(RequestOptionsFactory requestOptionsFactory)
        {
            this.requestOptionsFactory = requestOptionsFactory ?? throw new ArgumentNullException(nameof(requestOptionsFactory));
            return this;
        }

        public DocumentServiceLeaseStoreManagerBuilder WithHostName(string hostName)
        {
            this.options.HostName = hostName ?? throw new ArgumentNullException(nameof(hostName));
            return this;
        }

        public Task<DocumentServiceLeaseStoreManager> BuildAsync()
        {
            if (this.container == null)
                throw new InvalidOperationException(nameof(this.container) + " was not specified");
            if (this.requestOptionsFactory == null)
                throw new InvalidOperationException(nameof(this.requestOptionsFactory) + " was not specified");

            DocumentServiceLeaseStoreManagerCosmos leaseStoreManager = new DocumentServiceLeaseStoreManagerCosmos(this.options, this.container, this.requestOptionsFactory);
            return Task.FromResult<DocumentServiceLeaseStoreManager>(leaseStoreManager);
        }
    }
}
