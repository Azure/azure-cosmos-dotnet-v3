// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace CachingKeyResolverSample
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Core;
    using Azure.Core.Cryptography;
    using Azure.Security.KeyVault.Keys.Cryptography;

    /// <summary>
    /// A caching wrapper around <see cref="KeyResolver"/> (or any <see cref="IKeyEncryptionKeyResolver"/>)
    /// that keeps resolved <see cref="IKeyEncryptionKey"/> instances in memory so that subsequent
    /// calls to <see cref="Resolve"/> and <see cref="ResolveAsync"/> return instantly with zero I/O.
    /// 
    /// A background timer proactively refreshes entries that are approaching expiry so that
    /// callers never experience a cache miss on the hot path.
    /// </summary>
    /// <remarks>
    /// This is a customer-facing sample. Review the caching strategy, TTL, and security
    /// implications for your own workload before using in production.
    /// </remarks>
    public sealed class CachingKeyResolver : IKeyEncryptionKeyResolver, IDisposable, IAsyncDisposable
    {
        private readonly IKeyEncryptionKeyResolver innerResolver;
        private readonly CachingKeyResolverOptions options;
        private readonly ConcurrentDictionary<string, CachedKeyEntry> cache;
        private readonly ConcurrentDictionary<string, byte> refreshesInFlight;
        private readonly Timer refreshTimer;
        private readonly CancellationTokenSource disposalCts;
        private readonly ConcurrentDictionary<string, CachingKeyEncryptionKey.CachedUnwrapEntry> sharedUnwrapCache;
        private int disposed;

        /// <summary>
        /// Raised when a key is served from cache (true) or resolved from the inner resolver (false).
        /// Useful for diagnostics and testing.
        /// </summary>
        public event Action<string, bool> OnCacheAccess;

        /// <summary>
        /// Initializes a new instance of <see cref="CachingKeyResolver"/> using an Azure Key Vault
        /// <see cref="KeyResolver"/> created from the provided <paramref name="credential"/>.
        /// </summary>
        /// <param name="credential">Token credential used to authenticate to Azure Key Vault.</param>
        /// <param name="options">Caching options. If <c>null</c>, defaults are used.</param>
        public CachingKeyResolver(TokenCredential credential, CachingKeyResolverOptions options = null)
            : this(new KeyResolver(credential), options)
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="CachingKeyResolver"/> using the provided
        /// inner resolver. This constructor is useful for testing or wrapping custom resolvers.
        /// </summary>
        /// <param name="innerResolver">The inner resolver to delegate to on cache miss.</param>
        /// <param name="options">Caching options. If <c>null</c>, defaults are used.</param>
        public CachingKeyResolver(IKeyEncryptionKeyResolver innerResolver, CachingKeyResolverOptions options = null)
        {
            this.innerResolver = innerResolver ?? throw new ArgumentNullException(nameof(innerResolver));
            this.options = options ?? new CachingKeyResolverOptions();
            this.cache = new ConcurrentDictionary<string, CachedKeyEntry>(StringComparer.OrdinalIgnoreCase);
            this.refreshesInFlight = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
            this.disposalCts = new CancellationTokenSource();
            this.sharedUnwrapCache = this.options.UnwrapKeyCacheTimeToLive > TimeSpan.Zero
                ? new ConcurrentDictionary<string, CachingKeyEncryptionKey.CachedUnwrapEntry>(StringComparer.Ordinal)
                : null;

            this.refreshTimer = new Timer(
                callback: this.BackgroundRefreshCallback,
                state: null,
                dueTime: this.options.RefreshTimerInterval,
                period: this.options.RefreshTimerInterval);
        }

        /// <inheritdoc />
        public IKeyEncryptionKey Resolve(string keyId, CancellationToken cancellationToken = default)
        {
            this.ThrowIfDisposed();

            if (this.cache.TryGetValue(keyId, out CachedKeyEntry entry) && entry.ExpiresUtc > DateTime.UtcNow)
            {
                this.OnCacheAccess?.Invoke(keyId, true);
                return entry.Key;
            }

            IKeyEncryptionKey resolved = this.innerResolver.Resolve(keyId, cancellationToken);
            IKeyEncryptionKey stored = this.CacheEntry(keyId, resolved);
            this.OnCacheAccess?.Invoke(keyId, false);
            return stored;
        }

        /// <inheritdoc />
        public async Task<IKeyEncryptionKey> ResolveAsync(string keyId, CancellationToken cancellationToken = default)
        {
            this.ThrowIfDisposed();

            if (this.cache.TryGetValue(keyId, out CachedKeyEntry entry) && entry.ExpiresUtc > DateTime.UtcNow)
            {
                this.OnCacheAccess?.Invoke(keyId, true);
                return entry.Key;
            }

            IKeyEncryptionKey resolved = await this.innerResolver.ResolveAsync(keyId, cancellationToken).ConfigureAwait(false);
            IKeyEncryptionKey stored = this.CacheEntry(keyId, resolved);
            this.OnCacheAccess?.Invoke(keyId, false);
            return stored;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (Interlocked.Exchange(ref this.disposed, 1) != 0)
            {
                return;
            }

            this.refreshTimer.Dispose();
            this.disposalCts.Cancel();
            this.disposalCts.Dispose();
            this.cache.Clear();
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref this.disposed, 1) != 0)
            {
                return;
            }

            await this.refreshTimer.DisposeAsync().ConfigureAwait(false);
            this.disposalCts.Cancel();
            this.disposalCts.Dispose();
            this.cache.Clear();
        }

        private IKeyEncryptionKey CacheEntry(string keyId, IKeyEncryptionKey key)
        {
            IKeyEncryptionKey wrappedKey = this.WrapKeyIfEnabled(key);
            CachedKeyEntry newEntry = new CachedKeyEntry(wrappedKey, DateTime.UtcNow.Add(this.options.KeyCacheTimeToLive));
            this.cache[keyId] = newEntry;
            return wrappedKey;
        }

        /// <summary>
        /// Wraps the resolved key with <see cref="CachingKeyEncryptionKey"/> if
        /// <see cref="CachingKeyResolverOptions.UnwrapKeyCacheTimeToLive"/> is configured.
        /// Already-wrapped keys are returned as-is to avoid double-wrapping on refresh.
        /// </summary>
        private IKeyEncryptionKey WrapKeyIfEnabled(IKeyEncryptionKey key)
        {
            if (this.sharedUnwrapCache == null || key is CachingKeyEncryptionKey)
            {
                return key;
            }

            return new CachingKeyEncryptionKey(key, this.options.UnwrapKeyCacheTimeToLive, this.sharedUnwrapCache);
        }

        private void BackgroundRefreshCallback(object state)
        {
            if (this.disposed != 0)
            {
                return;
            }

            foreach (var kvp in this.cache)
            {
                string keyId = kvp.Key;
                CachedKeyEntry entry = kvp.Value;

                TimeSpan timeUntilExpiry = entry.ExpiresUtc - DateTime.UtcNow;

                if (timeUntilExpiry <= this.options.ProactiveRefreshThreshold)
                {
                    if (!this.refreshesInFlight.TryAdd(keyId, 0))
                    {
                        // A refresh is already in flight for this key.
                        continue;
                    }

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            CancellationToken ct = this.disposalCts.Token;
                            IKeyEncryptionKey refreshed = await this.innerResolver
                                .ResolveAsync(keyId, ct)
                                .ConfigureAwait(false);

                            this.CacheEntry(keyId, refreshed);
                        }
                        catch
                        {
                            // Swallow: failed refresh should not evict the existing cached entry.
                            // The old entry remains usable until it fully expires.
                        }
                        finally
                        {
                            this.refreshesInFlight.TryRemove(keyId, out _);
                        }
                    });
                }
            }
        }

        private void ThrowIfDisposed()
        {
            if (this.disposed != 0)
            {
                throw new ObjectDisposedException(nameof(CachingKeyResolver));
            }
        }

        /// <summary>
        /// Internal representation of a cached key entry.
        /// </summary>
        internal sealed class CachedKeyEntry
        {
            public CachedKeyEntry(IKeyEncryptionKey key, DateTime expiresUtc)
            {
                this.Key = key;
                this.ExpiresUtc = expiresUtc;
            }

            public IKeyEncryptionKey Key { get; }

            public DateTime ExpiresUtc { get; }
        }
    }
}
