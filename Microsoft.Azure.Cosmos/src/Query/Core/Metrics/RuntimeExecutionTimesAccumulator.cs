//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Metrics
{
    using System;
    using System.Collections.Generic;

    internal class RuntimeExecutionTimesAccumulator
    {
        public RuntimeExecutionTimesAccumulator()
        {
            this.RuntimeExecutionTimesList = new List<RuntimeExecutionTimes>();
        }

        private readonly List<RuntimeExecutionTimes> RuntimeExecutionTimesList;

        public void Accumulate(RuntimeExecutionTimes runtimeExecutionTimes)
        {
            if (runtimeExecutionTimes == null)
            {
                throw new ArgumentNullException(nameof(runtimeExecutionTimes));
            }

            this.RuntimeExecutionTimesList.Add(runtimeExecutionTimes);
        }

        public RuntimeExecutionTimes GetRuntimeExecutionTimes()
        {
            TimeSpan queryEngineExecutionTime = default;
            TimeSpan systemFunctionExecutionTime = default;
            TimeSpan userDefinedFunctionExecutionTime = default;

            foreach (RuntimeExecutionTimes runtimeExecutionTimes in this.RuntimeExecutionTimesList)
            {
                queryEngineExecutionTime += runtimeExecutionTimes.QueryEngineExecutionTime;
                systemFunctionExecutionTime += runtimeExecutionTimes.SystemFunctionExecutionTime;
                userDefinedFunctionExecutionTime += runtimeExecutionTimes.UserDefinedFunctionExecutionTime;
            }

            return new RuntimeExecutionTimes(
                queryEngineExecutionTime: queryEngineExecutionTime,
                systemFunctionExecutionTime: systemFunctionExecutionTime,
                userDefinedFunctionExecutionTime: userDefinedFunctionExecutionTime);
        }
    }
}
