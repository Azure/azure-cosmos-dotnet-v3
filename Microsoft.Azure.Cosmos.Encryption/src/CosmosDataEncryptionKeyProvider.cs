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
    public sealed class CosmosDataEncryptionKeyProvider : DataEncryptionKeyProvider, IDisposable
    {
        private const string ContainerPartitionKeyPath = "/id";

        private bool isDisposed = false;

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

        /// <summary>
        /// A provider that will be used to wrap (encrypt) and unwrap (decrypt) data encryption keys for envelope based encryption.
        /// </summary>
        public EncryptionKeyWrapProvider EncryptionKeyWrapProvider { get; }

        /// <summary>
        /// Container for data encryption keys.
        /// </summary>
        public DataEncryptionKeyContainer DataEncryptionKeyContainer => this.dataEncryptionKeyContainerCore;

        /// <summary>
        /// Initializes a new instance of the <see cref="CosmosDataEncryptionKeyProvider"/> class.
        /// </summary>
        /// <param name="encryptionKeyWrapProvider">A provider that will be used to wrap (encrypt) and unwrap (decrypt) data encryption keys for envelope based encryption</param>
        /// <param name="dekCacheOptions">Options for DekCache properties.</param>
        public CosmosDataEncryptionKeyProvider(
            EncryptionKeyWrapProvider encryptionKeyWrapProvider,
            DekCacheOptions? dekCacheOptions = null)
        {
            this.EncryptionKeyWrapProvider = encryptionKeyWrapProvider;
            this.dataEncryptionKeyContainerCore = new DataEncryptionKeyContainerCore(this);
            this.DekCache = new DekCache(dekCacheOptions);
        }

        /// <summary>
        /// Initialize Cosmos DB container for CosmosDataEncryptionKeyProvider to store wrapped DEKs
        /// </summary>
        /// <param name="database">Database</param>
        /// <param name="containerId">Container id</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A task to await on.</returns>
        public async Task InitializeAsync(
            Database database,
            string containerId,
            CancellationToken cancellationToken = default)
        {
            this.ThrowIfDisposed();
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

        /// <inheritdoc/>
        public override async Task<DataEncryptionKey> FetchDataEncryptionKeyAsync(
            string id,
            string encryptionAlgorithm,
            CancellationToken cancellationToken)
        {
            this.ThrowIfDisposed();
            (DataEncryptionKeyProperties _, InMemoryRawDek inMemoryRawDek) = await this.dataEncryptionKeyContainerCore.FetchUnwrappedAsync(
                id,
                diagnosticsContext: CosmosDiagnosticsContext.Create(null),
                cancellationToken: cancellationToken);

            return inMemoryRawDek.DataEncryptionKey;
        }

        private void ThrowIfDisposed()
        {
            if (this.isDisposed)
            {
                throw new ObjectDisposedException(nameof(CosmosDataEncryptionKeyProvider));
            }
        }

        private void Dispose(bool disposing)
        {
            if (disposing && !this.isDisposed)
            {
                this.DekCache.ExpiredRawDekCleaner.Dispose();
                this.isDisposed = true;
            }
        }

        /// <summary>
        /// Dispose of unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            this.Dispose(true);
        }
    }
}
