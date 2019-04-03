//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Bootstrapping
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.Azure.Cosmos.ChangeFeed.Logging;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement;
    using Microsoft.Azure.Cosmos.ChangeFeed.Utils;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Documents;

    internal sealed class PartitionSynchronizerCore : PartitionSynchronizer
    {
        internal static int DefaultDegreeOfParallelism = 25;

        private static readonly ILog Logger = LogProvider.GetCurrentClassLogger();
        private readonly CosmosContainer container;
        private readonly DocumentServiceLeaseContainer leaseContainer;
        private readonly DocumentServiceLeaseManager leaseManager;
        private readonly int degreeOfParallelism;
        private readonly int maxBatchSize;

        public PartitionSynchronizerCore(
            CosmosContainer container,
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
            var partitionIds = new HashSet<string>(ranges.Select(range => range.Id));
            Logger.InfoFormat("Source collection: '{0}', {1} partition(s)", this.container.LinkUri.ToString(), partitionIds.Count);
            await this.CreateLeasesAsync(partitionIds).ConfigureAwait(false);
        }

        public override async Task<IEnumerable<DocumentServiceLease>> SplitPartitionAsync(DocumentServiceLease lease)
        {
            if (lease == null)
                throw new ArgumentNullException(nameof(lease));

            string partitionId = lease.CurrentLeaseToken;
            string lastContinuationToken = lease.ContinuationToken;

            Logger.InfoFormat("Partition {0} is gone due to split", partitionId);

            // After split the childs are either all or none available
            List<PartitionKeyRange> ranges = await this.EnumPartitionKeyRangesAsync().ConfigureAwait(false);
            List<string> addedPartitionIds = ranges.Where(range => range.Parents.Contains(partitionId)).Select(range => range.Id).ToList();
            if (addedPartitionIds.Count == 0)
            {
                Logger.ErrorFormat("Partition {0} had split but we failed to find at least one child partition", partitionId);
                throw new InvalidOperationException();
            }

            var newLeases = new ConcurrentQueue<DocumentServiceLease>();
            await addedPartitionIds.ForEachAsync(
                async addedRangeId =>
                {
                    DocumentServiceLease newLease = await this.leaseManager.CreateLeaseIfNotExistAsync(addedRangeId, lastContinuationToken).ConfigureAwait(false);
                    if (newLease != null)
                    {
                        newLeases.Enqueue(newLease);
                    }
                },
                this.degreeOfParallelism).ConfigureAwait(false);

            if (Logger.IsInfoEnabled())
            {
                Logger.InfoFormat("partition {0} split into {1}", partitionId, string.Join(", ", newLeases.Select(l => l.CurrentLeaseToken)));
            }

            return newLeases;
        }

        private async Task<List<PartitionKeyRange>> EnumPartitionKeyRangesAsync()
        {
            string containerUri = this.container.LinkUri.ToString();
            string partitionKeyRangesPath = string.Format(CultureInfo.InvariantCulture, "{0}/pkranges", containerUri);

            IFeedResponse<PartitionKeyRange> response = null;
            var partitionKeyRanges = new List<PartitionKeyRange>();
            do
            {
                var feedOptions = new FeedOptions
                {
                    MaxItemCount = this.maxBatchSize,
                    RequestContinuation = response?.ResponseContinuation,
                };
                response = await this.container.Client.DocumentClient.ReadPartitionKeyRangeFeedAsync(containerUri, feedOptions).ConfigureAwait(false);
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
            // Get leases after getting ranges, to make sure that no other hosts checked in continuation for split partition after we got leases.
            IEnumerable<DocumentServiceLease> leases = await this.leaseContainer.GetAllLeasesAsync().ConfigureAwait(false);
            var existingPartitionIds = new HashSet<string>(leases.Select(lease => lease.CurrentLeaseToken));
            var addedPartitionIds = new HashSet<string>(partitionIds);
            addedPartitionIds.ExceptWith(existingPartitionIds);

            await addedPartitionIds.ForEachAsync(
                async addedRangeId => { await this.leaseManager.CreateLeaseIfNotExistAsync(addedRangeId, continuationToken: null).ConfigureAwait(false); },
                this.degreeOfParallelism).ConfigureAwait(false);
        }
    }
}