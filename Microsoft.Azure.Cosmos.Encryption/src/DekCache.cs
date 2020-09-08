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
        private readonly bool isUnwrappedDekCached;

        // Internal for unit testing
        internal AsyncCache<string, CachedDekProperties> DekPropertiesCache { get; } = new AsyncCache<string, CachedDekProperties>();

        internal AsyncCache<string, InMemoryRawDek> RawDekCache { get; } = new AsyncCache<string, InMemoryRawDek>();

        public DekCache(
            bool cacheUnwrappedDek,
            TimeSpan? dekPropertiesTimeToLive = null)
        {
            if (dekPropertiesTimeToLive.HasValue)
            {
                this.dekPropertiesTimeToLive = dekPropertiesTimeToLive.Value;
            }
            else
            {
                this.dekPropertiesTimeToLive = TimeSpan.FromMinutes(30);
            }

            this.isUnwrappedDekCached = cacheUnwrappedDek;
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
            Func<DataEncryptionKeyProperties, RequestOptions, CosmosDiagnosticsContext, CancellationToken, Task<InMemoryRawDek>> unwrapper,
            RequestOptions requestOptions,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            if (!this.isUnwrappedDekCached)
            {
                return await unwrapper(
                    dekProperties,
                    requestOptions,
                    diagnosticsContext,
                    cancellationToken);
            }

            InMemoryRawDek inMemoryRawDek = await this.RawDekCache.GetAsync(
                dekProperties.Id,
                null,
                () => unwrapper(dekProperties, requestOptions, diagnosticsContext, cancellationToken),
                cancellationToken);

            if (inMemoryRawDek.RawDekExpiry <= DateTime.UtcNow)
            {
                inMemoryRawDek = await this.RawDekCache.GetAsync(
                   dekProperties.Id,
                   null,
                   () => unwrapper(dekProperties, requestOptions, diagnosticsContext, cancellationToken),
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
            if (this.isUnwrappedDekCached)
            {
                this.RawDekCache.Set(dekId, inMemoryRawDek);
            }
        }

        public async Task RemoveAsync(string dekId)
        {
            CachedDekProperties cachedDekProperties = await this.DekPropertiesCache.RemoveAsync(dekId);
            if (cachedDekProperties != null &&
                this.isUnwrappedDekCached)
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
