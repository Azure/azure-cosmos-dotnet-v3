//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;

    /// <summary>
    /// Query index utilization metrics in the Azure Cosmos database service.
    /// </summary>
    internal sealed class IndexUtilizationInfo
    {
        public static readonly IndexUtilizationInfo Empty = new IndexUtilizationInfo(
            utilizedIndexes: new List<IndexUtilizationData>(),
            potentialIndexes: new List<IndexUtilizationData>());

        public IReadOnlyList<IndexUtilizationData> UtilizedIndexes { get; }
        public IReadOnlyList<IndexUtilizationData> PotentialIndexes { get; }

        /// <summary>
        /// Iniialized a new instance of the Index Utilization class.
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
        /// <param name="delimitedString">The backend delimted string to desrialize from.</param>
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

        /// <summary>
        /// Creates a new IndexUtilizationInfo that is the sum of all elements in an IEnumerable.
        /// </summary>
        /// <param name="indexUtilizationInfoList">The IEnumerable to aggregate.</param>
        /// <returns>A new QueryPreparationTimes that is the sum of all elements in an IEnumerable.</returns>
        internal static IndexUtilizationInfo CreateFromIEnumerable(IEnumerable<IndexUtilizationInfo> indexUtilizationInfoList)
        {
            if (indexUtilizationInfoList == null)
            {
                throw new ArgumentNullException(nameof(indexUtilizationInfoList));
            }

            List<IndexUtilizationData> utilizedIndexesCopy = new List<IndexUtilizationData>();
            List<IndexUtilizationData> potentialIndexesCopy = new List<IndexUtilizationData>();
            foreach (IndexUtilizationInfo indexUtilizationInfo in indexUtilizationInfoList)
            {
                if (indexUtilizationInfo == null)
                {
                    throw new ArgumentException(nameof(indexUtilizationInfoList) + " can not have a null element");
                }

                utilizedIndexesCopy.AddRange(indexUtilizationInfo.UtilizedIndexes);
                potentialIndexesCopy.AddRange(indexUtilizationInfo.PotentialIndexes);
            }

            return new IndexUtilizationInfo(utilizedIndexesCopy, potentialIndexesCopy);
        }
    }
}