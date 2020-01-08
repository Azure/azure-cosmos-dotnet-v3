//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;

    internal class DekCache
    {
        private readonly TimeSpan dekPropertiesTimeToLive = TimeSpan.FromMinutes(30);

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
        }

        public async Task<DataEncryptionKeyProperties> GetOrAddByRidSelfLinkAsync(
            string dekRidSelfLink,
            string databaseId,
            Func<string, CancellationToken, Task<DataEncryptionKeyProperties>> fetcher,
            Uri dekNameLinkUri,
            CancellationToken cancellationToken)
        {
            CachedDekProperties cachedDekProperties = await this.DekPropertiesByRidSelfLinkCache.GetAsync(
                    dekRidSelfLink,
                    null,
                    () => this.FetchAsync(fetcher, dekRidSelfLink, databaseId, cancellationToken),
                    cancellationToken);

            if (cachedDekProperties.ServerPropertiesExpiryUtc <= DateTime.UtcNow)
            {
                cachedDekProperties = await this.DekPropertiesByRidSelfLinkCache.GetAsync(
                    dekRidSelfLink,
                    null,
                    () => this.FetchAsync(fetcher, dekRidSelfLink, databaseId, cancellationToken),
                    cancellationToken,
                    forceRefresh: true);
            }

            this.DekPropertiesByNameLinkUriCache.Set(dekNameLinkUri, cachedDekProperties);
            return cachedDekProperties.ServerProperties;
        }

        public async Task<DataEncryptionKeyProperties> GetOrAddByNameLinkUriAsync(
            Uri dekNameLinkUri,
            string databaseId,
            Func<CancellationToken, Task<DataEncryptionKeyProperties>> fetcher,
            CancellationToken cancellationToken)
        { 
            CachedDekProperties cachedDekProperties = await this.DekPropertiesByNameLinkUriCache.GetAsync(
                    dekNameLinkUri,
                    null,
                    () => this.FetchAsync(fetcher, databaseId, cancellationToken),
                    cancellationToken);

            if (cachedDekProperties.ServerPropertiesExpiryUtc <= DateTime.UtcNow)
            {
                cachedDekProperties = await this.DekPropertiesByNameLinkUriCache.GetAsync(
                    dekNameLinkUri,
                    null,
                    () => this.FetchAsync(fetcher, databaseId, cancellationToken),
                    cancellationToken,
                    forceRefresh: true);
            }

            this.DekPropertiesByRidSelfLinkCache.Set(cachedDekProperties.ServerProperties.SelfLink, cachedDekProperties);
            return cachedDekProperties.ServerProperties;
        }

        public async Task<InMemoryRawDek> GetOrAddRawDekAsync(
            DataEncryptionKeyProperties dekProperties,
            Func<DataEncryptionKeyProperties, CancellationToken, Task<InMemoryRawDek>> unwrapper,
            CancellationToken cancellationToken)
        {
            InMemoryRawDek inMemoryRawDek = await this.RawDekByRidSelfLinkCache.GetAsync(
                   dekProperties.SelfLink,
                   null,
                   () => unwrapper(dekProperties, cancellationToken),
                   cancellationToken);

            if (inMemoryRawDek.RawDekExpiry <= DateTime.UtcNow)
            {
                inMemoryRawDek = await this.RawDekByRidSelfLinkCache.GetAsync(
                   dekProperties.SelfLink,
                   null,
                   () => unwrapper(dekProperties, cancellationToken),
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
            Func<string, CancellationToken, Task<DataEncryptionKeyProperties>> fetcher,
            string dekRidSelfLink,
            string databaseId,
            CancellationToken cancellationToken)
        {
            DataEncryptionKeyProperties serverProperties = await fetcher(dekRidSelfLink, cancellationToken);
            return new CachedDekProperties(databaseId, serverProperties, DateTime.UtcNow + this.dekPropertiesTimeToLive);
        }

        private async Task<CachedDekProperties> FetchAsync(
            Func<CancellationToken, Task<DataEncryptionKeyProperties>> fetcher,
            string databaseId,
            CancellationToken cancellationToken)
        {
            DataEncryptionKeyProperties serverProperties = await fetcher(cancellationToken);
            return new CachedDekProperties(databaseId, serverProperties, DateTime.UtcNow + this.dekPropertiesTimeToLive);
        }
    }
}
