//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests.Query.Metrics
{
    using System;
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;
    using System.Diagnostics;
    using BenchmarkDotNet.Attributes;

    public class Performance
    {
        private static readonly TimeSpan totalExecutionTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 33.67));
        private static readonly TimeSpan queryCompileTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 0.06));
        private static readonly TimeSpan logicalPlanBuildTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 0.02));
        private static readonly TimeSpan physicalPlanBuildTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 0.10));
        private static readonly TimeSpan queryOptimizationTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 0.01));
        private static readonly TimeSpan vmExecutionTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 32.56));
        private static readonly TimeSpan indexLookupTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 0.36));
        private static readonly TimeSpan documentLoadTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 9.58));
        private static readonly TimeSpan documentWriteTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 18.10));
        private static readonly TimeSpan systemFunctionExecuteTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 0.05));
        private static readonly TimeSpan userFunctionExecuteTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 0.07));
        private static readonly long retrievedDocumentCount = 2000;
        private static readonly long retrievedDocumentSize = 1125600;
        private static readonly long outputDocumentCount = 2000;
        private static readonly long outputDocumentSize = 1125600;
        private static readonly double indexHitRatio = 1.0;

        private static readonly string delimitedString = $"totalExecutionTimeInMs={totalExecutionTime.TotalMilliseconds};queryCompileTimeInMs={queryCompileTime.TotalMilliseconds};queryLogicalPlanBuildTimeInMs={logicalPlanBuildTime.TotalMilliseconds};queryPhysicalPlanBuildTimeInMs={physicalPlanBuildTime.TotalMilliseconds};queryOptimizationTimeInMs={queryOptimizationTime.TotalMilliseconds};VMExecutionTimeInMs={vmExecutionTime.TotalMilliseconds};indexLookupTimeInMs={indexLookupTime.TotalMilliseconds};documentLoadTimeInMs={documentLoadTime.TotalMilliseconds};systemFunctionExecuteTimeInMs={systemFunctionExecuteTime.TotalMilliseconds};userFunctionExecuteTimeInMs={userFunctionExecuteTime.TotalMilliseconds};retrievedDocumentCount={retrievedDocumentCount};retrievedDocumentSize={retrievedDocumentSize};outputDocumentCount={outputDocumentCount};outputDocumentSize={outputDocumentSize};writeOutputTimeInMs={documentWriteTime.TotalMilliseconds};indexUtilizationRatio={indexHitRatio}";

        [Benchmark]
        public void TestParse()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < 100000; i++)
            {
                BackendMetricsParser.TryParse(delimitedString, out BackendMetrics backendMetrics);
            }
            stopwatch.Stop();
            Console.WriteLine(stopwatch.ElapsedMilliseconds);
        }
    }
}
