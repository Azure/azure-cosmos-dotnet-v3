//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Metrics
{
    using System;
    using System.Collections.Generic;

    internal class RuntimeExecutionTimesAccumulator
    {
        private readonly List<RuntimeExecutionTimes> runtimeExecutionTimesList;

        public RuntimeExecutionTimesAccumulator()
        {
            this.runtimeExecutionTimesList = new List<RuntimeExecutionTimes>();
        }

        public void Accumulate(RuntimeExecutionTimes runtimeExecutionTimes)
        {
            if (runtimeExecutionTimes == null)
            {
                throw new ArgumentNullException(nameof(runtimeExecutionTimes));
            }

            this.runtimeExecutionTimesList.Add(runtimeExecutionTimes);
        }

        public RuntimeExecutionTimes GetRuntimeExecutionTimes()
        {
            TimeSpan queryEngineExecutionTime = TimeSpan.Zero;
            TimeSpan systemFunctionExecutionTime = TimeSpan.Zero;
            TimeSpan userDefinedFunctionExecutionTime = TimeSpan.Zero;

            foreach (RuntimeExecutionTimes runtimeExecutionTimes in this.runtimeExecutionTimesList)
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
