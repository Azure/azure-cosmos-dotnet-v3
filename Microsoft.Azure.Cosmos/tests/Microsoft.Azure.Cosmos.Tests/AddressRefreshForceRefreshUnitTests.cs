//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tests;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    /// <summary>
    /// Plumbing regression guard for the "AddressRefresh forceRefresh after partition migration" work item.
    ///
    /// This is NOT the work-item signal. It verifies the two distinct header pass-throughs in
    /// <see cref="GatewayAddressCache.GetServerAddressesViaGatewayAsync"/>
    /// (Microsoft.Azure.Cosmos/src/Routing/GatewayAddressCache.cs), which cover two DIFFERENT caches and
    /// must not be conflated:
    ///
    ///   * <c>x-ms-force-refresh</c> — bypasses the Gateway's ADDRESS cache, i.e. re-resolves the replica
    ///     addresses backing a partition key range. Driven by <c>forceRefreshPartitionAddresses: true</c>.
    ///   * <c>x-ms-collectionroutingmap-refresh</c> — bypasses the Gateway's COLLECTION ROUTING MAP cache,
    ///     i.e. re-resolves WHICH partition key ranges exist. Driven by
    ///     <see cref="DocumentServiceRequest.ForceCollectionRoutingMapRefresh"/>.
    ///
    /// The distinction matters because the two conditions it has to cover are not the same:
    ///
    ///   * A physical partition MIGRATION can move the replicas backing a partition key range while the
    ///     range topology is preserved. The range identity stays valid, so the addresses go stale but the
    ///     routing map does not: re-resolving the addresses is sufficient.
    ///   * A SPLIT or MERGE changes WHICH partition key ranges exist. The routing map must be re-resolved
    ///     before the addresses of the replacement range can be resolved at all, so force-refreshing the
    ///     addresses of the old range cannot recover the request on its own.
    ///   * A generic 410/0 does not identify which of the two occurred, which is why both refresh axes are
    ///     worth investigating. It does not by itself establish the policy the SDK should apply.
    ///
    /// The coupling between the two flags is ONE-WAY: a requested collection routing map refresh also
    /// drives client-side address re-resolution (subject to the concurrent-update guard in
    /// <see cref="GatewayAddressCache"/>), while forcing an ADDRESS refresh never requests a routing map
    /// refresh. A test that only covers <c>x-ms-force-refresh</c> therefore does not protect the
    /// routing-map re-resolution path. For the same reason, the <c>ForceAddressRefresh</c> diagnostics block
    /// proves that this shared client branch ran, but does not identify which flag drove it or which service
    /// header was sent.
    ///
    /// The actual question under investigation — whether the SDK DECIDES to set either flag on a generic
    /// 410 after a physical migration or topology change — lives in the closed-source
    /// Microsoft.Azure.Cosmos.Direct binary and cannot be exercised here. The FaultInjection matrix in
    /// AddressRefreshForceRefreshPostMigrationTests pins the shared-branch marker, but cannot attribute it
    /// to either flag; resolving that decision requires Direct-side or outbound-header evidence.
    /// </summary>
    [TestClass]
    public class AddressRefreshForceRefreshUnitTests
    {
        private const string DatabaseAccountApiEndpoint = "https://endpoint.azure.com";

        private readonly Mock<ICosmosAuthorizationTokenProvider> mockTokenProvider;
        private readonly Mock<IServiceConfigurationReader> mockServiceConfigReader;
        private readonly int targetReplicaSetSize = 4;
        private readonly PartitionKeyRangeIdentity testPartitionKeyRangeIdentity;
        private readonly ServiceIdentity serviceIdentity;
        private readonly Uri serviceName;

        public AddressRefreshForceRefreshUnitTests()
        {
            this.mockTokenProvider = new Mock<ICosmosAuthorizationTokenProvider>();
            this.mockTokenProvider
                .Setup(foo => foo.GetUserAuthorizationTokenAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Documents.Collections.INameValueCollection>(),
                    It.IsAny<AuthorizationTokenType>(),
                    It.IsAny<ITrace>()))
                .Returns(new ValueTask<string>("token!"));

            this.mockServiceConfigReader = new Mock<IServiceConfigurationReader>();
            this.mockServiceConfigReader.Setup(foo => foo.SystemReplicationPolicy).Returns(new ReplicationPolicy() { MaxReplicaSetSize = this.targetReplicaSetSize });
            this.mockServiceConfigReader.Setup(foo => foo.UserReplicationPolicy).Returns(new ReplicationPolicy() { MaxReplicaSetSize = this.targetReplicaSetSize });

            this.testPartitionKeyRangeIdentity = new PartitionKeyRangeIdentity("YxM9ANCZIwABAAAAAAAAAA==", "YxM9ANCZIwABAAAAAAAAAA==");
            this.serviceName = new Uri(AddressRefreshForceRefreshUnitTests.DatabaseAccountApiEndpoint);
            this.serviceIdentity = new ServiceIdentity("federation1", this.serviceName, false);
        }

        [TestMethod]
        [Owner("nalutripician")]
        public async Task TryGetAddressesAsync_ForceRefreshTrue_SendsForceRefreshHeaderToGateway()
        {
            // Arrange: capture, per outgoing Gateway address GET, whether the x-ms-force-refresh header is present.
            List<bool> forceRefreshHeaderPresence = new List<bool>();
            GatewayAddressCache cache = this.CreateCacheCapturingHeader(
                HttpConstants.HttpHeaders.ForceRefresh,
                forceRefreshHeaderPresence);

            // Act 1: a non-forced lookup on a cold cache should populate addresses WITHOUT the force-refresh header.
            DocumentServiceRequest coldRequest = DocumentServiceRequest.Create(OperationType.Invalid, ResourceType.Address, AuthorizationTokenType.Invalid);
            await cache.TryGetAddressesAsync(
                request: coldRequest,
                partitionKeyRangeIdentity: this.testPartitionKeyRangeIdentity,
                serviceIdentity: this.serviceIdentity,
                forceRefreshPartitionAddresses: false,
                cancellationToken: CancellationToken.None);

            Assert.IsFalse(
                forceRefreshHeaderPresence.Contains(true),
                "A non-forced (cold) address lookup must not send x-ms-force-refresh to the Gateway.");

            // Act 2: a forced lookup (fresh request context) must send a Gateway GET carrying x-ms-force-refresh: true.
            DocumentServiceRequest forcedRequest = DocumentServiceRequest.Create(OperationType.Invalid, ResourceType.Address, AuthorizationTokenType.Invalid);
            await cache.TryGetAddressesAsync(
                request: forcedRequest,
                partitionKeyRangeIdentity: this.testPartitionKeyRangeIdentity,
                serviceIdentity: this.serviceIdentity,
                forceRefreshPartitionAddresses: true,
                cancellationToken: CancellationToken.None);

            // Assert: a forced refresh propagated the x-ms-force-refresh header to the Gateway address feed.
            Assert.IsTrue(
                forceRefreshHeaderPresence.Contains(true),
                "A forced address refresh must send x-ms-force-refresh: true to the Gateway so it bypasses its own address cache.");
        }

        /// <summary>
        /// Companion guard for the OTHER refresh axis, and the one that governs re-routing after a range
        /// topology change: <c>x-ms-collectionroutingmap-refresh</c>.
        ///
        /// A split or a merge changes which partition key ranges exist, so re-resolving the ADDRESSES of a
        /// range that is no longer in the map cannot recover the request — the client must re-resolve the
        /// routing map itself. A physical migration is the other case: it can stale the addresses of a range
        /// while leaving the range topology intact, so the address axis alone covers it. A generic 410/0 does
        /// not say which happened, which is why both axes are guarded. This pins the
        /// <c>x-ms-collectionroutingmap-refresh</c> pass-through in
        /// <see cref="GatewayAddressCache.GetServerAddressesViaGatewayAsync"/> so that a regression which
        /// silently stops emitting the routing map refresh header is caught here rather than in a live-site
        /// incident.
        ///
        /// Note this header is driven by <see cref="DocumentServiceRequest.ForceCollectionRoutingMapRefresh"/>,
        /// NOT by the <c>forceRefreshPartitionAddresses</c> argument. The coupling is one-way: requesting a
        /// routing map refresh also triggers client-side address re-resolution (subject to the
        /// concurrent-update guard), while forcing an address refresh never requests a routing map refresh —
        /// which is precisely why the force-refresh test above does not cover this path.
        /// </summary>
        [TestMethod]
        [Owner("nalutripician")]
        public async Task TryGetAddressesAsync_ForceCollectionRoutingMapRefresh_SendsRoutingMapRefreshHeaderToGateway()
        {
            // Arrange: capture, per outgoing Gateway address GET, whether x-ms-collectionroutingmap-refresh is present.
            List<bool> routingMapRefreshHeaderPresence = new List<bool>();
            GatewayAddressCache cache = this.CreateCacheCapturingHeader(
                HttpConstants.HttpHeaders.ForceCollectionRoutingMapRefresh,
                routingMapRefreshHeaderPresence);

            // Act 1: a request that does NOT ask for a routing map refresh must not send the header, even
            // when the addresses themselves are force-refreshed. This is the assertion that keeps the two
            // axes from being conflated.
            DocumentServiceRequest addressOnlyRequest = DocumentServiceRequest.Create(OperationType.Invalid, ResourceType.Address, AuthorizationTokenType.Invalid);
            Assert.IsFalse(
                addressOnlyRequest.ForceCollectionRoutingMapRefresh,
                "Guard: a freshly created DocumentServiceRequest must not opt into a routing map refresh.");

            await cache.TryGetAddressesAsync(
                request: addressOnlyRequest,
                partitionKeyRangeIdentity: this.testPartitionKeyRangeIdentity,
                serviceIdentity: this.serviceIdentity,
                forceRefreshPartitionAddresses: true,
                cancellationToken: CancellationToken.None);

            Assert.IsTrue(
                routingMapRefreshHeaderPresence.Count > 0,
                "Guard: the cold forced address lookup must have reached the Gateway, otherwise the negative assertion below is vacuous.");
            Assert.IsFalse(
                routingMapRefreshHeaderPresence.Contains(true),
                "Forcing an ADDRESS refresh must not imply a COLLECTION ROUTING MAP refresh; address-force does not request map refresh.");

            // Act 2: a request that opts into the routing map refresh must propagate the header.
            DocumentServiceRequest routingMapRequest = DocumentServiceRequest.Create(OperationType.Invalid, ResourceType.Address, AuthorizationTokenType.Invalid);
            routingMapRequest.ForceCollectionRoutingMapRefresh = true;

            await cache.TryGetAddressesAsync(
                request: routingMapRequest,
                partitionKeyRangeIdentity: this.testPartitionKeyRangeIdentity,
                serviceIdentity: this.serviceIdentity,
                forceRefreshPartitionAddresses: false,
                cancellationToken: CancellationToken.None);

            // Assert: the routing map refresh reached the Gateway address feed. Without this the client
            // stays pinned to a stale partition key range set after a split or a merge.
            Assert.IsTrue(
                routingMapRefreshHeaderPresence.Contains(true),
                "ForceCollectionRoutingMapRefresh must send x-ms-collectionroutingmap-refresh: true so the Gateway bypasses its cached collection routing map.");
        }

        /// <summary>
        /// Builds a <see cref="GatewayAddressCache"/> whose Gateway address feed is mocked, recording for
        /// every outgoing request whether <paramref name="headerName"/> was present with value "True".
        /// </summary>
        private GatewayAddressCache CreateCacheCapturingHeader(string headerName, List<bool> headerPresence)
        {
            // Four addresses == target replica set size, so the suboptimal-replica-set timer never fires and
            // does not add a spurious forced refresh.
            List<string> addresses = new List<string>
            {
                "rntbd://dummytenant.documents.azure.com:14003/apps/APPGUID/services/SERVICEGUID/partitions/PARTITIONGUID/replicas/1p",
                "rntbd://dummytenant.documents.azure.com:14003/apps/APPGUID/services/SERVICEGUID/partitions/PARTITIONGUID/replicas/2s",
                "rntbd://dummytenant.documents.azure.com:14003/apps/APPGUID/services/SERVICEGUID/partitions/PARTITIONGUID/replicas/3s",
                "rntbd://dummytenant.documents.azure.com:14003/apps/APPGUID/services/SERVICEGUID/partitions/PARTITIONGUID/replicas/4s",
            };

            Mock<IHttpHandler> mockHttpHandler = new Mock<IHttpHandler>(MockBehavior.Strict);
            mockHttpHandler
                .Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
                .Returns<HttpRequestMessage, CancellationToken>((request, _) =>
                {
                    bool present = request.Headers.TryGetValues(headerName, out IEnumerable<string> values)
                        && values.Any(value => string.Equals(value, bool.TrueString, StringComparison.OrdinalIgnoreCase));
                    headerPresence.Add(present);
                    return MockCosmosUtil.CreateHttpResponseOfAddresses(addresses);
                });

            HttpClient httpClient = new HttpClient(new HttpHandlerHelper(mockHttpHandler.Object));
            return new GatewayAddressCache(
                new Uri(AddressRefreshForceRefreshUnitTests.DatabaseAccountApiEndpoint),
                Documents.Client.Protocol.Tcp,
                this.mockTokenProvider.Object,
                this.mockServiceConfigReader.Object,
                MockCosmosUtil.CreateCosmosHttpClient(() => httpClient),
                openConnectionsHandler: null,
                Mock.Of<IConnectionStateListener>(),
                suboptimalPartitionForceRefreshIntervalInSeconds: 2,
                enableTcpConnectionEndpointRediscovery: true);
        }
    }
}
