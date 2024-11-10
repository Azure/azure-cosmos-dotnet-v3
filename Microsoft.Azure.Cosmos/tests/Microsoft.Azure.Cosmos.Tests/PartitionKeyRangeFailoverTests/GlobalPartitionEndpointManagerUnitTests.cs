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
    public class GlobalPartitionEndpointManagerUnitTests
    {
        [TestMethod]
        [Timeout(10000)]
        public void TestSingleReadRegionScenario()
        {
            Mock<IGlobalEndpointManager> mockEndpointManager = new Mock<IGlobalEndpointManager>(MockBehavior.Strict);
            GlobalPartitionEndpointManagerCore failoverManager = new GlobalPartitionEndpointManagerCore(mockEndpointManager.Object);

            mockEndpointManager.Setup(x => x.ReadEndpoints).Returns(() => new ReadOnlyCollection<Uri>(new List<Uri>() { new Uri("https://localhost:443/") }));

            PartitionKeyRange partitionKeyRange = new PartitionKeyRange()
            {
                Id = "0",
                MinInclusive = "",
                MaxExclusive = "FF"
            };

            Uri routeToLocation = new Uri("https://localhost:443/");
            using DocumentServiceRequest readRequest = DocumentServiceRequest.Create(OperationType.Read, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);
            readRequest.RequestContext.ResolvedPartitionKeyRange = partitionKeyRange;
            readRequest.RequestContext.RouteToLocation(routeToLocation);
            Assert.IsFalse(failoverManager.TryMarkEndpointUnavailableForPartitionKeyRange(
                readRequest));
            Assert.IsFalse(failoverManager.TryAddPartitionLevelLocationOverride(
                readRequest));

            using DocumentServiceRequest createRequest = DocumentServiceRequest.Create(OperationType.Create, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);
            createRequest.RequestContext.ResolvedPartitionKeyRange = partitionKeyRange;
            readRequest.RequestContext.RouteToLocation(routeToLocation);
            Assert.IsFalse(failoverManager.TryMarkEndpointUnavailableForPartitionKeyRange(
                createRequest));
            Assert.IsFalse(failoverManager.TryAddPartitionLevelLocationOverride(
                createRequest));

            using DocumentServiceRequest databaseRequest = DocumentServiceRequest.Create(OperationType.Read, ResourceType.Database, AuthorizationTokenType.PrimaryMasterKey);
            readRequest.RequestContext.RouteToLocation(routeToLocation);
            Assert.IsFalse(failoverManager.TryMarkEndpointUnavailableForPartitionKeyRange(
                databaseRequest));
            Assert.IsFalse(failoverManager.TryAddPartitionLevelLocationOverride(
                databaseRequest));
        }

        [TestMethod]
        [DataRow(2)]
        [DataRow(3)]
        [DataRow(5)]
        [DataRow(10)]
        [DataRow(100)]
        [Timeout(30000)]
        public void VerifyAllReadRegionsAreVisited(int numOfReadRegions)
        {
            Mock<IGlobalEndpointManager> mockEndpointManager = new Mock<IGlobalEndpointManager>(MockBehavior.Strict);

            GlobalPartitionEndpointManagerCore failoverManager = new GlobalPartitionEndpointManagerCore(mockEndpointManager.Object);
            List<Uri> readRegions = new(), writeRegions = new();
            for (int i = 0; i < numOfReadRegions; i++)
            {
                readRegions.Add(new Uri($"https://localhost:{i}/"));
            }

            mockEndpointManager.Setup(x => x.ReadEndpoints).Returns(() => new ReadOnlyCollection<Uri>(readRegions));
            mockEndpointManager.Setup(x => x.AccountReadEndpoints).Returns(() => new ReadOnlyCollection<Uri>(readRegions));
            mockEndpointManager.Setup(x => x.WriteEndpoints).Returns(() => new ReadOnlyCollection<Uri>(readRegions));

            // Create a random pk range
            PartitionKeyRange partitionKeyRange = new PartitionKeyRange()
            {
                Id = "0",
                MinInclusive = "",
                MaxExclusive = "BB"
            };

            PartitionKeyRange partitionKeyRangeNotOverriden = new PartitionKeyRange()
            {
                Id = "1",
                MinInclusive = "BB",
                MaxExclusive = "FF"
            };

            using DocumentServiceRequest createRequest = DocumentServiceRequest.Create(OperationType.Create, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);
            createRequest.RequestContext.ResolvedPartitionKeyRange = partitionKeyRange;
            createRequest.RequestContext.RouteToLocation(readRegions.First());

            mockEndpointManager.Setup(x => x.CanUseMultipleWriteLocations(createRequest)).Returns(false);

            foreach (Uri region in readRegions)
            {
                Assert.AreEqual(region, createRequest.RequestContext.LocationEndpointToRoute);
                bool tryFailover = failoverManager.TryMarkEndpointUnavailableForPartitionKeyRange(
                    createRequest);

                // If there are no more regions to failover it will return false.
                if (region == readRegions.Last())
                {
                    Assert.IsFalse(tryFailover);
                    Assert.IsFalse(failoverManager.TryAddPartitionLevelLocationOverride(createRequest));
                }
                else
                {
                    Assert.IsTrue(tryFailover);
                    Assert.IsTrue(failoverManager.TryAddPartitionLevelLocationOverride(createRequest));
                    Assert.AreNotEqual(region, createRequest.RequestContext.LocationEndpointToRoute);
                }
            }
        }
    }
}