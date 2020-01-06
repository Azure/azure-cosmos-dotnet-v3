//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using VisualStudio.TestTools.UnitTesting;
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;

    [TestClass]
    public class QueryMetricsParserTests
    {
        [TestMethod]
        public void TestParse()
        {
            TimeSpan totalExecutionTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 33.67));
            TimeSpan queryCompileTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 0.06));
            TimeSpan logicalPlanBuildTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 0.02));
            TimeSpan queryPhysicalPlanBuildTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 0.10));
            TimeSpan queryOptimizationTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 0.01));
            TimeSpan vmExecutionTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 32.56));
            TimeSpan indexLookupTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 0.36));
            TimeSpan documentLoadTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 9.58));
            TimeSpan systemFunctionExecuteTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 0.05));
            TimeSpan userFunctionExecuteTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 0.07));
            TimeSpan documentWriteTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 18.10));
            long retrievedDocumentCount = 2000;
            long retrievedDocumentSize = 1125600;
            long outputDocumentCount = 2000;
            long outputDocumentSize = 1125600;

            Assert.IsTrue(QueryMetricsParser.TryParse(
                deliminatedString: "totalExecutionTimeInMs=33.67;queryCompileTimeInMs=0.06;queryLogicalPlanBuildTimeInMs=0.02;queryPhysicalPlanBuildTimeInMs=0.10;queryOptimizationTimeInMs=0.01;VMExecutionTimeInMs=32.56;indexLookupTimeInMs=0.36;documentLoadTimeInMs=9.58;systemFunctionExecuteTimeInMs=0.05;userFunctionExecuteTimeInMs=0.07;retrievedDocumentCount=2000;retrievedDocumentSize=1125600;outputDocumentCount=2000;outputDocumentSize=1125600;writeOutputTimeInMs=18.10",
                queryMetrics: out QueryMetrics queryMetricsFromParse));

            Assert.AreEqual(documentLoadTime, queryMetricsFromParse.DocumentLoadTime);
            Assert.AreEqual(documentWriteTime, queryMetricsFromParse.DocumentWriteTime);
            Assert.AreEqual(outputDocumentCount, queryMetricsFromParse.OutputDocumentCount);
            Assert.AreEqual(outputDocumentSize, queryMetricsFromParse.OutputDocumentSize);
            Assert.AreEqual(retrievedDocumentCount, queryMetricsFromParse.RetrievedDocumentCount);
            Assert.AreEqual(retrievedDocumentSize, queryMetricsFromParse.RetrievedDocumentSize);
            Assert.AreEqual(queryCompileTime, queryMetricsFromParse.QueryPreparationTimes.QueryCompilationTime);
            Assert.AreEqual(logicalPlanBuildTime, queryMetricsFromParse.QueryPreparationTimes.LogicalPlanBuildTime);
            Assert.AreEqual(queryPhysicalPlanBuildTime, queryMetricsFromParse.QueryPreparationTimes.PhysicalPlanBuildTime);
            Assert.AreEqual(queryOptimizationTime, queryMetricsFromParse.QueryPreparationTimes.QueryOptimizationTime);
            Assert.AreEqual(systemFunctionExecuteTime, queryMetricsFromParse.RuntimeExecutionTimes.SystemFunctionExecutionTime);
            Assert.AreEqual(userFunctionExecuteTime, queryMetricsFromParse.RuntimeExecutionTimes.UserDefinedFunctionExecutionTime);
            Assert.AreEqual(totalExecutionTime, queryMetricsFromParse.TotalTime);
            Assert.AreEqual(vmExecutionTime, queryMetricsFromParse.VMExecutionTime);
        }
    }
}
