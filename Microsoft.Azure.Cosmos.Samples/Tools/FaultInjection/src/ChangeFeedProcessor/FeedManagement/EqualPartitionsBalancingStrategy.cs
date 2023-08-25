﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.Azure.Cosmos.Core.Trace;

    internal sealed class EqualPartitionsBalancingStrategy : LoadBalancingStrategy
    {
        internal static int DefaultMinLeaseCount = 0;
        internal static int DefaultMaxLeaseCount = 0;

        private readonly string hostName;
        private readonly int minPartitionCount;
        private readonly int maxPartitionCount;
        private readonly TimeSpan leaseExpirationInterval;

        public EqualPartitionsBalancingStrategy(string hostName, int minPartitionCount, int maxPartitionCount, TimeSpan leaseExpirationInterval)
        {
            if (hostName == null)
            {
                throw new ArgumentNullException(nameof(hostName));
            }

            this.hostName = hostName;
            this.minPartitionCount = minPartitionCount;
            this.maxPartitionCount = maxPartitionCount;
            this.leaseExpirationInterval = leaseExpirationInterval;
        }

        public override IEnumerable<DocumentServiceLease> SelectLeasesToTake(IEnumerable<DocumentServiceLease> allLeases)
        {
            Dictionary<string, int> workerToPartitionCount = new Dictionary<string, int>();
            List<DocumentServiceLease> expiredLeases = new List<DocumentServiceLease>();
            Dictionary<string, DocumentServiceLease> allPartitions = new Dictionary<string, DocumentServiceLease>();
            this.CategorizeLeases(allLeases, allPartitions, expiredLeases, workerToPartitionCount);

            int partitionCount = allPartitions.Count;
            int workerCount = workerToPartitionCount.Count;
            if (partitionCount <= 0)
            {
                return Enumerable.Empty<DocumentServiceLease>();
            }

            int target = this.CalculateTargetPartitionCount(partitionCount, workerCount);
            int myCount = workerToPartitionCount[this.hostName];
            int partitionsNeededForMe = target - myCount;

            DefaultTrace.TraceInformation(
                "Host '{0}' {1} partitions, {2} hosts, {3} available leases, target = {4}, min = {5}, max = {6}, mine = {7}, will try to take {8} lease(s) for myself'.",
                this.hostName,
                partitionCount,
                workerCount,
                expiredLeases.Count,
                target,
                this.minPartitionCount,
                this.maxPartitionCount,
                myCount,
                Math.Max(partitionsNeededForMe, 0));

            if (partitionsNeededForMe <= 0)
            {
                return Enumerable.Empty<DocumentServiceLease>();
            }

            if (expiredLeases.Count > 0)
            {
                return expiredLeases.Take(partitionsNeededForMe);
            }

            DocumentServiceLease stolenLease = GetLeaseToSteal(workerToPartitionCount, target, partitionsNeededForMe, allPartitions);
            return stolenLease == null ? Enumerable.Empty<DocumentServiceLease>() : new[] { stolenLease };
        }

        private static DocumentServiceLease GetLeaseToSteal(
            Dictionary<string, int> workerToPartitionCount,
            int target,
            int partitionsNeededForMe,
            Dictionary<string, DocumentServiceLease> allPartitions)
        {
            KeyValuePair<string, int> workerToStealFrom = FindWorkerWithMostPartitions(workerToPartitionCount);
            if (workerToStealFrom.Value > target - (partitionsNeededForMe > 1 ? 1 : 0))
            {
                return allPartitions.Values.First(partition => string.Equals(partition.Owner, workerToStealFrom.Key, StringComparison.OrdinalIgnoreCase));
            }

            return null;
        }

        private static KeyValuePair<string, int> FindWorkerWithMostPartitions(Dictionary<string, int> workerToPartitionCount)
        {
            KeyValuePair<string, int> workerToStealFrom = default(KeyValuePair<string, int>);
            foreach (KeyValuePair<string, int> kvp in workerToPartitionCount)
            {
                if (workerToStealFrom.Value <= kvp.Value)
                {
                    workerToStealFrom = kvp;
                }
            }

            return workerToStealFrom;
        }

        private int CalculateTargetPartitionCount(int partitionCount, int workerCount)
        {
            int target = 1;
            if (partitionCount > workerCount)
            {
                target = (int)Math.Ceiling((double)partitionCount / workerCount);
            }

            if (this.maxPartitionCount > 0 && target > this.maxPartitionCount)
            {
                target = this.maxPartitionCount;
            }

            if (this.minPartitionCount > 0 && target < this.minPartitionCount)
            {
                target = this.minPartitionCount;
            }

            return target;
        }

        private void CategorizeLeases(
            IEnumerable<DocumentServiceLease> allLeases,
            Dictionary<string, DocumentServiceLease> allPartitions,
            List<DocumentServiceLease> expiredLeases,
            Dictionary<string, int> workerToPartitionCount)
        {
            foreach (DocumentServiceLease lease in allLeases)
            {
                Debug.Assert(lease.CurrentLeaseToken != null, "TakeLeasesAsync: lease.CurrentLeaseToken cannot be null.");

                allPartitions.Add(lease.CurrentLeaseToken, lease);
                if (string.IsNullOrWhiteSpace(lease.Owner) || this.IsExpired(lease))
                {
                    DefaultTrace.TraceVerbose("Found unused or expired lease: {0}", lease);
                    expiredLeases.Add(lease);
                }
                else
                {
                    string assignedTo = lease.Owner;
                    if (workerToPartitionCount.TryGetValue(assignedTo, out int count))
                    {
                        workerToPartitionCount[assignedTo] = count + 1;
                    }
                    else
                    {
                        workerToPartitionCount.Add(assignedTo, 1);
                    }
                }
            }

            if (!workerToPartitionCount.ContainsKey(this.hostName))
            {
                workerToPartitionCount.Add(this.hostName, 0);
            }
        }

        private bool IsExpired(DocumentServiceLease lease)
        {
            return lease.Timestamp.ToUniversalTime() + this.leaseExpirationInterval < DateTime.UtcNow;
        }
    }
}