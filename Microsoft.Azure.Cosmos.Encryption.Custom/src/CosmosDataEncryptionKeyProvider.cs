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
    /// Default implementation for a provider to get a data encryption key (DEK). Wrapped DEKs are stored as items in a Cosmos DB container.
    /// Container requirements:
    ///  - Partition key path must be <c>/id</c> (DEK <c>id</c> used as partition key).
    ///  - Use a dedicated container to isolate access control and throughput.
    ///  - Disable TTL to avoid accidental key deletion.
    /// Usage pattern: construct <see cref="CosmosDataEncryptionKeyProvider"/>, call <see cref="InitializeAsync(Database,string,CancellationToken)"/> or <see cref="Initialize(Container)"/> once at startup, then use <see cref="DataEncryptionKeyContainer"/> for DEK operations.
    /// Concurrency: initialization is single-assignment (uses <see cref="System.Threading.Interlocked"/>) so concurrent calls after success throw <see cref="InvalidOperationException"/>. Fetch/create operations are safe concurrently.
    /// Resilience: if the container is deleted or unavailable after initialization, operations surface the underlying exception (for example NotFound). Automatic re-creation is not attempted.
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
        /// Ensures the Cosmos DB container for storing wrapped DEKs exists (creating it if needed) and initializes this provider with that container.
        /// This must be invoked exactly once before any fetch/create operations.
        /// </summary>
        /// <param name="database">The Cosmos DB <see cref="Database"/> in which the DEK container should exist.</param>
        /// <param name="containerId">The identifier of the DEK container. If the container does not exist it will be created with partition key path <c>/id</c>.</param>
        /// <param name="cancellationToken">An optional cancellation token.</param>
        /// <returns>A task representing the asynchronous initialization.</returns>
        public async Task InitializeAsync(
            Database database,
            string containerId,
            CancellationToken cancellationToken = default)
        {
            if (Volatile.Read(ref this.container) != null)
            {
                throw new InvalidOperationException($"{nameof(CosmosDataEncryptionKeyProvider)} has already been initialized.");
            }

            ArgumentValidation.ThrowIfNull(database);

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

            this.SetContainer(containerResponse.Container);
        }

        /// <summary>
        /// Initializes the provider with an already created Cosmos DB container that meets the required partition key definition (<c>/id</c>).
        /// </summary>
        /// <param name="container">Existing Cosmos DB container containing wrapped DEKs or ready to store them.</param>
        public void Initialize(Container container)
        {
            if (container == null)
            {
                throw new ArgumentNullException(nameof(container));
            }

            this.SetContainer(container);
        }

        /// <summary>
        /// Sets the backing Cosmos <see cref="Container"/> exactly once.
        /// Throws if already initialized to prevent accidental reassignment.
        /// </summary>
        /// <param name="container">The container to associate with this provider.</param>
        private void SetContainer(Container container)
        {
            Container previous = Interlocked.CompareExchange(ref this.container, container, null);
            if (previous != null)
            {
                throw new InvalidOperationException($"{nameof(CosmosDataEncryptionKeyProvider)} has already been initialized.");
            }
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
