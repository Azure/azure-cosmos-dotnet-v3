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
        // Cache-entry format version. Embedded both in the cache key (so mixed-version fleets
        // do not share a slot and therefore cannot downgrade each other) and in the serialised
        // payload (so a peer can reject a blob whose shape it does not understand). Bump this
        // whenever CachedDekPropertiesDto gains, loses, or changes the semantics of a field.
        private const int CurrentCacheFormatVersion = 1;

        // Bounded best-effort drain timeout used by Dispose / DisposeAsync. Background
        // distributed-cache writes that fail to complete within this window are abandoned —
        // the L1 cache is authoritative for the current process and peers will repopulate
        // L2 on their next miss, so abandonment never produces a correctness regression.
        private static readonly TimeSpan DisposeDrainTimeout = TimeSpan.FromSeconds(5);

        private readonly TimeSpan dekPropertiesTimeToLive;
        private readonly TimeSpan distributedCacheEntryLifetime;
        private readonly TimeSpan? refreshBeforeExpiry;
        private readonly IDistributedCache distributedCache;
        private readonly string cacheKeyPrefix;
        private readonly Func<DateTime> utcNow;

        private readonly CancellationTokenSource disposalCts = new CancellationTokenSource();

        // Captured once at construction so the IsCancellationRequested check survives even after
        // the CTS is disposed (CTS.Token throws ObjectDisposedException once disposed). We never
        // re-read disposalCts.Token after Dispose has run.
        private readonly CancellationToken disposalToken;

        // Tracks every in-flight fire-and-forget distributed-cache write so Dispose can drain
        // them. The companion ContinueWith both observes the task's exception (preventing
        // UnobservedTaskException) and removes it from this dictionary when it finishes.
        private readonly ConcurrentDictionary<Task, byte> inFlightWrites = new ConcurrentDictionary<Task, byte>();

        // 0 = alive, 1 = disposed. Updated via Interlocked.Exchange so the transition is atomic
        // and Dispose is idempotent across concurrent callers.
        private int isDisposed;

        private static readonly ActivitySource ActivitySource = new ("Microsoft.Azure.Cosmos.Encryption.Custom");

        // Internal for unit testing
        internal AsyncCache<string, CachedDekProperties> DekPropertiesCache { get; } = new AsyncCache<string, CachedDekProperties>();

        internal AsyncCache<string, InMemoryRawDek> RawDekCache { get; } = new AsyncCache<string, InMemoryRawDek>();

        // Exposed for deterministic test waiting on fire-and-forget distributed cache writes
        internal Task LastDistributedCacheWriteTask { get; private set; } = Task.CompletedTask;

        public DekCache(
            TimeSpan? dekPropertiesTimeToLive = null,
            IDistributedCache distributedCache = null,
            TimeSpan? refreshBeforeExpiry = null,
            string cacheKeyPrefix = null,
            Func<DateTime> utcNow = null,
            TimeSpan? distributedCacheEntryLifetime = null)
        {
            this.dekPropertiesTimeToLive = dekPropertiesTimeToLive.HasValue == true ? dekPropertiesTimeToLive.Value : TimeSpan.FromMinutes(Constants.DekPropertiesDefaultTTLInMinutes);

            // Distributed cache entries live strictly longer than the in-memory TTL so that a
            // peer-populated L2 entry can rescue a request after L1 expiry — this is the
            // resilience property the feature exists to provide. Default = 2x the L1 TTL.
            this.distributedCacheEntryLifetime = distributedCacheEntryLifetime
                ?? TimeSpan.FromTicks(this.dekPropertiesTimeToLive.Ticks * 2);
            if (this.distributedCacheEntryLifetime <= this.dekPropertiesTimeToLive)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(distributedCacheEntryLifetime),
                    "distributedCacheEntryLifetime must be strictly greater than dekPropertiesTimeToLive so that L2 entries outlive L1 expiry.");
            }

            // A distributed cache without a caller-supplied prefix is ambiguous: multiple
            // providers sharing a single cache would silently collide on identical dekIds. A
            // prefix is required whenever distributed caching is enabled so the caller must
            // consciously partition the keyspace (e.g. by container id, tenant id, etc.).
            //
            // When NO distributed cache is configured the prefix is dead state. Accepting a
            // non-default prefix silently would hide a misconfiguration (caller plumbed a
            // tenant discriminator that will never reach a cache); throwing only on empty /
            // whitespace and silently accepting valid strings would be the worst of both
            // worlds. Reject any non-null prefix in that case so the caller gets a clear
            // signal of misconfiguration.
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

            // Validate refreshBeforeExpiry
            if (refreshBeforeExpiry.HasValue)
            {
                ArgumentValidation.ThrowIfNegative(refreshBeforeExpiry.Value, nameof(refreshBeforeExpiry));
                ArgumentValidation.ThrowIfGreaterThanOrEqual(refreshBeforeExpiry.Value, this.dekPropertiesTimeToLive, nameof(refreshBeforeExpiry));
            }

            this.distributedCache = distributedCache;
            this.refreshBeforeExpiry = refreshBeforeExpiry;
            this.cacheKeyPrefix = cacheKeyPrefix;
            this.utcNow = utcNow ?? (() => DateTime.UtcNow);

            // Capture the disposal token once so post-Dispose IsCancellationRequested checks
            // never read disposalCts.Token (which would throw ObjectDisposedException).
            this.disposalToken = this.disposalCts.Token;
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

                    // Route the refresh through FetchDekPropertiesAsync (which consults L2 first),
                    // not FetchFromSourceAndUpdateCachesAsync (which goes straight to Cosmos). This
                    // is the resilience guarantee of the feature: if a peer populated L2 and
                    // Cosmos metadata is momentarily unavailable, the L1-expiry path must serve
                    // from L2 rather than failing the caller.
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

                    // Trigger background refresh without blocking caller.
                    // Pass the disposal token so a stale BackgroundRefresh task that survives the
                    // owning provider can be cancelled on shutdown rather than hammering Cosmos /
                    // L2 from a process that is on its way out.
                    this.DekPropertiesCache.BackgroundRefreshNonBlocking(
                        dekId,
                        () => this.FetchFromSourceAndUpdateCachesAsync(dekId, fetcher, diagnosticsContext, this.disposalToken));
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
        /// Sets DEK properties in both memory and distributed cache.
        /// </summary>
        /// <param name="dekId">The DEK identifier.</param>
        /// <param name="dekProperties">The DEK properties to cache.</param>
        /// <remarks>
        /// <para>
        /// Memory cache is updated synchronously to ensure immediate consistency for the current process,
        /// while distributed cache is updated asynchronously using a fire-and-forget pattern for performance.
        /// </para>
        /// <para>
        /// <strong>Known Limitation - Eventual Consistency:</strong>
        /// In rare scenarios involving rapid successive updates to the same DEK (e.g., multiple rewrap
        /// operations in quick succession), the distributed cache may temporarily contain stale data due
        /// to out-of-order completion of asynchronous update tasks.
        /// </para>
        /// <para>
        /// Example scenario:
        /// <code>
        /// T0: SetDekProperties("dek1", v1) → Memory: v1, Background Task A starts
        /// T1: SetDekProperties("dek1", v2) → Memory: v2, Background Task B starts
        /// T2: Task B completes → Distributed Cache: v2 ✓
        /// T3: Task A completes → Distributed Cache: v1 (stale) ⚠
        /// </code>
        /// </para>
        /// <para>
        /// <strong>Mitigations:</strong>
        /// <list type="bullet">
        /// <item>All cache reads validate expiration timestamps, preventing indefinite staleness</item>
        /// <item>Memory cache is always authoritative for the current process</item>
        /// <item>DEK updates are infrequent in typical workloads (created once, rarely modified)</item>
        /// <item>Distributed cache failures do not affect operation correctness</item>
        /// </list>
        /// </para>
        /// <para>
        /// For scenarios requiring strict consistency guarantees during DEK rotation, consider using
        /// <see cref="RemoveAsync"/> to explicitly invalidate the cache entry, followed by a
        /// fresh read operation to force synchronization across all cache layers.
        /// </para>
        /// </remarks>
        public void SetDekProperties(string dekId, DataEncryptionKeyProperties dekProperties)
        {
            this.ThrowIfDisposed();

            using (Activity activity = ActivitySource.StartActivity("DekCache.SetProperties"))
            {
                activity?.SetTag("cache.system", "cosmos.encryption.dek");
                activity?.SetTag("cache.key", dekId);

                CachedDekProperties cachedDekProperties = new (dekProperties, this.utcNow() + this.dekPropertiesTimeToLive);

                // Update memory cache
                this.DekPropertiesCache.Set(dekId, cachedDekProperties);
                activity?.SetTag("cache.memory.updated", true);

                // Update distributed cache if available (fire-and-forget).
                // Cold-path / refresh-path L2 writes (FetchFromSourceAndUpdateCachesAsync) and
                // SetDekProperties go through the same UpdateDistributedCacheInBackground
                // helper so they share the same lifecycle, drain semantics, and diagnostics
                // contract — the helper tracks the task in inFlightWrites so Dispose can drain
                // it, and surfaces failures on EncryptionCustomEventSource.
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
        /// Schedules a non-blocking write to the distributed cache and returns immediately.
        /// The returned task is tracked so disposal can drain it; failures surface on
        /// <see cref="EncryptionCustomEventSource"/>.
        /// </summary>
        /// <remarks>
        /// Pairs with <see cref="UpdateDistributedCacheAsync"/> (the awaiting helper). Both
        /// SetDekProperties and the cold-miss / forced-refresh path go through this helper
        /// so they share lifecycle, drain semantics, and diagnostics.
        /// <para>
        /// <see cref="Task.Run(System.Action)"/> is intentionally NOT given the disposal token —
        /// passing it would surface as <see cref="TaskCanceledException"/> on the
        /// <see cref="LastDistributedCacheWriteTask"/> seam if the token was already set when
        /// scheduling. The lambda checks the token explicitly instead so the returned task
        /// always completes cleanly (skipped on shutdown).
        /// </para>
        /// </remarks>
        private void UpdateDistributedCacheInBackground(string dekId, CachedDekProperties cachedDekProperties)
        {
            Task write = Task.Run(async () =>
            {
                using (Activity dcActivity = ActivitySource.StartActivity("DekCache.UpdateDistributedCache"))
                {
                    dcActivity?.SetTag("cache.system", "cosmos.encryption.dek");
                    dcActivity?.SetTag("cache.key", dekId);
                    dcActivity?.SetTag("cache.operation", "write");

                    if (this.disposalToken.IsCancellationRequested)
                    {
                        dcActivity?.SetTag("cache.result", "skipped-disposed");
                        return;
                    }

                    try
                    {
                        await this.UpdateDistributedCacheAsync(dekId, cachedDekProperties, this.disposalToken).ConfigureAwait(false);
                        dcActivity?.SetTag("cache.result", "success");
                    }
                    catch (OperationCanceledException) when (this.disposalToken.IsCancellationRequested)
                    {
                        // Expected on disposal. Tag without escalating to an error event.
                        dcActivity?.SetTag("cache.result", "cancelled-disposed");
                    }
                    catch (Exception ex)
                    {
                        dcActivity?.SetTag("cache.result", "failure");
                        dcActivity?.SetTag("cache.error", ex.GetType().Name);
                        dcActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);

                        // Surface the failure on the package EventSource so the failure is visible
                        // in Release without requiring an ActivityListener. Activity tags above
                        // remain the primary correlation channel for subscribers with OTel wired up.
                        EncryptionCustomEventSource.DistributedCacheBackgroundWriteFailed(dekId, ex);
                    }
                }
            });

            this.inFlightWrites.TryAdd(write, 0);

            // Observe + deregister. Static lambda + state object keeps allocations low and prevents
            // the continuation from rooting `this`. ExecuteSynchronously is safe because the
            // continuation only touches a ConcurrentDictionary and observes a Task's exception.
            // The continuation Task itself is intentionally discarded — it is a synchronous,
            // exception-free bookkeeping callback whose completion has no observers.
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

            this.LastDistributedCacheWriteTask = write;
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
        /// Optional cancellation token. NOTE: this is the CALLER's token, not the disposal
        /// token. RemoveAsync is a user-initiated invalidation and disposal MUST NOT abort it,
        /// because abandoning it would leave stale entries in the distributed cache.
        /// </param>
        public async Task RemoveAsync(string dekId, CancellationToken cancellationToken = default)
        {
            this.ThrowIfDisposed();

            CachedDekProperties cachedDekProperties = await this.DekPropertiesCache.RemoveAsync(dekId).ConfigureAwait(false);

            // Always evict the raw DEK regardless of whether DekPropertiesCache had a local entry,
            // since RawDekCache may have been populated under either dekId (via SetRawDek) or the
            // properties' SelfLink (via GetOrAddRawDekAsync). RemoveRawDek covers both keys.
            this.RemoveRawDek(dekId, cachedDekProperties?.ServerProperties);

            // Always remove from distributed cache regardless of memory cache state,
            // since another instance may have populated it.
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
                    // Don't fail the operation if distributed cache removal fails.
                    EncryptionCustomEventSource.DistributedCacheRemoveFailed(dekId, ex);
                }
            }
        }

        /// <summary>
        /// Removes RawDekCache entries that could plausibly correspond to <paramref name="dekId"/>.
        /// </summary>
        /// <remarks>
        /// RawDekCache is populated under two different keys depending on the path:
        /// <see cref="SetRawDek"/> uses the caller's <c>dekId</c>, while <see cref="GetOrAddRawDekAsync"/>
        /// uses <see cref="DataEncryptionKeyProperties.SelfLink"/>. To make invalidation robust we
        /// remove under both candidate keys when we have them.
        /// </remarks>
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
        /// Fetches DEK properties with cache hierarchy: Memory Cache -> Distributed Cache -> Source (Cosmos DB)
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

                // Try distributed cache first if available
                CachedDekProperties cachedProperties = await this.TryGetFromDistributedCacheAsync(dekId, cancellationToken).ConfigureAwait(false);
                if (cachedProperties != null)
                {
                    activity?.SetTag("cache.hit", true);
                    activity?.SetTag("cache.hit.level", "distributed");
                    return cachedProperties;
                }

                // Cache miss - fetch from source and update all caches
                activity?.SetTag("cache.hit", false);
                activity?.SetTag("cache.miss", true);
                return await this.FetchFromSourceAndUpdateCachesAsync(dekId, fetcher, diagnosticsContext, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Attempts to retrieve DEK properties from distributed cache
        /// </summary>
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

                        // Restamp with a fresh L1 freshness horizon so that the returned entry is
                        // valid per L1's TTL semantics and the L1-expiry branch does not re-trigger
                        // on the very next call. The original payload stamp is informational only;
                        // the IDistributedCache's own AbsoluteExpiration bounds the maximum age of
                        // the L2 entry at the store layer.
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
                    // The caller genuinely asked for cancellation; honour it.
                    throw;
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    activity?.SetTag("cache.latency_ms", stopwatch.ElapsedMilliseconds);
                    activity?.SetTag("cache.result", "error");
                    activity?.SetTag("cache.error", ex.GetType().Name);
                    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

                    // If distributed cache fails, fall back to source. An OperationCanceledException
                    // raised by the IDistributedCache implementation itself (e.g., an internal
                    // timeout it wired up) falls here — the caller's token was not cancelled, so
                    // surfacing it would defeat the fail-open contract of this optimization layer.
                    EncryptionCustomEventSource.DistributedCacheReadFailed(dekId, ex);
                }

                return null;
            }
        }

        /// <summary>
        /// Fetches DEK properties from source (Cosmos DB) and updates all cache layers
        /// </summary>
        private async Task<CachedDekProperties> FetchFromSourceAndUpdateCachesAsync(
            string dekId,
            Func<string, CosmosDiagnosticsContext, CancellationToken, Task<DataEncryptionKeyProperties>> fetcher,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            // Fetch from source (Cosmos DB)
            DataEncryptionKeyProperties serverProperties = await fetcher(dekId, diagnosticsContext, cancellationToken).ConfigureAwait(false);

            // Guard against a fetcher returning a DEK whose Id disagrees with the one the caller
            // requested. Caching such a result under the requested key would cross-pollinate the
            // L1/L2 slot and hand peers the wrong material on their next lookup.
            if (serverProperties == null || !string.Equals(serverProperties.Id, dekId, StringComparison.Ordinal))
            {
                string returnedId = serverProperties?.Id ?? "<null>";
                throw new InvalidOperationException(
                    $"DEK fetcher returned a DataEncryptionKeyProperties with Id '{returnedId}' for requested dekId '{dekId}'. Refusing to cache mismatched properties.");
            }

            CachedDekProperties cachedProperties = new CachedDekProperties(
                serverProperties,
                this.utcNow() + this.dekPropertiesTimeToLive);

            // Best-effort raw DEK invalidation on properties refresh: after a rewrap the wrapped
            // key bytes change but SelfLink does not, so a stale RawDekCache entry keyed by
            // SelfLink would unwrap with the old wrapped bytes. Eviction forces re-unwrap on
            // next use. NOT atomic with the L1 properties update — a concurrent reader can
            // briefly observe new properties + old raw, in which case it unwraps the new wrapped
            // bytes (correct) and the stale raw entry is overwritten on its next miss.
            this.RemoveRawDek(dekId, serverProperties);

            // Update distributed cache via the shared fire-and-forget helper so cold-path /
            // refresh-path L2 writes have the same lifecycle, drain semantics, and Release-visible
            // diagnostics contract as SetDekProperties. Resolves C1 (inconsistent fire-and-forget
            // L2 writes) — caller no longer pays L2 RTT on cold miss / forced refresh, and the
            // request's CancellationToken does not abort an in-flight L2 hydration.
            if (this.distributedCache != null)
            {
                this.UpdateDistributedCacheInBackground(dekId, cachedProperties);
            }

            return cachedProperties;
        }

        /// <summary>
        /// Writes the cached DEK properties to the distributed cache.
        /// </summary>
        /// <remarks>
        /// Exceptions from <see cref="IDistributedCache.SetAsync"/> propagate to the caller so that
        /// each call site can categorise the failure (e.g. background fire-and-forget vs cold-path
        /// best-effort) on the appropriate <see cref="EncryptionCustomEventSource"/> event.
        /// <see cref="OperationCanceledException"/> raised because the caller's token was cancelled
        /// is rethrown so callers honour cancellation; cancellation raised by an
        /// <see cref="IDistributedCache"/> implementation's own internal timeout still propagates
        /// here (caller decides whether to swallow).
        /// </remarks>
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

            // L2's absolute expiration is the L2 entry's hard lifetime, intentionally longer
            // than L1's TTL so that a peer's L2 entry survives the peer's (or this process')
            // L1 expiry. This decoupling is what lets L2 rescue the caller on L1 expiry when
            // Cosmos is unavailable.
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

            try
            {
                this.disposalCts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed concurrently — Cancel is a no-op contract.
            }

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

            try
            {
                this.disposalCts.Dispose();
            }
            catch (ObjectDisposedException)
            {
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

            try
            {
                this.disposalCts.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

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

            try
            {
                this.disposalCts.Dispose();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }
}
