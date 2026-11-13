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
        private readonly AsyncCache<string, Snapshot> cache;
        // Concurrent set keyed by source collection RID; the byte value is unused.
        // Membership preserves fresh-read semantics after a failed refresh evicts its snapshot.
        private readonly ConcurrentDictionary<string, byte> refreshRetryRequired;
        private readonly Func<DateTime> utcNowProvider;

        public SecondaryIndexMetadataCache(
            ISecondaryIndexMetadataProvider indexMetadataProvider,
            bool enableAsyncCacheExceptionNoSharing = true,
            Func<DateTime> utcNowProvider = null)
        {
            this.provider = indexMetadataProvider ?? throw new ArgumentNullException(nameof(indexMetadataProvider));
            this.cache = new AsyncCache<string, Snapshot>(enableAsyncCacheExceptionNoSharing);
            this.refreshRetryRequired = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
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
            if (forceRefresh)
            {
                Snapshot forcedSnapshot = await this.cache.GetAsync(
                    sourceCollectionRid,
                    obsoleteValue: null,
                    () => this.CreateSnapshotAsync(source, trace, cancellationToken, forceCollectionRefresh: true),
                    cancellationToken,
                    forceRefresh: true);
                this.refreshRetryRequired.TryRemove(sourceCollectionRid, out _);
                return forcedSnapshot?.Metadata;
            }

            bool requiresRefreshRetry = this.refreshRetryRequired.ContainsKey(sourceCollectionRid);
            Snapshot currentSnapshot = await this.cache.GetAsync(
                sourceCollectionRid,
                obsoleteValue: null,
                () => requiresRefreshRetry
                    ? this.CreateRefreshSnapshotAsync(source, trace, cancellationToken)
                    : this.CreateSnapshotAsync(source, trace, cancellationToken, forceCollectionRefresh: false),
                cancellationToken);
            this.ThrowIfRefreshFailed(sourceCollectionRid, currentSnapshot);

            if (currentSnapshot == null
                || (!currentSnapshot.IsExpired(this.utcNowProvider())
                    && string.Equals(
                        currentSnapshot.SourceMembershipFingerprint,
                        CreateSourceMembershipFingerprint(source),
                        StringComparison.Ordinal)))
            {
                return currentSnapshot?.Metadata;
            }

            // Supplying the stale snapshot as the obsolete value coalesces concurrent refreshes
            // without returning stale metadata while the replacement is being discovered.
            Snapshot refreshedSnapshot = await this.cache.GetAsync(
                sourceCollectionRid,
                obsoleteValue: currentSnapshot,
                () => this.CreateRefreshSnapshotAsync(source, trace, cancellationToken),
                cancellationToken);
            this.ThrowIfRefreshFailed(sourceCollectionRid, refreshedSnapshot);
            return refreshedSnapshot?.Metadata;
        }

        public void Invalidate(string sourceCollectionRid)
        {
            if (sourceCollectionRid == null)
            {
                throw new ArgumentNullException(nameof(sourceCollectionRid));
            }

            this.cache.Remove(sourceCollectionRid);
            this.refreshRetryRequired.TryRemove(sourceCollectionRid, out _);
        }

        private static string CreateSourceMembershipFingerprint(ContainerProperties source)
        {
            // Length-prefixing the ordered referenced RIDs avoids ambiguous concatenations.
            StringBuilder fingerprint = new StringBuilder();
            foreach (MaterializedViewProperties materializedView in source.MaterializedViews)
            {
                AppendFingerprintValue(fingerprint, materializedView?.ResourceId);
            }

            return fingerprint.ToString();
        }

        private static void AppendFingerprintValue(StringBuilder fingerprint, string value)
        {
            if (value == null)
            {
                fingerprint.Append("-1:");
                return;
            }

            fingerprint.Append(value.Length);
            fingerprint.Append(':');
            fingerprint.Append(value);
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
                    CreateSourceMembershipFingerprint(source),
                    this.utcNowProvider());
        }

        private async Task<Snapshot> CreateRefreshSnapshotAsync(
            ContainerProperties source,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            try
            {
                Snapshot snapshot = await this.CreateSnapshotAsync(
                    source,
                    trace,
                    cancellationToken,
                    forceCollectionRefresh: true);
                this.refreshRetryRequired.TryRemove(source.ResourceId, out _);
                return snapshot;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // The sentinel lets every caller coalesced on this refresh observe the same failure.
                // ThrowIfRefreshFailed removes it; the retry marker then makes the next miss force-fresh.
                this.refreshRetryRequired[source.ResourceId] = 0;
                return Snapshot.CreateRefreshFailure(exception);
            }
        }

        private void ThrowIfRefreshFailed(string sourceCollectionRid, Snapshot snapshot)
        {
            if (snapshot?.RefreshException != null)
            {
                this.cache.Remove(sourceCollectionRid);
                throw new SecondaryIndexMetadataRefreshException(snapshot.RefreshException);
            }
        }

        /// <summary>
        /// Represents either successfully discovered metadata and its freshness signals,
        /// or a refresh failure shared by all callers coalesced on the same refresh.
        /// </summary>
        private sealed class Snapshot
        {
            public Snapshot(
                IEnumerable<ISecondaryIndexMetadata> metadata,
                string sourceMembershipFingerprint,
                DateTime createdUtc)
            {
                this.Metadata = metadata;
                this.SourceMembershipFingerprint = sourceMembershipFingerprint;
                this.CreatedUtc = createdUtc;
            }

            private Snapshot(Exception refreshException)
            {
                this.RefreshException = refreshException;
            }

            public IEnumerable<ISecondaryIndexMetadata> Metadata { get; }

            public Exception RefreshException { get; }

            public string SourceMembershipFingerprint { get; }

            private DateTime CreatedUtc { get; }

            public bool IsExpired(DateTime utcNow)
            {
                return utcNow - this.CreatedUtc >= SnapshotTimeToLive;
            }

            public static Snapshot CreateRefreshFailure(Exception exception)
            {
                return new Snapshot(exception);
            }
        }
    }

    internal sealed class SecondaryIndexMetadataRefreshException : Exception
    {
        public SecondaryIndexMetadataRefreshException(Exception innerException)
            : base("Secondary index metadata refresh failed.", innerException)
        {
        }
    }
}
