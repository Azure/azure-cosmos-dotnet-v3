//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.SecondaryIndexRouting
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Tracing;

    internal sealed class SecondaryIndexMetadataCache : ISecondaryIndexMetadataCache
    {
        internal static readonly TimeSpan SnapshotTimeToLive = TimeSpan.FromMinutes(3);

        private readonly ISecondaryIndexMetadataProvider provider;
        private readonly ConcurrentDictionary<string, CacheEntry> entries = new ConcurrentDictionary<string, CacheEntry>();
        private readonly Func<DateTime> utcNowProvider;

        public SecondaryIndexMetadataCache(
            ISecondaryIndexMetadataProvider indexMetadataProvider,
            Func<DateTime> utcNowProvider = null)
        {
            this.provider = indexMetadataProvider ?? throw new ArgumentNullException(nameof(indexMetadataProvider));
            this.utcNowProvider = utcNowProvider ?? (() => DateTime.UtcNow);
        }

        public async Task<IEnumerable<ISecondaryIndexMetadata>> GetSecondaryIndexMetadataAsync(
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
            cancellationToken.ThrowIfCancellationRequested();

            this.entries.TryGetValue(sourceCollectionRid, out CacheEntry currentEntry);
            if (currentEntry != null
                && !forceRefresh
                && !currentEntry.IsExpired(this.utcNowProvider())
                && string.Equals(
                    currentEntry.MembershipFingerprint,
                    CreateMembershipFingerprint(source),
                    StringComparison.Ordinal))
            {
                return currentEntry.Metadata;
            }

            IEnumerable<ISecondaryIndexMetadata> metadata;
            try
            {
                metadata = await this.provider.GetSecondaryIndexMetadataAsync(
                    source,
                    trace,
                    cancellationToken,
                    forceRefresh: forceRefresh || currentEntry != null);
            }
            catch
            {
                this.TryRemoveEntry(sourceCollectionRid, currentEntry);
                throw;
            }

            if (metadata == null)
            {
                this.TryRemoveEntry(sourceCollectionRid, currentEntry);
                return null;
            }

            CacheEntry refreshedEntry = new CacheEntry(
                metadata,
                CreateMembershipFingerprint(source),
                this.utcNowProvider());
            if (currentEntry == null)
            {
                this.entries.TryAdd(sourceCollectionRid, refreshedEntry);
            }
            else
            {
                this.entries.TryUpdate(sourceCollectionRid, refreshedEntry, currentEntry);
            }

            return metadata;
        }

        public void Invalidate(string sourceCollectionRid)
        {
            if (sourceCollectionRid == null)
            {
                throw new ArgumentNullException(nameof(sourceCollectionRid));
            }

            this.entries.TryRemove(sourceCollectionRid, out _);
        }

        private static string CreateMembershipFingerprint(ContainerProperties source)
        {
            StringBuilder fingerprint = new StringBuilder();
            foreach (MaterializedViewProperties materializedView in source.MaterializedViews)
            {
                string resourceId = materializedView.ResourceId;
                fingerprint.Append(resourceId.Length);
                fingerprint.Append(':');
                fingerprint.Append(resourceId);
            }

            return fingerprint.ToString();
        }

        private bool TryRemoveEntry(string sourceCollectionRid, CacheEntry entry)
        {
            if (entry == null)
            {
                return false;
            }

            ICollection<KeyValuePair<string, CacheEntry>> entries = this.entries;
            return entries.Remove(new KeyValuePair<string, CacheEntry>(sourceCollectionRid, entry));
        }

        private sealed class CacheEntry
        {
            public CacheEntry(
                IEnumerable<ISecondaryIndexMetadata> metadata,
                string membershipFingerprint,
                DateTime createdUtc)
            {
                this.Metadata = metadata;
                this.MembershipFingerprint = membershipFingerprint;
                this.CreatedUtc = createdUtc;
            }

            public IEnumerable<ISecondaryIndexMetadata> Metadata { get; }

            /// <summary>
            /// GSI resource identifiers in source order, each prefixed by its character count and a colon
            /// (for example, "3:abc4:wxyz").
            /// </summary>
            public string MembershipFingerprint { get; }

            private DateTime CreatedUtc { get; }

            public bool IsExpired(DateTime utcNow)
            {
                return utcNow - this.CreatedUtc >= SnapshotTimeToLive;
            }
        }
    }
}
