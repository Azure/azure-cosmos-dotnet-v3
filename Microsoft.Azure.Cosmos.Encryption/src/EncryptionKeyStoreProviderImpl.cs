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
    /// The purpose/intention to introduce this class is to utilize the cache provide by the <see cref="EncryptionKeyStoreProvider"/> abstract class. This class basically
    /// redirects all the corresponding calls to <see cref="IKeyEncryptionKeyResolver"/> 's methods and thus allowing us
    /// to utilize the virtual method <see cref="EncryptionKeyStoreProvider.GetOrCreateDataEncryptionKey"/> to access the cache.
    ///
    /// Note: Since <see cref="EncryptionKeyStoreProvider.Sign"/> and <see cref="EncryptionKeyStoreProvider.Verify"/> methods are not exposed, <see cref="EncryptionKeyStoreProvider.GetOrCreateSignatureVerificationResult"/> is not supported either.
    ///
    /// <remark>
    /// The call hierarchy is as follows. Note, all core MDE API's used in internal cosmos encryption code are passed an EncryptionKeyStoreProviderImpl object.
    /// ProtectedDataEncryptionKey -> KeyEncryptionKey(containing EncryptionKeyStoreProviderImpl object) -> EncryptionKeyStoreProviderImpl.WrapKey -> this.keyEncryptionKeyResolver.WrapKey
    /// ProtectedDataEncryptionKey -> KeyEncryptionKey(containing EncryptionKeyStoreProviderImpl object) -> EncryptionKeyStoreProviderImpl.UnWrapKey -> this.keyEncryptionKeyResolver.UnwrapKey
    /// </remark>
    /// </summary>
    internal class EncryptionKeyStoreProviderImpl : EncryptionKeyStoreProvider, IDisposable
    {
        public const string RsaOaepWrapAlgorithm = "RSA-OAEP";

        /// <summary>
        /// When a cached key is within this duration of its expiry a background refresh
        /// is scheduled so the sync path never encounters a cold cache.
        /// </summary>
        private static readonly TimeSpan ProactiveRefreshThreshold = TimeSpan.FromMinutes(5);

        private readonly IKeyEncryptionKeyResolver keyEncryptionKeyResolver;

        /// <summary>
        /// Cache of asynchronously pre-fetched unwrapped key bytes, keyed by the hex
        /// representation of the encrypted key.  Populated by
        /// <see cref="PrefetchUnwrapKeyAsync"/> (called outside the global encryption
        /// semaphore) and read by <see cref="UnwrapKey"/> (called inside the semaphore)
        /// for an instant, I/O-free return.
        /// </summary>
        private readonly ConcurrentDictionary<string, PrefetchedKeyData> prefetchedKeys = new ConcurrentDictionary<string, PrefetchedKeyData>();

        /// <summary>
        /// Tracks cache keys that have a background refresh in-flight to deduplicate concurrent refresh tasks.
        /// </summary>
        private readonly ConcurrentDictionary<string, byte> refreshesInFlight = new ConcurrentDictionary<string, byte>();

        /// <summary>
        /// Cancellation source for background proactive-refresh tasks.  Cancelled on
        /// <see cref="Dispose"/> so in-flight refreshes are promptly stopped and the
        /// provider / key-resolver / credential chain can be garbage collected.
        /// </summary>
        private readonly CancellationTokenSource backgroundCts = new CancellationTokenSource();

        /// <summary>
        /// Guard for <see cref="Dispose"/> to make double-dispose and concurrent
        /// dispose calls safe. 0 = not disposed, 1 = disposed.
        /// </summary>
        private int disposed;

        public EncryptionKeyStoreProviderImpl(IKeyEncryptionKeyResolver keyEncryptionKeyResolver, string providerName)
        {
            this.keyEncryptionKeyResolver = keyEncryptionKeyResolver;
            this.ProviderName = providerName;
            this.DataEncryptionKeyCacheTimeToLive = TimeSpan.Zero;
        }

        public override string ProviderName { get; }

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

            // Slow path (safety net): if the prefetch cache is cold — e.g. first call
            // before PrefetchUnwrapKeyAsync ran, or a narrow race on cache expiry —
            // fall back to the original synchronous Resolve + UnwrapKey.  On success
            // the result is pushed into the prefetch cache so future calls are fast.
            // Note: cacheKey (already computed above) is captured by the closure —
            // avoids recomputing encryptedKey.ToHexString() inside GetOrCreateDataEncryptionKey.
            return this.GetOrCreateDataEncryptionKey(cacheKey, UnWrapKeyCore);

            byte[] UnWrapKeyCore()
            {
                byte[] unwrapped = this.keyEncryptionKeyResolver
                    .Resolve(encryptionKeyId)
                    .UnwrapKey(EncryptionKeyStoreProviderImpl.GetNameForKeyEncryptionKeyAlgorithm(algorithm), encryptedKey);

                this.prefetchedKeys[cacheKey] = new PrefetchedKeyData(
                    unwrapped,
                    DateTime.UtcNow.Add(ProtectedDataEncryptionKey.TimeToLive));

                return unwrapped;
            }
        }

        public override byte[] WrapKey(string encryptionKeyId, KeyEncryptionKeyAlgorithm algorithm, byte[] key)
        {
            return this.keyEncryptionKeyResolver
                .Resolve(encryptionKeyId)
                .WrapKey(EncryptionKeyStoreProviderImpl.GetNameForKeyEncryptionKeyAlgorithm(algorithm), key);
        }

        /// <Remark>
        /// The public facing Cosmos Encryption library interface does not expose this method, hence not supported.
        /// </Remark>
        public override byte[] Sign(string encryptionKeyId, bool allowEnclaveComputations)
        {
            throw new NotSupportedException("The Sign operation is not supported.");
        }

        /// <Remark>
        /// The public facing Cosmos Encryption library interface does not expose this method, hence not supported.
        /// </Remark>
        public override bool Verify(string encryptionKeyId, bool allowEnclaveComputations, byte[] signature)
        {
            throw new NotSupportedException("The Verify operation is not supported.");
        }

        /// <summary>
        /// Cancels any in-flight background refresh tasks and releases the
        /// <see cref="CancellationTokenSource"/>.  This allows the provider,
        /// key resolver, and credential chain to be garbage collected promptly
        /// when the <see cref="EncryptionCosmosClient"/> is disposed.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref this.disposed, 1) != 0)
            {
                return; // already disposed — safe for double/concurrent Dispose calls
            }

            this.backgroundCts.Cancel();
            this.backgroundCts.Dispose();
            this.prefetchedKeys.Clear();
        }

        /// <summary>
        /// Asynchronously pre-warms the unwrapped-key cache for <paramref name="encryptedKey"/>
        /// so that the synchronous <see cref="UnwrapKey"/> call (which runs inside the global
        /// encryption semaphore) can return instantly without any Key Vault I/O.
        ///
        /// <para>This MUST be called <strong>before</strong> acquiring the global semaphore.</para>
        /// </summary>
        /// <param name="encryptionKeyId">Key Encryption Key identifier (e.g. AKV key URI).</param>
        /// <param name="encryptedKey">The wrapped Data Encryption Key bytes.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        internal async Task PrefetchUnwrapKeyAsync(
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
            IKeyEncryptionKey keyEncryptionKey = await this.keyEncryptionKeyResolver.ResolveAsync(encryptionKeyId, cancellationToken).ConfigureAwait(false);

            byte[] unwrappedKey = await keyEncryptionKey.UnwrapKeyAsync(
                EncryptionKeyStoreProviderImpl.RsaOaepWrapAlgorithm,
                encryptedKey,
                cancellationToken).ConfigureAwait(false);

            this.prefetchedKeys[cacheKey] = new PrefetchedKeyData(
                unwrappedKey,
                DateTime.UtcNow.Add(ProtectedDataEncryptionKey.TimeToLive));
        }

        private static string GetNameForKeyEncryptionKeyAlgorithm(KeyEncryptionKeyAlgorithm algorithm)
        {
            if (algorithm == KeyEncryptionKeyAlgorithm.RSA_OAEP)
            {
                return EncryptionKeyStoreProviderImpl.RsaOaepWrapAlgorithm;
            }

            throw new InvalidOperationException(string.Format("Unexpected algorithm {0}", algorithm));
        }

        /// <summary>
        /// Fires a background task to refresh the prefetch cache entry for the given
        /// encrypted key, keeping the sync <see cref="UnwrapKey"/> path warm.
        /// Concurrent refreshes for the same key are deduplicated.
        /// The task is cancelled when <see cref="Dispose"/> is called.
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
                    // cancellation on Dispose), the next sync UnwrapKey call will
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
