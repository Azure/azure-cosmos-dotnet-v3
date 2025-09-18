//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.Metrics
{
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Query index utilization data for composite indexes (sub-structure of the Index Metrics class) in the Azure Cosmos database service.
    /// </summary>
#if INTERNAL
#pragma warning disable SA1600
#pragma warning disable CS1591
    public
#else
    internal
#endif
    sealed class IndexMetricsInfo
    {
        /// <summary>
        /// Initializes a new instance of the Index Metrics class.
        /// </summary>
        /// <param name="utilizedEntity">The utilized indexes</param>
        /// <param name="potentialEntity">The potential indexes</param>
        [JsonConstructor]
        public IndexMetricsInfo(
             IndexMetricsInfoEntity utilizedEntity,
             IndexMetricsInfoEntity potentialEntity)
        {
            this.UtilizedEntity = utilizedEntity;
            this.PotentialEntity = potentialEntity;
        }

        [JsonPropertyName("Utilized")]
        public IndexMetricsInfoEntity UtilizedEntity { get; }

        [JsonPropertyName("Potential")]
        public IndexMetricsInfoEntity PotentialEntity { get; }

        /// <summary>
        /// Creates a new IndexMetricsInfo from the backend delimited string.
        /// </summary>
        /// <param name="delimitedString">The backend delimited string to deserialize from.</param>
        /// <param name="result">The parsed index utilization info</param>
        /// <returns>A new IndexMetricsInfo from the backend delimited string.</returns>
        public static bool TryCreateFromString(string delimitedString, out IndexMetricsInfo result)
        {
            if (delimitedString == null)
            {
                result = null;
                return false;
            }

            try
            {
                // Decode and deserialize the response string
                string decodedString = System.Web.HttpUtility.UrlDecode(delimitedString, Encoding.UTF8);

                result = JsonSerializer.Deserialize<IndexMetricsInfo>(decodedString, new JsonSerializerOptions()
                    {
                        // Allowing null values to be resilient to Json structure change
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                    }) ?? null;

                return true;
            }
            catch (JsonException)
            {
                result = null;
                return false;
            }
        }
    }
}
