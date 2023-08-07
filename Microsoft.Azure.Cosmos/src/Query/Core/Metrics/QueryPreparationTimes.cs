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
        /// Initializes a new instance of the QueryPreparationTimes class.
        /// </summary>
        /// <param name="queryCompilationTime">Query compile and optimization time</param>
        /// <param name="logicalPlanBuildTime">Query logical plan build time</param>
        /// <param name="physicalPlanBuildTime">Query physical plan build time</param>
        /// <param name="queryOptimizationTime">Query optimization time</param>
        public QueryPreparationTimes(
            TimeSpan queryCompilationTime,
            TimeSpan logicalPlanBuildTime,
            TimeSpan physicalPlanBuildTime,
            TimeSpan queryOptimizationTime)
        {
            this.QueryCompilationTime = queryCompilationTime;
            this.LogicalPlanBuildTime = logicalPlanBuildTime;
            this.PhysicalPlanBuildTime = physicalPlanBuildTime;
            this.QueryOptimizationTime = queryOptimizationTime;
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