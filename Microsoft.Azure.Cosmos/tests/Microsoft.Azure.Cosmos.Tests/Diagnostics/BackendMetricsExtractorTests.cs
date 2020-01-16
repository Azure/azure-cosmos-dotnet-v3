//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Diagnostics
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;
    using Microsoft.Azure.Cosmos.Tests.Query.Metrics;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public sealed class BackendMetricsExtractorTests
    {
        private static readonly PointOperationStatistics MockPointOperationStatistics = new PointOperationStatistics(
            activityId: Guid.NewGuid().ToString(),
            statusCode: HttpStatusCode.OK,
            subStatusCode: Documents.SubStatusCodes.Unknown,
            requestCharge: 0,
            errorMessage: string.Empty,
            method: HttpMethod.Get,
            requestUri: new Uri("http://localhost"),
            requestSessionToken: null,
            responseSessionToken: null,
            clientSideRequestStatistics: new CosmosClientSideRequestStatistics());

        private static readonly QueryPageDiagnostics MockQueryPageDiagnostics = new QueryPageDiagnostics(
            partitionKeyRangeId: nameof(QueryPageDiagnostics.PartitionKeyRangeId),
            queryMetricText: BackendMetricsTests.MockBackendMetrics.ToString(),
            indexUtilizationText: nameof(QueryPageDiagnostics.IndexUtilizationText),
            requestDiagnostics: default(CosmosDiagnostics),
            schedulingStopwatch: new SchedulingStopwatch());

        private static readonly QueryAggregateDiagnostics MockQueryAggregateDiagnostics = new QueryAggregateDiagnostics(
            new List<QueryPageDiagnostics>()
            {
                MockQueryPageDiagnostics,
                MockQueryPageDiagnostics
            });

        private static readonly CosmosDiagnosticsAggregate MockCosmosDiagnosticsAggregate = new CosmosDiagnosticsAggregate(
            cosmosDiagnosticsInternals: new List<CosmosDiagnosticsInternal>()
            {
                MockQueryAggregateDiagnostics,
                MockQueryAggregateDiagnostics
            });

        [TestMethod]
        public void TestPointOperationStatistics()
        {
            (bool extracted, BackendMetrics extractedBackendMetrics) = MockPointOperationStatistics.Accept(BackendMetricsExtractor.Singleton);
            Assert.IsFalse(extracted);
        }

        [TestMethod]
        public void TestQueryAggregateDiagnostics()
        {
            (bool extracted, BackendMetrics extractedBackendMetrics) = MockQueryAggregateDiagnostics.Accept(BackendMetricsExtractor.Singleton);
            Assert.IsTrue(extracted);
            Assert.AreEqual(BackendMetricsTests.MockBackendMetrics.TotalTime * 2, extractedBackendMetrics.TotalTime);
        }

        [TestMethod]
        public void TestCosmosDiagnosticsAggregate()
        {
            (bool extracted, BackendMetrics extractedBackendMetrics) = MockCosmosDiagnosticsAggregate.Accept(BackendMetricsExtractor.Singleton);
            Assert.IsTrue(extracted);
            Assert.AreEqual(BackendMetricsTests.MockBackendMetrics.TotalTime * 4, extractedBackendMetrics.TotalTime);
        }

        [TestMethod]
        public void TestWithMalformedString()
        {
            string malformedString = "totalExecutionTimeInMs+33.67";
            QueryPageDiagnostics queryPageDiagnostics = new QueryPageDiagnostics(
                partitionKeyRangeId: nameof(QueryPageDiagnostics.PartitionKeyRangeId),
                queryMetricText: malformedString,
                indexUtilizationText: nameof(QueryPageDiagnostics.IndexUtilizationText),
                requestDiagnostics: default(CosmosDiagnostics),
                schedulingStopwatch: new SchedulingStopwatch());
            QueryAggregateDiagnostics queryAggregateDiagnostics = new QueryAggregateDiagnostics(
                new List<QueryPageDiagnostics>()
                {
                    queryPageDiagnostics
                });

            (bool extracted, BackendMetrics extractedBackendMetrics) = queryAggregateDiagnostics.Accept(BackendMetricsExtractor.Singleton);
            Assert.IsFalse(extracted);
        }

        [TestMethod]
        public void TestWithUnexpectedNestedType()
        {
            CosmosDiagnosticsAggregate cosmosDiagnosticsAggregate = new CosmosDiagnosticsAggregate(
                new List<CosmosDiagnosticsInternal>()
                {
                    MockPointOperationStatistics
                });

            (bool extracted, BackendMetrics extractedBackendMetrics) = cosmosDiagnosticsAggregate.Accept(BackendMetricsExtractor.Singleton);
            Assert.IsFalse(extracted);
        }
    }
}
