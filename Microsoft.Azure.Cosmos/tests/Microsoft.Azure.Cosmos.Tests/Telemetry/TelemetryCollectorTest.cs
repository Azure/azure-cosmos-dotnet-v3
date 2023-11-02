//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Telemetry
{
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.Azure.Cosmos.Telemetry.Collector;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using static Microsoft.Azure.Cosmos.Tracing.TraceData.ClientSideRequestStatisticsTraceDatum;

    [TestClass]
    public class TelemetryCollectorTest
    {
        [TestMethod]
        public void TestTraceDuringCollection()
        {
            TelemetryInformation data = new TelemetryInformation
            {
                DatabaseId = "databaseId",
                ContainerId = "containerId",
                Trace = Trace.GetRootTrace("Testing")
            };

            ConnectionPolicy connectionPolicy = new ConnectionPolicy
            {
                ConnectionMode = ConnectionMode.Direct
            };

            Mock<ClientTelemetry> clientTelemetry = new Mock<ClientTelemetry>();
            clientTelemetry
            .Setup(_ => _.PushCacheDatapoint(It.IsAny<string>(), data)).Throws(new System.Exception("Not recording cache"));
            clientTelemetry
            .Setup(_ => _.PushOperationDatapoint(data)).Throws(new System.Exception("Not recording operation"));
            clientTelemetry
            .Setup(_ => _.PushNetworkDataPoint(It.IsAny<List<StoreResponseStatistics>>(), "databaseId", "containerId")).Throws(new System.Exception("Not recording network"));

            TelemetryCollector collector = new TelemetryCollector(clientTelemetry.Object, connectionPolicy);
            collector.CollectCacheInfo("testCache", () => data);
            collector.CollectOperationAndNetworkInfo(() => data);

            string diagnostics = new CosmosTraceDiagnostics(data.Trace).ToString();

            Assert.IsTrue(diagnostics.Contains("TelemetryCollectFailed-testCache"));
            Assert.IsTrue(diagnostics.Contains("TelemetryCollectFailed-Operation"));
            Assert.IsTrue(diagnostics.Contains("TelemetryCollectFailed-Network"));
        }
    }
}
