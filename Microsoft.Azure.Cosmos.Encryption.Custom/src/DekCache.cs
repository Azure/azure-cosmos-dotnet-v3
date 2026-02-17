//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Caching.Distributed;
    using Newtonsoft.Json;

    internal class DekCache
    {
        private readonly TimeSpan dekPropertiesTimeToLive;
        private readonly TimeSpan? proactiveRefreshThreshold;
        private readonly IDistributedCache distributedCache;
        private readonly string cacheKeyPrefix;

        private static readonly ActivitySource ActivitySource = new ("Microsoft.Azure.Cosmos.Encryption.Custom.DekCache");

        // Internal for unit testing
        internal AsyncCache<string, CachedDekProperties> DekPropertiesCache { get; } = new AsyncCache<string, CachedDekProperties>();

        internal AsyncCache<string, InMemoryRawDek> RawDekCache { get; } = new AsyncCache<string, InMemoryRawDek>();

        public DekCache(
            TimeSpan? dekPropertiesTimeToLive = null,
            IDistributedCache distributedCache = null,
            TimeSpan? proactiveRefreshThreshold = null,
            string cacheKeyPrefix = "dek")
        {
            this.dekPropertiesTimeToLive = dekPropertiesTimeToLive.HasValue == true ? dekPropertiesTimeToLive.Value : TimeSpan.FromMinutes(Constants.DekPropertiesDefaultTTLInMinutes);

            // Validate cacheKeyPrefix
            ArgumentValidation.ThrowIfNullOrWhiteSpace(cacheKeyPrefix, nameof(cacheKeyPrefix));

            // Validate proactiveRefreshThreshold
            if (proactiveRefreshThreshold.HasValue)
            {
                ArgumentValidation.ThrowIfNegative(proactiveRefreshThreshold.Value, nameof(proactiveRefreshThreshold));
                ArgumentValidation.ThrowIfGreaterThanOrEqual(proactiveRefreshThreshold.Value, this.dekPropertiesTimeToLive, nameof(proactiveRefreshThreshold));
            }

            this.distributedCache = distributedCache;
            this.proactiveRefreshThreshold = proactiveRefreshThreshold;
            this.cacheKeyPrefix = cacheKeyPrefix;
        }

        public async Task<DataEncryptionKeyProperties> GetOrAddDekPropertiesAsync(
            string dekId,
            Func<string, CosmosDiagnosticsContext, CancellationToken, Task<DataEncryptionKeyProperties>> fetcher,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            using (Activity activity = ActivitySource.StartActivity("DekCache.GetOrAddProperties"))
            {
                activity?.SetTag("cache.system", "cosmos.encryption.dek");
                activity?.SetTag("cache.key", dekId);

                CachedDekProperties cachedDekProperties = await this.DekPropertiesCache.GetAsync(
                        dekId,
                        null,
                        () => this.FetchDekPropertiesAsync(dekId, fetcher, diagnosticsContext, cancellationToken),
                        cancellationToken);

                if (cachedDekProperties.ServerPropertiesExpiryUtc <= DateTime.UtcNow)
                {
                    activity?.SetTag("cache.expired", true);
                    activity?.SetTag("cache.operation", "refresh");

                    cachedDekProperties = await this.DekPropertiesCache.GetAsync(
                        dekId,
                        null,
                        () => this.FetchFromSourceAndUpdateCachesAsync(dekId, fetcher, diagnosticsContext, cancellationToken),
                        cancellationToken,
                        forceRefresh: true);
                }
                else if (this.ShouldProactivelyRefresh(cachedDekProperties))
                {
                    activity?.SetTag("cache.proactive_refresh", true);

                    // Trigger background refresh without blocking caller
                    this.DekPropertiesCache.BackgroundRefreshNonBlocking(
                        dekId,
                        () => this.FetchFromSourceAndUpdateCachesAsync(dekId, fetcher, diagnosticsContext, cancellationToken));
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
            InMemoryRawDek inMemoryRawDek = await this.RawDekCache.GetAsync(
                   dekProperties.SelfLink,
                   null,
                   () => unwrapper(dekProperties, diagnosticsContext, cancellationToken),
                   cancellationToken);

            if (inMemoryRawDek.RawDekExpiry <= DateTime.UtcNow)
            {
                inMemoryRawDek = await this.RawDekCache.GetAsync(
                   dekProperties.SelfLink,
                   null,
                   () => unwrapper(dekProperties, diagnosticsContext, cancellationToken),
                   cancellationToken,
                   forceRefresh: true);
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
        /// <see cref="RemoveAsync(string)"/> to explicitly invalidate the cache entry, followed by a
        /// fresh read operation to force synchronization across all cache layers.
        /// </para>
        /// </remarks>
        public void SetDekProperties(string dekId, DataEncryptionKeyProperties dekProperties)
        {
            using (Activity activity = ActivitySource.StartActivity("DekCache.SetProperties"))
            {
                activity?.SetTag("cache.system", "cosmos.encryption.dek");
                activity?.SetTag("cache.key", dekId);

                CachedDekProperties cachedDekProperties = new (dekProperties, DateTime.UtcNow + this.dekPropertiesTimeToLive);

                // Update memory cache
                this.DekPropertiesCache.Set(dekId, cachedDekProperties);
                activity?.SetTag("cache.memory.updated", true);

                // Update distributed cache if available (fire-and-forget)
                if (this.distributedCache != null)
                {
                    activity?.SetTag("cache.distributed.enabled", true);

                    _ = Task.Run(async () =>
                    {
                        using (Activity dcActivity = ActivitySource.StartActivity("DekCache.UpdateDistributedCache"))
                        {
                            dcActivity?.SetTag("cache.system", "cosmos.encryption.dek");
                            dcActivity?.SetTag("cache.key", dekId);
                            dcActivity?.SetTag("cache.operation", "write");

                            try
                            {
                                await this.UpdateDistributedCacheAsync(dekId, cachedDekProperties, CancellationToken.None).ConfigureAwait(false);
                                dcActivity?.SetTag("cache.result", "success");
                            }
                            catch (Exception ex)
                            {
                                dcActivity?.SetTag("cache.result", "failure");
                                dcActivity?.SetTag("cache.error", ex.GetType().Name);
                                dcActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);

                                // Log the failure but don't fail the operation
                                Debug.WriteLine($"Failed to update distributed cache for DEK '{dekId}': {ex.Message}");
                            }
                        }
                    });
                }
                else
                {
                    activity?.SetTag("cache.distributed.enabled", false);
                }
            }
        }

        public void SetRawDek(string dekId, InMemoryRawDek inMemoryRawDek)
        {
            this.RawDekCache.Set(dekId, inMemoryRawDek);
        }

        public async Task RemoveAsync(string dekId)
        {
            CachedDekProperties cachedDekProperties = await this.DekPropertiesCache.RemoveAsync(dekId);
            if (cachedDekProperties != null)
            {
                this.RawDekCache.Remove(dekId);

                // Remove from distributed cache if available
                if (this.distributedCache != null)
                {
                    try
                    {
                        await this.distributedCache.RemoveAsync(this.GetDistributedCacheKey(dekId));
                    }
                    catch (Exception ex)
                    {
                        // Don't fail the operation if distributed cache removal fails
                        Debug.WriteLine($"Failed to remove DEK '{dekId}' from distributed cache: {ex.Message}");
                    }
                }
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
                CachedDekProperties cachedProperties = await this.TryGetFromDistributedCacheAsync(dekId, cancellationToken);
                if (cachedProperties != null)
                {
                    activity?.SetTag("cache.hit", true);
                    activity?.SetTag("cache.hit.level", "distributed");
                    return cachedProperties;
                }

                // Cache miss - fetch from source and update all caches
                activity?.SetTag("cache.hit", false);
                activity?.SetTag("cache.miss", true);
                return await this.FetchFromSourceAndUpdateCachesAsync(dekId, fetcher, diagnosticsContext, cancellationToken);
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
                        cancellationToken);

                    stopwatch.Stop();
                    activity?.SetTag("cache.latency_ms", stopwatch.ElapsedMilliseconds);

                    if (cachedBytes != null)
                    {
                        CachedDekProperties cachedProps = DeserializeCachedDekProperties(cachedBytes);

                        // Validate the cached entry is still valid
                        if (cachedProps.ServerPropertiesExpiryUtc > DateTime.UtcNow)
                        {
                            activity?.SetTag("cache.result", "hit");
                            activity?.SetTag("cache.entry.valid", true);
                            return cachedProps;
                        }

                        activity?.SetTag("cache.result", "expired");
                        activity?.SetTag("cache.entry.valid", false);
                    }
                    else
                    {
                        activity?.SetTag("cache.result", "miss");
                    }
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    activity?.SetTag("cache.latency_ms", stopwatch.ElapsedMilliseconds);
                    activity?.SetTag("cache.result", "error");
                    activity?.SetTag("cache.error", ex.GetType().Name);
                    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

                    // If distributed cache fails, fall back to source
                    // Don't throw - this is an optimization layer
                    Debug.WriteLine($"Failed to retrieve DEK '{dekId}' from distributed cache: {ex.Message}");
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
            DataEncryptionKeyProperties serverProperties = await fetcher(dekId, diagnosticsContext, cancellationToken);
            CachedDekProperties cachedProperties = new CachedDekProperties(
                serverProperties,
                DateTime.UtcNow + this.dekPropertiesTimeToLive);

            // Update distributed cache (best effort - don't fail if this fails)
            await this.UpdateDistributedCacheAsync(dekId, cachedProperties, cancellationToken);

            return cachedProperties;
        }

        /// <summary>
        /// Updates the distributed cache with DEK properties (best effort, non-blocking on failures)
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

            try
            {
                byte[] serialized = SerializeCachedDekProperties(cachedProperties);
                await this.distributedCache.SetAsync(
                    this.GetDistributedCacheKey(dekId),
                    serialized,
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpiration = cachedProperties.ServerPropertiesExpiryUtc,
                    },
                    cancellationToken);
            }
            catch (Exception ex)
            {
                // Don't fail the operation if distributed cache write fails
                // The memory cache still has the value, and we'll try again on next fetch
                Debug.WriteLine($"Failed to write DEK '{dekId}' to distributed cache: {ex.Message}");
            }
        }

        private bool ShouldProactivelyRefresh(CachedDekProperties cached)
        {
            if (!this.proactiveRefreshThreshold.HasValue)
            {
                return false;
            }

            DateTime refreshThreshold = cached.ServerPropertiesExpiryUtc - this.proactiveRefreshThreshold.Value;
            return DateTime.UtcNow >= refreshThreshold;
        }

        private string GetDistributedCacheKey(string dekId)
        {
            return $"{this.cacheKeyPrefix}:{dekId}";
        }

        private static byte[] SerializeCachedDekProperties(CachedDekProperties cachedProps)
        {
            // Create a DTO for serialization
            CachedDekPropertiesDto dto = new CachedDekPropertiesDto
            {
                ServerProperties = cachedProps.ServerProperties,
                ServerPropertiesExpiryUtc = cachedProps.ServerPropertiesExpiryUtc,
            };

            string json = JsonConvert.SerializeObject(dto);
            return System.Text.Encoding.UTF8.GetBytes(json);
        }

        private static CachedDekProperties DeserializeCachedDekProperties(byte[] bytes)
        {
            string json = System.Text.Encoding.UTF8.GetString(bytes);
            CachedDekPropertiesDto dto = JsonConvert.DeserializeObject<CachedDekPropertiesDto>(json);

            if (dto?.ServerProperties == null)
            {
                throw new InvalidOperationException("Failed to deserialize cached DEK properties or properties are null.");
            }

            return new CachedDekProperties(
                dto.ServerProperties,
                dto.ServerPropertiesExpiryUtc);
        }

        // DTO for serialization to distributed cache
        private sealed class CachedDekPropertiesDto
        {
            public DataEncryptionKeyProperties ServerProperties { get; set; }

            public DateTime ServerPropertiesExpiryUtc { get; set; }
        }
    }
}
