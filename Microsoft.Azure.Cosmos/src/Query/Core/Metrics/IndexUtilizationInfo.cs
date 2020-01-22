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
            utilizedIndexes: new List<IndexUtilizationData>(),
            potentialIndexes: new List<IndexUtilizationData>());

        public IReadOnlyList<IndexUtilizationData> UtilizedIndexes { get; }
        public IReadOnlyList<IndexUtilizationData> PotentialIndexes { get; }

        /// <summary>
        /// Initializes a new instance of the Index Utilization class.
        /// </summary>
        /// <param name="utilizedIndexes">The utilized indexes list</param>
        /// <param name="potentialIndexes">The potential indexes list</param>
        [JsonConstructor]
        public IndexUtilizationInfo(
             IReadOnlyList<IndexUtilizationData> utilizedIndexes,
             IReadOnlyList<IndexUtilizationData> potentialIndexes)
        {
            List<IndexUtilizationData> utilizedIndexesCopy = new List<IndexUtilizationData>();
            List<IndexUtilizationData> potentialIndexesCopy = new List<IndexUtilizationData>();

            if (utilizedIndexes != null)
            {
                foreach (IndexUtilizationData indexUtilizationData in utilizedIndexes)
                {
                    if (indexUtilizationData != null)
                    {
                        utilizedIndexesCopy.Add(indexUtilizationData);
                    }
                }
            }

            if (potentialIndexes != null)
            {
                foreach (IndexUtilizationData indexUtilizationData in potentialIndexes)
                {
                    if (indexUtilizationData != null)
                    {
                        potentialIndexesCopy.Add(indexUtilizationData);
                    }
                }
            }

            this.UtilizedIndexes = utilizedIndexesCopy;
            this.PotentialIndexes = potentialIndexesCopy;
        }

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
                throw new FormatException();
            }

            return indexUtilizationInfo;
        }

        public ref struct Accumulator
        {
            public Accumulator(
                IEnumerable<IndexUtilizationData> utilizedIndexes,
                IEnumerable<IndexUtilizationData> potentialIndexes)
            {
                this.UtilizedIndexes = utilizedIndexes;
                this.PotentialIndexes = potentialIndexes;
            }

            public IEnumerable<IndexUtilizationData> UtilizedIndexes { get; }
            public IEnumerable<IndexUtilizationData> PotentialIndexes { get; }

            public Accumulator Accumulate(IndexUtilizationInfo indexUtilizationInfo)
            {
                return new Accumulator(
                    utilizedIndexes: (this.UtilizedIndexes ?? Enumerable.Empty<IndexUtilizationData>()).Concat(indexUtilizationInfo.UtilizedIndexes),
                    potentialIndexes: (this.PotentialIndexes ?? Enumerable.Empty<IndexUtilizationData>()).Concat(indexUtilizationInfo.PotentialIndexes));
            }

            public static IndexUtilizationInfo ToIndexUtilizationInfo(Accumulator accumulator)
            {
                return new IndexUtilizationInfo(
                    utilizedIndexes: accumulator.UtilizedIndexes.ToList(),
                    potentialIndexes: accumulator.PotentialIndexes.ToList());
            }
        }
    }
}
