//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query.Metrics
{
    using System;
    using VisualStudio.TestTools.UnitTesting;
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;
    using System.Diagnostics;
    using System.Collections.Generic;

    [TestClass]
    public class BackendMetricsTests
    {
        [TestMethod]
        public void TestParse()
        {
            TimeSpan totalExecutionTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 33.67));
            TimeSpan queryCompileTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 0.06));
            TimeSpan logicalPlanBuildTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 0.02));
            TimeSpan physicalPlanBuildTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 0.10));
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

            string delimitedString = $"totalExecutionTimeInMs={totalExecutionTime.TotalMilliseconds};queryCompileTimeInMs={queryCompileTime.TotalMilliseconds};queryLogicalPlanBuildTimeInMs={logicalPlanBuildTime.TotalMilliseconds};queryPhysicalPlanBuildTimeInMs={physicalPlanBuildTime.TotalMilliseconds};queryOptimizationTimeInMs={queryOptimizationTime.TotalMilliseconds};VMExecutionTimeInMs={vmExecutionTime.TotalMilliseconds};indexLookupTimeInMs={indexLookupTime.TotalMilliseconds};documentLoadTimeInMs={documentLoadTime.TotalMilliseconds};systemFunctionExecuteTimeInMs={systemFunctionExecuteTime.TotalMilliseconds};userFunctionExecuteTimeInMs={userFunctionExecuteTime.TotalMilliseconds};retrievedDocumentCount={retrievedDocumentCount};retrievedDocumentSize={retrievedDocumentSize};outputDocumentCount={outputDocumentCount};outputDocumentSize={outputDocumentSize};writeOutputTimeInMs={documentWriteTime.TotalMilliseconds}";

            BackendMetrics expected = new BackendMetrics(
                retrievedDocumentCount,
                retrievedDocumentSize,
                outputDocumentCount,
                outputDocumentSize,
                totalExecutionTime,
                new QueryPreparationTimes(
                    queryCompileTime,
                    logicalPlanBuildTime,
                    physicalPlanBuildTime,
                    queryOptimizationTime),
                indexLookupTime,
                documentLoadTime,
                vmExecutionTime,
                new RuntimeExecutionTimes(
                    totalExecutionTime - systemFunctionExecuteTime - userFunctionExecuteTime,
                    systemFunctionExecuteTime,
                    userFunctionExecuteTime),
                documentWriteTime);

            BackendMetricsTests.ValidateParse(delimitedString, expected);
        }

        [TestMethod]
        public void TestParseEmptyString()
        {
            BackendMetricsTests.ValidateParse(string.Empty, BackendMetrics.Empty);
        }

        [TestMethod]
        public void TestParseStringWithMissingFields()
        {
            TimeSpan totalExecutionTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 33.67));
            string delimitedString = $"totalExecutionTimeInMs={totalExecutionTime.TotalMilliseconds}";

            BackendMetrics expected = new BackendMetrics(
                default(long),
                default(long),
                default(long),
                default(long),
                totalExecutionTime,
                new QueryPreparationTimes(
                    default(TimeSpan),
                    default(TimeSpan),
                    default(TimeSpan),
                    default(TimeSpan)),
                default(TimeSpan),
                default(TimeSpan),
                default(TimeSpan),
                new RuntimeExecutionTimes(
                    default(TimeSpan),
                    default(TimeSpan),
                    default(TimeSpan)),
                default(TimeSpan));

            BackendMetricsTests.ValidateParse(delimitedString, expected);
        }

        [TestMethod]
        public void TestParseStringWithTrailingUnknownField()
        {
            string delimitedString = $"thisIsNotAKnownField=asdf";
            BackendMetrics expected = new BackendMetrics(
                default(long),
                default(long),
                default(long),
                default(long),
                default(TimeSpan),
                new QueryPreparationTimes(
                    default(TimeSpan),
                    default(TimeSpan),
                    default(TimeSpan),
                    default(TimeSpan)),
                default(TimeSpan),
                default(TimeSpan),
                default(TimeSpan),
                new RuntimeExecutionTimes(
                    default(TimeSpan),
                    default(TimeSpan),
                    default(TimeSpan)),
                default(TimeSpan));

            BackendMetricsTests.ValidateParse(delimitedString, expected);
        }

        [TestMethod]
        public void TestParseStringWithUnknownField()
        {
            TimeSpan totalExecutionTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 33.67));
            string delimitedString = $"totalExecutionTimeInMs={totalExecutionTime.TotalMilliseconds};thisIsNotAKnownField={totalExecutionTime.TotalMilliseconds};totalExecutionTimeInMs={totalExecutionTime.TotalMilliseconds}";
            BackendMetrics expected = new BackendMetrics(
                default(long),
                default(long),
                default(long),
                default(long),
                totalExecutionTime,
                new QueryPreparationTimes(
                    default(TimeSpan),
                    default(TimeSpan),
                    default(TimeSpan),
                    default(TimeSpan)),
                default(TimeSpan),
                default(TimeSpan),
                default(TimeSpan),
                new RuntimeExecutionTimes(
                    default(TimeSpan),
                    default(TimeSpan),
                    default(TimeSpan)),
                default(TimeSpan));

            BackendMetricsTests.ValidateParse(delimitedString, expected);
        }

        [TestMethod]
        public void TestAccumulator()
        {
            TimeSpan totalExecutionTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 33.67));
            TimeSpan queryCompileTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 0.06));
            TimeSpan logicalPlanBuildTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 0.02));
            TimeSpan physicalPlanBuildTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 0.10));
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

            BackendMetrics singleMetric = new BackendMetrics(
                retrievedDocumentCount,
                retrievedDocumentSize,
                outputDocumentCount,
                outputDocumentSize,
                totalExecutionTime,
                new QueryPreparationTimes(
                    queryCompileTime,
                    logicalPlanBuildTime,
                    physicalPlanBuildTime,
                    queryOptimizationTime),
                indexLookupTime,
                documentLoadTime,
                vmExecutionTime,
                new RuntimeExecutionTimes(
                    totalExecutionTime - systemFunctionExecuteTime - userFunctionExecuteTime,
                    systemFunctionExecuteTime,
                    userFunctionExecuteTime),
                documentWriteTime);

            BackendMetrics.Accumulator accumulator = new BackendMetrics.Accumulator();
            accumulator = accumulator.Accumulate(singleMetric);
            accumulator = accumulator.Accumulate(singleMetric);

            BackendMetrics backendMetricsFromAddition = BackendMetrics.Accumulator.ToBackendMetrics(accumulator);
            BackendMetrics expected = new BackendMetrics(
                retrievedDocumentCount * 2,
                retrievedDocumentSize * 2,
                outputDocumentCount * 2,
                outputDocumentSize * 2,
                totalExecutionTime * 2,
                new QueryPreparationTimes(
                    queryCompileTime * 2,
                    logicalPlanBuildTime * 2,
                    physicalPlanBuildTime * 2,
                    queryOptimizationTime * 2),
                indexLookupTime * 2,
                documentLoadTime * 2,
                vmExecutionTime * 2,
                new RuntimeExecutionTimes(
                    (totalExecutionTime - systemFunctionExecuteTime - userFunctionExecuteTime) * 2,
                    systemFunctionExecuteTime * 2,
                    userFunctionExecuteTime * 2),
                documentWriteTime * 2);

            BackendMetricsTests.ValidateBackendMetricsEquals(expected, backendMetricsFromAddition);
        }

        private static void ValidateParse(string delimitedString, BackendMetrics expected)
        {
            Assert.IsTrue(BackendMetricsParser.TryParse(delimitedString, out BackendMetrics actual));
            BackendMetricsTests.ValidateBackendMetricsEquals(expected, actual);
        }

        private static void ValidateBackendMetricsEquals(BackendMetrics expected, BackendMetrics actual)
        {
            Assert.AreEqual(expected.DocumentLoadTime, actual.DocumentLoadTime);
            Assert.AreEqual(expected.DocumentWriteTime, actual.DocumentWriteTime);
            Assert.AreEqual(expected.OutputDocumentCount, actual.OutputDocumentCount);
            Assert.AreEqual(expected.OutputDocumentSize, actual.OutputDocumentSize);
            Assert.AreEqual(expected.RetrievedDocumentCount, actual.RetrievedDocumentCount);
            Assert.AreEqual(expected.RetrievedDocumentSize, actual.RetrievedDocumentSize);
            Assert.AreEqual(expected.QueryPreparationTimes.QueryCompilationTime, actual.QueryPreparationTimes.QueryCompilationTime);
            Assert.AreEqual(expected.QueryPreparationTimes.LogicalPlanBuildTime, actual.QueryPreparationTimes.LogicalPlanBuildTime);
            Assert.AreEqual(expected.QueryPreparationTimes.PhysicalPlanBuildTime, actual.QueryPreparationTimes.PhysicalPlanBuildTime);
            Assert.AreEqual(expected.QueryPreparationTimes.QueryOptimizationTime, actual.QueryPreparationTimes.QueryOptimizationTime);
            Assert.AreEqual(expected.RuntimeExecutionTimes.SystemFunctionExecutionTime, actual.RuntimeExecutionTimes.SystemFunctionExecutionTime);
            Assert.AreEqual(expected.RuntimeExecutionTimes.UserDefinedFunctionExecutionTime, actual.RuntimeExecutionTimes.UserDefinedFunctionExecutionTime);
            Assert.AreEqual(expected.TotalTime, actual.TotalTime);
            Assert.AreEqual(expected.VMExecutionTime, actual.VMExecutionTime);
        }
    }
}
