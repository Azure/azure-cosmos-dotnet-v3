//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query.Metrics
{
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;
    using VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class QueryMetricsTests
    {
        private static readonly QueryMetrics MockQueryMetrics = new QueryMetrics(
            ServerSideMetricsTests.ServerSideMetrics,
            IndexUtilizationInfoTests.MockIndexUtilizationInfo,
            ClientSideMetricsTests.MockClientSideMetrics);

        [TestMethod]
        public void TestAccumulator()
        {
            QueryMetricsAccumulator accumulator = new QueryMetricsAccumulator();
            accumulator.Accumulate(MockQueryMetrics);
            accumulator.Accumulate(MockQueryMetrics);

            QueryMetrics doubleQueryMetrics = accumulator.GetQueryMetrics();

            // Spot check
            Assert.AreEqual(2 * ServerSideMetricsTests.ServerSideMetrics.IndexLookupTime, doubleQueryMetrics.ServerSideMetrics.IndexLookupTime);
            Assert.AreEqual(2 * IndexUtilizationInfoTests.MockIndexUtilizationInfo.PotentialSingleIndexes.Count, doubleQueryMetrics.IndexUtilizationInfo.PotentialSingleIndexes.Count);
            Assert.AreEqual(2 * ClientSideMetricsTests.MockClientSideMetrics.RequestCharge, doubleQueryMetrics.ClientSideMetrics.RequestCharge);
        }

        [TestMethod]
        public void TestAddition()
        {
            QueryMetrics doubleQueryMetrics = MockQueryMetrics + MockQueryMetrics;

            // Spot check
            Assert.AreEqual(2 * ServerSideMetricsTests.ServerSideMetrics.IndexLookupTime, doubleQueryMetrics.ServerSideMetrics.IndexLookupTime);
            Assert.AreEqual(2 * IndexUtilizationInfoTests.MockIndexUtilizationInfo.PotentialSingleIndexes.Count, doubleQueryMetrics.IndexUtilizationInfo.PotentialSingleIndexes.Count);
            Assert.AreEqual(2 * ClientSideMetricsTests.MockClientSideMetrics.RequestCharge, doubleQueryMetrics.ClientSideMetrics.RequestCharge);
        }

        [TestMethod]
        public void TestCreateFromEnumerable()
        {
            QueryMetrics tripleQueryMetrics = QueryMetrics.CreateFromIEnumerable(new List<QueryMetrics>() { MockQueryMetrics, MockQueryMetrics, MockQueryMetrics });

            // Spot check
            Assert.AreEqual(3 * ServerSideMetricsTests.ServerSideMetrics.IndexLookupTime, tripleQueryMetrics.ServerSideMetrics.IndexLookupTime);
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