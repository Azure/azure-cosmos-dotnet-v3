//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Telemetry
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.Azure.Cosmos.Telemetry.Models;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class DataSamplerTests
    {
        [TestMethod]
        [DataRow(10)]
        public void TestNetworkRequestSamplerForThreshold(int threshold)
        {
            List<RequestInfo> requestInfoList = new List<RequestInfo>();

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
                requestInfoList.Add(requestInfo);
            }

            Assert.AreEqual(10, DataSampler.SampleOrderByP99(requestInfoList).Count);
            Assert.AreEqual(10, DataSampler.SampleOrderByCount(requestInfoList).Count);
        }
    }
}
