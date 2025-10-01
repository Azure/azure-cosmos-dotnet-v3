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
                    () => this.FetchFromL2OrSourceAsync(dekId, fetcher, diagnosticsContext, cancellationToken),
                    cancellationToken);

            if (cachedDekProperties.ServerPropertiesExpiryUtc <= DateTime.UtcNow)
            {
                cachedDekProperties = await this.DekPropertiesCache.GetAsync(
                    dekId,
                    null,
                    () => this.FetchFromL2OrSourceAsync(dekId, fetcher, diagnosticsContext, cancellationToken, forceSource: true),
                    cancellationToken,
                    forceRefresh: true);
            }
            else if (this.ShouldProactivelyRefresh(cachedDekProperties))
            {
                // Trigger background refresh without blocking caller
                this.DekPropertiesCache.BackgroundRefreshNonBlocking(
                    dekId,
                    () => this.FetchFromL2OrSourceAsync(dekId, fetcher, diagnosticsContext, cancellationToken, forceSource: true));
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
            this.DekPropertiesCache.Set(dekId, cachedDekProperties);
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
            }
        }

        private async Task<CachedDekProperties> FetchAsync(
            string dekId,
            Func<string, CosmosDiagnosticsContext, CancellationToken, Task<DataEncryptionKeyProperties>> fetcher,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            DataEncryptionKeyProperties serverProperties = await fetcher(dekId, diagnosticsContext, cancellationToken);
            return new CachedDekProperties(serverProperties, DateTime.UtcNow + this.dekPropertiesTimeToLive);
        }

        private async Task<CachedDekProperties> FetchFromL2OrSourceAsync(
            string dekId,
            Func<string, CosmosDiagnosticsContext, CancellationToken, Task<DataEncryptionKeyProperties>> fetcher,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken,
            bool forceSource = false)
        {
            // Try L2 cache if available and not forcing source refresh
            if (!forceSource && this.distributedCache != null)
            {
                byte[] cachedBytes = await this.distributedCache.GetAsync(this.GetDistributedCacheKey(dekId), cancellationToken);
                if (cachedBytes != null)
                {
                    CachedDekProperties cachedProps = DeserializeCachedDekProperties(cachedBytes);

                    // Check if still valid
                    if (cachedProps.ServerPropertiesExpiryUtc > DateTime.UtcNow)
                    {
                        return cachedProps;
                    }
                }
            }

            // Fetch from source (Cosmos DB)
            CachedDekProperties result = await this.FetchAsync(dekId, fetcher, diagnosticsContext, cancellationToken);

            // Update L2 cache if available
            if (this.distributedCache != null)
            {
                try
                {
                    byte[] serialized = SerializeCachedDekProperties(result);
                    await this.distributedCache.SetAsync(
                        this.GetDistributedCacheKey(dekId),
                        serialized,
                        new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = result.ServerPropertiesExpiryUtc,
                        },
                        cancellationToken);
                }
                catch
                {
                    // Don't fail the operation if distributed cache write fails
                    // The L1 cache still has the value
                }
            }

            return result;
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
