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
    /// This is NOT the work-item signal. It only verifies the three-line pass-through in
    /// <see cref="GatewayAddressCache.GetServerAddressesViaGatewayAsync"/>
    /// (Microsoft.Azure.Cosmos/src/Routing/GatewayAddressCache.cs:849-851): when the cache is asked to
    /// resolve addresses with <c>forceRefreshPartitionAddresses: true</c>, the outgoing Gateway address
    /// feed request must carry the <c>x-ms-force-refresh: true</c> header (the header the Gateway uses to
    /// bypass its own address cache); when asked without force-refresh, it must not.
    ///
    /// The actual question under investigation — whether the SDK DECIDES to force-refresh on a generic 410
    /// after a migration — lives in the closed-source Microsoft.Azure.Cosmos.Direct binary and cannot be
    /// exercised here. See AddressRefreshForceRefreshPostMigrationTests (FaultInjection) for that.
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
                    bool present = request.Headers.TryGetValues(HttpConstants.HttpHeaders.ForceRefresh, out IEnumerable<string> values)
                        && values.Any(value => string.Equals(value, bool.TrueString, StringComparison.OrdinalIgnoreCase));
                    forceRefreshHeaderPresence.Add(present);
                    return MockCosmosUtil.CreateHttpResponseOfAddresses(addresses);
                });

            HttpClient httpClient = new HttpClient(new HttpHandlerHelper(mockHttpHandler.Object));
            GatewayAddressCache cache = new GatewayAddressCache(
                new Uri(AddressRefreshForceRefreshUnitTests.DatabaseAccountApiEndpoint),
                Documents.Client.Protocol.Tcp,
                this.mockTokenProvider.Object,
                this.mockServiceConfigReader.Object,
                MockCosmosUtil.CreateCosmosHttpClient(() => httpClient),
                openConnectionsHandler: null,
                Mock.Of<IConnectionStateListener>(),
                suboptimalPartitionForceRefreshIntervalInSeconds: 2,
                enableTcpConnectionEndpointRediscovery: true);

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
    }
}
