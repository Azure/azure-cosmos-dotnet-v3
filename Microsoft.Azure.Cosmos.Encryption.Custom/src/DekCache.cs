//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Caching.Distributed;
    using Newtonsoft.Json;

    internal sealed class DekCache : IDisposable, IAsyncDisposable
    {
        // Embedded in the cache key (so mixed-version fleets cannot downgrade each other) and
        // in the serialised payload (so a peer can reject a blob whose shape it does not
        // understand). Bump whenever CachedDekPropertiesDto changes shape.
        private const int CurrentCacheFormatVersion = 1;

        private static readonly TimeSpan DisposeDrainTimeout = TimeSpan.FromSeconds(5);

        private readonly TimeSpan dekPropertiesTimeToLive;
        private readonly TimeSpan distributedCacheEntryLifetime;
        private readonly TimeSpan? refreshBeforeExpiry;
        private readonly IDistributedCache distributedCache;
        private readonly string cacheKeyPrefix;
        private readonly Func<DateTime> utcNow;

        private readonly CancellationTokenSource disposalCts = new CancellationTokenSource();

        // Tracks every in-flight fire-and-forget write so Dispose can drain. The companion
        // ContinueWith observes the task's exception and removes it from this dictionary.
        private readonly ConcurrentDictionary<Task, byte> inFlightWrites = new ConcurrentDictionary<Task, byte>();

        private int isDisposed;

        private static readonly ActivitySource ActivitySource = new ("Microsoft.Azure.Cosmos.Encryption.Custom");

        internal AsyncCache<string, CachedDekProperties> DekPropertiesCache { get; } = new AsyncCache<string, CachedDekProperties>();

        internal AsyncCache<string, InMemoryRawDek> RawDekCache { get; } = new AsyncCache<string, InMemoryRawDek>();

        /// <summary>
        /// Test helper: returns a <see cref="Task"/> that completes when every fire-and-forget
        /// distributed-cache write currently in flight has finished. Returns <see cref="Task.CompletedTask"/>
        /// when none are pending.
        /// </summary>
        internal Task WhenAllPendingWritesAsync()
        {
            Task[] pending = this.inFlightWrites.Keys.ToArray();
            return pending.Length == 0 ? Task.CompletedTask : Task.WhenAll(pending);
        }

        public DekCache(
            TimeSpan? dekPropertiesTimeToLive = null,
            IDistributedCache distributedCache = null,
            TimeSpan? refreshBeforeExpiry = null,
            string cacheKeyPrefix = null,
            Func<DateTime> utcNow = null,
            TimeSpan? distributedCacheEntryLifetime = null)
        {
            this.dekPropertiesTimeToLive = dekPropertiesTimeToLive.HasValue == true ? dekPropertiesTimeToLive.Value : TimeSpan.FromMinutes(Constants.DekPropertiesDefaultTTLInMinutes);

            // Distributed cache entries live longer than L1 so a peer-populated entry can rescue
            // a request after L1 expiry. Default = 2x the L1 TTL.
            this.distributedCacheEntryLifetime = distributedCacheEntryLifetime
                ?? TimeSpan.FromTicks(this.dekPropertiesTimeToLive.Ticks * 2);
            if (this.distributedCacheEntryLifetime <= this.dekPropertiesTimeToLive)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(distributedCacheEntryLifetime),
                    "distributedCacheEntryLifetime must be strictly greater than dekPropertiesTimeToLive so that L2 entries outlive L1 expiry.");
            }

            // Prefix is required when distributed caching is enabled (so providers sharing one
            // cache cannot collide) and rejected when it is not (the prefix would be dead state
            // and silently accepting it would hide misconfiguration).
            if (distributedCache != null)
            {
                ArgumentValidation.ThrowIfNullOrWhiteSpace(cacheKeyPrefix, nameof(cacheKeyPrefix));
            }
            else if (cacheKeyPrefix != null)
            {
                throw new ArgumentException(
                    $"'{nameof(cacheKeyPrefix)}' can only be specified when '{nameof(distributedCache)}' is provided.",
                    nameof(cacheKeyPrefix));
            }

            if (refreshBeforeExpiry.HasValue)
            {
                ArgumentValidation.ThrowIfNegative(refreshBeforeExpiry.Value, nameof(refreshBeforeExpiry));
                ArgumentValidation.ThrowIfGreaterThanOrEqual(refreshBeforeExpiry.Value, this.dekPropertiesTimeToLive, nameof(refreshBeforeExpiry));
            }

            this.distributedCache = distributedCache;
            this.refreshBeforeExpiry = refreshBeforeExpiry;
            this.cacheKeyPrefix = cacheKeyPrefix;
            this.utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref this.isDisposed) != 0)
            {
                throw new ObjectDisposedException(nameof(DekCache));
            }
        }

        public async Task<DataEncryptionKeyProperties> GetOrAddDekPropertiesAsync(
            string dekId,
            Func<string, CosmosDiagnosticsContext, CancellationToken, Task<DataEncryptionKeyProperties>> fetcher,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            this.ThrowIfDisposed();

            using (Activity activity = ActivitySource.StartActivity("DekCache.GetOrAddProperties"))
            {
                activity?.SetTag("cache.system", "cosmos.encryption.dek");
                activity?.SetTag("cache.key", dekId);

                CachedDekProperties cachedDekProperties = await this.DekPropertiesCache.GetAsync(
                        dekId,
                        null,
                        () => this.FetchDekPropertiesAsync(dekId, fetcher, diagnosticsContext, cancellationToken),
                        cancellationToken).ConfigureAwait(false);

                if (cachedDekProperties.ServerPropertiesExpiryUtc <= this.utcNow())
                {
                    activity?.SetTag("cache.expired", true);
                    activity?.SetTag("cache.operation", "refresh");

                    // Route the refresh through FetchDekPropertiesAsync (which consults L2 first)
                    // so a peer-populated L2 entry can rescue the caller when Cosmos metadata is
                    // momentarily unavailable.
                    cachedDekProperties = await this.DekPropertiesCache.GetAsync(
                        dekId,
                        null,
                        () => this.FetchDekPropertiesAsync(dekId, fetcher, diagnosticsContext, cancellationToken),
                        cancellationToken,
                        forceRefresh: true).ConfigureAwait(false);
                }
                else if (this.ShouldProactivelyRefresh(cachedDekProperties))
                {
                    activity?.SetTag("cache.proactive_refresh", true);

                    // Background refresh; pass the disposal token so a stale BackgroundRefresh
                    // task surviving the owning provider can be cancelled on shutdown.
                    this.DekPropertiesCache.BackgroundRefreshNonBlocking(
                        dekId,
                        () => this.FetchFromSourceAndUpdateCachesAsync(dekId, fetcher, diagnosticsContext, this.disposalCts.Token));
                }

                return cachedDekProperties.ServerProperties;
            }
        }

        public async Task<InMemoryRawDek> GetOrAddRawDekAsync(
            DataEncryptionKeyProperties dekProperties,
            Func<DataEncryptionKeyProperties, CosmosDiagnosticsContext, CancellationToken, Task<InMemoryRawDek>> unwrapper,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            this.ThrowIfDisposed();

            InMemoryRawDek inMemoryRawDek = await this.RawDekCache.GetAsync(
                   dekProperties.SelfLink,
                   null,
                   () => unwrapper(dekProperties, diagnosticsContext, cancellationToken),
                   cancellationToken).ConfigureAwait(false);

            if (inMemoryRawDek.RawDekExpiry <= this.utcNow())
            {
                inMemoryRawDek = await this.RawDekCache.GetAsync(
                   dekProperties.SelfLink,
                   null,
                   () => unwrapper(dekProperties, diagnosticsContext, cancellationToken),
                   cancellationToken,
                   forceRefresh: true).ConfigureAwait(false);
            }

            return inMemoryRawDek;
        }

        /// <summary>
        /// Updates the in-memory cache synchronously and schedules a fire-and-forget write to
        /// the distributed cache (if configured).
        /// </summary>
        /// <param name="dekId">The DEK identifier.</param>
        /// <param name="dekProperties">The DEK properties to cache.</param>
        /// <remarks>
        /// Background distributed-cache writes are tracked so <see cref="Dispose"/> can drain
        /// them; failures surface on <see cref="EncryptionCustomEventSource"/>. Concurrent
        /// rapid updates to the same DEK can produce out-of-order L2 writes (last-completer
        /// wins). All cache reads validate expiration so staleness is bounded; for strict
        /// consistency during DEK rotation, call <see cref="RemoveAsync"/> before re-reading.
        /// </remarks>
        public void SetDekProperties(string dekId, DataEncryptionKeyProperties dekProperties)
        {
            this.ThrowIfDisposed();

            using (Activity activity = ActivitySource.StartActivity("DekCache.SetProperties"))
            {
                activity?.SetTag("cache.system", "cosmos.encryption.dek");
                activity?.SetTag("cache.key", dekId);

                CachedDekProperties cachedDekProperties = new (dekProperties, this.utcNow() + this.dekPropertiesTimeToLive);

                this.DekPropertiesCache.Set(dekId, cachedDekProperties);
                activity?.SetTag("cache.memory.updated", true);

                if (this.distributedCache != null)
                {
                    activity?.SetTag("cache.distributed.enabled", true);
                    this.UpdateDistributedCacheInBackground(dekId, cachedDekProperties);
                }
                else
                {
                    activity?.SetTag("cache.distributed.enabled", false);
                }
            }
        }

        /// <summary>
        /// Schedules a non-blocking write to the distributed cache. The returned task is
        /// tracked so disposal can drain it; failures surface on
        /// <see cref="EncryptionCustomEventSource"/>. Pairs with the awaiting
        /// <see cref="UpdateDistributedCacheAsync"/>.
        /// </summary>
        private void UpdateDistributedCacheInBackground(string dekId, CachedDekProperties cachedDekProperties)
        {
            // Task.Run intentionally does NOT receive the disposal token: the lambda checks the
            // token explicitly and treats post-disposal cancellation as a clean no-op so the
            // tracked Task always finishes RanToCompletion (never Canceled / Faulted).
            Task write = Task.Run(async () =>
            {
                using (Activity dcActivity = ActivitySource.StartActivity("DekCache.UpdateDistributedCache"))
                {
                    dcActivity?.SetTag("cache.system", "cosmos.encryption.dek");
                    dcActivity?.SetTag("cache.key", dekId);
                    dcActivity?.SetTag("cache.operation", "write");

                    CancellationToken token = this.disposalCts.Token;
                    if (token.IsCancellationRequested)
                    {
                        dcActivity?.SetTag("cache.result", "skipped-disposed");
                        return;
                    }

                    try
                    {
                        await this.UpdateDistributedCacheAsync(dekId, cachedDekProperties, token).ConfigureAwait(false);
                        dcActivity?.SetTag("cache.result", "success");
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        dcActivity?.SetTag("cache.result", "cancelled-disposed");
                    }
                    catch (Exception ex)
                    {
                        dcActivity?.SetTag("cache.result", "failure");
                        dcActivity?.SetTag("cache.error", ex.GetType().Name);
                        dcActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                        EncryptionCustomEventSource.DistributedCacheBackgroundWriteFailed(dekId, ex);
                    }
                }
            });

            this.inFlightWrites.TryAdd(write, 0);

            // Static lambda + state object keeps the continuation from rooting `this`.
            // ExecuteSynchronously is safe: bookkeeping only, no exceptions.
            _ = write.ContinueWith(
                static (t, state) =>
                {
                    _ = t.Exception; // observe to suppress UnobservedTaskException
                    ((ConcurrentDictionary<Task, byte>)state).TryRemove(t, out _);
                },
                this.inFlightWrites,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        public void SetRawDek(string dekId, InMemoryRawDek inMemoryRawDek)
        {
            this.ThrowIfDisposed();
            this.RawDekCache.Set(dekId, inMemoryRawDek);
        }

        /// <summary>
        /// Removes a DEK from the in-memory and (if configured) distributed caches.
        /// </summary>
        /// <param name="dekId">The DEK identifier to invalidate.</param>
        /// <param name="cancellationToken">
        /// Optional caller token. NOTE: this is the CALLER's token, not the disposal token —
        /// disposal MUST NOT abort RemoveAsync, otherwise stale entries would survive in L2.
        /// </param>
        public async Task RemoveAsync(string dekId, CancellationToken cancellationToken = default)
        {
            this.ThrowIfDisposed();

            CachedDekProperties cachedDekProperties = await this.DekPropertiesCache.RemoveAsync(dekId).ConfigureAwait(false);

            // Always evict raw under both candidate keys; another instance may have populated
            // either entry independently.
            this.RemoveRawDek(dekId, cachedDekProperties?.ServerProperties);

            if (this.distributedCache != null)
            {
                try
                {
                    await this.distributedCache.RemoveAsync(this.GetDistributedCacheKey(dekId), cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    EncryptionCustomEventSource.DistributedCacheRemoveFailed(dekId, ex);
                }
            }
        }

        /// <summary>
        /// Removes RawDekCache entries under both candidate keys: <see cref="SetRawDek"/>
        /// uses <c>dekId</c>, while <see cref="GetOrAddRawDekAsync"/> uses
        /// <see cref="DataEncryptionKeyProperties.SelfLink"/>.
        /// </summary>
        private void RemoveRawDek(string dekId, DataEncryptionKeyProperties dekProperties)
        {
            if (!string.IsNullOrEmpty(dekId))
            {
                this.RawDekCache.Remove(dekId);
            }

            string selfLink = dekProperties?.SelfLink;
            if (!string.IsNullOrEmpty(selfLink) && !string.Equals(selfLink, dekId, StringComparison.Ordinal))
            {
                this.RawDekCache.Remove(selfLink);
            }
        }

        /// <summary>
        /// Memory cache → distributed cache → source (Cosmos).
        /// </summary>
        private async Task<CachedDekProperties> FetchDekPropertiesAsync(
            string dekId,
            Func<string, CosmosDiagnosticsContext, CancellationToken, Task<DataEncryptionKeyProperties>> fetcher,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            using (Activity activity = ActivitySource.StartActivity("DekCache.FetchProperties"))
            {
                activity?.SetTag("cache.system", "cosmos.encryption.dek");
                activity?.SetTag("cache.key", dekId);

                CachedDekProperties cachedProperties = await this.TryGetFromDistributedCacheAsync(dekId, cancellationToken).ConfigureAwait(false);
                if (cachedProperties != null)
                {
                    activity?.SetTag("cache.hit", true);
                    activity?.SetTag("cache.hit.level", "distributed");
                    return cachedProperties;
                }

                activity?.SetTag("cache.hit", false);
                activity?.SetTag("cache.miss", true);
                return await this.FetchFromSourceAndUpdateCachesAsync(dekId, fetcher, diagnosticsContext, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task<CachedDekProperties> TryGetFromDistributedCacheAsync(
            string dekId,
            CancellationToken cancellationToken)
        {
            if (this.distributedCache == null)
            {
                return null;
            }

            using (Activity activity = ActivitySource.StartActivity("DekCache.DistributedCacheRead"))
            {
                activity?.SetTag("cache.system", "cosmos.encryption.dek");
                activity?.SetTag("cache.key", dekId);
                activity?.SetTag("cache.operation", "read");

                Stopwatch stopwatch = Stopwatch.StartNew();

                try
                {
                    byte[] cachedBytes = await this.distributedCache.GetAsync(
                        this.GetDistributedCacheKey(dekId),
                        cancellationToken).ConfigureAwait(false);

                    stopwatch.Stop();
                    activity?.SetTag("cache.latency_ms", stopwatch.ElapsedMilliseconds);

                    if (cachedBytes != null)
                    {
                        CachedDekProperties cachedProps = DeserializeCachedDekProperties(cachedBytes);

                        // Restamp with a fresh L1 freshness horizon so the L1-expiry branch does
                        // not re-trigger on the very next call. The IDistributedCache's own
                        // AbsoluteExpiration bounds the maximum age at the store layer.
                        CachedDekProperties freshForL1 = new CachedDekProperties(
                            cachedProps.ServerProperties,
                            this.utcNow() + this.dekPropertiesTimeToLive);

                        activity?.SetTag("cache.result", "hit");
                        activity?.SetTag("cache.entry.valid", true);
                        return freshForL1;
                    }

                    activity?.SetTag("cache.result", "miss");
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    activity?.SetTag("cache.latency_ms", stopwatch.ElapsedMilliseconds);
                    activity?.SetTag("cache.result", "error");
                    activity?.SetTag("cache.error", ex.GetType().Name);
                    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

                    // OperationCanceledException raised by the IDistributedCache implementation's
                    // own internal timeout (caller token not cancelled) lands here too — surfacing
                    // it would defeat the fail-open contract of this optimization layer.
                    EncryptionCustomEventSource.DistributedCacheReadFailed(dekId, ex);
                }

                return null;
            }
        }

        private async Task<CachedDekProperties> FetchFromSourceAndUpdateCachesAsync(
            string dekId,
            Func<string, CosmosDiagnosticsContext, CancellationToken, Task<DataEncryptionKeyProperties>> fetcher,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            DataEncryptionKeyProperties serverProperties = await fetcher(dekId, diagnosticsContext, cancellationToken).ConfigureAwait(false);

            // Refuse to cache a result whose Id disagrees with the requested key — would
            // cross-pollinate the L1/L2 slot and hand peers the wrong material on lookup.
            if (serverProperties == null || !string.Equals(serverProperties.Id, dekId, StringComparison.Ordinal))
            {
                string returnedId = serverProperties?.Id ?? "<null>";
                throw new InvalidOperationException(
                    $"DEK fetcher returned a DataEncryptionKeyProperties with Id '{returnedId}' for requested dekId '{dekId}'. Refusing to cache mismatched properties.");
            }

            CachedDekProperties cachedProperties = new CachedDekProperties(
                serverProperties,
                this.utcNow() + this.dekPropertiesTimeToLive);

            // After a rewrap the wrapped-key bytes change but SelfLink does not, so a stale
            // RawDekCache entry would unwrap with old wrapped bytes. Best-effort eviction.
            this.RemoveRawDek(dekId, serverProperties);

            if (this.distributedCache != null)
            {
                this.UpdateDistributedCacheInBackground(dekId, cachedProperties);
            }

            return cachedProperties;
        }

        /// <summary>
        /// Writes the cached DEK properties to the distributed cache. Exceptions propagate to the
        /// caller so each call site can categorise the failure on the appropriate
        /// <see cref="EncryptionCustomEventSource"/> event.
        /// </summary>
        private async Task UpdateDistributedCacheAsync(
            string dekId,
            CachedDekProperties cachedProperties,
            CancellationToken cancellationToken)
        {
            if (this.distributedCache == null)
            {
                return;
            }

            byte[] serialized = SerializeCachedDekProperties(cachedProperties);

            // L2 absolute expiration outlives L1 so a peer's L2 entry can rescue this process
            // after its own L1 expiry.
            DateTime l2HardExpiry = this.utcNow() + this.distributedCacheEntryLifetime;

            await this.distributedCache.SetAsync(
                this.GetDistributedCacheKey(dekId),
                serialized,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpiration = new DateTimeOffset(l2HardExpiry, TimeSpan.Zero),
                },
                cancellationToken).ConfigureAwait(false);
        }

        private bool ShouldProactivelyRefresh(CachedDekProperties cached)
        {
            if (!this.refreshBeforeExpiry.HasValue)
            {
                return false;
            }

            DateTime refreshThreshold = cached.ServerPropertiesExpiryUtc - this.refreshBeforeExpiry.Value;
            return this.utcNow() >= refreshThreshold;
        }

        private string GetDistributedCacheKey(string dekId)
        {
            // Key shape: "{prefix}:v{version}:{escaped-dekId}".
            //  - prefix scopes the entry to one container (M6).
            //  - version isolates mixed-SDK-version fleets so a v1 reader cannot overwrite a v2
            //    writer's entry during a rolling upgrade (M4).
            //  - Uri.EscapeDataString on dekId prevents colon-collision between providers
            //    with differently-sized prefixes (M7).
            return $"{this.cacheKeyPrefix}:v{CurrentCacheFormatVersion}:{Uri.EscapeDataString(dekId)}";
        }

        private static readonly JsonSerializerSettings CacheSerializerSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
            ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver(),
        };

        private static byte[] SerializeCachedDekProperties(CachedDekProperties cachedProps)
        {
            // Create a DTO for serialization
            CachedDekPropertiesDto dto = new CachedDekPropertiesDto
            {
                ServerProperties = cachedProps.ServerProperties,
                ServerPropertiesExpiryUtc = cachedProps.ServerPropertiesExpiryUtc,
            };

            string json = JsonConvert.SerializeObject(dto, CacheSerializerSettings);
            return System.Text.Encoding.UTF8.GetBytes(json);
        }

        private static CachedDekProperties DeserializeCachedDekProperties(byte[] bytes)
        {
            string json = System.Text.Encoding.UTF8.GetString(bytes);
            CachedDekPropertiesDto dto = JsonConvert.DeserializeObject<CachedDekPropertiesDto>(json, CacheSerializerSettings);

            if (dto == null || dto.ServerProperties == null)
            {
                throw new InvalidOperationException("Failed to deserialize cached DEK properties or properties are null.");
            }

            // Newtonsoft deserialises DataEncryptionKeyProperties via its protected parameterless
            // ctor and sets fields through internal setters, bypassing the validating public ctor.
            // A partial payload (missing wrapped-key bytes or wrap metadata) would otherwise pass
            // through and only blow up later in the unwrap pipeline with a confusing NRE. Reject
            // incomplete entries here so the caller falls back to the authoritative source (Cosmos).
            DataEncryptionKeyProperties props = dto.ServerProperties;
            if (string.IsNullOrEmpty(props.Id)
                || props.WrappedDataEncryptionKey == null
                || props.EncryptionKeyWrapMetadata == null
                || string.IsNullOrEmpty(props.EncryptionAlgorithm))
            {
                throw new InvalidOperationException("Cached DEK properties are incomplete.");
            }

            if (dto.Version != CurrentCacheFormatVersion)
            {
                throw new InvalidOperationException(
                    $"Unsupported cache format version: {dto.Version}. Expected version {CurrentCacheFormatVersion}.");
            }

            return new CachedDekProperties(
                dto.ServerProperties,
                dto.ServerPropertiesExpiryUtc);
        }

        // DTO for serialization to distributed cache.
        // JsonProperty names are pinned to ensure cross-process interop
        // regardless of host-level JsonConvert.DefaultSettings.
        private sealed class CachedDekPropertiesDto
        {
            [JsonProperty("v")]
            public int Version { get; set; } = CurrentCacheFormatVersion;

            [JsonProperty("serverProperties")]
            public DataEncryptionKeyProperties ServerProperties { get; set; }

            [JsonProperty("serverPropertiesExpiryUtc")]
            public DateTime ServerPropertiesExpiryUtc { get; set; }
        }

        /// <summary>
        /// Cancels in-flight background distributed-cache writes and best-effort drains them
        /// up to <see cref="DisposeDrainTimeout"/>. Idempotent.
        /// </summary>
        /// <remarks>
        /// Synchronous Dispose blocks the calling thread on the drain. <see cref="DisposeAsync"/>
        /// is preferred. Dispose does NOT dispose the externally-supplied
        /// <see cref="IDistributedCache"/> — the caller owns that lifetime.
        /// </remarks>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref this.isDisposed, 1) != 0)
            {
                return;
            }

            this.disposalCts.Cancel();

            Task[] pending = this.inFlightWrites.Keys.ToArray();
            if (pending.Length > 0)
            {
                try
                {
                    // Bounded best-effort drain. Background writes that fail to complete in time
                    // are abandoned; the L1 cache is authoritative for the current process and
                    // peers will repopulate L2 on their next miss. The synchronous Wait is the
                    // accepted SDK precedent for IDisposable.Dispose drain (see
                    // BatchAsyncStreamer.Dispose) — DisposeAsync remains the preferred path for
                    // hosts that can avoid the block.
#pragma warning disable VSTHRD002 // sync Wait inside Dispose is intentional and bounded
                    Task.WhenAll(pending).Wait(DisposeDrainTimeout);
#pragma warning restore VSTHRD002
                }
                catch
                {
                    // Drain failures (faulted background writes / aggregate exceptions) have
                    // already been observed by the per-task ContinueWith. Swallow here so
                    // Dispose itself never throws.
                }
            }
        }

        /// <summary>
        /// Async counterpart of <see cref="Dispose"/>: cancels in-flight background writes and
        /// awaits a bounded drain instead of blocking. Idempotent.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref this.isDisposed, 1) != 0)
            {
                return;
            }

            this.disposalCts.Cancel();

            Task[] pending = this.inFlightWrites.Keys.ToArray();
            if (pending.Length > 0)
            {
                // netstandard2.0 lacks Task.WaitAsync(TimeSpan); use WhenAny + Task.Delay as a
                // bounded wait. The unobserved Task.Delay completes shortly after the timeout
                // and is GC-eligible; for an at-most-once Dispose path this is acceptable.
                Task drain = Task.WhenAll(pending);
                Task timeout = Task.Delay(DisposeDrainTimeout);
                Task winner = await Task.WhenAny(drain, timeout).ConfigureAwait(false);
                if (winner == drain)
                {
                    // Observe any aggregate exception from the drained tasks; per-task continuations
                    // already observed individual exceptions, so this is belt-and-braces.
                    try
                    {
                        await drain.ConfigureAwait(false);
                    }
                    catch
                    {
                    }
                }
            }
        }
    }
}
