// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure;
    using global::Azure.Core.Cryptography;
    using Microsoft.Data.Encryption.Cryptography;

    /// <summary>
    /// Background worker that proactively refreshes <see cref="ProtectedDataEncryptionKey"/> cache entries
    /// before their TTL expires, preventing cache-miss contention under concurrent load.
    ///
    /// The worker performs Key Vault I/O (Resolve + UnwrapKey) on a background thread outside the global
    /// semaphore, then directly stores the refreshed PDEK in the cache under the semaphore (microseconds).
    /// This eliminates the scenario where a hot-path thread holds the semaphore for 200ms-2.4s of sync
    /// Key Vault I/O while other threads queue behind it.
    ///
    /// Activation: only when <c>keyCacheTimeToLive &gt;= 1 hour</c>.
    /// Refresh entries are processed serially to comply with Azure Key Vault RPS limits.
    /// </summary>
    internal sealed class PdekCacheRefreshWorker : IDisposable
    {
        private const int MaxRetryAttemptsOn429 = 5;
        private const double RefreshWindowFraction = 0.9;

        private static readonly TimeSpan MaxScanInterval = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan SemaphoreAcquireTimeout = TimeSpan.FromSeconds(30);

        private readonly ConcurrentDictionary<string, RefreshEntry> trackedEntries;
        private readonly EncryptionCosmosClient encryptionCosmosClient;
        private readonly TimeSpan keyCacheTimeToLive;
        private readonly TimeSpan scanInterval;
        private readonly CancellationTokenSource cts;
        private readonly Task workerTask;
        private readonly Random jitterRandom;
        private int disposed;

        internal PdekCacheRefreshWorker(
            EncryptionCosmosClient encryptionCosmosClient,
            TimeSpan keyCacheTimeToLive)
        {
            this.encryptionCosmosClient = encryptionCosmosClient ?? throw new ArgumentNullException(nameof(encryptionCosmosClient));
            this.keyCacheTimeToLive = keyCacheTimeToLive;
            this.scanInterval = TimeSpan.FromSeconds(
                Math.Min(keyCacheTimeToLive.TotalSeconds * 0.1, MaxScanInterval.TotalSeconds));
            this.trackedEntries = new ConcurrentDictionary<string, RefreshEntry>();
            this.cts = new CancellationTokenSource();
            this.jitterRandom = new Random();
            this.workerTask = Task.Run(() => this.ScanAndRefreshLoopAsync(this.cts.Token));
        }

        // Visible for testing: allows tests to wait for the worker loop to complete after disposal.
        internal Task WorkerTask => this.workerTask;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref this.disposed, 1) == 0)
            {
                this.cts.Cancel();
                this.cts.Dispose();
            }
        }

        /// <summary>
        /// Registers or updates a PDEK cache entry for proactive background refresh tracking.
        /// Called after every successful <see cref="ProtectedDataEncryptionKey.GetOrCreate"/> to keep
        /// the tracked set current.
        /// </summary>
        internal void TrackEntry(
            string clientEncryptionKeyId,
            EncryptionContainer encryptionContainer,
            string databaseRid)
        {
            if (this.disposed == 1)
            {
                return;
            }

            string key = BuildTrackingKey(databaseRid, clientEncryptionKeyId);
            TimeSpan effectiveTtl = ProtectedDataEncryptionKey.TimeToLive;

            this.trackedEntries.AddOrUpdate(
                key,
                _ => new RefreshEntry
                {
                    ClientEncryptionKeyId = clientEncryptionKeyId,
                    EncryptionContainer = encryptionContainer,
                    DatabaseRid = databaseRid,
                    CreatedAtUtc = DateTime.UtcNow,
                    JitterOffset = this.GenerateJitter(effectiveTtl),
                },
                (_, existing) =>
                {
                    // Only update CreatedAtUtc if the entry has likely expired and was just recreated
                    // (cache miss). If this is a cache hit, don't reset the creation time -- that would
                    // delay the background refresh indefinitely.
                    if (DateTime.UtcNow >= existing.CreatedAtUtc + effectiveTtl)
                    {
                        existing.CreatedAtUtc = DateTime.UtcNow;
                        existing.JitterOffset = this.GenerateJitter(effectiveTtl);
                    }

                    // Always update the container reference in case the caller's container changed
                    existing.EncryptionContainer = encryptionContainer;
                    return existing;
                });
        }

        private static TimeSpan GetBackoffDuration(RequestFailedException ex, int retryAttempt)
        {
            // Try to read Retry-After header
            Response response = ex.GetRawResponse();
            if (response != null && response.Headers.TryGetValue("Retry-After", out string retryAfterValue)
                && int.TryParse(retryAfterValue, out int retryAfterSeconds)
                && retryAfterSeconds > 0)
            {
                return TimeSpan.FromSeconds(retryAfterSeconds);
            }

            // Exponential backoff: 1s, 2s, 4s, 8s, 16s, capped at 30s
            double seconds = Math.Min(30.0, Math.Pow(2, retryAttempt - 1));
            return TimeSpan.FromSeconds(seconds);
        }

        private static string BuildTrackingKey(string databaseRid, string clientEncryptionKeyId)
        {
            return databaseRid + "|" + clientEncryptionKeyId;
        }

        private async Task ScanAndRefreshLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(this.scanInterval, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                try
                {
                    await this.ScanAndRefreshEntriesAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception)
                {
                    // Swallow unexpected exceptions to prevent worker death.
                    // The entry will be retried on the next scan cycle.
                }
            }
        }

        private async Task ScanAndRefreshEntriesAsync(CancellationToken ct)
        {
            TimeSpan effectiveTtl = ProtectedDataEncryptionKey.TimeToLive;

            foreach (System.Collections.Generic.KeyValuePair<string, RefreshEntry> kvp in this.trackedEntries)
            {
                if (ct.IsCancellationRequested)
                {
                    break;
                }

                RefreshEntry entry = kvp.Value;
                DateTime refreshThreshold = entry.CreatedAtUtc
                    + TimeSpan.FromSeconds(effectiveTtl.TotalSeconds * RefreshWindowFraction)
                    + entry.JitterOffset;

                if (DateTime.UtcNow >= refreshThreshold)
                {
                    await this.RefreshSingleEntryAsync(entry, effectiveTtl, ct).ConfigureAwait(false);
                }

                // Prune entries not refreshed for > 3x TTL (stale/unused keys)
                if (DateTime.UtcNow >= entry.CreatedAtUtc + TimeSpan.FromSeconds(effectiveTtl.TotalSeconds * 3))
                {
                    this.trackedEntries.TryRemove(kvp.Key, out _);
                }
            }
        }

        private async Task RefreshSingleEntryAsync(
            RefreshEntry entry,
            TimeSpan effectiveTtl,
            CancellationToken ct)
        {
            int retryAttempt = 0;

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    // Step 1: Fetch latest CEK properties (fast -- from AsyncCache)
                    ClientEncryptionKeyProperties cekProperties =
                        await this.encryptionCosmosClient.GetClientEncryptionKeyPropertiesAsync(
                            clientEncryptionKeyId: entry.ClientEncryptionKeyId,
                            encryptionContainer: entry.EncryptionContainer,
                            databaseRid: entry.DatabaseRid,
                            ifNoneMatchEtag: null,
                            shouldForceRefresh: false,
                            cancellationToken: ct).ConfigureAwait(false);

                    // Step 2: Key Vault I/O OUTSIDE the semaphore (async calls)
                    string kekUrl = cekProperties.EncryptionKeyWrapMetadata.Value;
                    IKeyEncryptionKey resolvedKek = await this.encryptionCosmosClient.KeyEncryptionKeyResolver
                        .ResolveAsync(kekUrl, ct).ConfigureAwait(false);
                    byte[] unwrappedDek = await resolvedKek.UnwrapKeyAsync(
                        EncryptionKeyStoreProviderImpl.RsaOaepWrapAlgorithm,
                        cekProperties.WrappedDataEncryptionKey,
                        ct).ConfigureAwait(false);

                    // Step 3: Acquire semaphore (with timeout to prevent deadlock)
                    if (!await EncryptionCosmosClient.EncryptionKeyCacheSemaphore
                        .WaitAsync(SemaphoreAcquireTimeout, ct).ConfigureAwait(false))
                    {
                        // Could not acquire semaphore; skip this entry, retry next cycle
                        return;
                    }

                    try
                    {
                        // Step 4: Get the MDE KeyEncryptionKey from cache (fast -- cache hit)
                        KeyEncryptionKey kek = KeyEncryptionKey.GetOrCreate(
                            cekProperties.EncryptionKeyWrapMetadata.Name,
                            cekProperties.EncryptionKeyWrapMetadata.Value,
                            this.encryptionCosmosClient.EncryptionKeyStoreProviderImpl);

                        // Step 5: Directly set the refreshed PDEK in cache (microseconds)
                        ProtectedDataEncryptionKey.SetInCache(
                            entry.ClientEncryptionKeyId,
                            kek,
                            cekProperties.WrappedDataEncryptionKey,
                            unwrappedDek);
                    }
                    finally
                    {
                        EncryptionCosmosClient.EncryptionKeyCacheSemaphore.Release(1);
                    }

                    // Step 6: Update tracking
                    entry.CreatedAtUtc = DateTime.UtcNow;
                    entry.JitterOffset = this.GenerateJitter(effectiveTtl);

                    return; // Success
                }
                catch (RequestFailedException ex) when (ex.Status == 429)
                {
                    retryAttempt++;
                    if (retryAttempt > MaxRetryAttemptsOn429)
                    {
                        return; // Give up, retry next scan cycle
                    }

                    TimeSpan backoff = GetBackoffDuration(ex, retryAttempt);
                    try
                    {
                        await Task.Delay(backoff, ct).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }

                    // Loop retries the same entry
                }
                catch (RequestFailedException ex) when (ex.Status == 403)
                {
                    // KEK revoked: remove from tracking so the hot-path thread triggers
                    // the existing force-refresh flow in BuildEncryptionAlgorithmForSettingAsync.
                    string trackingKey = BuildTrackingKey(entry.DatabaseRid, entry.ClientEncryptionKeyId);
                    this.trackedEntries.TryRemove(trackingKey, out _);
                    return;
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception)
                {
                    // Transient error: skip entry, will be retried on next scan cycle
                    return;
                }
            }
        }

        private TimeSpan GenerateJitter(TimeSpan effectiveTtl)
        {
            // Random offset within the refresh window (last refreshWindow% of TTL)
            double refreshWindowSeconds = effectiveTtl.TotalSeconds * (1.0 - RefreshWindowFraction);
            double jitterSeconds;
            lock (this.jitterRandom)
            {
                jitterSeconds = this.jitterRandom.NextDouble() * refreshWindowSeconds;
            }

            return TimeSpan.FromSeconds(jitterSeconds);
        }

        /// <summary>
        /// Tracks a PDEK cache entry for proactive background refresh.
        /// </summary>
        private class RefreshEntry
        {
            public string ClientEncryptionKeyId { get; set; }

            public EncryptionContainer EncryptionContainer { get; set; }

            public string DatabaseRid { get; set; }

            public DateTime CreatedAtUtc { get; set; }

            public TimeSpan JitterOffset { get; set; }
        }
    }
}
