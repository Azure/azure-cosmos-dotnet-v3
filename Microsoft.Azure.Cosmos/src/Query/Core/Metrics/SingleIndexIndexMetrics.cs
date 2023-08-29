//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.Metrics
{
    using System;
    using Newtonsoft.Json;

    /// <summary>
    /// Query index utilization data for single index (sub-structure of the Index Utilization metrics) in the Azure Cosmos database service.
    /// </summary>
#if INTERNAL
#pragma warning disable SA1600
#pragma warning disable CS1591
    public
#else
    internal
#endif
    sealed class SingleIndexIndexMetrics
    {
        /// <summary>
        /// Initialized a new instance of the Single Index Utilization Entity class.
        /// </summary>
        /// <param name="indexDocumentExpression">The index representation of the filter expression.</param>
        /// <param name="indexImpactScore">The index impact score.</param>
        [JsonConstructor]
        public SingleIndexIndexMetrics(
            string indexDocumentExpression,
            string indexImpactScore)
        {
            this.IndexDocumentExpression = indexDocumentExpression;
            this.IndexImpactScore = indexImpactScore;
        }

        [JsonProperty(PropertyName = "IndexSpec")]
        public string IndexDocumentExpression { get; }

        [JsonProperty(PropertyName = "IndexImpactScore")]
        public string IndexImpactScore { get; }
    }
}