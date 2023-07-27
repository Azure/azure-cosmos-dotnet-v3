//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Metrics
{
    using System;

    internal struct QueryPreparationTimesAccumulator
    {
        public QueryPreparationTimesAccumulator(TimeSpan queryCompliationTime, TimeSpan logicalPlanBuildTime, TimeSpan physicalPlanBuildTime, TimeSpan queryOptimizationTime)
        {
            this.QueryCompilationTime = queryCompliationTime;
            this.LogicalPlanBuildTime = logicalPlanBuildTime;
            this.PhysicalPlanBuildTime = physicalPlanBuildTime;
            this.QueryOptimizationTime = queryOptimizationTime;
        }

        public TimeSpan QueryCompilationTime { get; set; }
        public TimeSpan LogicalPlanBuildTime { get; set; }
        public TimeSpan PhysicalPlanBuildTime { get; set; }
        public TimeSpan QueryOptimizationTime { get; set; }

        public void Accumulate(QueryPreparationTimes queryPreparationTimes)
        {
            if (queryPreparationTimes == null)
            {
                throw new ArgumentNullException(nameof(queryPreparationTimes));
            }

            this.QueryCompilationTime += queryPreparationTimes.QueryCompilationTime;
            this.LogicalPlanBuildTime += queryPreparationTimes.LogicalPlanBuildTime;
            this.PhysicalPlanBuildTime += queryPreparationTimes.PhysicalPlanBuildTime;
            this.QueryOptimizationTime += queryPreparationTimes.QueryOptimizationTime;
        }

        public static QueryPreparationTimes ToQueryPreparationTimes(QueryPreparationTimesAccumulator accumulator)
        {
            return new QueryPreparationTimes(
                queryCompilationTime: accumulator.QueryCompilationTime,
                logicalPlanBuildTime: accumulator.LogicalPlanBuildTime,
                physicalPlanBuildTime: accumulator.PhysicalPlanBuildTime,
                queryOptimizationTime: accumulator.QueryOptimizationTime);
        }
    }
}
