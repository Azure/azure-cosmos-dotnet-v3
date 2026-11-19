// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Collections.Concurrent;
    using System.Globalization;
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
        private const double DefaultRefreshWindowFraction = 0.9;
        private const string MaxScanIntervalEnvVar = "COSMOS_PDEK_BG_REFRESH_MAX_SCAN_INTERVAL_SECONDS";
        private const string RefreshWindowFractionEnvVar = "COSMOS_PDEK_BG_REFRESH_WINDOW_FRACTION";

        private static readonly TimeSpan DefaultMaxScanInterval = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan SemaphoreAcquireTimeout = TimeSpan.FromSeconds(30);

        // Worst-case Key Vault latency budget used to derive the safety margin that keeps a
        // refresh from landing on or after the TTL boundary. 5s is generous for regional AKV.
        private static readonly TimeSpan MaxKeyVaultLatencyBudget = TimeSpan.FromSeconds(5);

        // Sum of exponential backoff caps for the 5 retry attempts: 1+2+4+8+16 = 31s, rounded up.
        private static readonly TimeSpan MaxCumulativeBackoff = TimeSpan.FromSeconds(35);

        private readonly ConcurrentDictionary<string, RefreshEntry> trackedEntries;
        private readonly EncryptionCosmosClient encryptionCosmosClient;
        private readonly TimeSpan keyCacheTimeToLive;
        private readonly TimeSpan scanInterval;
        private readonly TimeSpan maxScanInterval;
        private readonly TimeSpan refreshSafetyMargin;
        private readonly double refreshWindowFraction;
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
            this.maxScanInterval = ReadTimeSpanFromEnv(MaxScanIntervalEnvVar, DefaultMaxScanInterval);
            this.refreshWindowFraction = ReadDoubleFromEnv(RefreshWindowFractionEnvVar, DefaultRefreshWindowFraction);
            this.scanInterval = TimeSpan.FromSeconds(
                Math.Min(keyCacheTimeToLive.TotalSeconds * 0.1, this.maxScanInterval.TotalSeconds));

            // Safety margin ensures a refresh started at the threshold has runway to complete
            // (I/O + full 429 backoff + one extra scan) before the entry actually expires. Never
            // consume more than half the TTL for margin — small TTLs still get a refresh window.
            TimeSpan rawMargin = MaxKeyVaultLatencyBudget + MaxCumulativeBackoff + this.scanInterval;
            this.refreshSafetyMargin = TimeSpan.FromSeconds(
                Math.Min(rawMargin.TotalSeconds, keyCacheTimeToLive.TotalSeconds * 0.5));

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

                // Intentionally do NOT dispose the CTS here. The background worker holds the token
                // and may still be awaiting Task.Delay(...ct) or WaitAsync(...ct) at the moment of
                // disposal; disposing the CTS synchronously races those awaits and raises
                // ObjectDisposedException, which faults the worker task. A CTS with no timer or
                // registrations is safe to leave to GC.
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
            DateTime nowUtc = DateTime.UtcNow;

            this.trackedEntries.AddOrUpdate(
                key,
                _ => new RefreshEntry
                {
                    ClientEncryptionKeyId = clientEncryptionKeyId,
                    EncryptionContainer = encryptionContainer,
                    DatabaseRid = databaseRid,
                    CreatedAtUtc = nowUtc,
                    LastHotPathTouchUtc = nowUtc,
                    JitterOffset = this.GenerateJitter(this.keyCacheTimeToLive),
                },
                (_, existing) =>
                {
                    // Track hot-path usage separately from refresh time so the prune heuristic
                    // ("no hot-path touch for 3x TTL") isn't defeated by the worker's own refreshes.
                    existing.LastHotPathTouchUtc = nowUtc;

                    // Only update CreatedAtUtc if the entry has likely expired and was just
                    // recreated (cache miss). If this is a cache hit, don't reset the creation
                    // time -- that would delay the background refresh indefinitely.
                    if (nowUtc >= existing.CreatedAtUtc + this.keyCacheTimeToLive)
                    {
                        existing.CreatedAtUtc = nowUtc;
                        existing.JitterOffset = this.GenerateJitter(this.keyCacheTimeToLive);
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

        private static TimeSpan ReadTimeSpanFromEnv(string envVarName, TimeSpan defaultValue)
        {
            string value = Environment.GetEnvironmentVariable(envVarName);
            if (!string.IsNullOrWhiteSpace(value)
                && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds)
                && seconds > 0)
            {
                return TimeSpan.FromSeconds(seconds);
            }

            return defaultValue;
        }

        private static double ReadDoubleFromEnv(string envVarName, double defaultValue)
        {
            string value = Environment.GetEnvironmentVariable(envVarName);
            if (!string.IsNullOrWhiteSpace(value)
                && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
                && parsed > 0
                && parsed < 1.0)
            {
                return parsed;
            }

            return defaultValue;
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
                catch (ObjectDisposedException)
                {
                    // Race with Dispose(): the CTS backing the token may be disposed while we are
                    // awaiting Task.Delay. Treat as terminal, like cancellation.
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
                catch (ObjectDisposedException)
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
            // Use the TTL captured at construction rather than the process-global static
            // ProtectedDataEncryptionKey.TimeToLive, which can be lowered by another
            // EncryptionCosmosClient with a different TTL and cause scan cadence and refresh
            // window to disagree.
            TimeSpan effectiveTtl = this.keyCacheTimeToLive;

            foreach (System.Collections.Generic.KeyValuePair<string, RefreshEntry> kvp in this.trackedEntries)
            {
                if (ct.IsCancellationRequested)
                {
                    break;
                }

                RefreshEntry entry = kvp.Value;

                // The refresh threshold is the earlier of (a) the fractional window + jitter,
                // and (b) the entry's expiry minus a safety margin large enough to fit a full
                // KV roundtrip + 429 backoff + one scan interval. This guarantees a refresh
                // started at the threshold has runway to *complete* before actual expiry,
                // eliminating the "refresh lands on/after expiry" cold-miss window.
                DateTime windowThreshold = entry.CreatedAtUtc
                    + TimeSpan.FromSeconds(effectiveTtl.TotalSeconds * this.refreshWindowFraction)
                    + entry.JitterOffset;
                DateTime hardDeadline = entry.CreatedAtUtc + effectiveTtl - this.refreshSafetyMargin;
                DateTime refreshThreshold = windowThreshold < hardDeadline ? windowThreshold : hardDeadline;

                if (DateTime.UtcNow >= refreshThreshold)
                {
                    await this.RefreshSingleEntryAsync(entry, effectiveTtl, ct).ConfigureAwait(false);
                }

                // Prune entries whose *hot path* has not touched them for > 3x TTL. Note this
                // is intentionally distinct from the refresh time (CreatedAtUtc), which the
                // worker resets on every successful refresh -- if we used CreatedAtUtc the
                // prune would never fire for any successfully-refreshing entry.
                if (DateTime.UtcNow >= entry.LastHotPathTouchUtc + TimeSpan.FromSeconds(effectiveTtl.TotalSeconds * 3))
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
                    // KEK revoked. The cached PDEK is still non-expired, so the hot path will
                    // keep returning it (and successfully decrypting) until natural TTL. That's
                    // a stale-key window that can be as long as the full TTL. Actively evict the
                    // PDEK from the static cache so the next hot-path access misses and re-runs
                    // the standard build flow, which will then observe the 403 from Key Vault.
                    //
                    // Best-effort: to evict we need the same KEK identity used at cache-insert
                    // time. Fetch the current CEK properties (fast, from AsyncCache), reconstruct
                    // the KEK under the semaphore, then remove. If any of that fails we still
                    // remove from tracking so we don't re-arm the stale entry with SetInCache.
                    try
                    {
                        ClientEncryptionKeyProperties cekPropsForEvict =
                            await this.encryptionCosmosClient.GetClientEncryptionKeyPropertiesAsync(
                                clientEncryptionKeyId: entry.ClientEncryptionKeyId,
                                encryptionContainer: entry.EncryptionContainer,
                                databaseRid: entry.DatabaseRid,
                                ifNoneMatchEtag: null,
                                shouldForceRefresh: false,
                                cancellationToken: ct).ConfigureAwait(false);

                        if (await EncryptionCosmosClient.EncryptionKeyCacheSemaphore
                            .WaitAsync(SemaphoreAcquireTimeout, ct).ConfigureAwait(false))
                        {
                            try
                            {
                                KeyEncryptionKey kekForEvict = KeyEncryptionKey.GetOrCreate(
                                    cekPropsForEvict.EncryptionKeyWrapMetadata.Name,
                                    cekPropsForEvict.EncryptionKeyWrapMetadata.Value,
                                    this.encryptionCosmosClient.EncryptionKeyStoreProviderImpl);

                                ProtectedDataEncryptionKey.RemoveFromCache(
                                    entry.ClientEncryptionKeyId,
                                    kekForEvict,
                                    cekPropsForEvict.WrappedDataEncryptionKey);
                            }
                            finally
                            {
                                EncryptionCosmosClient.EncryptionKeyCacheSemaphore.Release(1);
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // Best-effort eviction: on failure the hot path still eventually
                        // observes the 403 when the PDEK's own TTL expires. We still remove
                        // from tracking below so the worker will not re-extend the entry.
                    }

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
            double refreshWindowSeconds = effectiveTtl.TotalSeconds * (1.0 - this.refreshWindowFraction);
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

            /// <summary>
            /// Gets or sets the wall-clock time at which the underlying PDEK's TTL clock started
            /// (i.e., when <see cref="ProtectedDataEncryptionKey.SetInCache"/> was last called for
            /// this key, or when the hot path first created it). Used to schedule the next refresh.
            /// </summary>
            public DateTime CreatedAtUtc { get; set; }

            /// <summary>
            /// Gets or sets the wall-clock time of the last hot-path <see cref="TrackEntry"/> call.
            /// Used only for prune decisions so that keys the worker keeps refreshing but that no
            /// hot-path caller is actually using are eventually evicted. Refresh does NOT update this.
            /// </summary>
            public DateTime LastHotPathTouchUtc { get; set; }

            public TimeSpan JitterOffset { get; set; }
        }
    }
}
