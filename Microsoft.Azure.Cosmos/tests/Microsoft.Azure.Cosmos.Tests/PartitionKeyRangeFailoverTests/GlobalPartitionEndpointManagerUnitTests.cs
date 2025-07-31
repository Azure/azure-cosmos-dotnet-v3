//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Threading;
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
            GlobalPartitionEndpointManagerCore failoverManager = new GlobalPartitionEndpointManagerCore(mockEndpointManager.Object, isPartitionLevelFailoverEnabled: true);

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

            GlobalPartitionEndpointManagerCore failoverManager = new GlobalPartitionEndpointManagerCore(
                mockEndpointManager.Object,
                isPartitionLevelFailoverEnabled: true);

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

            mockEndpointManager.Setup(x => x.CanSupportMultipleWriteLocations(It.IsAny<ResourceType>(), It.IsAny<OperationType>())).Returns(false);

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

        [TestMethod]
        [Owner("dkunda")]
        [DataRow(false, true, DisplayName = "Scenario when PPAF is disabled and circuit breaker is enabled.")]
        [DataRow(true, false, DisplayName = "Scenario when PPAF is enabled and circuit breaker is disabled.")]
        [DataRow(true, true, DisplayName = "Scenario when PPAF is enabled and circuit breaker is enabled.")]
        [DataRow(false, false, DisplayName = "Scenario when PPAF is disabled and circuit breaker is disabled.")]
        [Timeout(10000)]
        public void TryMarkEndpointUnavailableForPartitionKeyRange_WithSingleMasterWriteAccount_WritesShouldNotAddOverrideWhenCircuitBreakerEnabled(
            bool ppafEnabled,
            bool ppcbEnabled)
        {
            Mock<IGlobalEndpointManager> mockEndpointManager = new Mock<IGlobalEndpointManager>(MockBehavior.Strict);

            List<Uri> readRegions = new(), writeRegions = new();
            for (int i = 0; i < 3; i++)
            {
                readRegions.Add(new Uri($"https://localhost:{i}/"));
            }

            mockEndpointManager.Setup(x => x.ReadEndpoints).Returns(() => new ReadOnlyCollection<Uri>(readRegions));
            mockEndpointManager.Setup(x => x.AccountReadEndpoints).Returns(() => new ReadOnlyCollection<Uri>(readRegions));
            mockEndpointManager.Setup(x => x.WriteEndpoints).Returns(() => new ReadOnlyCollection<Uri>(readRegions));
            mockEndpointManager.Setup(x => x.CanSupportMultipleWriteLocations(It.IsAny<ResourceType>(), It.IsAny<OperationType>())).Returns(false);

            GlobalPartitionEndpointManagerCore failoverManager = new GlobalPartitionEndpointManagerCore(
                mockEndpointManager.Object,
                isPartitionLevelFailoverEnabled: ppafEnabled,
                isPartitionLevelCircuitBreakerEnabled: ppcbEnabled);

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

            // Simulate 11 consecutive failures.
            GlobalPartitionEndpointManagerUnitTests.SimulateConsecutiveFailures(failoverManager, readRequest);

            if (ppcbEnabled)
            {
                Assert.IsTrue(failoverManager.TryMarkEndpointUnavailableForPartitionKeyRange(
                    readRequest));
                Assert.IsTrue(failoverManager.TryAddPartitionLevelLocationOverride(
                    readRequest));
            }
            else
            {
                Assert.IsFalse(failoverManager.TryMarkEndpointUnavailableForPartitionKeyRange(
                    readRequest));
                Assert.IsFalse(failoverManager.TryAddPartitionLevelLocationOverride(
                    readRequest));
            }

            using DocumentServiceRequest createRequest = DocumentServiceRequest.Create(OperationType.Create, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);
            createRequest.RequestContext.ResolvedPartitionKeyRange = partitionKeyRange;
            createRequest.RequestContext.RouteToLocation(routeToLocation);

            if (ppafEnabled)
            {
                Assert.IsTrue(failoverManager.TryMarkEndpointUnavailableForPartitionKeyRange(
                    createRequest));
                Assert.IsTrue(failoverManager.TryAddPartitionLevelLocationOverride(
                    createRequest));
            }
            else
            {
                Assert.IsFalse(failoverManager.TryMarkEndpointUnavailableForPartitionKeyRange(
                    createRequest));
                Assert.IsFalse(failoverManager.TryAddPartitionLevelLocationOverride(
                    createRequest));
            }
        }

        [TestMethod]
        [Owner("dkunda")]
        [DataRow(true, DisplayName = "Scenario when circuit breaker is enabled.")]
        [DataRow(false, DisplayName = "Scenario when circuit breaker is disabled.")]
        [Timeout(10000)]
        public void TryMarkEndpointUnavailableForPartitionKeyRange_WithMultiMasterWriteAccount_WritesShouldAddOverrideWhenCircuitBreakerEnabled(
            bool circuitBreakerEnabled)
        {
            Mock<IGlobalEndpointManager> mockEndpointManager = new Mock<IGlobalEndpointManager>(MockBehavior.Strict);

            List<Uri> readRegions = new(), writeRegions = new();
            for (int i = 0; i < 3; i++)
            {
                readRegions.Add(new Uri($"https://localhost:{i}/"));
            }

            mockEndpointManager.Setup(x => x.ReadEndpoints).Returns(() => new ReadOnlyCollection<Uri>(readRegions));
            mockEndpointManager.Setup(x => x.AccountReadEndpoints).Returns(() => new ReadOnlyCollection<Uri>(readRegions));
            mockEndpointManager.Setup(x => x.WriteEndpoints).Returns(() => new ReadOnlyCollection<Uri>(readRegions));
            mockEndpointManager.Setup(x => x.CanSupportMultipleWriteLocations(ResourceType.Document, OperationType.Create)).Returns(true);

            GlobalPartitionEndpointManagerCore failoverManager = new GlobalPartitionEndpointManagerCore(
                mockEndpointManager.Object,
                isPartitionLevelFailoverEnabled: false,
                isPartitionLevelCircuitBreakerEnabled: circuitBreakerEnabled);

            PartitionKeyRange partitionKeyRange = new PartitionKeyRange()
            {
                Id = "0",
                MinInclusive = "",
                MaxExclusive = "FF"
            };

            Uri routeToLocation = new Uri("https://localhost:443/");
            using DocumentServiceRequest createRequest = DocumentServiceRequest.Create(OperationType.Create, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);
            createRequest.RequestContext.ResolvedPartitionKeyRange = partitionKeyRange;
            createRequest.RequestContext.RouteToLocation(routeToLocation);

            // Simulate 11 consecutive failures.
            GlobalPartitionEndpointManagerUnitTests.SimulateConsecutiveFailures(failoverManager, createRequest);

            if (circuitBreakerEnabled)
            {
                Assert.IsTrue(failoverManager.TryMarkEndpointUnavailableForPartitionKeyRange(
                    createRequest));
                Assert.IsTrue(failoverManager.TryAddPartitionLevelLocationOverride(
                    createRequest));
            }
            else
            {
                Assert.IsFalse(failoverManager.TryMarkEndpointUnavailableForPartitionKeyRange(
                    createRequest));
                Assert.IsFalse(failoverManager.TryAddPartitionLevelLocationOverride(
                    createRequest));
            }
        }

        [TestMethod]
        [Owner("dkunda")]
        [DataRow(6, DisplayName = "Scenario when request failure counter is lesser than the default threshold of 10.")]
        [DataRow(11, DisplayName = "Scenario when request failure counter is higher than the default threshold of 10.")]
        [Timeout(10000)]
        public void IncrementRequestFailureCounterAndCheckIfPartitionCanFailover_WithSingleMasterWriteAccount_ShouldReturnTrueWhenCounterReachesDefaultThreshold(
            int failureCount)
        {
            Mock<IGlobalEndpointManager> mockEndpointManager = new Mock<IGlobalEndpointManager>(MockBehavior.Strict);

            List<Uri> readRegions = new(), writeRegions = new();
            for (int i = 0; i < 3; i++)
            {
                readRegions.Add(new Uri($"https://localhost:{i}/"));
            }

            mockEndpointManager.Setup(x => x.ReadEndpoints).Returns(() => new ReadOnlyCollection<Uri>(readRegions));
            mockEndpointManager.Setup(x => x.AccountReadEndpoints).Returns(() => new ReadOnlyCollection<Uri>(readRegions));
            mockEndpointManager.Setup(x => x.WriteEndpoints).Returns(() => new ReadOnlyCollection<Uri>(readRegions));
            mockEndpointManager.Setup(x => x.CanSupportMultipleWriteLocations(ResourceType.Document, OperationType.Create)).Returns(true);

            GlobalPartitionEndpointManagerCore failoverManager = new GlobalPartitionEndpointManagerCore(
                mockEndpointManager.Object,
                isPartitionLevelFailoverEnabled: false,
                isPartitionLevelCircuitBreakerEnabled: true);

            PartitionKeyRange partitionKeyRange = new PartitionKeyRange()
            {
                Id = "0",
                MinInclusive = "",
                MaxExclusive = "FF"
            };

            Uri routeToLocation = new Uri("https://localhost:443/");
            using DocumentServiceRequest createRequest = DocumentServiceRequest.Create(OperationType.Create, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);
            createRequest.RequestContext.ResolvedPartitionKeyRange = partitionKeyRange;
            createRequest.RequestContext.RouteToLocation(routeToLocation);

            bool shouldPartitionFailOver = false;
            for(int i=0; i<=failureCount; i++)
            {
                shouldPartitionFailOver = failoverManager.IncrementRequestFailureCounterAndCheckIfPartitionCanFailover(createRequest);
            }

            // The default value for the write threshold is 5.
            if (failureCount < 5)
            {
                Assert.IsFalse(shouldPartitionFailOver);
            }
            else
            {
                Assert.IsTrue(shouldPartitionFailOver);
            }
        }

        [TestMethod]
        [Owner("dkunda")]
        [Timeout(10000)]
        public async Task InitializeAndStartCircuitBreakerFailbackBackgroundRefresh_WithCircuitBreakerEnabled_ShouldValidateUnhealthyEndpoints()
        {
            Environment.SetEnvironmentVariable(ConfigurationManager.StalePartitionUnavailabilityRefreshIntervalInSeconds, "1");
            Environment.SetEnvironmentVariable(ConfigurationManager.AllowedPartitionUnavailabilityDurationInSeconds, "1");
            try
            {
                string collectionRid = "test-collection-1";
                Mock<IGlobalEndpointManager> mockEndpointManager = new Mock<IGlobalEndpointManager>(MockBehavior.Strict);

                List<Uri> readRegions = new(), writeRegions = new();
                for (int i = 1; i <= 3; i++)
                {
                    readRegions.Add(new Uri($"https://localhost:{i}/"));
                }

                mockEndpointManager.Setup(x => x.ReadEndpoints).Returns(() => new ReadOnlyCollection<Uri>(readRegions));
                mockEndpointManager.Setup(x => x.AccountReadEndpoints).Returns(() => new ReadOnlyCollection<Uri>(readRegions));
                mockEndpointManager.Setup(x => x.WriteEndpoints).Returns(() => new ReadOnlyCollection<Uri>(readRegions));
                mockEndpointManager.Setup(x => x.CanSupportMultipleWriteLocations(ResourceType.Document, OperationType.Create)).Returns(true);

                GlobalPartitionEndpointManagerCore failoverManager = new GlobalPartitionEndpointManagerCore(
                    mockEndpointManager.Object,
                    isPartitionLevelFailoverEnabled: false,
                    isPartitionLevelCircuitBreakerEnabled: true);

                failoverManager.SetBackgroundConnectionPeriodicRefreshTask(this.OpenConnectionToUnhealthyEndpointsAsync);

                PartitionKeyRange partitionKeyRange = new PartitionKeyRange()
                {
                    Id = "0",
                    MinInclusive = "",
                    MaxExclusive = "FF"
                };

                Uri routeToLocation = new Uri("https://localhost:0/");

                using DocumentServiceRequest createRequest = DocumentServiceRequest.Create(OperationType.Create, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);
                createRequest.RequestContext.ResolvedPartitionKeyRange = partitionKeyRange;
                createRequest.RequestContext.RouteToLocation(routeToLocation);
                createRequest.RequestContext.ResolvedCollectionRid = collectionRid;

                // Simulate 11 consecutive failures.
                GlobalPartitionEndpointManagerUnitTests.SimulateConsecutiveFailures(failoverManager, createRequest);

                Assert.IsTrue(failoverManager.TryMarkEndpointUnavailableForPartitionKeyRange(createRequest));
                Assert.IsTrue(failoverManager.TryAddPartitionLevelLocationOverride(createRequest));
                Assert.AreEqual(new Uri("https://localhost:1"), createRequest.RequestContext.LocationEndpointToRoute);

                // Wait for 3 seconds for the background task to finish execution.
                await Task.Delay(TimeSpan.FromSeconds(3));

                Assert.IsFalse(failoverManager.TryAddPartitionLevelLocationOverride(createRequest));
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.AllowedPartitionUnavailabilityDurationInSeconds, null);
                Environment.SetEnvironmentVariable(ConfigurationManager.StalePartitionUnavailabilityRefreshIntervalInSeconds, null);
            }
        }

        private async Task OpenConnectionToUnhealthyEndpointsAsync(
            Dictionary<PartitionKeyRange, Tuple<string, Uri, TransportAddressHealthState.HealthStatus>> pkRangeUriMappings)
        {
            foreach (PartitionKeyRange pkRange in pkRangeUriMappings.Keys)
            {
                string collectionRid = pkRangeUriMappings[pkRange].Item1;
                Uri originalFailedLocation = pkRangeUriMappings[pkRange].Item2;

                await Task.Delay(TimeSpan.FromMilliseconds(1));

                pkRangeUriMappings[pkRange] = new Tuple<string, Uri, TransportAddressHealthState.HealthStatus>(collectionRid, originalFailedLocation, TransportAddressHealthState.HealthStatus.Connected);
            }
        }

        private static void SimulateConsecutiveFailures(
            GlobalPartitionEndpointManagerCore failoverManager,
            DocumentServiceRequest requestMessage)
        {
            for (int i = 0; i < 11; i++)
            {
                failoverManager.IncrementRequestFailureCounterAndCheckIfPartitionCanFailover(requestMessage);
            }
        }
    }
}