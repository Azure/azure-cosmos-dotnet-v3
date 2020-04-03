//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.DataEncryptionKeyProvider
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Abstraction for a provider to get a data encryption key.
    /// See https://aka.ms/CosmosClientEncryption for more information on client-side encryption support in Azure Cosmos DB.
    /// </summary>
    public class CosmosDataEncryptionKeyProvider : Cosmos.DataEncryptionKeyProvider
    {
        private const string ContainerPartitionKeyPath = "/id";

        private const int KeyContainerThroughput = 400;

        private DataEncryptionKeyContainerCore dataEncryptionKeyContainerCore;

        private Container container;

        internal DekCache DekCache { get; }

        internal Container Container
        {
            get
            {
                if (this.container != null)
                {
                    return this.container;
                }

                throw new InvalidOperationException($"The {nameof(CosmosDataEncryptionKeyProvider)} was not initialized.");
            }
        }

        public EncryptionKeyWrapProvider EncryptionKeyWrapProvider { get; }

        public DataEncryptionKeyContainer DataEncryptionKeyContainer => dataEncryptionKeyContainerCore;

        public CosmosDataEncryptionKeyProvider(
            EncryptionKeyWrapProvider encryptionKeyWrapProvider,
            TimeSpan? dekPropertiesTimeToLive = null)
        {
            this.EncryptionKeyWrapProvider = encryptionKeyWrapProvider;
            this.dataEncryptionKeyContainerCore = new DataEncryptionKeyContainerCore(this);
            this.DekCache = new DekCache(dekPropertiesTimeToLive);
        }

        public async Task InitializeAsync(
            Database database, 
            string containerId,
            CancellationToken cancellationToken = default)
        {
            if(this.container != null)
            {
                throw new InvalidOperationException($"{nameof(CosmosDataEncryptionKeyProvider)} has already been initialized.");
            }

            ContainerResponse containerResponse = await database.CreateContainerIfNotExistsAsync(
                containerId,
                partitionKeyPath: CosmosDataEncryptionKeyProvider.ContainerPartitionKeyPath,
                throughput: CosmosDataEncryptionKeyProvider.KeyContainerThroughput);

            if(containerResponse.Resource.PartitionKeyPath != CosmosDataEncryptionKeyProvider.ContainerPartitionKeyPath)
            {
                throw new ArgumentException($"Provided container {containerId} did not have the appropriate partition key definition. " +
                    $"The container needs to be created with PartitionKeyPath set to {CosmosDataEncryptionKeyProvider.ContainerPartitionKeyPath}.",
                    nameof(containerId));
            }

            this.container = containerResponse.Container;
        }

        public override async Task<DataEncryptionKey> FetchDataEncryptionKeyAsync(
            string id,
            CosmosEncryptionAlgorithm encryptionAlgorithm,
            CancellationToken cancellationToken)
        {
            (DataEncryptionKeyProperties _, InMemoryRawDek inMemoryRawDek) = await this.dataEncryptionKeyContainerCore.FetchUnwrappedAsync(
                id, 
                diagnosticsContext: CosmosDiagnosticsContext.Create(null), // todo: should this be in the interface or removed
                cancellationToken: cancellationToken);

            return inMemoryRawDek.DataEncryptionKey;
        }
    }
}
