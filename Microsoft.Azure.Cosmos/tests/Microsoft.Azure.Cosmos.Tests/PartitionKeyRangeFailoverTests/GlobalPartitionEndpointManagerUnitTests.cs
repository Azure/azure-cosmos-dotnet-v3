//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Reflection;
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

        /// <summary>
        /// Verifies that DocumentClient.Dispose() disposes PartitionKeyRangeLocation
        /// when the implementation is IDisposable (GlobalPartitionEndpointManagerCore)
        /// and sets it to null.
        /// Regression test for: https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5777
        /// </summary>
        [TestMethod]
        [Timeout(10000)]
        public void Dispose_DisposesPartitionKeyRangeLocationWhenIDisposable()
        {
            using MockDocumentClient documentClient = new MockDocumentClient();

            Mock<IGlobalEndpointManager> mockEndpointManager = new Mock<IGlobalEndpointManager>(MockBehavior.Loose);
            GlobalPartitionEndpointManagerCore manager = new GlobalPartitionEndpointManagerCore(
                mockEndpointManager.Object,
                isPartitionLevelFailoverEnabled: false,
                isPartitionLevelCircuitBreakerEnabled: false);

            PropertyInfo property = typeof(DocumentClient).GetProperty(
                nameof(DocumentClient.PartitionKeyRangeLocation),
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(property, "Could not find PartitionKeyRangeLocation property on DocumentClient.");
            property.SetValue(documentClient, manager);
            Assert.IsNotNull(documentClient.PartitionKeyRangeLocation);
            Assert.IsInstanceOfType(documentClient.PartitionKeyRangeLocation, typeof(IDisposable));

            documentClient.Dispose();

            Assert.IsNull(documentClient.PartitionKeyRangeLocation, "PartitionKeyRangeLocation should be null after Dispose.");

            // Verify the manager was actually disposed: after disposal the cancellation token
            // is cancelled, so re-initialization of the background loop is a no-op.
            manager.InitializeAndStartCircuitBreakerFailbackBackgroundRefresh();
        }

        [TestMethod]
        [Timeout(10000)]
        public async Task Dispose_StopsBackgroundFailbackLoop()
        {
            Environment.SetEnvironmentVariable(ConfigurationManager.StalePartitionUnavailabilityRefreshIntervalInSeconds, "1");
            Environment.SetEnvironmentVariable(ConfigurationManager.AllowedPartitionUnavailabilityDurationInSeconds, "1");
            try
            {
                Mock<IGlobalEndpointManager> mockEndpointManager = new Mock<IGlobalEndpointManager>(MockBehavior.Strict);

                List<Uri> readRegions = new();
                for (int i = 1; i <= 3; i++)
                {
                    readRegions.Add(new Uri($"https://localhost:{i}/"));
                }

                mockEndpointManager.Setup(x => x.ReadEndpoints).Returns(() => new ReadOnlyCollection<Uri>(readRegions));
                mockEndpointManager.Setup(x => x.AccountReadEndpoints).Returns(() => new ReadOnlyCollection<Uri>(readRegions));
                mockEndpointManager.Setup(x => x.WriteEndpoints).Returns(() => new ReadOnlyCollection<Uri>(readRegions));
                mockEndpointManager.Setup(x => x.CanSupportMultipleWriteLocations(ResourceType.Document, OperationType.Create)).Returns(true);

                int callbackInvocationCount = 0;

                GlobalPartitionEndpointManagerCore manager = new GlobalPartitionEndpointManagerCore(
                    mockEndpointManager.Object,
                    isPartitionLevelFailoverEnabled: false,
                    isPartitionLevelCircuitBreakerEnabled: true);

                manager.SetBackgroundConnectionPeriodicRefreshTask(
                    async (pkRangeUriMappings) =>
                    {
                        Interlocked.Increment(ref callbackInvocationCount);
                        await Task.CompletedTask;
                    });

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
                createRequest.RequestContext.ResolvedCollectionRid = "test-collection";

                GlobalPartitionEndpointManagerUnitTests.SimulateConsecutiveFailures(manager, createRequest);

                Assert.IsTrue(manager.TryMarkEndpointUnavailableForPartitionKeyRange(createRequest));

                // Dispose should cancel the background loop.
                manager.Dispose();

                int countAfterDispose = callbackInvocationCount;

                // Wait long enough for the background loop to have triggered if it were still running.
                await Task.Delay(TimeSpan.FromSeconds(3));

                // The callback should not be invoked after dispose.
                Assert.AreEqual(countAfterDispose, callbackInvocationCount, "Background failback loop should not invoke callback after Dispose.");
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.AllowedPartitionUnavailabilityDurationInSeconds, null);
                Environment.SetEnvironmentVariable(ConfigurationManager.StalePartitionUnavailabilityRefreshIntervalInSeconds, null);
            }
        }

        /// <summary>
        /// Concurrent hub-region discovery on the same partition must converge to a single hub URI
        /// without torn writes. N parallel callers all complete discovery with the same success URI;
        /// the cache must end with exactly that URI as <c>Current</c> and no infinite loops.
        /// </summary>
        [TestMethod]
        [Timeout(15000)]
        public void TryCacheHubRegionLocationForPartition_ParallelCallers_ConvergesToHub()
        {
            const int parallelism = 50;
            Uri hubRegion = new Uri("https://hub-region/");
            Uri staleRegion = new Uri("https://stale-region/");

            Mock<IGlobalEndpointManager> mockEndpointManager = new Mock<IGlobalEndpointManager>(MockBehavior.Loose);
            mockEndpointManager.Setup(x => x.ReadEndpoints).Returns(() => new ReadOnlyCollection<Uri>(new List<Uri> { hubRegion, staleRegion }));
            mockEndpointManager.Setup(x => x.AccountReadEndpoints).Returns(() => new ReadOnlyCollection<Uri>(new List<Uri> { hubRegion, staleRegion }));
            mockEndpointManager.Setup(x => x.WriteEndpoints).Returns(() => new ReadOnlyCollection<Uri>(new List<Uri> { hubRegion }));
            mockEndpointManager.Setup(x => x.CanSupportMultipleWriteLocations(It.IsAny<ResourceType>(), It.IsAny<OperationType>())).Returns(false);

            GlobalPartitionEndpointManagerCore manager = new GlobalPartitionEndpointManagerCore(
                mockEndpointManager.Object,
                isPartitionLevelFailoverEnabled: true,
                isHubRegionProcessingEnabled: true);

            PartitionKeyRange partitionKeyRange = new PartitionKeyRange { Id = "0", MinInclusive = "", MaxExclusive = "FF" };

            Task[] callers = new Task[parallelism];
            for (int i = 0; i < parallelism; i++)
            {
                callers[i] = Task.Run(() =>
                {
                    DocumentServiceRequest request = DocumentServiceRequest.Create(
                        OperationType.Read, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);
                    request.RequestContext.ResolvedPartitionKeyRange = partitionKeyRange;
                    request.RequestContext.ResolvedCollectionRid = "rid";
                    request.RequestContext.RouteToLocation(hubRegion);
                    request.Headers[HttpConstants.HttpHeaders.ShouldProcessOnlyInHubRegion] = bool.TrueString;
                    manager.TryCacheHubRegionLocationForPartition(request);
                });
            }
            Task.WaitAll(callers);

            DocumentServiceRequest probe = DocumentServiceRequest.Create(
                OperationType.Read, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);
            probe.RequestContext.ResolvedPartitionKeyRange = partitionKeyRange;
            probe.RequestContext.ResolvedCollectionRid = "rid";
            probe.Headers[HttpConstants.HttpHeaders.ShouldProcessOnlyInHubRegion] = bool.TrueString;

            bool hit = manager.TryAddPartitionLevelLocationOverride(probe, checkHubRegionOverrideInCache: true);
            Assert.IsTrue(hit, "Cache must contain an entry for the partition after concurrent discovery.");
            Assert.AreEqual(hubRegion, probe.RequestContext.LocationEndpointToRoute,
                "Cache must converge to the hub URI under concurrent discovery on the same partition.");
        }

        /// <summary>
        /// A late-arriving 403/3 from a stale region (no longer Current) must NOT overwrite a cache
        /// entry already confirmed at the actual hub. Pins the <c>failedLocation != Current</c>
        /// guard in <c>TryMoveNextLocation</c>; prevents future regressions where a "losing" 403/3
        /// path poisons the cache.
        /// </summary>
        [TestMethod]
        [Timeout(10000)]
        public void TryMoveNextLocation_LateArrivingStale403_DoesNotOverwriteConfirmedHub()
        {
            Uri hubZ = new Uri("https://hub-z/");
            Uri staleX = new Uri("https://stale-x/");
            Uri otherY = new Uri("https://other-y/");

            Mock<IGlobalEndpointManager> mockEndpointManager = new Mock<IGlobalEndpointManager>(MockBehavior.Loose);
            mockEndpointManager.Setup(x => x.ReadEndpoints).Returns(() => new ReadOnlyCollection<Uri>(new List<Uri> { staleX, otherY, hubZ }));
            mockEndpointManager.Setup(x => x.AccountReadEndpoints).Returns(() => new ReadOnlyCollection<Uri>(new List<Uri> { staleX, otherY, hubZ }));
            mockEndpointManager.Setup(x => x.WriteEndpoints).Returns(() => new ReadOnlyCollection<Uri>(new List<Uri> { hubZ }));
            mockEndpointManager.Setup(x => x.CanSupportMultipleWriteLocations(It.IsAny<ResourceType>(), It.IsAny<OperationType>())).Returns(false);

            GlobalPartitionEndpointManagerCore manager = new GlobalPartitionEndpointManagerCore(
                mockEndpointManager.Object,
                isPartitionLevelFailoverEnabled: true,
                isHubRegionProcessingEnabled: true);

            PartitionKeyRange partitionKeyRange = new PartitionKeyRange { Id = "0", MinInclusive = "", MaxExclusive = "FF" };

            // Confirm hub Z via success.
            DocumentServiceRequest successRequest = DocumentServiceRequest.Create(
                OperationType.Read, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);
            successRequest.RequestContext.ResolvedPartitionKeyRange = partitionKeyRange;
            successRequest.RequestContext.ResolvedCollectionRid = "rid";
            successRequest.RequestContext.RouteToLocation(hubZ);
            successRequest.Headers[HttpConstants.HttpHeaders.ShouldProcessOnlyInHubRegion] = bool.TrueString;
            manager.TryCacheHubRegionLocationForPartition(successRequest);

            // Late-arriving 403/3 from stale X (a region that is NOT the current Current).
            DocumentServiceRequest late403Request = DocumentServiceRequest.Create(
                OperationType.Read, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);
            late403Request.RequestContext.ResolvedPartitionKeyRange = partitionKeyRange;
            late403Request.RequestContext.ResolvedCollectionRid = "rid";
            late403Request.RequestContext.RouteToLocation(staleX);
            late403Request.Headers[HttpConstants.HttpHeaders.ShouldProcessOnlyInHubRegion] = bool.TrueString;
            manager.TryMarkEndpointUnavailableForPartitionKeyRange(late403Request);

            // Probe: cache must still route to hub Z.
            DocumentServiceRequest probe = DocumentServiceRequest.Create(
                OperationType.Read, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);
            probe.RequestContext.ResolvedPartitionKeyRange = partitionKeyRange;
            probe.RequestContext.ResolvedCollectionRid = "rid";
            probe.Headers[HttpConstants.HttpHeaders.ShouldProcessOnlyInHubRegion] = bool.TrueString;

            bool hit = manager.TryAddPartitionLevelLocationOverride(probe, checkHubRegionOverrideInCache: true);
            Assert.IsTrue(hit, "Cache must remain present after a late stale 403/3.");
            Assert.AreEqual(hubZ, probe.RequestContext.LocationEndpointToRoute,
                "Late-arriving stale 403/3 from a region that is no longer Current must NOT overwrite the confirmed hub.");
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