// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.HybridSearch
{
    using Microsoft.Azure.Cosmos.CosmosElements;

    internal sealed class FullTextStatistics
    {
        private readonly long[] hitCounts;

        public long TotalWordCount { get; }

        public System.ReadOnlyMemory<long> HitCounts => this.hitCounts;

        public FullTextStatistics(long totalWordCount, long[] hitCounts)
        {
            this.TotalWordCount = totalWordCount;
            this.hitCounts = hitCounts;
        }

        public FullTextStatistics(CosmosObject cosmosObject)
        {
            if (cosmosObject == null)
            {
                throw new System.ArgumentNullException($"{nameof(cosmosObject)} must not be null.");
            }

            if (!cosmosObject.TryGetValue(FieldNames.TotalWordCount, out CosmosNumber totalWordCount))
            {
                throw new System.ArgumentException($"{FieldNames.TotalWordCount} must exist and be a number");
            }

            if (!cosmosObject.TryGetValue(FieldNames.HitCounts, out CosmosArray hitCountsArray))
            {
                throw new System.ArgumentException($"{FieldNames.HitCounts} must exist and be an array");
            }

            long[] hitCounts = new long[hitCountsArray.Count];
            for (int index = 0; index < hitCountsArray.Count; ++index)
            {
                if (!(hitCountsArray[index] is CosmosNumber cosmosNumber))
                {
                    throw new System.ArgumentException($"{FieldNames.HitCounts} must be an array of numbers");
                }

                hitCounts[index] = Number64.ToLong(cosmosNumber.Value);
            }

            this.TotalWordCount = Number64.ToLong(totalWordCount.Value);
            this.hitCounts = hitCounts;
        }

        private static class FieldNames
        {
            public const string TotalWordCount = "totalWordCount";

            public const string HitCounts = "hitCounts";
        }
    }
}