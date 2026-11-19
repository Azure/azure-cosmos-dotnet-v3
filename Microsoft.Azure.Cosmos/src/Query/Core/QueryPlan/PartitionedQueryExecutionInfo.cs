//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.QueryPlan
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Documents.Routing;
    using Newtonsoft.Json;
    using Constants = Documents.Constants;

    internal sealed class PartitionedQueryExecutionInfo
    {
        private List<Range<string>> queryRanges;

        public PartitionedQueryExecutionInfo()
        {
            this.Version = Constants.PartitionedQueryExecutionInfo.CurrentVersion;
        }

        [JsonProperty(Constants.Properties.PartitionedQueryExecutionInfoVersion)]
        public int Version
        {
            get;
            private set;
        }

        [JsonProperty(Constants.Properties.QueryInfo)]
        public QueryInfo QueryInfo
        {
            get;
            set;
        }

        [JsonProperty(Constants.Properties.QueryRanges)]
        public List<Range<string>> QueryRanges
        {
            get => this.queryRanges;
            set => this.queryRanges = RemoveContainedRanges(value);
        }

        // Change to the below after Direct package upgrade
        // [JsonProperty(Constants.Properties.HybridSearchQueryInfo)]
        [JsonProperty("hybridSearchQueryInfo")]
        public HybridSearchQueryInfo HybridSearchQueryInfo
        {
            get;
            set;
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }

        public static bool TryParse(string serializedQueryPlan, out PartitionedQueryExecutionInfo partitionedQueryExecutionInfo)
        {
            if (serializedQueryPlan == null)
            {
                throw new ArgumentNullException(nameof(serializedQueryPlan));
            }

            try
            {
                partitionedQueryExecutionInfo = JsonConvert.DeserializeObject<PartitionedQueryExecutionInfo>(serializedQueryPlan);
                return true;
            }
            catch (JsonException)
            {
                partitionedQueryExecutionInfo = default;
                return false;
            }
        }

        private static List<Range<string>> RemoveContainedRanges(List<Range<string>> ranges)
        {
            if (ranges == null || ranges.Count <= 1)
            {
                return ranges;
            }

            foreach (Range<string> range in ranges)
            {
                if (range == null || string.CompareOrdinal(range.Min, range.Max) > 0)
                {
                    // Do not alter malformed plans as a side effect of containment normalization.
                    return ranges;
                }
            }

            List<Range<string>> normalized = new List<Range<string>>(ranges);
            normalized.Sort((left, right) =>
            {
                int comparison = Range<string>.MinComparer.Instance.Compare(left, right);
                return comparison != 0 ? comparison : Range<string>.MaxComparer.Instance.Compare(right, left);
            });

            int retainedCount = 0;
            Range<string> coveringRange = null;
            for (int index = 0; index < normalized.Count; index++)
            {
                Range<string> range = normalized[index];
                if (range.IsEmpty)
                {
                    normalized[retainedCount++] = range;
                    continue;
                }

                if (coveringRange != null
                    && Range<string>.MinComparer.Instance.Compare(coveringRange, range) <= 0
                    && Range<string>.MaxComparer.Instance.Compare(coveringRange, range) >= 0)
                {
                    continue;
                }

                normalized[retainedCount++] = range;
                if (coveringRange == null || Range<string>.MaxComparer.Instance.Compare(range, coveringRange) > 0)
                {
                    coveringRange = range;
                }
            }

            normalized.RemoveRange(retainedCount, normalized.Count - retainedCount);
            return normalized;
        }
    }
}
