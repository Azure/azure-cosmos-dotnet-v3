//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query.Metrics
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;
    using VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ServerSideMetricsTests
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

        internal static readonly ServerSideMetricsInternal ServerSideMetrics = new ServerSideMetricsInternal(
            retrievedDocumentCount,
            retrievedDocumentSize,
            outputDocumentCount,
            outputDocumentSize,
            indexHitRatio,
            totalExecutionTime,
            new QueryPreparationTimesInternal(
                queryCompileTime,
                logicalPlanBuildTime,
                physicalPlanBuildTime,
                queryOptimizationTime),
            indexLookupTime,
            documentLoadTime,
            vmExecutionTime,
            new RuntimeExecutionTimesInternal(
                totalExecutionTime - systemFunctionExecuteTime - userFunctionExecuteTime,
                systemFunctionExecuteTime,
                userFunctionExecuteTime),
            documentWriteTime);


        [TestMethod]
        public void TestParse()
        {
            ServerSideMetricsTests.ValidateParse(delimitedString, ServerSideMetrics);
        }

        [TestMethod]
        public void TestParseEmptyString()
        {
            ServerSideMetricsTests.ValidateParse(string.Empty, ServerSideMetricsInternal.Empty);
        }

        [TestMethod]
        public void TestParseStringWithMissingFields()
        {
            TimeSpan totalExecutionTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 33.67));
            string delimitedString = $"totalExecutionTimeInMs={totalExecutionTime.TotalMilliseconds}";

            ServerSideMetricsInternal expected = new ServerSideMetricsInternal(
                default,
                default,
                default,
                default,
                default,
                totalExecutionTime,
                new QueryPreparationTimesInternal(
                    default,
                    default,
                    default,
                    default),
                default,
                default,
                default,
                new RuntimeExecutionTimesInternal(
                    default,
                    default,
                    default),
                default);

            ServerSideMetricsTests.ValidateParse(delimitedString, expected);
        }

        [TestMethod]
        public void TestParseStringWithTrailingUnknownField()
        {
            string delimitedString = $"thisIsNotAKnownField=asdf";
            ServerSideMetricsInternal expected = new ServerSideMetricsInternal(
                default,
                default,
                default,
                default,
                default,
                default,
                new QueryPreparationTimesInternal(
                    default,
                    default,
                    default,
                    default),
                default,
                default,
                default,
                new RuntimeExecutionTimesInternal(
                    default,
                    default,
                    default),
                default);

            ServerSideMetricsTests.ValidateParse(delimitedString, expected);
        }

        [TestMethod]
        [DataRow("totalExecutionTimeInMs=asdf", DisplayName = "Not a valid value")]
        [DataRow("totalExecutionTimeInMs=33.6+totalExecutionTimeInMs=33.6", DisplayName = "Wrong Delimiter")]
        public void TestNegativeCases(string delimitedString)
        {
            Assert.IsFalse(ServerSideMetricsParser.TryParse(delimitedString, out _));
        }

        [TestMethod]
        public void TestParseStringWithUnknownField()
        {
            TimeSpan totalExecutionTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 33.67));
            string delimitedString = $"totalExecutionTimeInMs={totalExecutionTime.TotalMilliseconds};thisIsNotAKnownField={totalExecutionTime.TotalMilliseconds};totalExecutionTimeInMs={totalExecutionTime.TotalMilliseconds}";
            ServerSideMetricsInternal expected = new ServerSideMetricsInternal(
                default,
                default,
                default,
                default,
                default,
                totalExecutionTime,
                new QueryPreparationTimesInternal(
                    default,
                    default,
                    default,
                    default),
                default,
                default,
                default,
                new RuntimeExecutionTimesInternal(
                    default,
                    default,
                    default),
                default);

            ServerSideMetricsTests.ValidateParse(delimitedString, expected);
        }

        [TestMethod]
        public void TestAccumulator()
        {
            ServerSideMetricsInternalAccumulator accumulator = new ServerSideMetricsInternalAccumulator();
            accumulator.Accumulate(ServerSideMetrics);
            accumulator.Accumulate(ServerSideMetrics);
            ServerSideMetricsInternal serverSideMetricsFromAddition = accumulator.GetServerSideMetrics();

            List<ServerSideMetricsInternal> metricsList = new List<ServerSideMetricsInternal> { ServerSideMetrics, ServerSideMetrics };
            ServerSideMetricsInternal serverSideMetricsFromCreate = ServerSideMetricsInternal.Create(metricsList);

            ServerSideMetricsInternal expected = new ServerSideMetricsInternal(
                retrievedDocumentCount * 2,
                retrievedDocumentSize * 2,
                outputDocumentCount * 2,
                outputDocumentSize * 2,
                indexHitRatio,
                totalExecutionTime * 2,
                new QueryPreparationTimesInternal(
                    queryCompileTime * 2,
                    logicalPlanBuildTime * 2,
                    physicalPlanBuildTime * 2,
                    queryOptimizationTime * 2),
                indexLookupTime * 2,
                documentLoadTime * 2,
                vmExecutionTime * 2,
                new RuntimeExecutionTimesInternal(
                    (totalExecutionTime - systemFunctionExecuteTime - userFunctionExecuteTime) * 2,
                    systemFunctionExecuteTime * 2,
                    userFunctionExecuteTime * 2),
                documentWriteTime * 2);

            ServerSideMetricsTests.ValidateServerSideMetricsEquals(expected, serverSideMetricsFromAddition);
            ServerSideMetricsTests.ValidateServerSideMetricsEquals(expected, serverSideMetricsFromCreate);
        }
        private static void ValidateParse(string delimitedString, ServerSideMetricsInternal expected)
        {
            Assert.IsTrue(ServerSideMetricsParser.TryParse(delimitedString, out ServerSideMetricsInternal actual));
            ServerSideMetricsTests.ValidateServerSideMetricsEquals(expected, actual);
        }

        private static void ValidateServerSideMetricsEquals(ServerSideMetricsInternal expected, ServerSideMetricsInternal actual)
        {
            Assert.AreEqual(expected.DocumentLoadTime, actual.DocumentLoadTime);
            Assert.AreEqual(expected.DocumentWriteTime, actual.DocumentWriteTime);
            Assert.AreEqual(expected.OutputDocumentCount, actual.OutputDocumentCount);
            Assert.AreEqual(expected.OutputDocumentSize, actual.OutputDocumentSize);
            Assert.AreEqual(expected.RetrievedDocumentCount, actual.RetrievedDocumentCount);
            Assert.AreEqual(expected.RetrievedDocumentSize, actual.RetrievedDocumentSize);
            Assert.AreEqual(expected.IndexHitRatio, actual.IndexHitRatio);
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