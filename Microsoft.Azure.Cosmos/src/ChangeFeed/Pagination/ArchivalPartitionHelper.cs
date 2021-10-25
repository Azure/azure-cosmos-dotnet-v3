// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;

    internal class ArchivalPartitionHelper
    {
        public ArchivalPartitionHelper()
        {
        }

        /// <summary>
        /// Get archival ranges for given range that returned Gone exception.
        /// </summary>
        /// <remarks>
        /// Important observations:
        /// 1) Gone exception means that either given range went thtough split, or that's simulated split 
        ///   when we start from range that is wider than any of PKRanges (e.g. change feed for entire collection).
        /// 2) We don't keep gone ranges (unfortunately) even on BE (Master partition). We only have parent ranges in active/leaf ranges.
        ///    Best we can do is use that information in order to determine which PKRangeId got spit.
        /// 3) In LogStore, gone partitions are not kept as-is after spits, intead one of active partitions would own split partition.
        ///    via 'archival' reference. I.e. some active partition would own itself and in addition also one of split partitions.
        ///    To get change feed for split/archival partition, the request will be routed to the owner partition and use special header.
        /// 4) The rule to assign archival partition during split is: 
        ///    left (the one with lesser PKRangeId) gets parent archival partition, right (the one with larger PKRangeId) gets current partition.
        ///
        /// Idea:
        /// 1) Find common closest parent PKRange(es) for spit partition.
        /// 2) Find MAX PKRangeId of child PKRanges of the common closest parent.
        /// 3) Build split graph starting from common closest parent with all its children.
        /// 3) Create FeedRangeArchivalPartition(s) with EPKRange, PKRangeId of split partition and the graph.
        ///
        /// Current limitations: the scenario when there are multiple roots is not supported.
        /// </remarks>
        public List<FeedRangeArchivalPartition> GetArchivalRanges(
            string splitPartitionKeyRangeId,
            FeedRangeInternal originalSplitRange,
            List<PartitionKeyRange> overlappingRanges,
            CancellationToken cancellationToken,
            ITrace trace)
        {
            if (string.IsNullOrEmpty(splitPartitionKeyRangeId))
            {
                throw new ArgumentException(nameof(splitPartitionKeyRangeId));
            }

            if (overlappingRanges == null || overlappingRanges.Count == 0)
            {
                return null;
            }

            List<(List<int> lineage, PartitionKeyRange leafRange)> splitLineage = BuildSplitLineages(overlappingRanges);

            int closestCommonParentId = FindClosestCommonParentRangeId(splitLineage);

            // The condition 'all overlapping have same parent' is not true.
            // TODO: FFCF: add logic to support 'forest' rather that just single tree of splits.
            if (closestCommonParentId == -1)
            {
                throw new NotSupportedException("Multiple partition splits are not supported.");
            }

            int splitPKRangeId = int.Parse(splitPartitionKeyRangeId);

            // TODO: FFCF: Debug only or keep validation for retail?
            if (splitPKRangeId != closestCommonParentId)
            {
                throw new ArgumentException(
                    $"The value of {nameof(splitPKRangeId)}={splitPKRangeId} is not closest common parent of {nameof(overlappingRanges)}->{closestCommonParentId}.");
            }

            // Build split graph starting from found common parent.
            SplitGraph splitGraph = this.BuildSplitGraphForCommonParent(splitLineage, closestCommonParentId);

            FeedRangeArchivalPartition archivalRange = new FeedRangeArchivalPartition(splitPartitionKeyRangeId, splitGraph);

            return new List<FeedRangeArchivalPartition> { archivalRange };
        }

        /// <summary>
        /// Returns list of lists for each leaf partition like this:
        /// [
        ///     for leaf partition 1: [ parent3, parent2, parent1, leaf ],
        ///     for leaf partition 2: [ parent2, parent1, leaf ],
        ///     ...
        /// ]
        /// </summary>
        private static List<(List<int> lineage, PartitionKeyRange leafRange)> BuildSplitLineages(
            List<PartitionKeyRange> overlappingRanges)
        {
            List<(List<int> lineage, PartitionKeyRange leafRange)> lineagesByLeafRange = new List<(List<int> lineage, PartitionKeyRange leafRange)>();

            foreach (PartitionKeyRange leafPkRange in overlappingRanges)
            {
                List<int> lineage = new List<int>();
                foreach (string parentId in leafPkRange.Parents)
                {
                    lineage.Add(int.Parse(parentId));
                }

                lineage.Add(int.Parse(leafPkRange.Id));

                lineagesByLeafRange.Add((lineage, leafPkRange));
            }

            return lineagesByLeafRange;
        }

        private static int FindClosestCommonParentRangeId(List<(List<int> lineage, PartitionKeyRange leafRange)> lineageAndRanges)
        {
            int lastCommonParentId = -1;
            for (int indexWithinChain = 0; true; ++indexWithinChain)
            {
                int currentCommonParentId = -1;

                bool areSameOnCurrentLevel = lineageAndRanges.Count > 0;
                foreach ((List<int> lineage, PartitionKeyRange leafRange) in lineageAndRanges)
                {
                    if (indexWithinChain >= lineage.Count || (currentCommonParentId != -1 && lineage[indexWithinChain] != currentCommonParentId))
                    {
                        areSameOnCurrentLevel = false;
                        break;
                    }

                    if (currentCommonParentId == -1)
                    {
                        currentCommonParentId = lineage[indexWithinChain];
                    }
                }

                if (!areSameOnCurrentLevel)
                {
                    break;
                }

                lastCommonParentId = currentCommonParentId;
            }

            return lastCommonParentId;
        }

        private SplitGraph BuildSplitGraphForCommonParent(
            List<(List<int> lineage, PartitionKeyRange leafRange)> splitLineages, int closestCommonParentId)
        {
            // Go from right to left until hit common parent and build the list. If hit node met before, stop and bind to that.

            Dictionary<int, SplitGraphNode> mapRangeIdToNode = new Dictionary<int, SplitGraphNode>();
            SplitGraphNode root = null;

            foreach ((List<int> lineage, PartitionKeyRange leafRange) in splitLineages)
            {
                SplitGraphNode head = null;
                bool headHasParent = true;
                for (int i = lineage.Count - 1; i >= 0; --i)
                {
                    int pkRangeId = lineage[i];
                    mapRangeIdToNode.TryGetValue(pkRangeId, out SplitGraphNode node);
                    if (node != null)
                    {
                        if (head != null)
                        {
                            node.AddChildNode(head);
                        }

                        break;
                    }

                    node = new SplitGraphNode(pkRangeId);
                    mapRangeIdToNode.Add(pkRangeId, node);

                    if (head != null)
                    {
                        node.AddChildNode(head);
                    }

                    head = node;

                    if (pkRangeId == closestCommonParentId)
                    {
                        headHasParent = i > 0;
                        break;
                    }
                }

                if (root == null)
                {
                    if (headHasParent)
                    {
                        head.HasParent = true;
                    }

                    root = head;
                }
            }

            Dictionary<int, PartitionKeyRange> leafRanges = new Dictionary<int, PartitionKeyRange>();
            foreach ((List<int> lineage, PartitionKeyRange leafRange) in splitLineages)
            {
                if (lineage.Count > 0)
                {
                    leafRanges.Add(lineage[lineage.Count - 1], leafRange);
                }
            }

            return new SplitGraph(root, leafRanges);
        }
    }

    internal class SplitGraphNode
    {
        private SortedList<int, SplitGraphNode> children = new SortedList<int, SplitGraphNode>();

        public SplitGraphNode(int partitionKeyRangeId)
        {
            this.PartitionKeyRangeId = partitionKeyRangeId;
        }

        public int PartitionKeyRangeId { get; }

        public bool HasParent { get; set; }

        public IList<SplitGraphNode> Children => this.children.Values;

        public void AddChildNode(SplitGraphNode child)
        {
            child.HasParent = true;
            this.children.Add(child.PartitionKeyRangeId, child);
        }
    }

    internal class SplitGraph
    {
        public SplitGraph(SplitGraphNode rootNode, Dictionary<int, PartitionKeyRange> leafRanges)
        {
            this.Root = rootNode;
            this.LeafRanges = leafRanges;
        }

        public SplitGraphNode Root { get; }

        public Dictionary<int, PartitionKeyRange> LeafRanges { get; }
    }
}
