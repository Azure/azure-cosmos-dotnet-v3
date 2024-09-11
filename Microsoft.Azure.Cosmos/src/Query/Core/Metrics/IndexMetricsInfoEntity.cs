//------------------------------------------------------------
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
    sealed class IndexMetricsInfoEntity
    {
        /// <summary>
        /// Initializes a new instance of the Index Utilization class. This is the legacy class of IndexMetricsInfoEntity.
        /// </summary>
        /// <param name="singleIndexes">The utilized single indexes list</param>
        /// <param name="compositeIndexes">The potential single indexes list</param>
        [JsonConstructor]
        public IndexMetricsInfoEntity(
             IReadOnlyList<SingleIndexIndexMetrics> singleIndexes,
             IReadOnlyList<CompositeIndexIndexMetrics> compositeIndexes)
        {
            this.SingleIndexes = (singleIndexes ?? Enumerable.Empty<SingleIndexIndexMetrics>()).Where(item => item != null).ToList();
            this.CompositeIndexes = (compositeIndexes ?? Enumerable.Empty<CompositeIndexIndexMetrics>()).Where(item => item != null).ToList();
        }

        public IReadOnlyList<SingleIndexIndexMetrics> SingleIndexes { get; }
        public IReadOnlyList<CompositeIndexIndexMetrics> CompositeIndexes { get; }
    }
}
