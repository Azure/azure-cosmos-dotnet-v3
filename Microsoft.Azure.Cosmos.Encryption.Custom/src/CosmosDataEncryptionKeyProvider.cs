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
    public sealed class CosmosDataEncryptionKeyProvider : DataEncryptionKeyProvider, IDisposable, IAsyncDisposable
    {
        private const string ContainerPartitionKeyPath = "/id";

        // ProtectedDataEncryptionKey rejects TimeSpan.MaxValue, so use ~100 years to model the
        // upstream "cache forever" semantics that the EncryptionKeyStoreProvider exposes via a
        // null DataEncryptionKeyCacheTimeToLive.
        private static readonly TimeSpan EffectivelyForeverTimeToLive = TimeSpan.FromDays(36500);

        private readonly DataEncryptionKeyContainerCore dataEncryptionKeyContainerCore;

        private int isDisposed;

        private Container container;

        internal DekCache DekCache { get; }

        internal TimeSpan? PdekCacheTimeToLive { get; private set; }

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
        [Obsolete("Please use a constructor that accepts EncryptionKeyStoreProvider (with DekCacheOptions to enable distributed-cache support).")]
        public CosmosDataEncryptionKeyProvider(
            EncryptionKeyWrapProvider encryptionKeyWrapProvider,
            TimeSpan? dekPropertiesTimeToLive = null)
        {
            this.EncryptionKeyWrapProvider = encryptionKeyWrapProvider ?? throw new ArgumentNullException(nameof(encryptionKeyWrapProvider));
            this.dataEncryptionKeyContainerCore = new DataEncryptionKeyContainerCore(this);
            this.DekCache = NewDekCache(dekPropertiesTimeToLive == null ? null : new DekCacheOptions { DekPropertiesTimeToLive = dekPropertiesTimeToLive });
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CosmosDataEncryptionKeyProvider"/> class.
        /// </summary>
        /// <param name="encryptionKeyStoreProvider"> MDE EncryptionKeyStoreProvider for Wrapping/UnWrapping services. </param>
        /// <param name="dekPropertiesTimeToLive">Time to live for DEK properties before having to refresh.</param>
        public CosmosDataEncryptionKeyProvider(
            EncryptionKeyStoreProvider encryptionKeyStoreProvider,
            TimeSpan? dekPropertiesTimeToLive = null)
            : this(encryptionKeyStoreProvider, dekPropertiesTimeToLive == null ? null : new DekCacheOptions { DekPropertiesTimeToLive = dekPropertiesTimeToLive })
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CosmosDataEncryptionKeyProvider"/> class with optional distributed cache support via a <see cref="DekCacheOptions"/> bag.
        /// </summary>
        /// <param name="encryptionKeyStoreProvider">MDE <see cref="EncryptionKeyStoreProvider"/> for wrapping / unwrapping services.</param>
        /// <param name="dekCacheOptions">Optional cache configuration; pass <see langword="null"/> to use defaults equivalent to the no-distributed-cache constructor.</param>
        /// <remarks>
        /// When supplying <see cref="DekCacheOptions.DistributedCache"/>, ensure the cache infrastructure
        /// is configured with encryption in transit (TLS) and encryption at rest. The cache stores
        /// wrapped (encrypted) DEK properties including key metadata. Raw DEK material is never written
        /// to the distributed cache.
        /// </remarks>
        public CosmosDataEncryptionKeyProvider(
            EncryptionKeyStoreProvider encryptionKeyStoreProvider,
            DekCacheOptions dekCacheOptions)
        {
            this.EncryptionKeyStoreProvider = encryptionKeyStoreProvider ?? throw new ArgumentNullException(nameof(encryptionKeyStoreProvider));
            this.MdeKeyWrapProvider = new MdeKeyWrapProvider(encryptionKeyStoreProvider);
            this.dataEncryptionKeyContainerCore = new DataEncryptionKeyContainerCore(this);
            this.DekCache = NewDekCache(dekCacheOptions);
            this.InitializeProtectedDataEncryptionKeyTimeToLive();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CosmosDataEncryptionKeyProvider"/> class.
        /// </summary>
        /// <param name="encryptionKeyWrapProvider">A provider that will be used to wrap (encrypt) and unwrap (decrypt) data encryption keys for envelope based encryption</param>
        /// <param name="encryptionKeyStoreProvider"> MDE EncryptionKeyStoreProvider for Wrapping/UnWrapping services. </param>
        /// <param name="dekPropertiesTimeToLive">Time to live for DEK properties before having to refresh.</param>
        [Obsolete("Please use the constructor that accepts a DekCacheOptions parameter.")]
        public CosmosDataEncryptionKeyProvider(
            EncryptionKeyWrapProvider encryptionKeyWrapProvider,
            EncryptionKeyStoreProvider encryptionKeyStoreProvider,
            TimeSpan? dekPropertiesTimeToLive = null)
            : this(encryptionKeyWrapProvider, encryptionKeyStoreProvider, dekPropertiesTimeToLive == null ? null : new DekCacheOptions { DekPropertiesTimeToLive = dekPropertiesTimeToLive })
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CosmosDataEncryptionKeyProvider"/> class with both an
        /// <see cref="EncryptionKeyWrapProvider"/> (legacy algorithm path) and an
        /// <see cref="EncryptionKeyStoreProvider"/> (MDE path), plus optional distributed-cache support via
        /// <see cref="DekCacheOptions"/>. Preserves backwards compatibility for hybrid consumers (e.g. ALE) that
        /// must support both algorithm paths during migration.
        /// </summary>
        /// <param name="encryptionKeyWrapProvider">Legacy wrap provider; only used for documents written with the legacy algorithm.</param>
        /// <param name="encryptionKeyStoreProvider">MDE <see cref="EncryptionKeyStoreProvider"/> for wrapping / unwrapping.</param>
        /// <param name="dekCacheOptions">Optional cache configuration; pass <see langword="null"/> to use defaults.</param>
#pragma warning disable CS0618 // EncryptionKeyWrapProvider is obsolete; surfaced here only for hybrid back-compat callers (e.g. ALE).
        public CosmosDataEncryptionKeyProvider(
            EncryptionKeyWrapProvider encryptionKeyWrapProvider,
            EncryptionKeyStoreProvider encryptionKeyStoreProvider,
            DekCacheOptions dekCacheOptions)
#pragma warning restore CS0618
        {
            this.EncryptionKeyWrapProvider = encryptionKeyWrapProvider ?? throw new ArgumentNullException(nameof(encryptionKeyWrapProvider));
            this.EncryptionKeyStoreProvider = encryptionKeyStoreProvider ?? throw new ArgumentNullException(nameof(encryptionKeyStoreProvider));
            this.MdeKeyWrapProvider = new MdeKeyWrapProvider(encryptionKeyStoreProvider);
            this.dataEncryptionKeyContainerCore = new DataEncryptionKeyContainerCore(this);
            this.DekCache = NewDekCache(dekCacheOptions);
            this.InitializeProtectedDataEncryptionKeyTimeToLive();
        }

        /// <summary>
        /// Materialises a <see cref="DekCache"/> from a (possibly null) <see cref="DekCacheOptions"/>.
        /// </summary>
        private static DekCache NewDekCache(DekCacheOptions dekCacheOptions)
        {
            DekCacheOptions opts = dekCacheOptions ?? new DekCacheOptions();
            return new DekCache(
                dekPropertiesTimeToLive: opts.DekPropertiesTimeToLive,
                distributedCache: opts.DistributedCache,
                refreshBeforeExpiry: opts.RefreshBeforeExpiry,
                cacheKeyPrefix: opts.DistributedCacheKeyPrefix,
                utcNow: null,
                distributedCacheEntryLifetime: opts.DistributedCacheEntryLifetime);
        }

        /// <summary>
        /// Mirrors <see cref="EncryptionKeyStoreProvider.DataEncryptionKeyCacheTimeToLive"/> onto
        /// the static <see cref="ProtectedDataEncryptionKey.TimeToLive"/>. Process-global mutation;
        /// shared across providers.
        /// </summary>
        private void InitializeProtectedDataEncryptionKeyTimeToLive()
        {
            this.PdekCacheTimeToLive = this.EncryptionKeyStoreProvider.DataEncryptionKeyCacheTimeToLive;
            ProtectedDataEncryptionKey.TimeToLive = this.PdekCacheTimeToLive ?? EffectivelyForeverTimeToLive;
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
            if (this.container != null)
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
            this.SetContainer(container);
        }

        /// <summary>
        /// Sets the backing Cosmos <see cref="Container"/> exactly once.
        /// Throws if already initialized to prevent accidental reassignment.
        /// </summary>
        /// <param name="container">The container to associate with this provider.</param>
        private void SetContainer(Container container)
        {
            ArgumentValidation.ThrowIfNull(container);

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

            // Encryption with MDE algorithm using a Legacy-algorithm-configured DEK.
#pragma warning disable CS0618 // Type or member is obsolete
            if (string.Equals(encryptionAlgorithm, CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized, StringComparison.Ordinal) &&
                string.Equals(dataEncryptionKeyProperties.EncryptionAlgorithm, CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized, StringComparison.Ordinal))
            {
                return await this.dataEncryptionKeyContainerCore.FetchUnWrappedMdeSupportedLegacyDekAsync(
                    dataEncryptionKeyProperties,
                    cancellationToken);
            }
#pragma warning restore CS0618 // Type or member is obsolete

            // Encryption with Legacy algorithm using an MDE-algorithm-configured DEK.
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

        /// <summary>
        /// Cancels in-flight background distributed-cache writes owned by this provider's
        /// <see cref="DekCache"/> and best-effort drains them. Idempotent. Does NOT dispose
        /// externally-supplied dependencies (providers, container, or
        /// <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/>) — caller
        /// owns those lifetimes. <see cref="DekCache.RemoveAsync(string,System.Threading.CancellationToken)"/>
        /// invalidations in flight are not interrupted by disposal.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref this.isDisposed, 1) != 0)
            {
                return;
            }

            this.DekCache?.Dispose();
        }

        /// <summary>
        /// Async counterpart of <see cref="Dispose"/>. Idempotent.
        /// </summary>
        /// <returns>A <see cref="ValueTask"/> that completes when the bounded drain finishes.</returns>
        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref this.isDisposed, 1) != 0)
            {
                return default;
            }

            return this.DekCache?.DisposeAsync() ?? default;
        }
    }
}
