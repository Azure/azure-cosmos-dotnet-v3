//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using VisualStudio.TestTools.UnitTesting;
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;
    using System.Diagnostics;

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
            TimeSpan documentWriteTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 18.10));
            TimeSpan systemFunctionExecuteTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 0.05));
            TimeSpan userFunctionExecuteTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 0.07));
            long retrievedDocumentCount = 2000;
            long retrievedDocumentSize = 1125600;
            long outputDocumentCount = 2000;
            long outputDocumentSize = 1125600;

            Assert.IsTrue(QueryMetricsParser.TryParse(
                deliminatedString: $"totalExecutionTimeInMs={totalExecutionTime.TotalMilliseconds};queryCompileTimeInMs={queryCompileTime.TotalMilliseconds};queryLogicalPlanBuildTimeInMs={logicalPlanBuildTime.TotalMilliseconds};queryPhysicalPlanBuildTimeInMs={queryPhysicalPlanBuildTime.TotalMilliseconds};queryOptimizationTimeInMs={queryOptimizationTime.TotalMilliseconds};VMExecutionTimeInMs={vmExecutionTime.TotalMilliseconds};indexLookupTimeInMs={indexLookupTime.TotalMilliseconds};documentLoadTimeInMs={documentLoadTime.TotalMilliseconds};systemFunctionExecuteTimeInMs={systemFunctionExecuteTime.TotalMilliseconds};userFunctionExecuteTimeInMs={userFunctionExecuteTime.TotalMilliseconds};retrievedDocumentCount={retrievedDocumentCount};retrievedDocumentSize={retrievedDocumentSize};outputDocumentCount={outputDocumentCount};outputDocumentSize={outputDocumentSize};writeOutputTimeInMs={documentWriteTime.TotalMilliseconds}",
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

        [TestMethod]
        public void TestParseEmptyString()
        {
            Assert.IsTrue(QueryMetricsParser.TryParse(deliminatedString: string.Empty, queryMetrics: out QueryMetrics queryMetricsFromParse));

            Assert.AreEqual(default(TimeSpan), queryMetricsFromParse.DocumentLoadTime);
            Assert.AreEqual(default(TimeSpan), queryMetricsFromParse.DocumentWriteTime);
            Assert.AreEqual(default(long), queryMetricsFromParse.OutputDocumentCount);
            Assert.AreEqual(default(long), queryMetricsFromParse.OutputDocumentSize);
            Assert.AreEqual(default(long), queryMetricsFromParse.RetrievedDocumentCount);
            Assert.AreEqual(default(long), queryMetricsFromParse.RetrievedDocumentSize);
            Assert.AreEqual(default(TimeSpan), queryMetricsFromParse.QueryPreparationTimes.QueryCompilationTime);
            Assert.AreEqual(default(TimeSpan), queryMetricsFromParse.QueryPreparationTimes.LogicalPlanBuildTime);
            Assert.AreEqual(default(TimeSpan), queryMetricsFromParse.QueryPreparationTimes.PhysicalPlanBuildTime);
            Assert.AreEqual(default(TimeSpan), queryMetricsFromParse.QueryPreparationTimes.QueryOptimizationTime);
            Assert.AreEqual(default(TimeSpan), queryMetricsFromParse.RuntimeExecutionTimes.SystemFunctionExecutionTime);
            Assert.AreEqual(default(TimeSpan), queryMetricsFromParse.RuntimeExecutionTimes.UserDefinedFunctionExecutionTime);
            Assert.AreEqual(default(TimeSpan), queryMetricsFromParse.TotalTime);
            Assert.AreEqual(default(TimeSpan), queryMetricsFromParse.VMExecutionTime);
        }

        [TestMethod]
        public void TestParseStringWithMissingFields()
        {
            TimeSpan totalExecutionTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 33.67));
            Assert.IsTrue(QueryMetricsParser.TryParse(deliminatedString: $"totalExecutionTimeInMs={totalExecutionTime.TotalMilliseconds}", queryMetrics: out QueryMetrics queryMetricsFromParse));

            Assert.AreEqual(default(TimeSpan), queryMetricsFromParse.DocumentLoadTime);
            Assert.AreEqual(default(TimeSpan), queryMetricsFromParse.DocumentWriteTime);
            Assert.AreEqual(default(long), queryMetricsFromParse.OutputDocumentCount);
            Assert.AreEqual(default(long), queryMetricsFromParse.OutputDocumentSize);
            Assert.AreEqual(default(long), queryMetricsFromParse.RetrievedDocumentCount);
            Assert.AreEqual(default(long), queryMetricsFromParse.RetrievedDocumentSize);
            Assert.AreEqual(default(TimeSpan), queryMetricsFromParse.QueryPreparationTimes.QueryCompilationTime);
            Assert.AreEqual(default(TimeSpan), queryMetricsFromParse.QueryPreparationTimes.LogicalPlanBuildTime);
            Assert.AreEqual(default(TimeSpan), queryMetricsFromParse.QueryPreparationTimes.PhysicalPlanBuildTime);
            Assert.AreEqual(default(TimeSpan), queryMetricsFromParse.QueryPreparationTimes.QueryOptimizationTime);
            Assert.AreEqual(default(TimeSpan), queryMetricsFromParse.RuntimeExecutionTimes.SystemFunctionExecutionTime);
            Assert.AreEqual(default(TimeSpan), queryMetricsFromParse.RuntimeExecutionTimes.UserDefinedFunctionExecutionTime);
            Assert.AreEqual(totalExecutionTime, queryMetricsFromParse.TotalTime);
            Assert.AreEqual(default(TimeSpan), queryMetricsFromParse.VMExecutionTime);
        }

        [TestMethod]
        public void TestParseStringWithTrailingUnknownField()
        {
            TimeSpan totalExecutionTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 33.67));
            Assert.IsTrue(QueryMetricsParser.TryParse(deliminatedString: $"thisIsNotAKnownField={totalExecutionTime.TotalMilliseconds}", queryMetrics: out QueryMetrics queryMetricsFromParse));

            Assert.AreEqual(default(TimeSpan), queryMetricsFromParse.DocumentLoadTime);
            Assert.AreEqual(default(TimeSpan), queryMetricsFromParse.DocumentWriteTime);
            Assert.AreEqual(default(long), queryMetricsFromParse.OutputDocumentCount);
            Assert.AreEqual(default(long), queryMetricsFromParse.OutputDocumentSize);
            Assert.AreEqual(default(long), queryMetricsFromParse.RetrievedDocumentCount);
            Assert.AreEqual(default(long), queryMetricsFromParse.RetrievedDocumentSize);
            Assert.AreEqual(default(TimeSpan), queryMetricsFromParse.QueryPreparationTimes.QueryCompilationTime);
            Assert.AreEqual(default(TimeSpan), queryMetricsFromParse.QueryPreparationTimes.LogicalPlanBuildTime);
            Assert.AreEqual(default(TimeSpan), queryMetricsFromParse.QueryPreparationTimes.PhysicalPlanBuildTime);
            Assert.AreEqual(default(TimeSpan), queryMetricsFromParse.QueryPreparationTimes.QueryOptimizationTime);
            Assert.AreEqual(default(TimeSpan), queryMetricsFromParse.RuntimeExecutionTimes.SystemFunctionExecutionTime);
            Assert.AreEqual(default(TimeSpan), queryMetricsFromParse.RuntimeExecutionTimes.UserDefinedFunctionExecutionTime);
            Assert.AreEqual(default(TimeSpan), queryMetricsFromParse.TotalTime);
            Assert.AreEqual(default(TimeSpan), queryMetricsFromParse.VMExecutionTime);
        }

        [TestMethod]
        public void TestParseStringWithUnknownField()
        {
            TimeSpan totalExecutionTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 33.67));
            Assert.IsTrue(QueryMetricsParser.TryParse(deliminatedString: $"totalExecutionTimeInMs={totalExecutionTime.TotalMilliseconds};thisIsNotAKnownField={totalExecutionTime.TotalMilliseconds};totalExecutionTimeInMs={totalExecutionTime.TotalMilliseconds}", queryMetrics: out QueryMetrics queryMetricsFromParse));

            Assert.AreEqual(default(TimeSpan), queryMetricsFromParse.DocumentLoadTime);
            Assert.AreEqual(default(TimeSpan), queryMetricsFromParse.DocumentWriteTime);
            Assert.AreEqual(default(long), queryMetricsFromParse.OutputDocumentCount);
            Assert.AreEqual(default(long), queryMetricsFromParse.OutputDocumentSize);
            Assert.AreEqual(default(long), queryMetricsFromParse.RetrievedDocumentCount);
            Assert.AreEqual(default(long), queryMetricsFromParse.RetrievedDocumentSize);
            Assert.AreEqual(default(TimeSpan), queryMetricsFromParse.QueryPreparationTimes.QueryCompilationTime);
            Assert.AreEqual(default(TimeSpan), queryMetricsFromParse.QueryPreparationTimes.LogicalPlanBuildTime);
            Assert.AreEqual(default(TimeSpan), queryMetricsFromParse.QueryPreparationTimes.PhysicalPlanBuildTime);
            Assert.AreEqual(default(TimeSpan), queryMetricsFromParse.QueryPreparationTimes.QueryOptimizationTime);
            Assert.AreEqual(default(TimeSpan), queryMetricsFromParse.RuntimeExecutionTimes.SystemFunctionExecutionTime);
            Assert.AreEqual(default(TimeSpan), queryMetricsFromParse.RuntimeExecutionTimes.UserDefinedFunctionExecutionTime);
            Assert.AreEqual(totalExecutionTime, queryMetricsFromParse.TotalTime);
            Assert.AreEqual(default(TimeSpan), queryMetricsFromParse.VMExecutionTime);
        }

        [TestMethod]
        public void PerformanceTests()
        {
            TimeSpan totalExecutionTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 33.67));
            TimeSpan queryCompileTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 0.06));
            TimeSpan logicalPlanBuildTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 0.02));
            TimeSpan queryPhysicalPlanBuildTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 0.10));
            TimeSpan queryOptimizationTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 0.01));
            TimeSpan vmExecutionTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 32.56));
            TimeSpan indexLookupTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 0.36));
            TimeSpan documentLoadTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 9.58));
            TimeSpan documentWriteTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 18.10));
            TimeSpan systemFunctionExecuteTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 0.05));
            TimeSpan userFunctionExecuteTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 0.07));
            long retrievedDocumentCount = 2000;
            long retrievedDocumentSize = 1125600;
            long outputDocumentCount = 2000;
            long outputDocumentSize = 1125600;

            string deliminatedString = $"totalExecutionTimeInMs={totalExecutionTime.TotalMilliseconds};queryCompileTimeInMs={queryCompileTime.TotalMilliseconds};queryLogicalPlanBuildTimeInMs={logicalPlanBuildTime.TotalMilliseconds};queryPhysicalPlanBuildTimeInMs={queryPhysicalPlanBuildTime.TotalMilliseconds};queryOptimizationTimeInMs={queryOptimizationTime.TotalMilliseconds};VMExecutionTimeInMs={vmExecutionTime.TotalMilliseconds};indexLookupTimeInMs={indexLookupTime.TotalMilliseconds};documentLoadTimeInMs={documentLoadTime.TotalMilliseconds};systemFunctionExecuteTimeInMs={systemFunctionExecuteTime.TotalMilliseconds};userFunctionExecuteTimeInMs={userFunctionExecuteTime.TotalMilliseconds};retrievedDocumentCount={retrievedDocumentCount};retrievedDocumentSize={retrievedDocumentSize};outputDocumentCount={outputDocumentCount};outputDocumentSize={outputDocumentSize};writeOutputTimeInMs={documentWriteTime.TotalMilliseconds}";

            int numIterations = 100000;
            Stopwatch newParserStopwatch = Stopwatch.StartNew();
            for (int i = 0; i < numIterations; i++)
            {
                QueryMetricsParser.TryParse(deliminatedString, out QueryMetrics queryMetrics);
            }

            newParserStopwatch.Stop();
            Console.WriteLine($"New Parser Stopwatch: {newParserStopwatch.ElapsedMilliseconds} ms");

            Stopwatch oldParserStopwatch = Stopwatch.StartNew();
            for (int i = 0; i < numIterations; i++)
            {
                QueryMetrics.CreateFromDelimitedString(deliminatedString, null);
            }

            oldParserStopwatch.Stop();
            Console.WriteLine($"Old Parser Stopwatch: {oldParserStopwatch.ElapsedMilliseconds} ms");
        }
    }
}
