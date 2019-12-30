//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Documents;

    internal class DekCache
    {
        // these are internal for unit testing
        internal readonly AsyncCache<Uri, CachedDekProperties> dekPropertiesByNameLinkUriCache = new AsyncCache<Uri, CachedDekProperties>();
        internal readonly AsyncCache<string, CachedDekProperties> dekPropertiesByRidSelfLinkCache = new AsyncCache<string, CachedDekProperties>();
        internal readonly AsyncCache<string, InMemoryRawDek> rawDekByRidSelfLinkCache = new AsyncCache<string, InMemoryRawDek>();

        private readonly TimeSpan dekPropertiesTimeToLive = TimeSpan.FromHours(1);

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
            CancellationToken cancellationToken)
        {
            CachedDekProperties cachedDekProperties = await this.dekPropertiesByRidSelfLinkCache.GetAsync(
                    dekRidSelfLink,
                    null,
                    () => this.FetchAsync(fetcher, dekRidSelfLink, databaseId, cancellationToken),
                    cancellationToken);

            if (cachedDekProperties.ServerPropertiesExpiry <= DateTime.UtcNow)
            {
                cachedDekProperties = await this.dekPropertiesByRidSelfLinkCache.GetAsync(
                    dekRidSelfLink,
                    null,
                    () => this.FetchAsync(fetcher, dekRidSelfLink, databaseId, cancellationToken),
                    cancellationToken,
                    forceRefresh: true);
            }

            // todo: use existing impl
            Uri dekNameLinkUri = new Uri(string.Format("{0}/{1}/{2}/{3}", Paths.DatabasesPathSegment, databaseId, Paths.ClientEncryptionKeysPathSegment, cachedDekProperties.ServerProperties.Id), UriKind.Relative);
            this.dekPropertiesByNameLinkUriCache.Set(dekNameLinkUri, cachedDekProperties);
            return cachedDekProperties.ServerProperties;
        }

        public async Task<DataEncryptionKeyProperties> GetOrAddByNameLinkUriAsync(
            Uri dekNameLinkUri,
            string databaseId,
            Func<CancellationToken, Task<DataEncryptionKeyProperties>> fetcher,
            CancellationToken cancellationToken)
        { 
            CachedDekProperties cachedDekProperties = await this.dekPropertiesByNameLinkUriCache.GetAsync(
                    dekNameLinkUri,
                    null,
                    () => this.FetchAsync(fetcher, databaseId, cancellationToken),
                    cancellationToken);

            if (cachedDekProperties.ServerPropertiesExpiry <= DateTime.UtcNow)
            {
                cachedDekProperties = await this.dekPropertiesByNameLinkUriCache.GetAsync(
                    dekNameLinkUri,
                    null,
                    () => this.FetchAsync(fetcher, databaseId, cancellationToken),
                    cancellationToken,
                    forceRefresh: true);
            }

            this.dekPropertiesByRidSelfLinkCache.Set(cachedDekProperties.ServerProperties.SelfLink, cachedDekProperties);
            return cachedDekProperties.ServerProperties;
        }

        public async Task<InMemoryRawDek> GetOrAddRawDekAsync(
            DataEncryptionKeyProperties dekProperties,
            Func<DataEncryptionKeyProperties, CancellationToken, Task<InMemoryRawDek>> unwrapper,
            CancellationToken cancellationToken)
        {
            InMemoryRawDek inMemoryRawDek = await this.rawDekByRidSelfLinkCache.GetAsync(
                   dekProperties.SelfLink,
                   null,
                   () => unwrapper(dekProperties, cancellationToken),
                   cancellationToken);

            if (inMemoryRawDek.RawDekExpiry <= DateTime.UtcNow)
            {
                inMemoryRawDek = await this.rawDekByRidSelfLinkCache.GetAsync(
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
            // todo: should we not overwrite if the input is the same so we don't lose raw DEK?
            CachedDekProperties cachedDekProperties = new CachedDekProperties(databaseId, dekProperties, DateTime.UtcNow + this.dekPropertiesTimeToLive);
            this.dekPropertiesByNameLinkUriCache.Set(dekNameLinkUri, cachedDekProperties);
            this.dekPropertiesByRidSelfLinkCache.Set(dekProperties.SelfLink, cachedDekProperties);
        }

        public void SetRawDek(string dekRidSelfLink, InMemoryRawDek inMemoryRawDek)
        {
            this.rawDekByRidSelfLinkCache.Set(dekRidSelfLink, inMemoryRawDek);
        }

        public async Task RemoveAsync(Uri linkUri)
        {
            CachedDekProperties cachedDekProperties = await this.dekPropertiesByNameLinkUriCache.RemoveAsync(linkUri);
            if (cachedDekProperties != null)
            {
                this.dekPropertiesByRidSelfLinkCache.Remove(cachedDekProperties.ServerProperties.SelfLink);
                this.rawDekByRidSelfLinkCache.Remove(cachedDekProperties.ServerProperties.SelfLink);
            }
        }

        // For unit testing
        internal void Clear()
        {
            this.dekPropertiesByNameLinkUriCache.Clear();
            this.dekPropertiesByRidSelfLinkCache.Clear();
            this.rawDekByRidSelfLinkCache.Clear();
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
