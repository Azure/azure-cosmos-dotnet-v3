//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.Metrics
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;

    /// <summary>
    /// Query preparation metrics in the Azure DocumentDB database service.
    /// </summary>
    internal sealed class QueryPreparationTimes
    {
        internal static readonly QueryPreparationTimes Zero = new QueryPreparationTimes(
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
        [JsonConstructor]
        internal QueryPreparationTimes(
            TimeSpan queryCompilationTime,
            TimeSpan logicalPlanBuildTime,
            TimeSpan physicalPlanBuildTime,
            TimeSpan queryOptimizationTime)
        {
            this.CompileTime = queryCompilationTime;
            this.LogicalPlanBuildTime = logicalPlanBuildTime;
            this.PhysicalPlanBuildTime = physicalPlanBuildTime;
            this.QueryOptimizationTime = queryOptimizationTime;
        }

        /// <summary>
        /// Gets the query compile time in the Azure DocumentDB database service. 
        /// </summary>
        internal TimeSpan QueryCompilationTime
        {
            get
            {
                return this.CompileTime;
            }
        }

        /// <summary>
        /// Gets the query compile time in the Azure DocumentDB database service. 
        /// </summary>
        public TimeSpan CompileTime { get; }

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

        /// <summary>
        /// Creates a new QueryPreparationTimes from the backend delimited string.
        /// </summary>
        /// <param name="delimitedString">The backend delimited string to deserialize from.</param>
        /// <returns>A new QueryPreparationTimes from the backend delimited string.</returns>
        internal static QueryPreparationTimes CreateFromDelimitedString(string delimitedString)
        {
            Dictionary<string, double> metrics = QueryMetricsUtils.ParseDelimitedString(delimitedString);

            return new QueryPreparationTimes(
                QueryMetricsUtils.TimeSpanFromMetrics(metrics, QueryMetricsConstants.QueryCompileTimeInMs),
                QueryMetricsUtils.TimeSpanFromMetrics(metrics, QueryMetricsConstants.LogicalPlanBuildTimeInMs),
                QueryMetricsUtils.TimeSpanFromMetrics(metrics, QueryMetricsConstants.PhysicalPlanBuildTimeInMs),
                QueryMetricsUtils.TimeSpanFromMetrics(metrics, QueryMetricsConstants.QueryOptimizationTimeInMs));
        }

        /// <summary>
        /// Creates a new QueryPreparationTimes that is the sum of all elements in an IEnumerable.
        /// </summary>
        /// <param name="queryPreparationTimesList">The IEnumerable to aggregate.</param>
        /// <returns>A new QueryPreparationTimes that is the sum of all elements in an IEnumerable.</returns>
        internal static QueryPreparationTimes CreateFromIEnumerable(IEnumerable<QueryPreparationTimes> queryPreparationTimesList)
        {
            if (queryPreparationTimesList == null)
            {
                throw new ArgumentNullException("queryPreparationTimesList");
            }

            TimeSpan queryCompilationTime = new TimeSpan();
            TimeSpan logicalPlanBuildTime = new TimeSpan();
            TimeSpan physicalPlanBuildTime = new TimeSpan();
            TimeSpan queryOptimizationTime = new TimeSpan();

            foreach (QueryPreparationTimes queryPreparationTimes in queryPreparationTimesList)
            {
                if (queryPreparationTimes == null)
                {
                    throw new ArgumentException("queryPreparationTimesList can not have a null element");
                }

                queryCompilationTime += queryPreparationTimes.CompileTime;
                logicalPlanBuildTime += queryPreparationTimes.LogicalPlanBuildTime;
                physicalPlanBuildTime += queryPreparationTimes.PhysicalPlanBuildTime;
                queryOptimizationTime += queryPreparationTimes.QueryOptimizationTime;
            }

            return new QueryPreparationTimes(
                queryCompilationTime,
                logicalPlanBuildTime,
                physicalPlanBuildTime,
                queryOptimizationTime);
        }
    }
}