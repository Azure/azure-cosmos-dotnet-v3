//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;

    internal class DekCache
    {
        private readonly TimeSpan dekPropertiesTimeToLive;

        // Internal for unit testing
        internal AsyncCache<string, CachedDekProperties> DekPropertiesCache { get; } = new AsyncCache<string, CachedDekProperties>();

        internal AsyncCache<string, InMemoryRawDek> RawDekCache { get; } = new AsyncCache<string, InMemoryRawDek>();

        internal CleanupExpiredRawDekFromMemory CleanupExpiredRawDekFromMemory { get; set; }

        public DekCache(TimeSpan? dekPropertiesTimeToLive = null)
        {
            if (dekPropertiesTimeToLive.HasValue)
            {
                this.dekPropertiesTimeToLive = dekPropertiesTimeToLive.Value;
            }
            else
            {
                this.dekPropertiesTimeToLive = TimeSpan.FromMinutes(30);
            }

            this.CleanupExpiredRawDekFromMemory = new CleanupExpiredRawDekFromMemory();
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
                    () => this.FetchAsync(dekId, fetcher, diagnosticsContext, cancellationToken),
                    cancellationToken);

            if (cachedDekProperties.ServerPropertiesExpiryUtc <= DateTime.UtcNow)
            {
                cachedDekProperties = await this.DekPropertiesCache.GetAsync(
                    dekId,
                    null,
                    () => this.FetchAsync(dekId, fetcher, diagnosticsContext, cancellationToken),
                    cancellationToken,
                    forceRefresh: true);
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
                   dekProperties.Id,
                   null,
                   () => unwrapper(dekProperties, diagnosticsContext, cancellationToken),
                   cancellationToken);

            if (inMemoryRawDek.RawDekExpiry <= DateTime.UtcNow)
            {
                inMemoryRawDek = await this.RawDekCache.GetAsync(
                   dekProperties.Id,
                   null,
                   () => unwrapper(dekProperties, diagnosticsContext, cancellationToken),
                   cancellationToken,
                   forceRefresh: true);
            }

            return inMemoryRawDek;
        }

        public void SetDekProperties(string dekId, DataEncryptionKeyProperties dekProperties)
        {
            CachedDekProperties cachedDekProperties = new CachedDekProperties(dekProperties, DateTime.UtcNow + this.dekPropertiesTimeToLive);
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
    }
}
