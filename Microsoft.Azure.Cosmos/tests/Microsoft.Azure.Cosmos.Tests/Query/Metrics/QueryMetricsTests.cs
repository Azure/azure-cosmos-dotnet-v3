//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query.Metrics
{
    using VisualStudio.TestTools.UnitTesting;
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;
    using System.Collections.Generic;

    [TestClass]
    public class QueryMetricsTests
    {
        private static readonly QueryMetrics MockQueryMetrics = new QueryMetrics(
            BackendMetricsTests.MockBackendMetrics,
            IndexUtilizationInfoTests.MockIndexUtilizationInfo,
            ClientSideMetricsTests.MockClientSideMetrics);

        [TestMethod]
        public void TestAccumulator()
        {
            QueryMetrics.Accumulator accumulator = new QueryMetrics.Accumulator();
            accumulator = accumulator.Accumulate(MockQueryMetrics);
            accumulator = accumulator.Accumulate(MockQueryMetrics);

            QueryMetrics doubleQueryMetrics = QueryMetrics.Accumulator.ToQueryMetrics(accumulator);

            // Spot check
            Assert.AreEqual(2 * BackendMetricsTests.MockBackendMetrics.IndexLookupTime, doubleQueryMetrics.BackendMetrics.IndexLookupTime);
            Assert.AreEqual(2 * IndexUtilizationInfoTests.MockIndexUtilizationInfo.PotentialSingleIndexes.Count, doubleQueryMetrics.IndexUtilizationInfo.PotentialSingleIndexes.Count);
            Assert.AreEqual(2 * ClientSideMetricsTests.MockClientSideMetrics.RequestCharge, doubleQueryMetrics.ClientSideMetrics.RequestCharge);
        }

        [TestMethod]
        public void TestAddition()
        {
            QueryMetrics doubleQueryMetrics = MockQueryMetrics + MockQueryMetrics;

            // Spot check
            Assert.AreEqual(2 * BackendMetricsTests.MockBackendMetrics.IndexLookupTime, doubleQueryMetrics.BackendMetrics.IndexLookupTime);
            Assert.AreEqual(2 * IndexUtilizationInfoTests.MockIndexUtilizationInfo.PotentialSingleIndexes.Count, doubleQueryMetrics.IndexUtilizationInfo.PotentialSingleIndexes.Count);
            Assert.AreEqual(2 * ClientSideMetricsTests.MockClientSideMetrics.RequestCharge, doubleQueryMetrics.ClientSideMetrics.RequestCharge);
        }

        [TestMethod]
        public void TestCreateFromEnumerable()
        {
            QueryMetrics tripleQueryMetrics = QueryMetrics.CreateFromIEnumerable(new List<QueryMetrics>() { MockQueryMetrics, MockQueryMetrics, MockQueryMetrics });

            // Spot check
            Assert.AreEqual(3 * BackendMetricsTests.MockBackendMetrics.IndexLookupTime, tripleQueryMetrics.BackendMetrics.IndexLookupTime);
            Assert.AreEqual(3 * IndexUtilizationInfoTests.MockIndexUtilizationInfo.PotentialSingleIndexes.Count, tripleQueryMetrics.IndexUtilizationInfo.PotentialSingleIndexes.Count);
            Assert.AreEqual(3 * ClientSideMetricsTests.MockClientSideMetrics.RequestCharge, tripleQueryMetrics.ClientSideMetrics.RequestCharge);
        }

        [TestMethod]
        public void TestToString()
        {
            string queryMetricsToString = MockQueryMetrics.ToString();
            Assert.IsNotNull(queryMetricsToString);
        }
    }
}
