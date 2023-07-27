//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Metrics
{
    using System;

    internal class RuntimeExecutionTimesAccumulator
    {
        public RuntimeExecutionTimesAccumulator(TimeSpan queryEngineExecutionTime, TimeSpan systemFunctionExecutionTime, TimeSpan userDefinedFunctionExecutionTimes)
        {
            this.QueryEngineExecutionTime = queryEngineExecutionTime;
            this.SystemFunctionExecutionTime = systemFunctionExecutionTime;
            this.UserDefinedFunctionExecutionTime = userDefinedFunctionExecutionTimes;
        }

        public RuntimeExecutionTimesAccumulator()
        {
            this.QueryEngineExecutionTime = default;
            this.SystemFunctionExecutionTime = default;
            this.UserDefinedFunctionExecutionTime = default;
        }

        private TimeSpan QueryEngineExecutionTime { get; set; }
        private TimeSpan SystemFunctionExecutionTime { get; set; }
        private TimeSpan UserDefinedFunctionExecutionTime { get; set; }
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

        public RuntimeExecutionTimes GetRuntimeExecutionTimes()
        {
            return new RuntimeExecutionTimes(
                queryEngineExecutionTime: this.QueryEngineExecutionTime,
                systemFunctionExecutionTime: this.SystemFunctionExecutionTime,
                userDefinedFunctionExecutionTime: this.UserDefinedFunctionExecutionTime);
        }
    }
}
