//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Metrics
{
    using System;
    using System.Collections.Generic;

    internal class RuntimeExecutionTimesAccumulator
    {
        private readonly List<RuntimeExecutionTimesInternal> runtimeExecutionTimesList;

        public RuntimeExecutionTimesAccumulator()
        {
            this.runtimeExecutionTimesList = new List<RuntimeExecutionTimesInternal>();
        }

        public void Accumulate(RuntimeExecutionTimesInternal runtimeExecutionTimes)
        {
            if (runtimeExecutionTimes == null)
            {
                throw new ArgumentNullException(nameof(runtimeExecutionTimes));
            }

            this.runtimeExecutionTimesList.Add(runtimeExecutionTimes);
        }

        public RuntimeExecutionTimesInternal GetRuntimeExecutionTimes()
        {
            TimeSpan queryEngineExecutionTime = TimeSpan.Zero;
            TimeSpan systemFunctionExecutionTime = TimeSpan.Zero;
            TimeSpan userDefinedFunctionExecutionTime = TimeSpan.Zero;

            foreach (RuntimeExecutionTimesInternal runtimeExecutionTimes in this.runtimeExecutionTimesList)
            {
                queryEngineExecutionTime += runtimeExecutionTimes.QueryEngineExecutionTime;
                systemFunctionExecutionTime += runtimeExecutionTimes.SystemFunctionExecutionTime;
                userDefinedFunctionExecutionTime += runtimeExecutionTimes.UserDefinedFunctionExecutionTime;
            }

            return new RuntimeExecutionTimesInternal(
                queryEngineExecutionTime: queryEngineExecutionTime,
                systemFunctionExecutionTime: systemFunctionExecutionTime,
                userDefinedFunctionExecutionTime: userDefinedFunctionExecutionTime);
        }
    }
}
