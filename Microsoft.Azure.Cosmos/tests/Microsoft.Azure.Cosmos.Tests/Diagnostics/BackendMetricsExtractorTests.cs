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

        private static readonly CosmosDiagnosticsContext MockCosmosDiagnosticsContext = new CosmosDiagnosticsContext();

        private static readonly QueryPageDiagnostics MockQueryPageDiagnostics = new QueryPageDiagnostics(
            partitionKeyRangeId: nameof(QueryPageDiagnostics.PartitionKeyRangeId),
            queryMetricText: BackendMetricsTests.MockBackendMetrics.ToString(),
            indexUtilizationText: nameof(QueryPageDiagnostics.IndexUtilizationText),
            diagnosticsContext: default(CosmosDiagnosticsContext),
            schedulingStopwatch: new SchedulingStopwatch());

        private static readonly CosmosDiagnosticScope MockCosmosDiagnosticScope = new CosmosDiagnosticScope(name: "asdf");

        private static readonly CosmosDiagnosticsContextList MockCosmosDiagnosticsContextList = new CosmosDiagnosticsContextList(
            new List<CosmosDiagnosticsInternal>()
            {
                MockQueryPageDiagnostics,
                MockCosmosDiagnosticsContext
            });

        [TestMethod]
        public void TestPointOperationStatistics()
        {
            (bool extracted, BackendMetrics extractedBackendMetrics) = MockPointOperationStatistics.Accept(BackendMetricsExtractor.Singleton);
            Assert.IsFalse(extracted);
        }

        [TestMethod]
        public void TestCosmosDiagnosticContext()
        {
            (bool extracted, BackendMetrics extractedBackendMetrics) = MockCosmosDiagnosticsContext.Accept(BackendMetricsExtractor.Singleton);
            Assert.IsTrue(extracted);
        }

        [TestMethod]
        public void TestCosmosDiagnosticScope()
        {
            (bool extracted, BackendMetrics extractedBackendMetrics) = MockCosmosDiagnosticScope.Accept(BackendMetricsExtractor.Singleton);
            Assert.IsFalse(extracted);
        }

        [TestMethod]
        public void TestCosmosDiagnosticsContextList()
        {
            (bool extracted, BackendMetrics extractedBackendMetrics) = MockCosmosDiagnosticsContextList.Accept(BackendMetricsExtractor.Singleton);
            Assert.IsTrue(extracted);
            Assert.AreEqual(BackendMetricsTests.MockBackendMetrics.IndexLookupTime, extractedBackendMetrics.IndexLookupTime);
        }

        [TestMethod]
        public void TestQueryPageDiagnostics()
        {
            (bool extracted, BackendMetrics extractedBackendMetrics) = MockQueryPageDiagnostics.Accept(BackendMetricsExtractor.Singleton);
            Assert.IsTrue(extracted);
            Assert.AreEqual(BackendMetricsTests.MockBackendMetrics.IndexLookupTime, extractedBackendMetrics.IndexLookupTime);
        }

        [TestMethod]
        public void TestWithMalformedString()
        {
            string malformedString = "totalExecutionTimeInMs+33.67";
            QueryPageDiagnostics queryPageDiagnostics = new QueryPageDiagnostics(
                partitionKeyRangeId: nameof(QueryPageDiagnostics.PartitionKeyRangeId),
                queryMetricText: malformedString,
                indexUtilizationText: nameof(QueryPageDiagnostics.IndexUtilizationText),
                diagnosticsContext: default(CosmosDiagnosticsContext),
                schedulingStopwatch: new SchedulingStopwatch());
            (bool extracted, BackendMetrics extractedBackendMetrics) = queryPageDiagnostics.Accept(BackendMetricsExtractor.Singleton);
            Assert.IsFalse(extracted);
        }
    }
}
