//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.SecondaryIndexRouting
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Tracing;

    internal sealed class SecondaryIndexMetadataCache : ISecondaryIndexMetadataCache
    {
        private readonly ISecondaryIndexMetadataProvider provider;
        private readonly AsyncCache<string, IEnumerable<ISecondaryIndexMetadata>> cache;

        public SecondaryIndexMetadataCache(ISecondaryIndexMetadataProvider indexMetadataProvider, bool enableAsyncCacheExceptionNoSharing = true)
        {
            this.provider = indexMetadataProvider ?? throw new ArgumentNullException(nameof(indexMetadataProvider));
            this.cache = new AsyncCache<string, IEnumerable<ISecondaryIndexMetadata>>(enableAsyncCacheExceptionNoSharing);
        }

        public Task<IEnumerable<ISecondaryIndexMetadata>> GetSecondaryIndexMetadataAsync(
            ContainerProperties source,
            ITrace trace,
            CancellationToken cancellationToken,
            bool forceRefresh = false)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (source.ResourceId == null)
            {
                throw new ArgumentException("The source container must have a resource identifier.", nameof(source));
            }

            string sourceCollectionRid = source.ResourceId;
            return this.cache.GetAsync(
                sourceCollectionRid,
                obsoleteValue: null,
                () => this.provider.GetSecondaryIndexMetadataAsync(source, trace, cancellationToken),
                cancellationToken,
                forceRefresh);
        }

        public void Invalidate(string sourceCollectionRid)
        {
            if (sourceCollectionRid == null)
            {
                throw new ArgumentNullException(nameof(sourceCollectionRid));
            }

            this.cache.Remove(sourceCollectionRid);
        }
    }
}
