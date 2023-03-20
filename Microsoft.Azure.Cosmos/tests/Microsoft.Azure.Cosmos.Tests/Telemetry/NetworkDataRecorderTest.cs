//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.AccessControl;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using static Microsoft.Azure.Cosmos.Tracing.TraceData.ClientSideRequestStatisticsTraceDatum;

    [TestClass]
    public class NetworkDataRecorderTest
    {
        [TestMethod]
        public void TestRecordWithErroredAndHighLatencyRequests()
        {
            NetworkDataRecorder recorder = new NetworkDataRecorder();

            List<StoreResponseStatistics> stats = new List<StoreResponseStatistics>()
            {
                new StoreResponseStatistics(
                     requestStartTime: DateTime.Now,
                     requestResponseTime: DateTime.Now.AddMilliseconds(10),
                     storeResult: StoreResult.CreateForTesting(storeResponse: new StoreResponse()
                     {
                         Status = 200
                     }).Target,
                     resourceType: Documents.ResourceType.Document,
                     operationType: OperationType.Create,
                     requestSessionToken: default,
                     locationEndpoint: new Uri("https://dummy.url")),

                 new StoreResponseStatistics(
                     requestStartTime: DateTime.Now,
                     requestResponseTime: DateTime.Now.AddMilliseconds(10),
                     storeResult: StoreResult.CreateForTesting(storeResponse: new StoreResponse()
                     {
                         Status = 401
                     }).Target,
                     resourceType: Documents.ResourceType.Document,
                     operationType: OperationType.Create,
                     requestSessionToken: default,
                     locationEndpoint: new Uri("https://dummy.url"))
            };

            recorder.Record(stats, "databaseId", "containerId");

            Assert.AreEqual(1, recorder.GetHighLatencyRequests().Count);
            Assert.AreEqual(1, recorder.GetErroredRequests().Count);

            // you can get the values only once
            Assert.AreEqual(0, recorder.GetHighLatencyRequests().Count);
            Assert.AreEqual(0, recorder.GetErroredRequests().Count);
        }

        [TestMethod]
        [DataRow(200, 0, 1, false)]
        [DataRow(404, 0, 1, false)]
        [DataRow(404, 1002, 1, true)]
        [DataRow(409, 0, 1, false)]
        [DataRow(409, 1002, 1, true)]
        [DataRow(503, 2001, 1, true)]
        [DataRow(200, 0, 6, true)]
        public void CheckEligibleStatistics(int statusCode, int subStatusCode, int latencyInMs, bool expectedFlag)
        {
            Assert.AreEqual(expectedFlag, NetworkDataRecorderTest.IsEligible(statusCode, subStatusCode, TimeSpan.FromMilliseconds(latencyInMs)));
        }

        private static bool IsEligible(int statusCode, int subStatusCode, TimeSpan latencyInMs)
        {
            return
                NetworkDataRecorder.IsStatusCodeNotExcluded(statusCode, subStatusCode) &&
                    (NetworkDataRecorder.IsUserOrServerError(statusCode) || NetworkDataRecorder.IsHighLatency(latencyInMs.TotalMilliseconds));
        }
    }
}
