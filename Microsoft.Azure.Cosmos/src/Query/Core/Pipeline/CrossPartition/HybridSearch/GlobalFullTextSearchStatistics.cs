// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.HybridSearch
{
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.CosmosElements;

    internal sealed class GlobalFullTextSearchStatistics
    {
        public long DocumentCount { get; }

        public IReadOnlyList<FullTextStatistics> FullTextStatistics { get; }

        public GlobalFullTextSearchStatistics(long documentCount, IReadOnlyList<FullTextStatistics> fullTextStatistics)
        {
            this.DocumentCount = documentCount;
            this.FullTextStatistics = fullTextStatistics ?? throw new System.ArgumentNullException($"{nameof(fullTextStatistics)} must not be null.");
        }

        public GlobalFullTextSearchStatistics(CosmosElement cosmosElement)
        {
            if (cosmosElement == null)
            {
                throw new System.ArgumentNullException($"{nameof(cosmosElement)} must not be null.");
            }

            if (!(cosmosElement is CosmosObject cosmosObject))
            {
                throw new System.ArgumentException($"{nameof(cosmosElement)} must be an object.");
            }

            if (!cosmosObject.TryGetValue(FieldNames.DocumentCount, out CosmosNumber cosmosNumber))
            {
                throw new System.ArgumentException($"{FieldNames.DocumentCount} must exist and be a number");
            }

            if (!cosmosObject.TryGetValue(FieldNames.Statistics, out CosmosArray statisticsArray))
            {
                throw new System.ArgumentException($"{FieldNames.Statistics} must exist and be an array");
            }

            List<FullTextStatistics> fullTextStatisticsList = new List<FullTextStatistics>(statisticsArray.Count);
            foreach (CosmosElement statisticsElement in statisticsArray)
            {
                if (!(statisticsElement is CosmosObject))
                {
                    throw new System.ArgumentException($"{FieldNames.Statistics} must be an array of objects");
                }

                FullTextStatistics fullTextStatistics = new FullTextStatistics(statisticsElement as CosmosObject);
                fullTextStatisticsList.Add(fullTextStatistics);
            }

            this.DocumentCount = Number64.ToLong(cosmosNumber.Value);
            this.FullTextStatistics = fullTextStatisticsList;
        }

        private static class FieldNames
        {
            public const string DocumentCount = "documentCount";

            public const string Statistics = "fullTextStatistics";
        }
    }
}