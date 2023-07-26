//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Metrics
{
    using System.Collections.Generic;
    using System.Linq;

    internal ref struct IndexUtilizationInfoAccumulator
    {
        public IndexUtilizationInfoAccumulator(
            IEnumerable<SingleIndexUtilizationEntity> utilizedSingleIndexes,
            IEnumerable<SingleIndexUtilizationEntity> potentialSingleIndexes,
            IEnumerable<CompositeIndexUtilizationEntity> utilizedCompositeIndexes,
            IEnumerable<CompositeIndexUtilizationEntity> potentialCompositeIndexes)
        {
            this.UtilizedSingleIndexes = utilizedSingleIndexes;
            this.PotentialSingleIndexes = potentialSingleIndexes;
            this.UtilizedCompositeIndexes = utilizedCompositeIndexes;
            this.PotentialCompositeIndexes = potentialCompositeIndexes;
        }

        public IEnumerable<SingleIndexUtilizationEntity> UtilizedSingleIndexes { get; set; }
        public IEnumerable<SingleIndexUtilizationEntity> PotentialSingleIndexes { get; set; }
        public IEnumerable<CompositeIndexUtilizationEntity> UtilizedCompositeIndexes { get; set; }
        public IEnumerable<CompositeIndexUtilizationEntity> PotentialCompositeIndexes { get; set; }

        public void Accumulate(IndexUtilizationInfo indexUtilizationInfo)
        {
            this.UtilizedSingleIndexes = (this.UtilizedSingleIndexes ?? Enumerable.Empty<SingleIndexUtilizationEntity>()).Concat(indexUtilizationInfo.UtilizedSingleIndexes);
            this.PotentialSingleIndexes = (this.PotentialSingleIndexes ?? Enumerable.Empty<SingleIndexUtilizationEntity>()).Concat(indexUtilizationInfo.PotentialSingleIndexes);
            this.UtilizedCompositeIndexes = (this.UtilizedCompositeIndexes ?? Enumerable.Empty<CompositeIndexUtilizationEntity>()).Concat(indexUtilizationInfo.UtilizedCompositeIndexes);
            this.PotentialCompositeIndexes = (this.PotentialCompositeIndexes ?? Enumerable.Empty<CompositeIndexUtilizationEntity>()).Concat(indexUtilizationInfo.PotentialCompositeIndexes);
            return;
        }

        public static IndexUtilizationInfo ToIndexUtilizationInfo(IndexUtilizationInfoAccumulator accumulator)
        {
            return new IndexUtilizationInfo(
                utilizedSingleIndexes: accumulator.UtilizedSingleIndexes.ToList(),
                potentialSingleIndexes: accumulator.PotentialSingleIndexes.ToList(),
                utilizedCompositeIndexes: accumulator.UtilizedCompositeIndexes.ToList(),
                potentialCompositeIndexes: accumulator.PotentialCompositeIndexes.ToList());
        }
    }
}
