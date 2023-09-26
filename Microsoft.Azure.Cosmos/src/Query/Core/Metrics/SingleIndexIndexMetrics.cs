//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.Metrics
{
    using System;
    using Newtonsoft.Json;

    /// <summary>
    /// Query index utilization data for single indexes (sub-structure of the Index Metrics class) in the Azure Cosmos database service.
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
        /// Initialized a new instance of an Index Metrics' Single Index class.
        /// </summary>
        /// <param name="indexDocumentExpression">The string representation of the single index.</param>
        /// <param name="indexImpactScore">The index impact score.</param>
        [JsonConstructor]
        public SingleIndexIndexMetrics(
            string indexDocumentExpression,
            string indexImpactScore)
        {
            this.IndexDocumentExpression = indexDocumentExpression;
            this.IndexImpactScore = indexImpactScore;
        }

        /// <summary>
        /// String representation of index paths of a composite index.
        /// </summary>
        [JsonProperty(PropertyName = "IndexSpec")]
        public string IndexDocumentExpression { get; }

        /// <summary>
        /// The index impact score of the single index.
        /// </summary>
        [JsonProperty(PropertyName = "IndexImpactScore")]
        public string IndexImpactScore { get; }
    }
}