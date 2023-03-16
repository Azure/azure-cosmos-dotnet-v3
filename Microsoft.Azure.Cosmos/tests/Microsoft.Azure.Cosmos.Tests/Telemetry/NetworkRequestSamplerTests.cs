//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Telemetry.Models;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class NetworkRequestSamplerTests
    {
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
            Assert.AreEqual(expectedFlag, NetworkRequestSampler.IsEligible(statusCode, subStatusCode, TimeSpan.FromMilliseconds(latencyInMs)));
        }

        [TestMethod]
        [DataRow(10)]
        public void TestNetworkRequestSamplerForThreshold(int threshold)
        {
            ISet<RequestInfo> requestInfoList = new HashSet<RequestInfo>();

            TopNSampler sampler = new TopNSampler(threshold);

            for (int counter = 0; counter < 200; counter++)
            { 
                RequestInfo requestInfo = new RequestInfo()
                {
                    DatabaseName = "dbId " + (counter % 20), // To repeat similar elements
                    ContainerName = "containerId",
                    Uri = "rntbd://host/partition/replica",
                    StatusCode = 429,
                    SubStatusCode = 1002,
                    Resource = ResourceType.Document.ToResourceTypeString(),
                    Operation = OperationType.Create.ToOperationTypeString()
                };
                
                if (sampler.ShouldSample(requestInfo))
                {
                    requestInfoList.Add(requestInfo);
                }
            }

            Assert.AreEqual(10, requestInfoList.Count);
        }
    }
}
