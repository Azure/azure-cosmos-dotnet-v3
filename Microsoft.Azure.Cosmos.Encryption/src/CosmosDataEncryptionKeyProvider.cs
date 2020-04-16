//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Default implementation for a provider to get a data encryption key - wrapped keys are stored in a Cosmos DB container.
    /// See https://aka.ms/CosmosClientEncryption for more information on client-side encryption support in Azure Cosmos DB.
    /// </summary>
    public class CosmosDataEncryptionKeyProvider : DataEncryptionKeyProvider, IDisposable
    {
        private bool isDisposed = false;

        private const string ContainerPartitionKeyPath = "/id";

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

        public DataEncryptionKeyContainer DataEncryptionKeyContainer => this.dataEncryptionKeyContainerCore;

        public CosmosDataEncryptionKeyProvider(
            EncryptionKeyWrapProvider encryptionKeyWrapProvider,
            TimeSpan? dekPropertiesTimeToLive = null,
            int? cleanupIterationDelayInSeconds = null,
            TimeSpan? cleanupBufferTimeAfterExpiry = null)
        {
            this.EncryptionKeyWrapProvider = encryptionKeyWrapProvider;
            this.dataEncryptionKeyContainerCore = new DataEncryptionKeyContainerCore(this);
            this.DekCache = new DekCache(dekPropertiesTimeToLive, cleanupIterationDelayInSeconds, cleanupBufferTimeAfterExpiry);
        }

        public async Task InitializeAsync(
            Database database,
            string containerId,
            CancellationToken cancellationToken = default)
        {
            if (this.container != null)
            {
                throw new InvalidOperationException($"{nameof(CosmosDataEncryptionKeyProvider)} has already been initialized.");
            }

            ContainerResponse containerResponse = await database.CreateContainerIfNotExistsAsync(
                containerId,
                partitionKeyPath: CosmosDataEncryptionKeyProvider.ContainerPartitionKeyPath);

            if (containerResponse.Resource.PartitionKeyPath != CosmosDataEncryptionKeyProvider.ContainerPartitionKeyPath)
            {
                throw new ArgumentException($"Provided container {containerId} did not have the appropriate partition key definition. " +
                    $"The container needs to be created with PartitionKeyPath set to {CosmosDataEncryptionKeyProvider.ContainerPartitionKeyPath}.",
                    nameof(containerId));
            }

            this.container = containerResponse.Container;
        }

        public override async Task<DataEncryptionKey> FetchDataEncryptionKeyAsync(
            string id,
            string encryptionAlgorithm,
            CancellationToken cancellationToken)
        {
            (DataEncryptionKeyProperties _, InMemoryRawDek inMemoryRawDek) = await this.dataEncryptionKeyContainerCore.FetchUnwrappedAsync(
                id,
                diagnosticsContext: CosmosDiagnosticsContext.Create(null),
                cancellationToken: cancellationToken);

            return inMemoryRawDek.DataEncryptionKey;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.isDisposed)
            {
                if (disposing)
                {
                    this.DekCache.ExpiredRawDekCleaner.Dispose();
                }

                this.isDisposed = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            this.Dispose(true);
        }
    }
}
