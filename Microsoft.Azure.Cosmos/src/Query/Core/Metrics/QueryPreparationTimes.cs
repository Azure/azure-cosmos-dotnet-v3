//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;

    /// <summary>
    /// Query preparation metrics in the Azure DocumentDB database service.
    /// </summary>
    public sealed class QueryPreparationTimes
    {
        /// <summary>
        /// Initializes a new instance of the QueryPreparationTimes class.
        /// </summary>
        /// <param name="queryPreparationTimesInternal"></param>
        internal QueryPreparationTimes(QueryPreparationTimesInternal queryPreparationTimesInternal)
        {
            this.QueryCompilationTime = queryPreparationTimesInternal.QueryCompilationTime;
            this.LogicalPlanBuildTime = queryPreparationTimesInternal.LogicalPlanBuildTime;
            this.PhysicalPlanBuildTime = queryPreparationTimesInternal.PhysicalPlanBuildTime;
            this.QueryOptimizationTime = queryPreparationTimesInternal.QueryOptimizationTime;
        }

        /// <summary>
        /// Gets the query compile time in the Azure DocumentDB database service. 
        /// </summary>
        public TimeSpan QueryCompilationTime { get; }

        /// <summary>
        /// Gets the query logical plan build time in the Azure DocumentDB database service. 
        /// </summary>
        public TimeSpan LogicalPlanBuildTime { get; }

        /// <summary>
        /// Gets the query physical plan build time in the Azure DocumentDB database service. 
        /// </summary>
        public TimeSpan PhysicalPlanBuildTime { get; }

        /// <summary>
        /// Gets the query optimization time in the Azure DocumentDB database service. 
        /// </summary>
        public TimeSpan QueryOptimizationTime { get; }
    }
}