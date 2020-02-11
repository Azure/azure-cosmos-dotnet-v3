//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.Metrics
{
    using System;
    using Newtonsoft.Json;

    /// <summary>
    /// Query index utilization data (sub-structure of the Index Utilization metrics) in the Azure Cosmos database service.
    /// </summary>
#if INTERNAL
#pragma warning disable SA1600
#pragma warning disable CS1591
    public
#else
    internal
#endif
    sealed class IndexUtilizationData
    {
        [JsonProperty(PropertyName = "FilterExpression")]
        public string FilterExpression { get; }

        [JsonProperty(PropertyName = "IndexSpec")]
        public string IndexDocumentExpression { get; }

        [JsonProperty(PropertyName = "FilterPreciseSet")]
        public bool FilterExpressionPrecision { get; }

        [JsonProperty(PropertyName = "IndexPreciseSet")]
        public bool IndexPlanFullFidelity { get; }

        /// <summary>
        /// Iniialized a new instance of the Index Utilization Data class.
        /// </summary>
        /// <param name="filterExpression">The filter expression.</param>
        /// <param name="indexDocumentExpression">The index representation of the filter expression.</param>
        /// <param name="filterExpressionPrecision">The precision flag of the filter expression.</param>
        /// <param name="indexPlanFullFidelity">The index plan full fidelity.</param>
        [JsonConstructor]
        public IndexUtilizationData(
            string filterExpression,
            string indexDocumentExpression,
            bool filterExpressionPrecision,
            bool indexPlanFullFidelity)
        {
            this.FilterExpression = filterExpression ?? throw new ArgumentNullException(nameof(filterExpression));
            this.IndexDocumentExpression = indexDocumentExpression ?? throw new ArgumentNullException(nameof(indexDocumentExpression));
            this.FilterExpressionPrecision = filterExpressionPrecision;
            this.IndexPlanFullFidelity = indexPlanFullFidelity;
        }
    }
}