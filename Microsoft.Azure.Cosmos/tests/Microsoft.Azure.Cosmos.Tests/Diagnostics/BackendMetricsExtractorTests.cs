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
            responseTimeUtc: DateTime.UtcNow,
            requestCharge: 0,
            errorMessage: string.Empty,
            method: HttpMethod.Get,
            requestUri: new Uri("http://localhost"),
            requestSessionToken: null,
            responseSessionToken: null);

        private static readonly QueryPageDiagnostics MockQueryPageDiagnostics = new QueryPageDiagnostics(
            clientQueryCorrelationId: Guid.NewGuid(),
            partitionKeyRangeId: nameof(QueryPageDiagnostics.PartitionKeyRangeId),
            queryMetricText: BackendMetricsTests.MockBackendMetrics.ToString(),
            indexUtilizationText: nameof(QueryPageDiagnostics.IndexUtilizationText),
            diagnosticsContext: default(CosmosDiagnosticsContext));

        private static readonly CosmosDiagnosticScope MockCosmosDiagnosticScope = new CosmosDiagnosticScope(name: "asdf");

        private static readonly CosmosDiagnosticsContext MockCosmosDiagnosticsContext = new CosmosDiagnosticsContextCore();

        [TestMethod]
        public void TestPointOperationStatistics()
        {
            (BackendMetricsExtractor.ParseFailureReason parseFailureReason, BackendMetrics extractedBackendMetrics) = MockPointOperationStatistics.Accept(BackendMetricsExtractor.Singleton);
            Assert.AreEqual(BackendMetricsExtractor.ParseFailureReason.MetricsNotFound, parseFailureReason);
        }

        [TestMethod]
        public void TestCosmosDiagnosticContext()
        {
            (BackendMetricsExtractor.ParseFailureReason parseFailureReason, BackendMetrics extractedBackendMetrics) = MockCosmosDiagnosticsContext.Accept(BackendMetricsExtractor.Singleton);
            Assert.AreEqual(BackendMetricsExtractor.ParseFailureReason.MetricsNotFound, parseFailureReason);
        }

        [TestMethod]
        public void TestCosmosDiagnosticScope()
        {
            (BackendMetricsExtractor.ParseFailureReason parseFailureReason, BackendMetrics extractedBackendMetrics) notFoundResult = MockCosmosDiagnosticScope.Accept(BackendMetricsExtractor.Singleton);
            Assert.AreEqual(BackendMetricsExtractor.ParseFailureReason.MetricsNotFound, notFoundResult.parseFailureReason);

            CosmosDiagnosticsContext contextWithQueryMetrics = new CosmosDiagnosticsContextCore();
            contextWithQueryMetrics.AddDiagnosticsInternal(MockQueryPageDiagnostics);
            (BackendMetricsExtractor.ParseFailureReason parseFailureReason, BackendMetrics extractedBackendMetrics) foundResult = contextWithQueryMetrics.Accept(BackendMetricsExtractor.Singleton);
            Assert.AreEqual(BackendMetricsExtractor.ParseFailureReason.None, foundResult.parseFailureReason);
            Assert.AreEqual(BackendMetricsTests.MockBackendMetrics.IndexLookupTime, foundResult.extractedBackendMetrics.IndexLookupTime);
        }

        [TestMethod]
        public void TestQueryPageDiagnostics()
        {
            (BackendMetricsExtractor.ParseFailureReason parseFailureReason, BackendMetrics extractedBackendMetrics) = MockQueryPageDiagnostics.Accept(BackendMetricsExtractor.Singleton);
            Assert.AreEqual(BackendMetricsExtractor.ParseFailureReason.None, parseFailureReason);
            Assert.AreEqual(BackendMetricsTests.MockBackendMetrics.IndexLookupTime, extractedBackendMetrics.IndexLookupTime);
        }

        [TestMethod]
        public void TestWithMalformedString()
        {
            string malformedString = "totalExecutionTimeInMs=asdf";
            QueryPageDiagnostics queryPageDiagnostics = new QueryPageDiagnostics(
                clientQueryCorrelationId: Guid.NewGuid(),
                partitionKeyRangeId: nameof(QueryPageDiagnostics.PartitionKeyRangeId),
                queryMetricText: malformedString,
                indexUtilizationText: nameof(QueryPageDiagnostics.IndexUtilizationText),
                diagnosticsContext: default(CosmosDiagnosticsContext));
            (BackendMetricsExtractor.ParseFailureReason parseFailureReason, BackendMetrics extractedBackendMetrics) = queryPageDiagnostics.Accept(BackendMetricsExtractor.Singleton);
            Assert.AreEqual(BackendMetricsExtractor.ParseFailureReason.MalformedString, parseFailureReason);
        }
    }
}
