//-----------------------------------------------------------------------
// <copyright file="QueryMetrics.QueryPreparationTimes.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Newtonsoft.Json;

    /// <summary>
    /// Query preparation metrics in the Azure DocumentDB database service.
    /// </summary>
    internal sealed class QueryPreparationTimes
    {
        public static readonly QueryPreparationTimes Zero = new QueryPreparationTimes(default(TimeSpan), default(TimeSpan), default(TimeSpan), default(TimeSpan));

        /// <summary>
        /// Initializes a new instance of the QueryPreparationTimes class.
        /// </summary>
        /// <param name="queryCompilationTime">Query compile and optimization time</param>
        /// <param name="logicalPlanBuildTime">Query logical plan build time</param>
        /// <param name="physicalPlanBuildTime">Query physical plan build time</param>
        /// <param name="queryOptimizationTime">Query optimization time</param>
        [JsonConstructor]
        public QueryPreparationTimes(TimeSpan queryCompilationTime, TimeSpan logicalPlanBuildTime, TimeSpan physicalPlanBuildTime, TimeSpan queryOptimizationTime)
        {
            this.QueryCompilationTime = queryCompilationTime;
            this.LogicalPlanBuildTime = logicalPlanBuildTime;
            this.PhysicalPlanBuildTime = physicalPlanBuildTime;
            this.QueryOptimizationTime = queryOptimizationTime;
        }

        /// <summary>
        /// Gets the query compile time in the Azure DocumentDB database service. 
        /// </summary>
        public TimeSpan QueryCompilationTime
        {
            get;
        }

        /// <summary>
        /// Gets the query logical plan build time in the Azure DocumentDB database service. 
        /// </summary>
        public TimeSpan LogicalPlanBuildTime
        {
            get;
        }

        /// <summary>
        /// Gets the query physical plan build time in the Azure DocumentDB database service. 
        /// </summary>
        public TimeSpan PhysicalPlanBuildTime
        {
            get;
        }

        /// <summary>
        /// Gets the query optimization time in the Azure DocumentDB database service. 
        /// </summary>
        public TimeSpan QueryOptimizationTime
        {
            get;
        }

        /// <summary>
        /// Creates a new QueryPreparationTimes from the backend delimited string.
        /// </summary>
        /// <param name="delimitedString">The backend delimited string to deserialize from.</param>
        /// <returns>A new QueryPreparationTimes from the backend delimited string.</returns>
        public static QueryPreparationTimes CreateFromDelimitedString(string delimitedString)
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
        public static QueryPreparationTimes CreateFromIEnumerable(IEnumerable<QueryPreparationTimes> queryPreparationTimesList)
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

                queryCompilationTime += queryPreparationTimes.QueryCompilationTime;
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

        /// <summary>
        /// Gets a human readable plain text of the QueryPreparationTimes (Please use monospace font).
        /// </summary>
        /// <param name="indentLevel">The indent / nesting level of the QueryPreparationTimes object.</param>
        /// <returns>A human readable plain text of the QueryPreparationTimes.</returns>
        public string ToTextString(int indentLevel = 0)
        {
            if (indentLevel == int.MaxValue)
            {
                throw new ArgumentOutOfRangeException("indentLevel", "input must be less than Int32.MaxValue");
            }

            StringBuilder stringBuilder = new StringBuilder();

            // Checked block is needed to suppress potential overflow warning ... even though I check it above
            checked
            {
                QueryMetricsUtils.AppendHeaderToStringBuilder(
                    stringBuilder,
                    QueryMetricsConstants.QueryPreparationTimesText,
                    indentLevel);

                QueryMetricsUtils.AppendMillisecondsToStringBuilder(
                    stringBuilder,
                    QueryMetricsConstants.QueryCompileTimeText,
                    this.QueryCompilationTime.TotalMilliseconds,
                    indentLevel + 1);

                QueryMetricsUtils.AppendMillisecondsToStringBuilder(
                    stringBuilder,
                    QueryMetricsConstants.LogicalPlanBuildTimeText,
                    this.LogicalPlanBuildTime.TotalMilliseconds,
                    indentLevel + 1);

                QueryMetricsUtils.AppendMillisecondsToStringBuilder(
                    stringBuilder,
                    QueryMetricsConstants.PhysicalPlanBuildTimeText,
                    this.PhysicalPlanBuildTime.TotalMilliseconds,
                    indentLevel + 1);

                QueryMetricsUtils.AppendMillisecondsToStringBuilder(
                    stringBuilder,
                    QueryMetricsConstants.QueryOptimizationTimeText,
                    this.QueryOptimizationTime.TotalMilliseconds,
                    indentLevel + 1);
            }

            return stringBuilder.ToString();
        }

        /// <summary>
        /// Gets the delimited stringified as if from a backend response.
        /// </summary>
        /// <returns>The delimited stringified as if from a backend response.</returns>
        public string ToDelimitedString()
        {
            const string FormatString = "{0}={1:0.00};{2}={3:0.00};{4}={5:0.00};{6}={7:0.00}";
            return string.Format(
                CultureInfo.InvariantCulture,
                FormatString,
                QueryMetricsConstants.QueryCompileTimeInMs,
                this.QueryCompilationTime.TotalMilliseconds,
                QueryMetricsConstants.LogicalPlanBuildTimeInMs,
                this.LogicalPlanBuildTime.TotalMilliseconds,
                QueryMetricsConstants.PhysicalPlanBuildTimeInMs,
                this.PhysicalPlanBuildTime.TotalMilliseconds,
                QueryMetricsConstants.QueryOptimizationTimeInMs,
                this.QueryOptimizationTime.TotalMilliseconds);
        }
    }
}