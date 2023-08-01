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
        private readonly List<IndexUtilizationInfo> indexUtilizationInfoList;

        public IndexUtilizationInfoAccumulator()
        {
            this.indexUtilizationInfoList = new List<IndexUtilizationInfo>();
        }

        public void Accumulate(IndexUtilizationInfo indexUtilizationInfo)
        {
            if (indexUtilizationInfo == null)
            {
                throw new ArgumentNullException(nameof(indexUtilizationInfo));
            }

            this.indexUtilizationInfoList.Add(indexUtilizationInfo);
        }

        public IndexUtilizationInfo GetIndexUtilizationInfo()
        {
            List<SingleIndexUtilizationEntity> utilizedSingleIndexes = new List<SingleIndexUtilizationEntity>();
            List<SingleIndexUtilizationEntity> potentialSingleIndexes = new List<SingleIndexUtilizationEntity>();
            List<CompositeIndexUtilizationEntity> utilizedCompositeIndexes = new List<CompositeIndexUtilizationEntity>();
            List<CompositeIndexUtilizationEntity> potentialCompositeIndexes = new List<CompositeIndexUtilizationEntity>();

            foreach (IndexUtilizationInfo indexUtilizationInfo in this.indexUtilizationInfoList)
            {
                utilizedSingleIndexes.AddRange(indexUtilizationInfo.UtilizedSingleIndexes);
                potentialSingleIndexes.AddRange(indexUtilizationInfo.PotentialSingleIndexes);
                utilizedCompositeIndexes.AddRange(indexUtilizationInfo.UtilizedCompositeIndexes);
                potentialCompositeIndexes.AddRange(indexUtilizationInfo.PotentialCompositeIndexes);
            }

            return new IndexUtilizationInfo(
                utilizedSingleIndexes: utilizedSingleIndexes.ToList(),
                potentialSingleIndexes: potentialSingleIndexes.ToList(),
                utilizedCompositeIndexes: utilizedCompositeIndexes.ToList(),
                potentialCompositeIndexes: potentialCompositeIndexes.ToList());
        }
    }
}
