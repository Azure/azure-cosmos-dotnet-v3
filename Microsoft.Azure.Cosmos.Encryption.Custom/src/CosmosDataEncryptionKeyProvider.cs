//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Encryption.Cryptography;

    /// <summary>
    /// Default implementation for a provider to get a data encryption key - wrapped keys are stored in a Cosmos DB container.
    /// See https://aka.ms/CosmosClientEncryption for more information on client-side encryption support in Azure Cosmos DB.
    /// </summary>
    public sealed class CosmosDataEncryptionKeyProvider : DataEncryptionKeyProvider
    {
        private const string ContainerPartitionKeyPath = "/id";

        private readonly DataEncryptionKeyContainerCore dataEncryptionKeyContainerCore;

        private Container container;

        internal DekCache DekCache { get; }

        // MDE's Protected Data Encryption key Cache TTL.
        internal TimeSpan? PdekCacheTimeToLive { get; }

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
        /// Gets a provider of type EncryptionKeyWrapProvider that will be used to wrap (encrypt) and unwrap (decrypt) data encryption keys for envelope based encryption.
        /// </summary>
#pragma warning disable CS0618 // Type or member is obsolete
        public EncryptionKeyWrapProvider EncryptionKeyWrapProvider { get; }
#pragma warning restore CS0618 // Type or member is obsolete

        /// <summary>
        /// Gets a provider of type EncryptionKeyStoreProvider that will be used to wrap (encrypt) and unwrap (decrypt) data encryption keys for envelope based encryption.
        /// </summary>
        public EncryptionKeyStoreProvider EncryptionKeyStoreProvider { get; }

        internal MdeKeyWrapProvider MdeKeyWrapProvider { get; }

        /// <summary>
        /// Gets Container for data encryption keys.
        /// </summary>
        public DataEncryptionKeyContainer DataEncryptionKeyContainer => this.dataEncryptionKeyContainerCore;

        /// <summary>
        /// Initializes a new instance of the <see cref="CosmosDataEncryptionKeyProvider"/> class.
        /// </summary>
        /// <param name="encryptionKeyWrapProvider">A provider that will be used to wrap (encrypt) and unwrap (decrypt) data encryption keys for envelope based encryption</param>
        /// <param name="dekPropertiesTimeToLive">Time to live for DEK properties before having to refresh.</param>
        [Obsolete("Please use the constructor with EncryptionKeyStoreProvider only.")]
        public CosmosDataEncryptionKeyProvider(
            EncryptionKeyWrapProvider encryptionKeyWrapProvider,
            TimeSpan? dekPropertiesTimeToLive = null)
        {
            this.EncryptionKeyWrapProvider = encryptionKeyWrapProvider ?? throw new ArgumentNullException(nameof(encryptionKeyWrapProvider));
            this.dataEncryptionKeyContainerCore = new DataEncryptionKeyContainerCore(this);
            this.DekCache = new DekCache(dekPropertiesTimeToLive);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CosmosDataEncryptionKeyProvider"/> class.
        /// </summary>
        /// <param name="encryptionKeyStoreProvider"> MDE EncryptionKeyStoreProvider for Wrapping/UnWrapping services. </param>
        /// <param name="dekPropertiesTimeToLive">Time to live for DEK properties before having to refresh.</param>
        public CosmosDataEncryptionKeyProvider(
            EncryptionKeyStoreProvider encryptionKeyStoreProvider,
            TimeSpan? dekPropertiesTimeToLive = null)
        {
            this.EncryptionKeyStoreProvider = encryptionKeyStoreProvider ?? throw new ArgumentNullException(nameof(encryptionKeyStoreProvider));
            this.MdeKeyWrapProvider = new MdeKeyWrapProvider(encryptionKeyStoreProvider);
            this.dataEncryptionKeyContainerCore = new DataEncryptionKeyContainerCore(this);
            this.DekCache = new DekCache(dekPropertiesTimeToLive);
            this.PdekCacheTimeToLive = this.EncryptionKeyStoreProvider.DataEncryptionKeyCacheTimeToLive;
            if (this.PdekCacheTimeToLive.HasValue)
            {
                // set the TTL for Protected Data Encryption.
                ProtectedDataEncryptionKey.TimeToLive = this.PdekCacheTimeToLive.Value;
            }
            else
            {
                // If null is passed to DataEncryptionKeyCacheTimeToLive it results in forever caching hence setting
                // arbitrarily large caching period. ProtectedDataEncryptionKey does not seem to handle TimeSpan.MaxValue.
                ProtectedDataEncryptionKey.TimeToLive = TimeSpan.FromDays(36500);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CosmosDataEncryptionKeyProvider"/> class.
        /// </summary>
        /// <param name="encryptionKeyWrapProvider">A provider that will be used to wrap (encrypt) and unwrap (decrypt) data encryption keys for envelope based encryption</param>
        /// <param name="encryptionKeyStoreProvider"> MDE EncryptionKeyStoreProvider for Wrapping/UnWrapping services. </param>
        /// <param name="dekPropertiesTimeToLive">Time to live for DEK properties before having to refresh.</param>
        [Obsolete("Please use the constructor with EncryptionKeyStoreProvider only.")]
        public CosmosDataEncryptionKeyProvider(
            EncryptionKeyWrapProvider encryptionKeyWrapProvider,
            EncryptionKeyStoreProvider encryptionKeyStoreProvider,
            TimeSpan? dekPropertiesTimeToLive = null)
        {
            this.EncryptionKeyWrapProvider = encryptionKeyWrapProvider ?? throw new ArgumentNullException(nameof(encryptionKeyWrapProvider));
            this.EncryptionKeyStoreProvider = encryptionKeyStoreProvider ?? throw new ArgumentNullException(nameof(encryptionKeyStoreProvider));
            this.MdeKeyWrapProvider = new MdeKeyWrapProvider(encryptionKeyStoreProvider);
            this.dataEncryptionKeyContainerCore = new DataEncryptionKeyContainerCore(this);
            this.DekCache = new DekCache(dekPropertiesTimeToLive);
            this.PdekCacheTimeToLive = this.EncryptionKeyStoreProvider.DataEncryptionKeyCacheTimeToLive;
            if (this.PdekCacheTimeToLive.HasValue)
            {
                // set the TTL for Protected Data Encryption.
                ProtectedDataEncryptionKey.TimeToLive = this.PdekCacheTimeToLive.Value;
            }
            else
            {
                // If null is passed to DataEncryptionKeyCacheTimeToLive it results in forever caching hence setting
                // arbitrarily large caching period. ProtectedDataEncryptionKey does not seem to handle TimeSpan.MaxValue.
                ProtectedDataEncryptionKey.TimeToLive = TimeSpan.FromDays(36500);
            }
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
            if (this.container != null)
            {
                throw new InvalidOperationException($"{nameof(CosmosDataEncryptionKeyProvider)} has already been initialized.");
            }

#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(database);
#else
            if (database == null)
            {
                throw new ArgumentNullException(nameof(database));
            }
#endif

            ContainerResponse containerResponse = await database.CreateContainerIfNotExistsAsync(
                containerId,
                partitionKeyPath: CosmosDataEncryptionKeyProvider.ContainerPartitionKeyPath,
                cancellationToken: cancellationToken);

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
        public override async Task<DataEncryptionKey> FetchDataEncryptionKeyWithoutRawKeyAsync(
            string id,
            string encryptionAlgorithm,
            CancellationToken cancellationToken)
        {
            return await this.FetchDekAsync(id, encryptionAlgorithm, cancellationToken);
        }

        /// <inheritdoc/>
        public override async Task<DataEncryptionKey> FetchDataEncryptionKeyAsync(
            string id,
            string encryptionAlgorithm,
            CancellationToken cancellationToken)
        {
            return await this.FetchDekAsync(id, encryptionAlgorithm, cancellationToken, true);
        }

        private async Task<DataEncryptionKey> FetchDekAsync(string id, string encryptionAlgorithm, CancellationToken cancellationToken, bool withRawKey = false)
        {
            DataEncryptionKeyProperties dataEncryptionKeyProperties = await this.dataEncryptionKeyContainerCore.FetchDataEncryptionKeyPropertiesAsync(
                id,
                diagnosticsContext: CosmosDiagnosticsContext.Create(null),
                cancellationToken: cancellationToken);

            // supports Encryption with MDE based algorithm using Legacy Encryption Algorithm Configured DEK.
#pragma warning disable CS0618 // Type or member is obsolete
            if (string.Equals(encryptionAlgorithm, CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized, StringComparison.Ordinal) &&
                string.Equals(dataEncryptionKeyProperties.EncryptionAlgorithm, CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized, StringComparison.Ordinal))
            {
                return await this.dataEncryptionKeyContainerCore.FetchUnWrappedMdeSupportedLegacyDekAsync(
                    dataEncryptionKeyProperties,
                    cancellationToken);
            }
#pragma warning restore CS0618 // Type or member is obsolete

            // supports Encryption with Legacy based algorithm using Mde Encryption Algorithm Configured DEK.
#pragma warning disable CS0618 // Type or member is obsolete
            if (string.Equals(encryptionAlgorithm, CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized, StringComparison.Ordinal) &&
                string.Equals(dataEncryptionKeyProperties.EncryptionAlgorithm, CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized, StringComparison.Ordinal))
            {
                return await this.dataEncryptionKeyContainerCore.FetchUnWrappedLegacySupportedMdeDekAsync(
                    dataEncryptionKeyProperties,
                    encryptionAlgorithm,
                    diagnosticsContext: CosmosDiagnosticsContext.Create(null),
                    cancellationToken);
            }
#pragma warning restore CS0618 // Type or member is obsolete

            InMemoryRawDek inMemoryRawDek = await this.dataEncryptionKeyContainerCore.FetchUnwrappedAsync(
                dataEncryptionKeyProperties,
                diagnosticsContext: CosmosDiagnosticsContext.Create(null),
                cancellationToken: cancellationToken,
                withRawKey);

            return inMemoryRawDek.DataEncryptionKey;
        }
    }
}
