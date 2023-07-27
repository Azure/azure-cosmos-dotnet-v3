//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Metrics
{
    using System;

    internal class QueryPreparationTimesAccumulator
    {
        public QueryPreparationTimesAccumulator(TimeSpan queryCompliationTime, TimeSpan logicalPlanBuildTime, TimeSpan physicalPlanBuildTime, TimeSpan queryOptimizationTime)
        {
            this.QueryCompilationTime = queryCompliationTime;
            this.LogicalPlanBuildTime = logicalPlanBuildTime;
            this.PhysicalPlanBuildTime = physicalPlanBuildTime;
            this.QueryOptimizationTime = queryOptimizationTime;
        }

        public QueryPreparationTimesAccumulator()
        {
            this.QueryCompilationTime = default;
            this.LogicalPlanBuildTime = default;
            this.PhysicalPlanBuildTime = default;
            this.QueryOptimizationTime = default;
        }

        private TimeSpan QueryCompilationTime { get; set; }
        private TimeSpan LogicalPlanBuildTime { get; set; }
        private TimeSpan PhysicalPlanBuildTime { get; set; }
        private TimeSpan QueryOptimizationTime { get; set; }

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

        public QueryPreparationTimes GetQueryPreparationTimes()
        {
            return new QueryPreparationTimes(
                queryCompilationTime: this.QueryCompilationTime,
                logicalPlanBuildTime: this.LogicalPlanBuildTime,
                physicalPlanBuildTime: this.PhysicalPlanBuildTime,
                queryOptimizationTime: this.QueryOptimizationTime);
        }
    }
}
