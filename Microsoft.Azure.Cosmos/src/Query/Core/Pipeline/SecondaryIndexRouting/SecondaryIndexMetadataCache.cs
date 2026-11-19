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
    using Microsoft.Azure.Cosmos.Common;
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

            // Atomically select the entry for this read. Concurrent callers either reuse the current
            // snapshot or await the same discovery/refresh entry; only one entry is stored per source RID.
            CacheEntry entry = this.entries.AddOrUpdate(
                sourceCollectionRid,
                _ => this.CreateInitializationEntry(
                    sourceCollectionRid,
                    source,
                    trace,
                    cancellationToken,
                    forceCollectionRefresh: forceRefresh),
                (_, currentEntry) => this.SelectEntry(
                    currentEntry,
                    sourceCollectionRid,
                    source,
                    trace,
                    cancellationToken,
                    forceRefresh));

            Snapshot snapshot = entry.Initialization == null
                ? entry.Snapshot
                : await entry.Initialization.Value;
            return snapshot?.Metadata;
        }

        private CacheEntry SelectEntry(
            CacheEntry currentEntry,
            string sourceCollectionRid,
            ContainerProperties source,
            ITrace trace,
            CancellationToken cancellationToken,
            bool forceRefresh)
        {
            // A discovery or refresh is already running. Reusing this entry coalesces concurrent reads.
            if (currentEntry.Initialization != null)
            {
                return currentEntry;
            }

            Snapshot currentSnapshot = currentEntry.Snapshot;
            bool sourceMembershipIsUnchanged = string.Equals(
                currentSnapshot.MembershipFingerprint,
                CreateMembershipFingerprint(source),
                StringComparison.Ordinal);
            if (!forceRefresh
                && !currentSnapshot.IsExpired(this.utcNowProvider())
                && sourceMembershipIsUnchanged)
            {
                return currentEntry;
            }

            // Expiration, changed source membership, and explicit refresh all require fresh source and
            // referenced collection metadata. AddOrUpdate installs only one of these refresh entries.
            return this.CreateInitializationEntry(
                sourceCollectionRid,
                source,
                trace,
                cancellationToken,
                forceCollectionRefresh: true);
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

        private CacheEntry CreateInitializationEntry(
            string sourceCollectionRid,
            ContainerProperties source,
            ITrace trace,
            CancellationToken cancellationToken,
            bool forceCollectionRefresh)
        {
            CacheEntry entry = null;
            entry = new CacheEntry(new AsyncLazy<Snapshot>(
                () => this.InitializeEntryAsync(
                    sourceCollectionRid,
                    entry,
                    source,
                    trace,
                    cancellationToken,
                    forceCollectionRefresh),
                CancellationToken.None));
            return entry;
        }

        private async Task<Snapshot> InitializeEntryAsync(
            string sourceCollectionRid,
            CacheEntry initializingEntry,
            ContainerProperties source,
            ITrace trace,
            CancellationToken cancellationToken,
            bool forceCollectionRefresh)
        {
            try
            {
                Snapshot snapshot = await this.CreateSnapshotAsync(
                    source,
                    trace,
                    cancellationToken,
                    forceCollectionRefresh);
                if (snapshot == null)
                {
                    // Null results are not cached, so the next read performs normal discovery again.
                    this.TryRemoveEntry(sourceCollectionRid, initializingEntry);
                }
                else
                {
                    // Publish the completed snapshot only if this initialization is still the current entry.
                    // Explicit invalidation or a newer entry must not be overwritten by an older completion.
                    this.entries.TryUpdate(
                        sourceCollectionRid,
                        new CacheEntry(snapshot),
                        initializingEntry);
                }

                return snapshot;
            }
            catch
            {
                // Remove only this failed initialization. A snapshot populated after invalidation is preserved.
                this.TryRemoveEntry(sourceCollectionRid, initializingEntry);
                throw;
            }
        }

        private bool TryRemoveEntry(string sourceCollectionRid, CacheEntry entry)
        {
            ICollection<KeyValuePair<string, CacheEntry>> entries = this.entries;
            return entries.Remove(new KeyValuePair<string, CacheEntry>(sourceCollectionRid, entry));
        }

        private async Task<Snapshot> CreateSnapshotAsync(
            ContainerProperties source,
            ITrace trace,
            CancellationToken cancellationToken,
            bool forceCollectionRefresh)
        {
            IEnumerable<ISecondaryIndexMetadata> metadata = await this.provider.GetSecondaryIndexMetadataAsync(
                source,
                trace,
                cancellationToken,
                forceCollectionRefresh);
            return metadata == null
                ? null
                : new Snapshot(
                    metadata,
                    CreateMembershipFingerprint(source),
                    this.utcNowProvider());
        }

        private sealed class CacheEntry
        {
            public CacheEntry(Snapshot snapshot)
            {
                this.Snapshot = snapshot;
            }

            public CacheEntry(AsyncLazy<Snapshot> initialization)
            {
                this.Initialization = initialization;
            }

            public AsyncLazy<Snapshot> Initialization { get; }

            public Snapshot Snapshot { get; }
        }

        /// <summary>
        /// Represents successfully discovered metadata and its freshness signals.
        /// </summary>
        private sealed class Snapshot
        {
            public Snapshot(
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
