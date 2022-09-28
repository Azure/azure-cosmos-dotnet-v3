﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.Metrics
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;

    /// <summary>
    /// Query index utilization data for composite indexes (sub-structure of the Index Utilization metrics) in the Azure Cosmos database service.
    /// </summary>
#if INTERNAL
#pragma warning disable SA1600
#pragma warning disable CS1591
    public
#else
    internal
#endif
    sealed class CompositeIndexUtilizationEntity
    {
        /// <summary>
        /// Initialized a new instance of the Composite Index Utilization Entity class.
        /// </summary>
        /// <param name="indexDocumentExpressions">The index representation of the filter expression.</param>
        /// <param name="indexPlanFullFidelity">The index plan full fidelity.</param>
        /// <param name="indexImpactScore">The index impact score.</param>
        [JsonConstructor]
        public CompositeIndexUtilizationEntity(
            IReadOnlyList<string> indexDocumentExpressions,
            bool indexPlanFullFidelity,
            string indexImpactScore)
        {
            this.IndexDocumentExpressions = indexDocumentExpressions;
            this.IndexPlanFullFidelity = indexPlanFullFidelity;
            this.IndexImpactScore = indexImpactScore;
        }

        [JsonProperty(PropertyName = "IndexSpecs")]
        public IReadOnlyList<string> IndexDocumentExpressions { get; }

        [JsonProperty(PropertyName = "IndexPreciseSet")]
        public bool IndexPlanFullFidelity { get; }

        [JsonProperty(PropertyName = "IndexImpactScore")]
        public string IndexImpactScore { get; }
    }
}