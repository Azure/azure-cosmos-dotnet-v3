//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Bootstrapping
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.Azure.Cosmos.ChangeFeed.Utils;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    internal sealed class PartitionSynchronizerCore : PartitionSynchronizer
    {
#pragma warning disable SA1401 // Fields should be private
        internal static int DefaultDegreeOfParallelism = 25;
#pragma warning restore SA1401 // Fields should be private

        private readonly ContainerInternal container;
        private readonly DocumentServiceLeaseContainer leaseContainer;
        private readonly DocumentServiceLeaseManager leaseManager;
        private readonly int degreeOfParallelism;
        private readonly Routing.PartitionKeyRangeCache partitionKeyRangeCache;
        private readonly string containerRid;

        public PartitionSynchronizerCore(
            ContainerInternal container,
            DocumentServiceLeaseContainer leaseContainer,
            DocumentServiceLeaseManager leaseManager,
            int degreeOfParallelism,
            Routing.PartitionKeyRangeCache partitionKeyRangeCache,
            string containerRid)
        {
            this.container = container;
            this.leaseContainer = leaseContainer;
            this.leaseManager = leaseManager;
            this.degreeOfParallelism = degreeOfParallelism;
            this.partitionKeyRangeCache = partitionKeyRangeCache;
            this.containerRid = containerRid;
        }

        public override async Task CreateMissingLeasesAsync()
        {
            IReadOnlyList<PartitionKeyRange> ranges = await this.partitionKeyRangeCache.TryGetOverlappingRangesAsync(
                this.containerRid, 
                FeedRangeEpk.FullRange.Range, 
                NoOpTrace.Singleton, 
                forceRefresh: false);
            DefaultTrace.TraceInformation("Source collection: '{0}', {1} partition(s)", this.container.LinkUri, ranges.Count);
            await this.CreateLeasesAsync(ranges).ConfigureAwait(false);
        }

        /// <summary>
        /// Handle a Partition Gone response and decide what to do based on the type of lease.
        /// </summary>
        /// <returns>Returns the list of leases to create and a boolean that indicates whether or not to remove the current lease.</returns>
        public override async Task<(IEnumerable<DocumentServiceLease>, bool)> HandlePartitionGoneAsync(DocumentServiceLease lease)
        {
            if (lease == null)
            {
                throw new ArgumentNullException(nameof(lease));
            }

            string leaseToken = lease.CurrentLeaseToken;
            string lastContinuationToken = lease.ContinuationToken;

            DefaultTrace.TraceInformation("Lease {0} is gone due to split or merge", leaseToken);

            IReadOnlyList<PartitionKeyRange> overlappingRanges = await this.partitionKeyRangeCache.TryGetOverlappingRangesAsync(
                this.containerRid, 
                ((FeedRangeEpk)lease.FeedRange).Range, 
                NoOpTrace.Singleton, 
                forceRefresh: true);
            if (overlappingRanges.Count == 0)
            {
                DefaultTrace.TraceError("Lease {0} is gone but we failed to find at least one child range", leaseToken);
                throw new InvalidOperationException();
            }

            return lease switch
            {
                DocumentServiceLeaseCoreEpk feedRangeBaseLease => await this.HandlePartitionGoneAsync(leaseToken, lastContinuationToken, feedRangeBaseLease, overlappingRanges),
                _ => await this.HandlePartitionGoneAsync(leaseToken, lastContinuationToken, (DocumentServiceLeaseCore)lease, overlappingRanges)
            };
        }

        /// <summary>
        /// Handles splits and merges for partition based leases.
        /// </summary>
        private async Task<(IEnumerable<DocumentServiceLease>, bool)> HandlePartitionGoneAsync(
            string leaseToken,
            string lastContinuationToken,
            DocumentServiceLeaseCore partitionBasedLease,
            IReadOnlyList<PartitionKeyRange> overlappingRanges)
        {
            ConcurrentQueue<DocumentServiceLease> newLeases = new ConcurrentQueue<DocumentServiceLease>();
            if (overlappingRanges.Count > 1)
            {
                // Split: More than two children
                await overlappingRanges.ForEachAsync(
                    async addedRange =>
                    {
                        DocumentServiceLease newLease = await this.leaseManager.CreateLeaseIfNotExistAsync(addedRange, lastContinuationToken);
                        if (newLease != null)
                        {
                            newLeases.Enqueue(newLease);
                        }
                    },
                    this.degreeOfParallelism).ConfigureAwait(false);

                DefaultTrace.TraceInformation("Lease {0} split into {1}", leaseToken, string.Join(", ", newLeases.Select(l => l.CurrentLeaseToken)));
            }
            else
            {
                // Merge: 1 children, multiple ranges merged into 1
                PartitionKeyRange mergedRange = overlappingRanges[0];
                DefaultTrace.TraceInformation("Lease {0} merged into {1}", leaseToken, mergedRange.Id);

                DocumentServiceLease newLease = await this.leaseManager.CreateLeaseIfNotExistAsync((FeedRangeEpk)partitionBasedLease.FeedRange, lastContinuationToken);
                if (newLease != null)
                {
                    newLeases.Enqueue(newLease);
                }
            }

            return (newLeases, true);
        }

        /// <summary>
        /// Handles splits and merges for feed range based leases.
        /// </summary>
        private async Task<(IEnumerable<DocumentServiceLease>, bool)> HandlePartitionGoneAsync(
            string leaseToken,
            string lastContinuationToken,
            DocumentServiceLeaseCoreEpk feedRangeBasedLease,
            IReadOnlyList<PartitionKeyRange> overlappingRanges)
        {
            List<DocumentServiceLease> newLeases = new List<DocumentServiceLease>();
            if (overlappingRanges.Count > 1)
            {
                // Split: More than two children spanning the feed range
                FeedRangeEpk splitRange = (FeedRangeEpk)feedRangeBasedLease.FeedRange;
                string min = splitRange.Range.Min;
                string max = splitRange.Range.Max;

                // Create new leases starting from the current min and ending in the current max and across the ordered list of partitions
                for (int i = 0; i < overlappingRanges.Count - 1; i++)
                {
                    Documents.Routing.Range<string> partitionRange = overlappingRanges[i].ToRange();
                    Documents.Routing.Range<string> mergedRange = new Documents.Routing.Range<string>(min, partitionRange.Max, true, false);
                    DocumentServiceLease newLease = await this.leaseManager.CreateLeaseIfNotExistAsync(new FeedRangeEpk(mergedRange), lastContinuationToken);
                    if (newLease != null)
                    {
                        newLeases.Add(newLease);
                    }

                    min = partitionRange.Max;
                }

                // Add the last range with the original max and the last min from the split
                Documents.Routing.Range<string> lastRangeAfterSplit = new Documents.Routing.Range<string>(min, max, true, false);
                DocumentServiceLease lastLease = await this.leaseManager.CreateLeaseIfNotExistAsync(new FeedRangeEpk(lastRangeAfterSplit), lastContinuationToken);
                if (lastLease != null)
                {
                    newLeases.Add(lastLease);
                }

                DefaultTrace.TraceInformation("Lease {0} split into {1}", leaseToken, string.Join(", ", newLeases.Select(l => l.CurrentLeaseToken)));

                return (newLeases, true);
            }
            else
            {
                // If we have only 1 mapped partition after the Gone, it means this epk range just remapped to another partition
                newLeases.Add(feedRangeBasedLease);

                DefaultTrace.TraceInformation("Lease {0} redirected to {1}", leaseToken, overlappingRanges[0].Id);

                // Since the lease was just redirected, we don't need to delete it
                return (newLeases, false);
            }
        }

        /// <summary>
        /// Creates leases if they do not exist. This might happen on initial start or if some lease was unexpectedly lost.
        /// Leases are created without the continuation token. It means partitions will be read according to 'From Beginning' or
        /// 'From current time'.
        /// Same applies also to split partitions. We do not search for parent lease and take continuation token since this might end up
        /// of reprocessing all the events since the split.
        /// </summary>
        private async Task CreateLeasesAsync(IReadOnlyList<PartitionKeyRange> partitionKeyRanges)
        {
            // Get leases after getting ranges, to make sure that no other hosts checked in continuation token for split partition after we got leases.
            IReadOnlyList<DocumentServiceLease> leases = await this.leaseContainer.GetAllLeasesAsync().ConfigureAwait(false);
            IReadOnlyList<PartitionKeyRange> rangesToAdd = partitionKeyRanges;
            if (leases.Count > 0)
            {
                List<string> pkRangeBasedLeases = leases.Where(lease => lease is DocumentServiceLeaseCore).Select(lease => lease.CurrentLeaseToken).ToList();
                List<PartitionKeyRange> missingRanges = new List<PartitionKeyRange>();
                foreach (PartitionKeyRange partitionKeyRange in partitionKeyRanges)
                {
                    // Check if there is a PKRange based lease already
                    if (pkRangeBasedLeases.Contains(partitionKeyRange.Id))
                    {
                        continue;
                    }

                    // Check if there are EPKBased leases for the partition range
                    // If there is at least one, we assume there are others that cover the rest of the full partition range 
                    // based on the fact that the lease store was always initialized for the full collection
                    Documents.Routing.Range<string> partitionRange = partitionKeyRange.ToRange();
                    if (leases.Where(lease => lease is DocumentServiceLeaseCoreEpk
                        && lease.FeedRange is FeedRangeEpk feedRangeEpk
                        && (partitionRange.Min == feedRangeEpk.Range.Min || partitionRange.Max == feedRangeEpk.Range.Max)).Any())
                    {
                        continue;
                    }

                    missingRanges.Add(partitionKeyRange);
                }

                rangesToAdd = missingRanges;
            }

            await rangesToAdd.ForEachAsync(
                async addedRange => await this.leaseManager.CreateLeaseIfNotExistAsync(addedRange, continuationToken: null).ConfigureAwait(false),
                this.degreeOfParallelism).ConfigureAwait(false);
        }
    }
}