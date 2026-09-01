//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.SecondaryIndexRouting
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Tracing;

    internal sealed class SecondaryIndexMetadataCache : ISecondaryIndexMetadataCache
    {
        private readonly ISecondaryIndexMetadataProvider provider;
        private readonly AsyncCache<string, IReadOnlyList<ISecondaryIndexMetadata>> cache;

        public SecondaryIndexMetadataCache(ISecondaryIndexMetadataProvider indexMetadataProvider, bool enableAsyncCacheExceptionNoSharing = true)
        {
            this.provider = indexMetadataProvider ?? throw new ArgumentNullException(nameof(indexMetadataProvider));
            this.cache = new AsyncCache<string, IReadOnlyList<ISecondaryIndexMetadata>>(enableAsyncCacheExceptionNoSharing);
        }

        public Task<IReadOnlyList<ISecondaryIndexMetadata>> TryGetSecondaryIndexMetadataAsync(
            string sourceCollectionRid,
            ITrace trace,
            bool forceRefresh = false,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sourceCollectionRid))
            {
                throw new ArgumentNullException(nameof(sourceCollectionRid));
            }

            cancellationToken.ThrowIfCancellationRequested();
            return this.cache.GetAsync(
                sourceCollectionRid,
                obsoleteValue: null,
                async () =>
                {
                    IReadOnlyList<ISecondaryIndexMetadata> metadata = 
                        await this.provider.GetSecondaryIndexMetadataAsync(sourceCollectionRid, trace, cancellationToken);
                    if (metadata == null)
                    {
                        throw new InvalidOperationException("Secondary index metadata providers must return an empty list when no candidates exist.");
                    }

                    return new ReadOnlyCollection<ISecondaryIndexMetadata>(new List<ISecondaryIndexMetadata>(metadata));
                },
                cancellationToken,
                forceRefresh);
        }

        public void Invalidate(string sourceCollectionRid)
        {
            if (string.IsNullOrWhiteSpace(sourceCollectionRid))
            {
                throw new ArgumentNullException(nameof(sourceCollectionRid));
            }

            this.cache.Remove(sourceCollectionRid);
        }
    }
}
