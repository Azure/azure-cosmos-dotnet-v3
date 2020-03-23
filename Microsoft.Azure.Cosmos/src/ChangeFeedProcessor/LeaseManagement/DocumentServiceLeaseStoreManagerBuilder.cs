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
    internal sealed class DocumentServiceLeaseStoreManagerBuilder
    {
        private readonly DocumentServiceLeaseStoreManagerOptions options = new DocumentServiceLeaseStoreManagerOptions();
        private Container container;
        private RequestOptionsFactory requestOptionsFactory;

        public DocumentServiceLeaseStoreManagerBuilder WithLeaseContainer(Container leaseContainer)
        {
            if (leaseContainer == null) throw new ArgumentNullException(nameof(leaseContainer));

            this.container = leaseContainer;
            return this;
        }

        public DocumentServiceLeaseStoreManagerBuilder WithLeasePrefix(string leasePrefix)
        {
            if (leasePrefix == null) throw new ArgumentNullException(nameof(leasePrefix));

            this.options.ContainerNamePrefix = leasePrefix;
            return this;
        }

        public DocumentServiceLeaseStoreManagerBuilder WithRequestOptionsFactory(RequestOptionsFactory requestOptionsFactory)
        {
            if (requestOptionsFactory == null) throw new ArgumentNullException(nameof(requestOptionsFactory));

            this.requestOptionsFactory = requestOptionsFactory;
            return this;
        }

        public DocumentServiceLeaseStoreManagerBuilder WithHostName(string hostName)
        {
            if (hostName == null) throw new ArgumentNullException(nameof(hostName));

            this.options.HostName = hostName;
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
