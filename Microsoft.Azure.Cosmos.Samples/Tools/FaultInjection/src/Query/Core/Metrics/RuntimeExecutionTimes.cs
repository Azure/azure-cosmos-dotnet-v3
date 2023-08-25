//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.Metrics
{
    using System;

    /// <summary>
    /// Query runtime execution times in the Azure Cosmos DB service.
    /// </summary>
#if INTERNAL
#pragma warning disable SA1600
#pragma warning disable CS1591
    public
#else
    internal
#endif
    sealed class RuntimeExecutionTimes
    {
        public static readonly RuntimeExecutionTimes Empty = new RuntimeExecutionTimes(
            queryEngineExecutionTime: default,
            systemFunctionExecutionTime: default,
            userDefinedFunctionExecutionTime: default);

        /// <summary>
        /// Initializes a new instance of the RuntimeExecutionTimes class.
        /// </summary>
        /// <param name="queryEngineExecutionTime">Query end - to - end execution time</param>
        /// <param name="systemFunctionExecutionTime">Total time spent executing system functions</param>
        /// <param name="userDefinedFunctionExecutionTime">Total time spent executing user - defined functions</param>
        public RuntimeExecutionTimes(
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
        public TimeSpan QueryEngineExecutionTime { get; }

        /// <summary>
        /// Gets the query system function execution time in the Azure Cosmos DB service.
        /// </summary>
        public TimeSpan SystemFunctionExecutionTime { get; }

        /// <summary>
        /// Gets the query user defined function execution time in the Azure Cosmos DB service.
        /// </summary>
        public TimeSpan UserDefinedFunctionExecutionTime { get; }

        public ref struct Accumulator
        {
            public Accumulator(TimeSpan queryEngineExecutionTime, TimeSpan systemFunctionExecutionTime, TimeSpan userDefinedFunctionExecutionTimes)
            {
                this.QueryEngineExecutionTime = queryEngineExecutionTime;
                this.SystemFunctionExecutionTime = systemFunctionExecutionTime;
                this.UserDefinedFunctionExecutionTime = userDefinedFunctionExecutionTimes;
            }

            public TimeSpan QueryEngineExecutionTime { get; set; }
            public TimeSpan SystemFunctionExecutionTime { get; set; }
            public TimeSpan UserDefinedFunctionExecutionTime { get; set; }

            public Accumulator Accumulate(RuntimeExecutionTimes runtimeExecutionTimes)
            {
                if (runtimeExecutionTimes == null)
                {
                    throw new ArgumentNullException(nameof(runtimeExecutionTimes));
                }

                return new Accumulator(
                    queryEngineExecutionTime: this.QueryEngineExecutionTime + runtimeExecutionTimes.QueryEngineExecutionTime,
                    systemFunctionExecutionTime: this.SystemFunctionExecutionTime + runtimeExecutionTimes.SystemFunctionExecutionTime,
                    userDefinedFunctionExecutionTimes: this.UserDefinedFunctionExecutionTime + runtimeExecutionTimes.UserDefinedFunctionExecutionTime);
            }

            public static RuntimeExecutionTimes ToRuntimeExecutionTimes(Accumulator accumulator)
            {
                return new RuntimeExecutionTimes(
                    queryEngineExecutionTime: accumulator.QueryEngineExecutionTime,
                    systemFunctionExecutionTime: accumulator.SystemFunctionExecutionTime,
                    userDefinedFunctionExecutionTime: accumulator.UserDefinedFunctionExecutionTime);
            }
        }
    }
}