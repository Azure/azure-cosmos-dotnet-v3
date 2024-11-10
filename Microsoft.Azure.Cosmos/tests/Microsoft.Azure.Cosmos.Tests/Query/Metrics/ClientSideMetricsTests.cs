//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;
    using VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ClientSideMetricsTests
    {
        internal static readonly ClientSideMetrics MockClientSideMetrics = new ClientSideMetrics(
            retries: 1,
            requestCharge: 2,
            fetchExecutionRanges: new List<FetchExecutionRange>() { new FetchExecutionRange("asdf", "asdf", default, default, 42, 42) });

        [TestMethod]
        public void TestAccumulator()
        {
            ClientSideMetricsAccumulator accumulator = new ClientSideMetricsAccumulator();
            accumulator.Accumulate(MockClientSideMetrics);
            accumulator.Accumulate(MockClientSideMetrics);

            ClientSideMetrics doubleMetrics = accumulator.GetClientSideMetrics();
            Assert.AreEqual(2 * MockClientSideMetrics.Retries, doubleMetrics.Retries);
            Assert.AreEqual(2 * MockClientSideMetrics.RequestCharge, doubleMetrics.RequestCharge);
            Assert.AreEqual(2 * MockClientSideMetrics.FetchExecutionRanges.Count(), doubleMetrics.FetchExecutionRanges.Count());
        }
    }
}