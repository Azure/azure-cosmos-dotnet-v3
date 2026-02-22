// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace CachingKeyResolverSample
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Core.Cryptography;

    /// <summary>
    /// A decorator around <see cref="IKeyEncryptionKey"/> that caches <see cref="UnwrapKey"/>
    /// results in memory. On an AKV failure, the cached unwrapped bytes are returned as a
    /// stale-while-revalidate fallback — making the data path resilient to AKV outages.
    ///
    /// <para>This is the <strong>critical missing piece</strong> for AKV resilience. The
    /// <see cref="CachingKeyResolver"/> caches the key handle (<c>Resolve</c>), but the
    /// handle's <c>UnwrapKey</c> is still a live HTTP POST to AKV.  This class caches that
    /// HTTP result so the data path survives AKV unavailability.</para>
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item>Thread-safe: uses <see cref="ConcurrentDictionary{TKey,TValue}"/>.</item>
    ///   <item><c>WrapKey</c> is NOT cached — wrapping is a write-path operation that
    ///         must always go to AKV for correctness.</item>
    ///   <item>Stale fallback is best-effort: if the KEK was rotated in AKV and the
    ///         cache holds bytes unwrapped with the old KEK, decryption will fail
    ///         naturally because the DEK bytes won't match.  This is safe.</item>
    /// </list>
    /// </remarks>
    public sealed class CachingKeyEncryptionKey : IKeyEncryptionKey
    {
        private readonly IKeyEncryptionKey innerKey;
        private readonly TimeSpan cacheTtl;

        /// <summary>
        /// Cache of unwrapped DEK bytes, keyed by the hex-encoded encrypted key.
        /// </summary>
        private readonly ConcurrentDictionary<string, CachedUnwrapEntry> unwrapCache;

        /// <summary>
        /// Initializes a new instance of <see cref="CachingKeyEncryptionKey"/>.
        /// </summary>
        /// <param name="innerKey">The real <see cref="IKeyEncryptionKey"/> (e.g., from AKV).</param>
        /// <param name="cacheTtl">How long unwrapped bytes are considered fresh. After this,
        /// the next call attempts a live unwrap but falls back to cached bytes on failure.</param>
        /// <param name="sharedCache">Optional shared cache across multiple key instances.
        /// If <c>null</c>, a private cache is created.</param>
        public CachingKeyEncryptionKey(
            IKeyEncryptionKey innerKey,
            TimeSpan cacheTtl,
            ConcurrentDictionary<string, CachedUnwrapEntry> sharedCache = null)
        {
            this.innerKey = innerKey ?? throw new ArgumentNullException(nameof(innerKey));
            this.cacheTtl = cacheTtl;
            this.unwrapCache = sharedCache ?? new ConcurrentDictionary<string, CachedUnwrapEntry>(StringComparer.Ordinal);
        }

        /// <inheritdoc />
        public string KeyId => this.innerKey.KeyId;

        /// <inheritdoc />
        public byte[] WrapKey(string algorithm, ReadOnlyMemory<byte> key, CancellationToken cancellationToken = default)
        {
            // WrapKey is a write-path operation — always go to AKV.
            return this.innerKey.WrapKey(algorithm, key, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<byte[]> WrapKeyAsync(string algorithm, ReadOnlyMemory<byte> key, CancellationToken cancellationToken = default)
        {
            return await this.innerKey.WrapKeyAsync(algorithm, key, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public byte[] UnwrapKey(string algorithm, ReadOnlyMemory<byte> encryptedKey, CancellationToken cancellationToken = default)
        {
            string cacheKey = Convert.ToHexString(encryptedKey.Span);

            // Fresh cache hit → return immediately.
            if (this.TryGetFresh(cacheKey, out byte[] cached))
            {
                return cached;
            }

            // Attempt live unwrap.
            try
            {
                byte[] unwrapped = this.innerKey.UnwrapKey(algorithm, encryptedKey, cancellationToken);
                this.PutCache(cacheKey, unwrapped);
                return unwrapped;
            }
            catch (Exception) when (this.TryGetStale(cacheKey, out byte[] stale))
            {
                // AKV failure + stale cache available → serve stale bytes.
                return stale;
            }
        }

        /// <inheritdoc />
        public async Task<byte[]> UnwrapKeyAsync(string algorithm, ReadOnlyMemory<byte> encryptedKey, CancellationToken cancellationToken = default)
        {
            string cacheKey = Convert.ToHexString(encryptedKey.Span);

            // Fresh cache hit → return immediately.
            if (this.TryGetFresh(cacheKey, out byte[] cached))
            {
                return cached;
            }

            // Attempt live unwrap.
            try
            {
                byte[] unwrapped = await this.innerKey.UnwrapKeyAsync(algorithm, encryptedKey, cancellationToken).ConfigureAwait(false);
                this.PutCache(cacheKey, unwrapped);
                return unwrapped;
            }
            catch (Exception) when (this.TryGetStale(cacheKey, out byte[] stale))
            {
                // AKV failure + stale cache available → serve stale bytes.
                return stale;
            }
        }

        private bool TryGetFresh(string cacheKey, out byte[] unwrappedBytes)
        {
            if (this.unwrapCache.TryGetValue(cacheKey, out CachedUnwrapEntry entry)
                && DateTime.UtcNow < entry.FreshUntilUtc)
            {
                unwrappedBytes = entry.UnwrappedBytes;
                return true;
            }

            unwrappedBytes = null;
            return false;
        }

        private bool TryGetStale(string cacheKey, out byte[] unwrappedBytes)
        {
            // Stale entries are usable regardless of TTL — the key bytes don't "go bad."
            // Security: if the KEK was rotated, the stale bytes will produce incorrect
            // DEK bytes and decryption will fail, so this is safe.
            if (this.unwrapCache.TryGetValue(cacheKey, out CachedUnwrapEntry entry))
            {
                unwrappedBytes = entry.UnwrappedBytes;
                return true;
            }

            unwrappedBytes = null;
            return false;
        }

        private void PutCache(string cacheKey, byte[] unwrappedBytes)
        {
            this.unwrapCache[cacheKey] = new CachedUnwrapEntry(unwrappedBytes, DateTime.UtcNow.Add(this.cacheTtl));
        }

        /// <summary>
        /// Immutable record holding cached unwrapped key bytes and their freshness timestamp.
        /// </summary>
        public sealed class CachedUnwrapEntry
        {
            public CachedUnwrapEntry(byte[] unwrappedBytes, DateTime freshUntilUtc)
            {
                this.UnwrappedBytes = unwrappedBytes;
                this.FreshUntilUtc = freshUntilUtc;
            }

            /// <summary>
            /// The raw unwrapped DEK bytes.
            /// </summary>
            public byte[] UnwrappedBytes { get; }

            /// <summary>
            /// When this entry is considered stale. After this time, a live unwrap is
            /// attempted first, but the entry can still be used as a fallback on failure.
            /// </summary>
            public DateTime FreshUntilUtc { get; }
        }
    }
}
