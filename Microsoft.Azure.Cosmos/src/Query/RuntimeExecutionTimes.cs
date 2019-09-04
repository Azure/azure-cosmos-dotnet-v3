//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;

    /// <summary>
    /// Query runtime execution times in the Azure Cosmos DB service.
    /// </summary>
    internal sealed class RuntimeExecutionTimes
    {
        internal static readonly RuntimeExecutionTimes Zero = new RuntimeExecutionTimes(
            queryEngineExecutionTime: default(TimeSpan),
            systemFunctionExecutionTime: default(TimeSpan),
            userDefinedFunctionExecutionTime: default(TimeSpan));

        private readonly TimeSpan queryEngineExecutionTime;
        private readonly TimeSpan systemFunctionExecutionTime;
        private readonly TimeSpan userDefinedFunctionExecutionTime;

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
            this.queryEngineExecutionTime = queryEngineExecutionTime;
            this.systemFunctionExecutionTime = systemFunctionExecutionTime;
            this.userDefinedFunctionExecutionTime = userDefinedFunctionExecutionTime;
        }

        /// <summary>
        /// Gets the total query runtime execution time in the Azure Cosmos DB service.
        /// </summary>
        internal TimeSpan QueryEngineExecutionTime
        {
            get
            {
                return this.queryEngineExecutionTime;
            }
        }

        /// <summary>
        /// Gets the query system function execution time in the Azure Cosmos DB service.
        /// </summary>
        public TimeSpan SystemFunctionExecutionTime
        {
            get
            {
                return this.systemFunctionExecutionTime;
            }
        }

        /// <summary>
        /// Gets the query user defined function execution time in the Azure Cosmos DB service.
        /// </summary>
        public TimeSpan UserDefinedFunctionExecutionTime
        {
            get
            {
                return this.userDefinedFunctionExecutionTime;
            }
        }

        /// <summary>
        /// Gets the total query runtime execution time in the Azure DocumentDB database service.
        /// </summary>
        public TimeSpan TotalTime
        {
            get
            {
                return this.queryEngineExecutionTime;
            }
        }

        /// <summary>
        /// Creates a new RuntimeExecutionTimes from the backend delimited string.
        /// </summary>
        /// <param name="delimitedString">The backend delimited string to deserialize from.</param>
        /// <returns>A new RuntimeExecutionTimes from the backend delimited string.</returns>
        internal static RuntimeExecutionTimes CreateFromDelimitedString(string delimitedString)
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
        internal static RuntimeExecutionTimes CreateFromIEnumerable(IEnumerable<RuntimeExecutionTimes> runtimeExecutionTimesList)
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
                queryEngineExecutionTime += runtimeExecutionTime.queryEngineExecutionTime;
                systemFunctionExecutionTime += runtimeExecutionTime.systemFunctionExecutionTime;
                userDefinedFunctionExecutionTime += runtimeExecutionTime.userDefinedFunctionExecutionTime;
            }

            return new RuntimeExecutionTimes(
                queryEngineExecutionTime,
                systemFunctionExecutionTime,
                userDefinedFunctionExecutionTime);
        }
    }
}