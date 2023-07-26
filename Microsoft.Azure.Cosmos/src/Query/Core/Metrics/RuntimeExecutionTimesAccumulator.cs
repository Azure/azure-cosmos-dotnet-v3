//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Metrics
{
    using System;

    internal ref struct RuntimeExecutionTimesAccumulator
    {
        public RuntimeExecutionTimesAccumulator(TimeSpan queryEngineExecutionTime, TimeSpan systemFunctionExecutionTime, TimeSpan userDefinedFunctionExecutionTimes)
        {
            this.QueryEngineExecutionTime = queryEngineExecutionTime;
            this.SystemFunctionExecutionTime = systemFunctionExecutionTime;
            this.UserDefinedFunctionExecutionTime = userDefinedFunctionExecutionTimes;
        }

        public TimeSpan QueryEngineExecutionTime { get; set; }
        public TimeSpan SystemFunctionExecutionTime { get; set; }
        public TimeSpan UserDefinedFunctionExecutionTime { get; set; }
        public void Accumulate(RuntimeExecutionTimes runtimeExecutionTimes)
        {
            if (runtimeExecutionTimes == null)
            {
                throw new ArgumentNullException(nameof(runtimeExecutionTimes));
            }

            this.QueryEngineExecutionTime += runtimeExecutionTimes.QueryEngineExecutionTime;
            this.SystemFunctionExecutionTime += runtimeExecutionTimes.SystemFunctionExecutionTime;
            this.UserDefinedFunctionExecutionTime += runtimeExecutionTimes.UserDefinedFunctionExecutionTime;
        }

        public static RuntimeExecutionTimes ToRuntimeExecutionTimes(RuntimeExecutionTimesAccumulator accumulator)
        {
            return new RuntimeExecutionTimes(
                queryEngineExecutionTime: accumulator.QueryEngineExecutionTime,
                systemFunctionExecutionTime: accumulator.SystemFunctionExecutionTime,
                userDefinedFunctionExecutionTime: accumulator.UserDefinedFunctionExecutionTime);
        }
    }
}
