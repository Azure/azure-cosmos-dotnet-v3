//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.Metrics
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;

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
    sealed class CompositeIndexIndexMetrics
    {
        /// <summary>
        /// Initialized a new instance of an Index Metrics' Composite Index class.
        /// </summary>
        /// <param name="indexDocumentExpressions">The string list representation of the composite index.</param>
        /// <param name="indexImpactScore">The index impact score.</param>
        [JsonConstructor]
        private CompositeIndexIndexMetrics(
            IReadOnlyList<string> indexDocumentExpressions,
            string indexImpactScore)
        {
            this.IndexSpecs = indexDocumentExpressions;
            this.IndexImpactScore = indexImpactScore;
        }

        /// <summary>
        /// String list representation of index paths of a composite index.
        /// </summary>
        [JsonProperty(PropertyName = "IndexSpecs")]
        public IReadOnlyList<string> IndexSpecs { get; }

        /// <summary>
        /// The index impact score of the composite index.
        /// </summary>
        [JsonProperty(PropertyName = "IndexImpactScore")]
        public string IndexImpactScore { get; }
    }
}