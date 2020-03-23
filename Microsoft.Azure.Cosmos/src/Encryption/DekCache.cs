//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;

    internal sealed class DekCache
    {
        private readonly TimeSpan dekPropertiesTimeToLive;

        // Internal for unit testing
        internal AsyncCache<Uri, CachedDekProperties> DekPropertiesByNameLinkUriCache { get; } = new AsyncCache<Uri, CachedDekProperties>();

        internal AsyncCache<string, CachedDekProperties> DekPropertiesByRidSelfLinkCache { get; } = new AsyncCache<string, CachedDekProperties>();

        internal AsyncCache<string, InMemoryRawDek> RawDekByRidSelfLinkCache { get; } = new AsyncCache<string, InMemoryRawDek>();

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
        }

        public async Task<DataEncryptionKeyProperties> GetOrAddByRidSelfLinkAsync(
            string dekRidSelfLink,
            string databaseId,
            Func<string, CosmosDiagnosticsContext, CancellationToken, Task<DataEncryptionKeyProperties>> fetcher,
            Uri dekNameLinkUri,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            CachedDekProperties cachedDekProperties = await this.DekPropertiesByRidSelfLinkCache.GetAsync(
                    dekRidSelfLink,
                    null,
                    () => this.FetchAsync(fetcher, dekRidSelfLink, databaseId, diagnosticsContext, cancellationToken),
                    cancellationToken);

            if (cachedDekProperties.ServerPropertiesExpiryUtc <= DateTime.UtcNow)
            {
                cachedDekProperties = await this.DekPropertiesByRidSelfLinkCache.GetAsync(
                    dekRidSelfLink,
                    obsoleteValue: null,
                    () => this.FetchAsync(fetcher, dekRidSelfLink, databaseId, diagnosticsContext, cancellationToken),
                    cancellationToken,
                    forceRefresh: true);
            }

            this.DekPropertiesByNameLinkUriCache.Set(dekNameLinkUri, cachedDekProperties);
            return cachedDekProperties.ServerProperties;
        }

        public async Task<DataEncryptionKeyProperties> GetOrAddByNameLinkUriAsync(
            Uri dekNameLinkUri,
            string databaseId,
            Func<CosmosDiagnosticsContext, CancellationToken, Task<DataEncryptionKeyProperties>> fetcher,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        { 
            CachedDekProperties cachedDekProperties = await this.DekPropertiesByNameLinkUriCache.GetAsync(
                    dekNameLinkUri,
                    null,
                    () => this.FetchAsync(fetcher, databaseId, diagnosticsContext, cancellationToken),
                    cancellationToken);

            if (cachedDekProperties.ServerPropertiesExpiryUtc <= DateTime.UtcNow)
            {
                cachedDekProperties = await this.DekPropertiesByNameLinkUriCache.GetAsync(
                    dekNameLinkUri,
                    null,
                    () => this.FetchAsync(fetcher, databaseId, diagnosticsContext, cancellationToken),
                    cancellationToken,
                    forceRefresh: true);
            }

            this.DekPropertiesByRidSelfLinkCache.Set(cachedDekProperties.ServerProperties.SelfLink, cachedDekProperties);
            return cachedDekProperties.ServerProperties;
        }

        public async Task<InMemoryRawDek> GetOrAddRawDekAsync(
            DataEncryptionKeyProperties dekProperties,
            Func<DataEncryptionKeyProperties, CosmosDiagnosticsContext, CancellationToken, Task<InMemoryRawDek>> unwrapper,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            InMemoryRawDek inMemoryRawDek = await this.RawDekByRidSelfLinkCache.GetAsync(
                   dekProperties.SelfLink,
                   null,
                   () => unwrapper(dekProperties, diagnosticsContext, cancellationToken),
                   cancellationToken);

            if (inMemoryRawDek.RawDekExpiry <= DateTime.UtcNow)
            {
                inMemoryRawDek = await this.RawDekByRidSelfLinkCache.GetAsync(
                   dekProperties.SelfLink,
                   null,
                   () => unwrapper(dekProperties, diagnosticsContext, cancellationToken),
                   cancellationToken,
                   forceRefresh: true);
            }

            return inMemoryRawDek;
        }

        public void Set(string databaseId, Uri dekNameLinkUri, DataEncryptionKeyProperties dekProperties)
        {
            CachedDekProperties cachedDekProperties = new CachedDekProperties(databaseId, dekProperties, DateTime.UtcNow + this.dekPropertiesTimeToLive);
            this.DekPropertiesByNameLinkUriCache.Set(dekNameLinkUri, cachedDekProperties);
            this.DekPropertiesByRidSelfLinkCache.Set(dekProperties.SelfLink, cachedDekProperties);
        }

        public void SetRawDek(string dekRidSelfLink, InMemoryRawDek inMemoryRawDek)
        {
            this.RawDekByRidSelfLinkCache.Set(dekRidSelfLink, inMemoryRawDek);
        }

        public async Task RemoveAsync(Uri linkUri)
        {
            CachedDekProperties cachedDekProperties = await this.DekPropertiesByNameLinkUriCache.RemoveAsync(linkUri);
            if (cachedDekProperties != null)
            {
                this.DekPropertiesByRidSelfLinkCache.Remove(cachedDekProperties.ServerProperties.SelfLink);
                this.RawDekByRidSelfLinkCache.Remove(cachedDekProperties.ServerProperties.SelfLink);
            }
        }

        private async Task<CachedDekProperties> FetchAsync(
            Func<string, CosmosDiagnosticsContext, CancellationToken, Task<DataEncryptionKeyProperties>> fetcher,
            string dekRidSelfLink,
            string databaseId,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            DataEncryptionKeyProperties serverProperties = await fetcher(dekRidSelfLink, diagnosticsContext, cancellationToken);
            return new CachedDekProperties(databaseId, serverProperties, DateTime.UtcNow + this.dekPropertiesTimeToLive);
        }

        private async Task<CachedDekProperties> FetchAsync(
            Func<CosmosDiagnosticsContext, CancellationToken, Task<DataEncryptionKeyProperties>> fetcher,
            string databaseId,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            DataEncryptionKeyProperties serverProperties = await fetcher(diagnosticsContext, cancellationToken);
            return new CachedDekProperties(databaseId, serverProperties, DateTime.UtcNow + this.dekPropertiesTimeToLive);
        }
    }
}
