//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Metrics
{
    using System;
    using System.Collections.Generic;

    internal class QueryPreparationTimesAccumulator
    {
        private readonly List<QueryPreparationTimes> queryPreparationTimesList;

        public QueryPreparationTimesAccumulator()
        {
            this.queryPreparationTimesList = new List<QueryPreparationTimes>();
        }

        public void Accumulate(QueryPreparationTimes queryPreparationTimes)
        {
            if (queryPreparationTimes == null)
            {
                throw new ArgumentNullException(nameof(queryPreparationTimes));
            }

            this.queryPreparationTimesList.Add(queryPreparationTimes);
        }

        public QueryPreparationTimes GetQueryPreparationTimes()
        {
            TimeSpan queryCompilationTime = TimeSpan.Zero;
            TimeSpan logicalPlanBuildTime = TimeSpan.Zero;
            TimeSpan physicalPlanBuildTime = TimeSpan.Zero;
            TimeSpan queryOptimizationTime = TimeSpan.Zero;

            foreach (QueryPreparationTimes queryPreparationTimes in this.queryPreparationTimesList)
            {
                queryCompilationTime += queryPreparationTimes.QueryCompilationTime;
                logicalPlanBuildTime += queryPreparationTimes.LogicalPlanBuildTime;
                physicalPlanBuildTime += queryPreparationTimes.PhysicalPlanBuildTime;
                queryOptimizationTime += queryPreparationTimes.QueryOptimizationTime;
            }

            return new QueryPreparationTimes(
                queryCompilationTime: queryCompilationTime,
                logicalPlanBuildTime: logicalPlanBuildTime,
                physicalPlanBuildTime: physicalPlanBuildTime,
                queryOptimizationTime: queryOptimizationTime);
        }
    }
}
