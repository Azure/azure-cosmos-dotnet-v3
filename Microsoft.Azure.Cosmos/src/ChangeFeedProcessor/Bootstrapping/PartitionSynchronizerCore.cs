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
        private readonly int maxBatchSize;

        public PartitionSynchronizerCore(
            ContainerInternal container,
            DocumentServiceLeaseContainer leaseContainer,
            DocumentServiceLeaseManager leaseManager,
            int degreeOfParallelism,
            int maxBatchSize)
        {
            this.container = container;
            this.leaseContainer = leaseContainer;
            this.leaseManager = leaseManager;
            this.degreeOfParallelism = degreeOfParallelism;
            this.maxBatchSize = maxBatchSize;
        }

        public override async Task CreateMissingLeasesAsync()
        {
            List<PartitionKeyRange> ranges = await this.EnumPartitionKeyRangesAsync().ConfigureAwait(false);
            HashSet<string> partitionIds = new HashSet<string>(ranges.Select(range => range.Id));
            DefaultTrace.TraceInformation("Source collection: '{0}', {1} partition(s)", this.container.LinkUri.ToString(), partitionIds.Count);
            await this.CreateLeasesAsync(partitionIds).ConfigureAwait(false);
        }

        public override async Task<IEnumerable<DocumentServiceLease>> HandlePartitionGoneAsync(DocumentServiceLease lease)
        {
            if (lease == null)
            {
                throw new ArgumentNullException(nameof(lease));
            }

            string leaseToken = lease.CurrentLeaseToken;

            DefaultTrace.TraceInformation("Lease {0} is gone due to split or merge", leaseToken);

            List<PartitionKeyRange> ranges = await this.EnumPartitionKeyRangesAsync().ConfigureAwait(false);
            List<PartitionKeyRange> resultingRanges = ranges.Where(range => range.Parents.Contains(leaseToken)).ToList();
            if (resultingRanges.Count == 0)
            {
                DefaultTrace.TraceError("Lease {0} is gone but we failed to find at least one child partition", leaseToken);
                throw new InvalidOperationException();
            }

            ConcurrentQueue<DocumentServiceLease> newLeases = new ConcurrentQueue<DocumentServiceLease>();
            if (resultingRanges.Count > 1)
            {
                // Split
                string lastContinuationToken = lease.ContinuationToken;                
                await resultingRanges.ForEachAsync(
                    async addedRange =>
                    {
                        DocumentServiceLease newLease = await this.leaseManager.CreateLeaseIfNotExistAsync(new FeedRangePartitionKeyRange(addedRange.Id), lastContinuationToken).ConfigureAwait(false);
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
                // Merge

            }

            return newLeases;
        }

        private async Task<List<PartitionKeyRange>> EnumPartitionKeyRangesAsync()
        {
            string containerUri = this.container.LinkUri.ToString();

            IDocumentFeedResponse<PartitionKeyRange> response = null;
            List<PartitionKeyRange> partitionKeyRanges = new List<PartitionKeyRange>();
            do
            {
                FeedOptions feedOptions = new FeedOptions
                {
                    MaxItemCount = this.maxBatchSize,
                    RequestContinuationToken = response?.ResponseContinuation,
                };

                response = await this.container.ClientContext.DocumentClient.ReadPartitionKeyRangeFeedAsync(containerUri, feedOptions).ConfigureAwait(false);
                IEnumerator<PartitionKeyRange> enumerator = response.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    partitionKeyRanges.Add(enumerator.Current);
                }
            }
            while (!string.IsNullOrEmpty(response.ResponseContinuation));

            return partitionKeyRanges;
        }

        /// <summary>
        /// Creates leases if they do not exist. This might happen on initial start or if some lease was unexpectedly lost.
        /// Leases are created without the continuation token. It means partitions will be read according to 'From Beginning' or
        /// 'From current time'.
        /// Same applies also to split partitions. We do not search for parent lease and take continuation token since this might end up
        /// of reprocessing all the events since the split.
        /// </summary>
        private async Task CreateLeasesAsync(HashSet<string> partitionIds)
        {
            // Get leases after getting ranges, to make sure that no other hosts checked in continuation token for split partition after we got leases.
            IEnumerable<DocumentServiceLease> leases = await this.leaseContainer.GetAllLeasesAsync().ConfigureAwait(false);
            HashSet<string> existingPartitionIds = new HashSet<string>(leases.Select(lease => lease.CurrentLeaseToken));
            HashSet<string> addedPartitionIds = new HashSet<string>(partitionIds);
            addedPartitionIds.ExceptWith(existingPartitionIds);

            await addedPartitionIds.ForEachAsync(
                async addedRangeId => await this.leaseManager.CreateLeaseIfNotExistAsync(new FeedRangePartitionKeyRange(addedRangeId), continuationToken: null).ConfigureAwait(false),
                this.degreeOfParallelism).ConfigureAwait(false);
        }
    }
}