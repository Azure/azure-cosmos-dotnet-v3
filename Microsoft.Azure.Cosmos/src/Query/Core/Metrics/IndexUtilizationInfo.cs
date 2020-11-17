//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Newtonsoft.Json;

    /// <summary>
    /// Query index utilization metrics in the Azure Cosmos database service.
    /// </summary>
#if INTERNAL
#pragma warning disable SA1600
#pragma warning disable CS1591
    public
#else
    internal
#endif
    sealed class IndexUtilizationInfo
    {
        public static readonly IndexUtilizationInfo Empty = new IndexUtilizationInfo(
            utilizedSingleIndexes: new List<SingleIndexUtilizationEntity>(),
            potentialSingleIndexes: new List<SingleIndexUtilizationEntity>(),
            utilizedCompositeIndexes: new List<CompositeIndexUtilizationEntity>(),
            potentialCompositeIndexes: new List<CompositeIndexUtilizationEntity>());
        
        /// <summary>
        /// Initializes a new instance of the Index Utilization class.
        /// </summary>
        /// <param name="utilizedSingleIndexes">The utilized single indexes list</param>
        /// <param name="potentialSingleIndexes">The potential single indexes list</param>
        /// <param name="utilizedCompositeIndexes">The potential composite indexes list</param>
        /// <param name="potentialCompositeIndexes">The utilized composite indexes list</param>
        [JsonConstructor]
        public IndexUtilizationInfo(
             IReadOnlyList<SingleIndexUtilizationEntity> utilizedSingleIndexes,
             IReadOnlyList<SingleIndexUtilizationEntity> potentialSingleIndexes,
             IReadOnlyList<CompositeIndexUtilizationEntity> utilizedCompositeIndexes,
             IReadOnlyList<CompositeIndexUtilizationEntity> potentialCompositeIndexes)
        {
            List<SingleIndexUtilizationEntity> utilizedSingleIndexesCopy = new List<SingleIndexUtilizationEntity>();
            List<SingleIndexUtilizationEntity> potentialSingleIndexesCopy = new List<SingleIndexUtilizationEntity>();
            List<CompositeIndexUtilizationEntity> utilizedCompositeIndexesCopy = new List<CompositeIndexUtilizationEntity>();
            List<CompositeIndexUtilizationEntity> potentialCompositeIndexesCopy = new List<CompositeIndexUtilizationEntity>();

            if (utilizedSingleIndexes == null)
            {
                throw new ArgumentNullException(nameof(utilizedSingleIndexes));
            }

            if (potentialSingleIndexes == null)
            {
                throw new ArgumentNullException(nameof(potentialSingleIndexes));
            }

            if (utilizedCompositeIndexes == null)
            {
                throw new ArgumentNullException(nameof(utilizedCompositeIndexes));
            }

            if (potentialCompositeIndexes == null)
            {
                throw new ArgumentNullException(nameof(potentialCompositeIndexes));
            }

            foreach (SingleIndexUtilizationEntity indexUtilizationEntity in utilizedSingleIndexes)
            {
                if (indexUtilizationEntity == null)
                {
                    throw new ArgumentNullException(nameof(indexUtilizationEntity));
                }

                utilizedSingleIndexesCopy.Add(indexUtilizationEntity);
            }

            foreach (SingleIndexUtilizationEntity indexUtilizationEntity in potentialSingleIndexes)
            {
                if (indexUtilizationEntity == null)
                {
                    throw new ArgumentNullException(nameof(indexUtilizationEntity));
                }

                potentialSingleIndexesCopy.Add(indexUtilizationEntity);
            }

            foreach (CompositeIndexUtilizationEntity indexUtilizationEntity in utilizedCompositeIndexes)
            {
                if (indexUtilizationEntity == null)
                {
                    throw new ArgumentNullException(nameof(indexUtilizationEntity));
                }

                utilizedCompositeIndexesCopy.Add(indexUtilizationEntity);
            }

            foreach (CompositeIndexUtilizationEntity indexUtilizationEntity in potentialCompositeIndexes)
            {
                if (indexUtilizationEntity == null)
                {
                    throw new ArgumentNullException(nameof(indexUtilizationEntity));
                }

                potentialCompositeIndexesCopy.Add(indexUtilizationEntity);
            }
            this.UtilizedSingleIndexes = utilizedSingleIndexesCopy;
            this.PotentialSingleIndexes = potentialSingleIndexesCopy;
            this.UtilizedCompositeIndexes = utilizedCompositeIndexesCopy;
            this.PotentialCompositeIndexes = potentialCompositeIndexesCopy;
        }

        public IReadOnlyList<SingleIndexUtilizationEntity> UtilizedSingleIndexes { get; }
        public IReadOnlyList<SingleIndexUtilizationEntity> PotentialSingleIndexes { get; }
        public IReadOnlyList<CompositeIndexUtilizationEntity> UtilizedCompositeIndexes { get; }
        public IReadOnlyList<CompositeIndexUtilizationEntity> PotentialCompositeIndexes { get; }

        /// <summary>
        /// Creates a new IndexUtilizationInfo from the backend delimited string.
        /// </summary>
        /// <param name="delimitedString">The backend delimited string to deserialize from.</param>
        /// <param name="result">The parsed index utilization info</param>
        /// <returns>A new IndexUtilizationInfo from the backend delimited string.</returns>
        internal static bool TryCreateFromDelimitedString(string delimitedString, out IndexUtilizationInfo result)
        {
            if (delimitedString == null)
            {
                result = IndexUtilizationInfo.Empty;
                return true;
            }
            try
            {
                string indexString = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(delimitedString));

                result = JsonConvert.DeserializeObject<IndexUtilizationInfo>(indexString);
                if (result == null)
                {
                    result = IndexUtilizationInfo.Empty;
                }
                return true;
            }
            catch
            {
                result = IndexUtilizationInfo.Empty;
                return false;
            }
        }

        public static IndexUtilizationInfo CreateFromString(string delimitedString)
        {
            if (!TryCreateFromDelimitedString(delimitedString, out IndexUtilizationInfo indexUtilizationInfo))
            {
                throw new FormatException($"Failed to parse {nameof(IndexUtilizationInfo)} : '{delimitedString}'");
            }

            return indexUtilizationInfo;
        }

        public ref struct Accumulator
        {
            public Accumulator(
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

            public IEnumerable<SingleIndexUtilizationEntity> UtilizedSingleIndexes { get; }
            public IEnumerable<SingleIndexUtilizationEntity> PotentialSingleIndexes { get; }
            public IEnumerable<CompositeIndexUtilizationEntity> UtilizedCompositeIndexes { get; }
            public IEnumerable<CompositeIndexUtilizationEntity> PotentialCompositeIndexes { get; }

            public Accumulator Accumulate(IndexUtilizationInfo indexUtilizationInfo)
            {
                return new Accumulator(
                    utilizedSingleIndexes: (this.UtilizedSingleIndexes ?? Enumerable.Empty<SingleIndexUtilizationEntity>()).Concat(indexUtilizationInfo.UtilizedSingleIndexes),
                    potentialSingleIndexes: (this.PotentialSingleIndexes ?? Enumerable.Empty<SingleIndexUtilizationEntity>()).Concat(indexUtilizationInfo.PotentialSingleIndexes),
                    utilizedCompositeIndexes: (this.UtilizedCompositeIndexes ?? Enumerable.Empty<CompositeIndexUtilizationEntity>()).Concat(indexUtilizationInfo.UtilizedCompositeIndexes),
                    potentialCompositeIndexes: (this.PotentialCompositeIndexes ?? Enumerable.Empty<CompositeIndexUtilizationEntity>()).Concat(indexUtilizationInfo.PotentialCompositeIndexes));
            }

            public static IndexUtilizationInfo ToIndexUtilizationInfo(Accumulator accumulator)
            {
                return new IndexUtilizationInfo(
                    utilizedSingleIndexes: accumulator.UtilizedSingleIndexes.ToList(),
                    potentialSingleIndexes: accumulator.PotentialSingleIndexes.ToList(),
                    utilizedCompositeIndexes: accumulator.UtilizedCompositeIndexes.ToList(),
                    potentialCompositeIndexes: accumulator.PotentialCompositeIndexes.ToList());
            }
        }
    }
}
