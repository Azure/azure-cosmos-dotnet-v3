//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Metrics
{
    using System;
    using System.Collections.Generic;

    internal class QueryPreparationTimesAccumulator
    {
        public QueryPreparationTimesAccumulator()
        {
            this.QueryPreparationTimesList = new List<QueryPreparationTimes>();
        }

        private readonly List<QueryPreparationTimes> QueryPreparationTimesList;

        public void Accumulate(QueryPreparationTimes queryPreparationTimes)
        {
            if (queryPreparationTimes == null)
            {
                throw new ArgumentNullException(nameof(queryPreparationTimes));
            }

            this.QueryPreparationTimesList.Add(queryPreparationTimes);
        }

        public QueryPreparationTimes GetQueryPreparationTimes()
        {
            TimeSpan queryCompilationTime;
            TimeSpan logicalPlanBuildTime;
            TimeSpan physicalPlanBuildTime;
            TimeSpan queryOptimizationTime;

            foreach (QueryPreparationTimes queryPreparationTimes in this.QueryPreparationTimesList)
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
