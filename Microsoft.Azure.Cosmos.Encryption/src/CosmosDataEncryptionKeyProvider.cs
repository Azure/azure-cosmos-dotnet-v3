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
    public sealed class CosmosDataEncryptionKeyProvider : DataEncryptionKeyProvider
    {
        private const string ContainerPartitionKeyPath = "/id";
        private readonly DataEncryptionKeyContainerCore dataEncryptionKeyContainerCore;
        private bool isDisposed = false;
        private Container container;

        internal UnwrappedDekLifecycleManager UnwrappedDekLifecycleManager { get; }

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
        /// Gets a provider that will be used to wrap (encrypt) and unwrap (decrypt) data encryption keys for envelope based encryption.
        /// </summary>
        public EncryptionKeyWrapProvider EncryptionKeyWrapProvider { get; }

        /// <summary>
        /// Gets Container for data encryption keys.
        /// </summary>
        public DataEncryptionKeyContainer DataEncryptionKeyContainer => this.dataEncryptionKeyContainerCore;

        /// <summary>
        /// Initializes a new instance of the <see cref="CosmosDataEncryptionKeyProvider"/> class.
        /// </summary>
        /// <param name="encryptionKeyWrapProvider">A provider that will be used to wrap (encrypt) and unwrap (decrypt) data encryption keys for envelope based encryption</param>
        /// <param name="dekPropertiesTimeToLive">Time to live for DEK properties before having to refresh.</param>
        /// <param name="backgroundRefreshInterval">Time interval between successive runs of background task to refresh DEKs.</param>
        /// <param name="dekRefreshFrequencyAsPercentageOfTtl">
        /// Frequency of refreshing raw DEKs in memory expressed as percentage of <see cref="InMemoryRawDek.ClientCacheTimeToLive"/>.
        /// Example: If ClientCacheTimeToLive is 24 hrs, and this param value is 25, then we'll attempt to refresh the DEK after every 6 hrs (25% of 24 hr).
        /// If no value is provided, default value defined at <see cref="Constants.DekRefreshFrequencyAsPercentageOfTtl"/> will be used.</param>
        public CosmosDataEncryptionKeyProvider(
            EncryptionKeyWrapProvider encryptionKeyWrapProvider,
            TimeSpan? dekPropertiesTimeToLive = null,
            TimeSpan? backgroundRefreshInterval = null,
            ushort? dekRefreshFrequencyAsPercentageOfTtl = null)
        {
            this.EncryptionKeyWrapProvider = encryptionKeyWrapProvider ?? throw new ArgumentNullException(nameof(encryptionKeyWrapProvider));
            this.dataEncryptionKeyContainerCore = new DataEncryptionKeyContainerCore(
                dekProvider: this,
                dekRefreshFrequencyAsPercentageOfTtl);
            this.DekCache = new DekCache(dekPropertiesTimeToLive);
            this.UnwrappedDekLifecycleManager = new UnwrappedDekLifecycleManager(
                dekProvider: this,
                backgroundRefreshInterval);
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

            if (database == null)
            {
                throw new ArgumentNullException(nameof(database));
            }

            ContainerResponse containerResponse = await database.CreateContainerIfNotExistsAsync(
                containerId,
                partitionKeyPath: CosmosDataEncryptionKeyProvider.ContainerPartitionKeyPath);

            if (containerResponse.Resource.PartitionKeyPath != CosmosDataEncryptionKeyProvider.ContainerPartitionKeyPath)
            {
                throw new ArgumentException(
                    $"Provided container {containerId} did not have the appropriate partition key definition. " +
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

        /// <summary>
        /// Disposes the unmanaged resources.
        /// </summary>
        public override void Dispose()
        {
            if (!this.isDisposed)
            {
                this.UnwrappedDekLifecycleManager.Dispose();
                this.isDisposed = true;
            }
        }
    }
}
