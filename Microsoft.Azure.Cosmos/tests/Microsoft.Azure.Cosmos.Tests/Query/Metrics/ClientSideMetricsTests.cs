﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query.Metrics
{
    using VisualStudio.TestTools.UnitTesting;
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;
    using System.Collections.Generic;
    using System;
    using System.Linq;

    [TestClass]
    public class ClientSideMetricsTests
    {
        internal static readonly ClientSideMetrics MockClientSideMetrics = new ClientSideMetrics(
            retries: 1,
            requestCharge: 2,
            fetchExecutionRanges: new List<FetchExecutionRange>() { new FetchExecutionRange("asdf", "asdf", default(DateTime), default(DateTime), 42, 42) });

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
