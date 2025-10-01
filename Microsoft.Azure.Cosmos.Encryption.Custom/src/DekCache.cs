//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
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

        // Internal for unit testing
        internal AsyncCache<string, CachedDekProperties> DekPropertiesCache { get; } = new AsyncCache<string, CachedDekProperties>();

        internal AsyncCache<string, InMemoryRawDek> RawDekCache { get; } = new AsyncCache<string, InMemoryRawDek>();

        public DekCache(
            TimeSpan? dekPropertiesTimeToLive = null,
            IDistributedCache distributedCache = null,
            TimeSpan? proactiveRefreshThreshold = null)
        {
            this.dekPropertiesTimeToLive = dekPropertiesTimeToLive.HasValue == true ? dekPropertiesTimeToLive.Value : TimeSpan.FromMinutes(Constants.DekPropertiesDefaultTTLInMinutes);
            this.distributedCache = distributedCache;
            this.proactiveRefreshThreshold = proactiveRefreshThreshold;
        }

        public async Task<DataEncryptionKeyProperties> GetOrAddDekPropertiesAsync(
            string dekId,
            Func<string, CosmosDiagnosticsContext, CancellationToken, Task<DataEncryptionKeyProperties>> fetcher,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            CachedDekProperties cachedDekProperties = await this.DekPropertiesCache.GetAsync(
                    dekId,
                    null,
                    () => this.FetchDekPropertiesAsync(dekId, fetcher, diagnosticsContext, cancellationToken),
                    cancellationToken);

            if (cachedDekProperties.ServerPropertiesExpiryUtc <= DateTime.UtcNow)
            {
                cachedDekProperties = await this.DekPropertiesCache.GetAsync(
                    dekId,
                    null,
                    () => this.FetchFromSourceAndUpdateCachesAsync(dekId, fetcher, diagnosticsContext, cancellationToken),
                    cancellationToken,
                    forceRefresh: true);
            }
            else if (this.ShouldProactivelyRefresh(cachedDekProperties))
            {
                // Trigger background refresh without blocking caller
                this.DekPropertiesCache.BackgroundRefreshNonBlocking(
                    dekId,
                    () => this.FetchFromSourceAndUpdateCachesAsync(dekId, fetcher, diagnosticsContext, cancellationToken));
            }

            return cachedDekProperties.ServerProperties;
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

        public void SetDekProperties(string dekId, DataEncryptionKeyProperties dekProperties)
        {
            CachedDekProperties cachedDekProperties = new (dekProperties, DateTime.UtcNow + this.dekPropertiesTimeToLive);

            // Update memory cache
            this.DekPropertiesCache.Set(dekId, cachedDekProperties);

            // Update distributed cache if available
            this.UpdateDistributedCacheAsync(dekId, cachedDekProperties, CancellationToken.None).ConfigureAwait(false);
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
                    catch
                    {
                        // Don't fail the operation if distributed cache removal fails
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
            // Try distributed cache first if available
            CachedDekProperties cachedProperties = await this.TryGetFromDistributedCacheAsync(dekId, cancellationToken);
            if (cachedProperties != null)
            {
                return cachedProperties;
            }

            // Cache miss - fetch from source and update all caches
            return await this.FetchFromSourceAndUpdateCachesAsync(dekId, fetcher, diagnosticsContext, cancellationToken);
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

            try
            {
                byte[] cachedBytes = await this.distributedCache.GetAsync(
                    this.GetDistributedCacheKey(dekId),
                    cancellationToken);

                if (cachedBytes != null)
                {
                    CachedDekProperties cachedProps = DeserializeCachedDekProperties(cachedBytes);

                    // Validate the cached entry is still valid
                    if (cachedProps.ServerPropertiesExpiryUtc > DateTime.UtcNow)
                    {
                        return cachedProps;
                    }
                }
            }
            catch
            {
                // If distributed cache fails, fall back to source
                // Don't throw - this is an optimization layer
            }

            return null;
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
            catch
            {
                // Don't fail the operation if distributed cache write fails
                // The memory cache still has the value, and we'll try again on next fetch
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
            return $"dek:{dekId}";
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
