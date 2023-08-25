﻿//------------------------------------------------------------
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
    sealed class SingleIndexUtilizationEntity
    {
        /// <summary>
        /// Initialized a new instance of the Single Index Utilization Entity class.
        /// </summary>
        /// <param name="filterExpression">The filter expression.</param>
        /// <param name="indexDocumentExpression">The index representation of the filter expression.</param>
        /// <param name="filterExpressionPrecision">The precision flag of the filter expression.</param>
        /// <param name="indexPlanFullFidelity">The index plan full fidelity.</param>
        /// <param name="indexImpactScore">The index impact score.</param>
        [JsonConstructor]
        public SingleIndexUtilizationEntity(
            string filterExpression,
            string indexDocumentExpression,
            bool filterExpressionPrecision,
            bool indexPlanFullFidelity,
            string indexImpactScore)
        {
            this.FilterExpression = filterExpression; 
            this.IndexDocumentExpression = indexDocumentExpression; 
            this.FilterExpressionPrecision = filterExpressionPrecision;
            this.IndexPlanFullFidelity = indexPlanFullFidelity;
            this.IndexImpactScore = indexImpactScore;
        }

        [JsonProperty(PropertyName = "FilterExpression")]
        public string FilterExpression { get; }

        [JsonProperty(PropertyName = "IndexSpec")]
        public string IndexDocumentExpression { get; }

        [JsonProperty(PropertyName = "FilterPreciseSet")]
        public bool FilterExpressionPrecision { get; }

        [JsonProperty(PropertyName = "IndexPreciseSet")]
        public bool IndexPlanFullFidelity { get; }

        [JsonProperty(PropertyName = "IndexImpactScore")]
        public string IndexImpactScore { get; }
    }
}