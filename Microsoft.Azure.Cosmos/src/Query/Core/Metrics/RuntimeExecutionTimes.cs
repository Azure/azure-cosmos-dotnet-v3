//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;

    /// <summary>
    /// Query runtime execution times in the Azure Cosmos DB service.
    /// </summary>
    public sealed class RuntimeExecutionTimes
    {
        /// <summary>
        /// Initializes a new instance of the RuntimeExecutionTimes class.
        /// </summary>
        /// <param name="runtimeExecutionTimesInternal"></param>
        internal RuntimeExecutionTimes(RuntimeExecutionTimesInternal runtimeExecutionTimesInternal)
        {
            this.QueryEngineExecutionTime = runtimeExecutionTimesInternal.QueryEngineExecutionTime;
            this.SystemFunctionExecutionTime = runtimeExecutionTimesInternal.SystemFunctionExecutionTime;
            this.UserDefinedFunctionExecutionTime = runtimeExecutionTimesInternal.UserDefinedFunctionExecutionTime;
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