﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.Cosmos.Core;
    using Microsoft.Azure.Cosmos.Core.Utf8;
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
        /// Initializes a new instance of the Index Utilization class. This is the legacy class of IndexMetricsInfo.
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
            this.UtilizedSingleIndexes = (utilizedSingleIndexes ?? Enumerable.Empty<SingleIndexUtilizationEntity>()).Where(item => item != null).ToList();
            this.PotentialSingleIndexes = (potentialSingleIndexes ?? Enumerable.Empty<SingleIndexUtilizationEntity>()).Where(item => item != null).ToList();
            this.UtilizedCompositeIndexes = (utilizedCompositeIndexes ?? Enumerable.Empty<CompositeIndexUtilizationEntity>()).Where(item => item != null).ToList();
            this.PotentialCompositeIndexes = (potentialCompositeIndexes ?? Enumerable.Empty<CompositeIndexUtilizationEntity>()).Where(item => item != null).ToList();
        }

        public IReadOnlyList<SingleIndexUtilizationEntity> UtilizedSingleIndexes { get; }
        public IReadOnlyList<SingleIndexUtilizationEntity> PotentialSingleIndexes { get; }
        public IReadOnlyList<CompositeIndexUtilizationEntity> UtilizedCompositeIndexes { get; }
        public IReadOnlyList<CompositeIndexUtilizationEntity> PotentialCompositeIndexes { get; }

        /// <summary>
        /// Creates a new IndexUtilizationInfo from the backend delimited base64 encoded string.
        /// </summary>
        /// <param name="delimitedString">The backend delimited string to deserialize from.</param>
        /// <param name="result">The parsed index utilization info</param>
        /// <returns>A new IndexUtilizationInfo from the backend delimited string.</returns>
        internal static bool TryCreateFromDelimitedBase64String(string delimitedString, out IndexUtilizationInfo result)
        {
            if (delimitedString == null)
            {
                result = IndexUtilizationInfo.Empty;
                return true;
            }

            // Even though this parsing is resilient, older version of the SDK doesn't have such lenient parsing.
            // As such, it is right not not possible to remove some of the field in the IndexUtilizationInfo class.
            // However, in newer version of the SDKs, the code base is going to start returning IndexMetricsInfo, 
            // so this class exists solely for legacy support.
            try
            {
                string decodedString = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(delimitedString));

                result = JsonConvert.DeserializeObject<IndexUtilizationInfo>(decodedString, new JsonSerializerSettings()
                {
                    // Allowing null values to be resilient to Json structure change
                    MissingMemberHandling = MissingMemberHandling.Ignore,
                    NullValueHandling = NullValueHandling.Ignore,
                    // Ignore parsing error encountered in desrialization
                    Error = (sender, parsingErrorEvent) => parsingErrorEvent.ErrorContext.Handled = true
                }) ?? IndexUtilizationInfo.Empty;

                return true;
            }
            catch (JsonException)
            {
                result = IndexUtilizationInfo.Empty;
                return false;
            }
        }

        /// <summary>
        /// Materialize the Index Utilization String into Concrete objects.
        /// </summary>
        /// <param name="delimitedString">The index utilization response string as sent by the back end.</param>
        /// <returns>Cpncrete Index utilization object.</returns>
        public static IndexUtilizationInfo CreateFromString(string delimitedString)
        {
            TryCreateFromDelimitedBase64String(delimitedString, out IndexUtilizationInfo indexUtilizationInfo);

            return indexUtilizationInfo;
        }
    }
}
