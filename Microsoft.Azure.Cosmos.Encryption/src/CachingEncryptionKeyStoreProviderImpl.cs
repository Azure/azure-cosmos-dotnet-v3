//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure.Core.Cryptography;
    using Microsoft.Data.Encryption.Cryptography;

    /// <summary>
    /// Extends <see cref="EncryptionKeyStoreProviderImpl"/> with an async prefetch cache
    /// and proactive background refresh so that the synchronous <see cref="UnwrapKey"/>
    /// call (which runs inside the global encryption semaphore) returns instantly from
    /// cache with zero Key Vault I/O.
    ///
    /// <para>Enabled by setting environment variable
    /// <c>AZURE_COSMOS_ENCRYPTION_OPTIMISTIC_DECRYPTION_ENABLED=true</c>.</para>
    ///
    /// <para>When disabled (default), <see cref="EncryptionKeyStoreProviderImpl"/> is
    /// used instead and behaviour is identical to the original sync-only implementation.</para>
    /// </summary>
    internal sealed class CachingEncryptionKeyStoreProviderImpl : EncryptionKeyStoreProviderImpl
    {
        /// <summary>
        /// When a cached key is within this duration of its expiry a background refresh
        /// is scheduled so the sync path never encounters a cold cache.
        /// </summary>
        private static readonly TimeSpan ProactiveRefreshThreshold = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Cache of asynchronously pre-fetched unwrapped key bytes, keyed by the hex
        /// representation of the encrypted key.
        /// </summary>
        private readonly ConcurrentDictionary<string, PrefetchedKeyData> prefetchedKeys = new ConcurrentDictionary<string, PrefetchedKeyData>();

        /// <summary>
        /// Tracks cache keys that have a background refresh in-flight to deduplicate concurrent refresh tasks.
        /// </summary>
        private readonly ConcurrentDictionary<string, byte> refreshesInFlight = new ConcurrentDictionary<string, byte>();

        /// <summary>
        /// Cancellation source for background proactive-refresh tasks.  Cancelled on
        /// <see cref="Cleanup"/> so in-flight refreshes are promptly stopped and the
        /// provider / key-resolver / credential chain can be garbage collected.
        /// </summary>
        private readonly CancellationTokenSource backgroundCts = new CancellationTokenSource();

        /// <summary>
        /// Guard for <see cref="Cleanup"/> to make double-cleanup and concurrent
        /// cleanup calls safe. 0 = not cleaned up, 1 = cleaned up.
        /// </summary>
        private int cleanedUp;

        public CachingEncryptionKeyStoreProviderImpl(IKeyEncryptionKeyResolver keyEncryptionKeyResolver, string providerName)
            : base(keyEncryptionKeyResolver, providerName)
        {
        }

        public override byte[] UnwrapKey(string encryptionKeyId, KeyEncryptionKeyAlgorithm algorithm, byte[] encryptedKey)
        {
            string cacheKey = encryptedKey.ToHexString();

            // Fast path: return from the prefetch cache — zero I/O, no latency.
            if (this.prefetchedKeys.TryGetValue(cacheKey, out PrefetchedKeyData cached))
            {
                if (DateTime.UtcNow < cached.ExpiresAtUtc)
                {
                    // Proactive refresh: if we are nearing expiry, kick off a background
                    // async refresh so the cache stays warm for the next caller.
                    if (DateTime.UtcNow > cached.ExpiresAtUtc - ProactiveRefreshThreshold)
                    {
                        this.ScheduleBackgroundRefresh(encryptionKeyId, encryptedKey);
                    }

                    return cached.UnwrappedKeyBytes;
                }

                // Entry has expired — remove it and fall through to the sync path.
                this.prefetchedKeys.TryRemove(cacheKey, out _);
            }

            // Slow path (safety net): sync Resolve + UnwrapKey.  On success the result
            // is pushed into the prefetch cache so future calls are fast.
            return this.GetOrCreateDataEncryptionKey(cacheKey, UnWrapKeyCore);

            byte[] UnWrapKeyCore()
            {
                byte[] unwrapped = this.KeyEncryptionKeyResolver
                    .Resolve(encryptionKeyId)
                    .UnwrapKey(EncryptionKeyStoreProviderImpl.GetNameForKeyEncryptionKeyAlgorithm(algorithm), encryptedKey);

                this.prefetchedKeys[cacheKey] = new PrefetchedKeyData(
                    unwrapped,
                    DateTime.UtcNow.Add(ProtectedDataEncryptionKey.TimeToLive));

                return unwrapped;
            }
        }

        /// <summary>
        /// Asynchronously pre-warms the unwrapped-key cache for <paramref name="encryptedKey"/>
        /// so that the synchronous <see cref="UnwrapKey"/> call (which runs inside the global
        /// encryption semaphore) can return instantly without any Key Vault I/O.
        ///
        /// <para>This MUST be called <strong>before</strong> acquiring the global semaphore.</para>
        /// </summary>
        internal override async Task PrefetchUnwrapKeyAsync(
            string encryptionKeyId,
            byte[] encryptedKey,
            CancellationToken cancellationToken)
        {
            string cacheKey = encryptedKey.ToHexString();

            // Skip when the cache is still well within its TTL.
            if (this.prefetchedKeys.TryGetValue(cacheKey, out PrefetchedKeyData existing)
                && DateTime.UtcNow < existing.ExpiresAtUtc - ProactiveRefreshThreshold)
            {
                return;
            }

            // ResolveAsync + UnwrapKeyAsync: fully async Key Vault I/O, done outside
            // the global semaphore so other threads are never blocked.
            IKeyEncryptionKey keyEncryptionKey = await this.KeyEncryptionKeyResolver.ResolveAsync(encryptionKeyId, cancellationToken).ConfigureAwait(false);

            byte[] unwrappedKey = await keyEncryptionKey.UnwrapKeyAsync(
                EncryptionKeyStoreProviderImpl.RsaOaepWrapAlgorithm,
                encryptedKey,
                cancellationToken).ConfigureAwait(false);

            this.prefetchedKeys[cacheKey] = new PrefetchedKeyData(
                unwrappedKey,
                DateTime.UtcNow.Add(ProtectedDataEncryptionKey.TimeToLive));
        }

        /// <summary>
        /// Cancels any in-flight background refresh tasks and releases the
        /// <see cref="CancellationTokenSource"/>.  Called from
        /// <see cref="EncryptionCosmosClient.Dispose(bool)"/>.
        /// </summary>
        internal override void Cleanup()
        {
            if (Interlocked.Exchange(ref this.cleanedUp, 1) != 0)
            {
                return;
            }

            this.backgroundCts.Cancel();
            this.backgroundCts.Dispose();
            this.prefetchedKeys.Clear();
        }

        /// <summary>
        /// Fires a background task to refresh the prefetch cache entry for the given
        /// encrypted key, keeping the sync <see cref="UnwrapKey"/> path warm.
        /// Concurrent refreshes for the same key are deduplicated.
        /// </summary>
        private void ScheduleBackgroundRefresh(string encryptionKeyId, byte[] encryptedKey)
        {
            string cacheKey = encryptedKey.ToHexString();

            if (!this.refreshesInFlight.TryAdd(cacheKey, 0))
            {
                return; // refresh already in progress
            }

            CancellationToken token = this.backgroundCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await this.PrefetchUnwrapKeyAsync(
                        encryptionKeyId,
                        encryptedKey,
                        token).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // Best-effort: if the background refresh fails (including
                    // cancellation on Cleanup), the next sync UnwrapKey call will
                    // fall through to the slow path.  No data loss.
                }
                finally
                {
                    this.refreshesInFlight.TryRemove(cacheKey, out _);
                }
            });
        }

        /// <summary>
        /// Immutable record holding a pre-fetched unwrapped key and its expiry.
        /// </summary>
        private sealed class PrefetchedKeyData
        {
            public PrefetchedKeyData(byte[] unwrappedKeyBytes, DateTime expiresAtUtc)
            {
                this.UnwrappedKeyBytes = unwrappedKeyBytes;
                this.ExpiresAtUtc = expiresAtUtc;
            }

            public byte[] UnwrappedKeyBytes { get; }

            public DateTime ExpiresAtUtc { get; }
        }
    }
}
