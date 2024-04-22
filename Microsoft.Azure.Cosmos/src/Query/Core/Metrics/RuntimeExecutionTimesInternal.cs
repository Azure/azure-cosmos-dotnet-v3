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
    sealed class RuntimeExecutionTimesInternal
    {
        public static readonly RuntimeExecutionTimesInternal Empty = new RuntimeExecutionTimesInternal(
            queryEngineExecutionTime: TimeSpan.Zero,
            systemFunctionExecutionTime: TimeSpan.Zero,
            userDefinedFunctionExecutionTime: TimeSpan.Zero);

        /// <summary>
        /// Initializes a new instance of the RuntimeExecutionTimes class.
        /// </summary>
        /// <param name="queryEngineExecutionTime">Query end - to - end execution time</param>
        /// <param name="systemFunctionExecutionTime">Total time spent executing system functions</param>
        /// <param name="userDefinedFunctionExecutionTime">Total time spent executing user - defined functions</param>
        public RuntimeExecutionTimesInternal(
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
    }
}