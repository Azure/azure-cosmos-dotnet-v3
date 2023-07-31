//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal class IndexUtilizationInfoAccumulator
    {
        public IndexUtilizationInfoAccumulator()
        {
            this.IndexUtilizationInfoList = new List<IndexUtilizationInfo>();
        }

        private readonly List<IndexUtilizationInfo> IndexUtilizationInfoList;

        public void Accumulate(IndexUtilizationInfo indexUtilizationInfo)
        {
            if (indexUtilizationInfo == null)
            {
                throw new ArgumentNullException(nameof(indexUtilizationInfo));
            }

            this.IndexUtilizationInfoList.Add(indexUtilizationInfo);
        }

        public IndexUtilizationInfo GetIndexUtilizationInfo()
        {
            IEnumerable<SingleIndexUtilizationEntity> utilizedSingleIndexes = default;
            IEnumerable<SingleIndexUtilizationEntity> potentialSingleIndexes = default;
            IEnumerable<CompositeIndexUtilizationEntity> utilizedCompositeIndexes = default;
            IEnumerable<CompositeIndexUtilizationEntity> potentialCompositeIndexes = default;

            foreach (IndexUtilizationInfo indexUtilizationInfo in this.IndexUtilizationInfoList)
            {
                utilizedSingleIndexes = (utilizedSingleIndexes ?? Enumerable.Empty<SingleIndexUtilizationEntity>()).Concat(indexUtilizationInfo.UtilizedSingleIndexes);
                potentialSingleIndexes = (potentialSingleIndexes ?? Enumerable.Empty<SingleIndexUtilizationEntity>()).Concat(indexUtilizationInfo.PotentialSingleIndexes);
                utilizedCompositeIndexes = (utilizedCompositeIndexes ?? Enumerable.Empty<CompositeIndexUtilizationEntity>()).Concat(indexUtilizationInfo.UtilizedCompositeIndexes);
                potentialCompositeIndexes = (potentialCompositeIndexes ?? Enumerable.Empty<CompositeIndexUtilizationEntity>()).Concat(indexUtilizationInfo.PotentialCompositeIndexes);

            }

            return new IndexUtilizationInfo(
                utilizedSingleIndexes: utilizedSingleIndexes.ToList(),
                potentialSingleIndexes: potentialSingleIndexes.ToList(),
                utilizedCompositeIndexes: utilizedCompositeIndexes.ToList(),
                potentialCompositeIndexes: potentialCompositeIndexes.ToList());
        }
    }
}
