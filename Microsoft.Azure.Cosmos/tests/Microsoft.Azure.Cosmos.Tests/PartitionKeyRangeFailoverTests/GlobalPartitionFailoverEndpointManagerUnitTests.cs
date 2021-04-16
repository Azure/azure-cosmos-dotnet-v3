//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class GlobalPartitionFailoverEndpointManagerUnitTests
    {
        [TestMethod]
        public void TestSingleReadRegionScenario()
        {
            Mock<IGlobalEndpointManager> mockEndpointManager = new Mock<IGlobalEndpointManager>();
            GlobalPartitionFailoverEndpointManagerCore failoverManager = new GlobalPartitionFailoverEndpointManagerCore(mockEndpointManager.Object);

            mockEndpointManager.Setup(x => x.ReadEndpoints).Returns(() => new ReadOnlyCollection<Uri>(new List<Uri>() {new Uri("https://localhost:443/") }));

            PartitionKeyRange partitionKeyRange = new PartitionKeyRange()
            {
                Id = "0",
                MinInclusive = "",
                MaxExclusive = "FF"
            };

            using DocumentServiceRequest readRequest = DocumentServiceRequest.Create(OperationType.Read, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);
            readRequest.RequestContext.ResolvedPartitionKeyRange = partitionKeyRange;
            Assert.IsFalse(failoverManager.TryMarkEndpointUnavailableForPartitionKeyRange(
                readRequest,
                new Uri("https://localhost:443/")));
            Assert.IsFalse(failoverManager.TryAddPartitionLevelLocationOverride(
                readRequest));

            using DocumentServiceRequest createRequest = DocumentServiceRequest.Create(OperationType.Create, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);
            createRequest.RequestContext.ResolvedPartitionKeyRange = partitionKeyRange;
            Assert.IsFalse(failoverManager.TryMarkEndpointUnavailableForPartitionKeyRange(
                createRequest,
                new Uri("https://localhost:443/")));
            Assert.IsFalse(failoverManager.TryAddPartitionLevelLocationOverride(
                createRequest));

            using DocumentServiceRequest databaseRequest = DocumentServiceRequest.Create(OperationType.Read, ResourceType.Database, AuthorizationTokenType.PrimaryMasterKey);
            Assert.IsFalse(failoverManager.TryMarkEndpointUnavailableForPartitionKeyRange(
                databaseRequest,
                new Uri("https://localhost:443/")));
            Assert.IsFalse(failoverManager.TryAddPartitionLevelLocationOverride(
                databaseRequest));
        }
    }
}
