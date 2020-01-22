//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.Metrics
{
    using System;

    /// <summary>
    /// Query preparation metrics in the Azure DocumentDB database service.
    /// </summary>
#if INTERNAL
#pragma warning disable SA1600
#pragma warning disable CS1591
    public
#else
    internal
#endif
    sealed class QueryPreparationTimes
    {
        public static readonly QueryPreparationTimes Zero = new QueryPreparationTimes(
            queryCompilationTime: default,
            logicalPlanBuildTime: default,
            physicalPlanBuildTime: default,
            queryOptimizationTime: default);

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

        public ref struct Accumulator
        {
            public Accumulator(TimeSpan queryCompliationTime, TimeSpan logicalPlanBuildTime, TimeSpan physicalPlanBuildTime, TimeSpan queryOptimizationTime)
            {
                this.QueryCompilationTime = queryCompliationTime;
                this.LogicalPlanBuildTime = logicalPlanBuildTime;
                this.PhysicalPlanBuildTime = physicalPlanBuildTime;
                this.QueryOptimizationTime = queryOptimizationTime;
            }

            public TimeSpan QueryCompilationTime { get; }
            public TimeSpan LogicalPlanBuildTime { get; }
            public TimeSpan PhysicalPlanBuildTime { get; }
            public TimeSpan QueryOptimizationTime { get; }

            public Accumulator Accumulate(QueryPreparationTimes queryPreparationTimes)
            {
                if (queryPreparationTimes == null)
                {
                    throw new ArgumentNullException(nameof(queryPreparationTimes));
                }

                return new Accumulator(
                    queryCompliationTime: this.QueryCompilationTime + queryPreparationTimes.QueryCompilationTime,
                    logicalPlanBuildTime: this.LogicalPlanBuildTime + queryPreparationTimes.LogicalPlanBuildTime,
                    physicalPlanBuildTime: this.PhysicalPlanBuildTime + queryPreparationTimes.PhysicalPlanBuildTime,
                    queryOptimizationTime: this.QueryOptimizationTime + queryPreparationTimes.QueryOptimizationTime);
            }

            public static QueryPreparationTimes ToQueryPreparationTimes(QueryPreparationTimes.Accumulator accumulator)
            {
                return new QueryPreparationTimes(
                    queryCompilationTime: accumulator.QueryCompilationTime,
                    logicalPlanBuildTime: accumulator.LogicalPlanBuildTime,
                    physicalPlanBuildTime: accumulator.PhysicalPlanBuildTime,
                    queryOptimizationTime: accumulator.QueryOptimizationTime);
            }
        }
    }
}