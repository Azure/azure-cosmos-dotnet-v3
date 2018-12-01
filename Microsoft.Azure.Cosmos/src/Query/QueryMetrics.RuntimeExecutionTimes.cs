//-----------------------------------------------------------------------
// <copyright file="QueryMetrics.RuntimeExecutionTimes.cs" company="Microsoft Corporation">
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
    /// Query runtime execution times in the Azure Cosmos DB service.
    /// </summary>
    internal sealed class RuntimeExecutionTimes
    {
        public static readonly RuntimeExecutionTimes Zero = new RuntimeExecutionTimes(default(TimeSpan), default(TimeSpan), default(TimeSpan));

        /// <summary>
        /// Initializes a new instance of the RuntimeExecutionTimes class.
        /// </summary>
        /// <param name="queryEngineExecutionTime">Query end - to - end execution time</param>
        /// <param name="systemFunctionExecutionTime">Total time spent executing system functions</param>
        /// <param name="userDefinedFunctionExecutionTime">Total time spent executing user - defined functions</param>
        [JsonConstructor]
        internal RuntimeExecutionTimes(
            TimeSpan queryEngineExecutionTime,
            TimeSpan systemFunctionExecutionTime,
            TimeSpan userDefinedFunctionExecutionTime)
        {
            this.QueryEngineExecutionTime = queryEngineExecutionTime;
            this.SystemFunctionExecutionTime = systemFunctionExecutionTime;
            this.UserDefinedFunctionExecutionTime = userDefinedFunctionExecutionTime;
        }

        /// <summary>
        /// Gets the total query runtime execution time in the Azure Cosmos DB service.
        /// </summary>
        public TimeSpan QueryEngineExecutionTime
        {
            get;
        }

        /// <summary>
        /// Gets the query system function execution time in the Azure Cosmos DB service.
        /// </summary>
        public TimeSpan SystemFunctionExecutionTime
        {
            get;
        }

        /// <summary>
        /// Gets the query user defined function execution time in the Azure Cosmos DB service.
        /// </summary>
        public TimeSpan UserDefinedFunctionExecutionTime
        {
            get;
        }

        /// <summary>
        /// Creates a new RuntimeExecutionTimes from the backend delimited string.
        /// </summary>
        /// <param name="delimitedString">The backend delimited string to deserialize from.</param>
        /// <returns>A new RuntimeExecutionTimes from the backend delimited string.</returns>
        public static RuntimeExecutionTimes CreateFromDelimitedString(string delimitedString)
        {
            Dictionary<string, double> metrics = QueryMetricsUtils.ParseDelimitedString(delimitedString);

            TimeSpan vmExecutionTime = QueryMetricsUtils.TimeSpanFromMetrics(
                metrics,
                QueryMetricsConstants.VMExecutionTimeInMs);
            TimeSpan indexLookupTime = QueryMetricsUtils.TimeSpanFromMetrics(
                metrics,
                QueryMetricsConstants.IndexLookupTimeInMs);
            TimeSpan documentLoadTime = QueryMetricsUtils.TimeSpanFromMetrics(
                metrics,
                QueryMetricsConstants.DocumentLoadTimeInMs);
            TimeSpan documentWriteTime = QueryMetricsUtils.TimeSpanFromMetrics(
                metrics,
                QueryMetricsConstants.DocumentWriteTimeInMs);

            return new RuntimeExecutionTimes(
                vmExecutionTime - indexLookupTime - documentLoadTime - documentWriteTime,
                QueryMetricsUtils.TimeSpanFromMetrics(metrics, QueryMetricsConstants.SystemFunctionExecuteTimeInMs),
                QueryMetricsUtils.TimeSpanFromMetrics(metrics, QueryMetricsConstants.UserDefinedFunctionExecutionTimeInMs));
        }

        /// <summary>
        /// Creates a new RuntimeExecutionTimes that is the sum of all elements in an IEnumerable.
        /// </summary>
        /// <param name="runtimeExecutionTimesList">The IEnumerable to aggregate.</param>
        /// <returns>A new RuntimeExecutionTimes that is the sum of all elements in an IEnumerable.</returns>
        public static RuntimeExecutionTimes CreateFromIEnumerable(IEnumerable<RuntimeExecutionTimes> runtimeExecutionTimesList)
        {
            if (runtimeExecutionTimesList == null)
            {
                throw new ArgumentNullException("runtimeExecutionTimesList");
            }

            TimeSpan queryEngineExecutionTime = new TimeSpan();
            TimeSpan systemFunctionExecutionTime = new TimeSpan();
            TimeSpan userDefinedFunctionExecutionTime = new TimeSpan();

            foreach (RuntimeExecutionTimes runtimeExecutionTime in runtimeExecutionTimesList)
            {
                queryEngineExecutionTime += runtimeExecutionTime.QueryEngineExecutionTime;
                systemFunctionExecutionTime += runtimeExecutionTime.SystemFunctionExecutionTime;
                userDefinedFunctionExecutionTime += runtimeExecutionTime.UserDefinedFunctionExecutionTime;
            }

            return new RuntimeExecutionTimes(
                queryEngineExecutionTime,
                systemFunctionExecutionTime,
                userDefinedFunctionExecutionTime);
        }

        /// <summary>
        /// Gets the delimited string as if from a backend response.
        /// </summary>
        /// <returns>The delimited string as if from a backend response.</returns>
        public string ToDelimitedString()
        {
            const string FormatString = "{0}={1:0.00};{2}={3:0.00}";

            // queryEngineExecutionTime is not emitted, since it is calculated as
            // vmExecutionTime - indexLookupTime - documentLoadTime - documentWriteTime
            return string.Format(
                CultureInfo.InvariantCulture,
                FormatString,
                QueryMetricsConstants.SystemFunctionExecuteTimeInMs,
                this.SystemFunctionExecutionTime.TotalMilliseconds,
                QueryMetricsConstants.UserDefinedFunctionExecutionTimeInMs,
                this.UserDefinedFunctionExecutionTime.TotalMilliseconds);
        }

        /// <summary>
        /// Gets a human readable plain text of the RuntimeExecutionTimes (Please use monospace font).
        /// </summary>
        /// <param name="indentLevel">The indent / nesting level of the RuntimeExecutionTimes.</param>
        /// <returns>A human readable plain text of the QueryMetrics.</returns>
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
                    QueryMetricsConstants.RuntimeExecutionTimesText,
                    indentLevel);

                QueryMetricsUtils.AppendMillisecondsToStringBuilder(
                    stringBuilder,
                    QueryMetricsConstants.TotalExecutionTimeText,
                    this.QueryEngineExecutionTime.TotalMilliseconds,
                    indentLevel + 1);

                QueryMetricsUtils.AppendMillisecondsToStringBuilder(
                    stringBuilder,
                    QueryMetricsConstants.SystemFunctionExecuteTimeText,
                    this.SystemFunctionExecutionTime.TotalMilliseconds,
                    indentLevel + 1);

                QueryMetricsUtils.AppendMillisecondsToStringBuilder(
                    stringBuilder,
                    QueryMetricsConstants.UserDefinedFunctionExecutionTimeText,
                    this.UserDefinedFunctionExecutionTime.TotalMilliseconds,
                    indentLevel + 1);
            }

            return stringBuilder.ToString();
        }
    }
}