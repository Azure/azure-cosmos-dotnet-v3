//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Direct.Test
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public sealed class ReplicatedResourceClientTests
    {
        /// <summary>
        /// Validates that even if the user cancels the token, we still handle the address resolution request.
        /// </summary>
        [TestMethod]
        [Owner("kirankk")]
        public async Task ValidateCancellationTokenDoesNotCancelAddressRefresh()
        {
            await this.ValidateCancellationTokenDoesNotCancelAddressRefreshInternalAsync(ConsistencyLevel.BoundedStaleness);
            await this.ValidateCancellationTokenDoesNotCancelAddressRefreshInternalAsync(ConsistencyLevel.Session);
        }

        [TestMethod]
        [Owner("kirankk")]
        public async Task StoreClient_ValidateCancellationTokenDoesNotCancelAddressRefresh()
        {
            await this.StoreClient_ValidateCancellationTokenDoesNotCancelAddressRefreshInternalAsync(ConsistencyLevel.BoundedStaleness);
            await this.StoreClient_ValidateCancellationTokenDoesNotCancelAddressRefreshInternalAsync(ConsistencyLevel.Session);
        }

        /// <summary>
        /// Test to validate that, when the <see cref="ReplicatedResourceClient"/> sends the request to a
        /// <see cref="IAddressResolverExtension"/>, the <see cref="IOpenConnectionsHandler"/> is indeed invoked.
        /// </summary>
        [TestMethod]
        [Owner("dkunda")]
        public async Task OpenConnectionsToAllReplicasAsync_WithValidContainerLink_ShouldInvokeAddressResolver()
        {
            // Arrange.
            string databaseName = "test-db";
            string containerLinkUri = "http://localhost:80/my-container";
            List<string> newPhysicalUris = new();
            newPhysicalUris.Add("http://one.com/");
            newPhysicalUris.Add("http://two.com/");

            CancellationTokenSource cancellationTokenSource = new();
            ISessionContainer sessionContainer = Mock.Of<ISessionContainer>();
            MockAddressResolver mockAddressResolver = new(
                oldPhysicalUris: newPhysicalUris,
                newPhysicalUris: newPhysicalUris);
            Mock<Documents.TransportClient> mockTransportClient = new();
            MockServiceConfigurationReader mockServiceConfigurationReader = new(ConsistencyLevel.Session);
            MockAuthorizationTokenProvider mockAuthorizationTokenProvider = new();
            StoreClient storeClient = new(
                addressResolver: mockAddressResolver,
                sessionContainer: sessionContainer,
                serviceConfigurationReader: mockServiceConfigurationReader,
                userTokenProvider: mockAuthorizationTokenProvider,
                protocol: Protocol.Tcp,
                transportClient: mockTransportClient.Object);

            // Act.
            await storeClient.OpenConnectionsToAllReplicasAsync(
                databaseName: databaseName,
                containerLinkUri: containerLinkUri,
                cancellationToken: cancellationTokenSource.Token);

            // Assert.
            Assert.IsTrue(mockAddressResolver.numberOfSetOpenConnectionHandlerCallsMadeSoFar == 1);
            Assert.IsTrue(mockAddressResolver.numberOfOpenConnectionCallsMadeSoFar == 1);
            Assert.IsFalse(cancellationTokenSource.IsCancellationRequested);
        }

        private async Task ValidateCancellationTokenDoesNotCancelAddressRefreshInternalAsync(ConsistencyLevel consistencyLevel)
        {
            List<string> oldPhysicalUris = new List<string>();
            oldPhysicalUris.Add("http://one.com/");
            oldPhysicalUris.Add("http://two.com/");

            List<string> newPhysicalUris = new List<string>();
            newPhysicalUris.Add("http://one.com/");
            newPhysicalUris.Add("http://two.com/");

            Dictionary<Uri, Exception> uriToException = new Dictionary<Uri, Exception>();
            uriToException.Add(new Uri("http://one.com"), new GoneException(new TransportException(TransportErrorCode.ConnectFailed, null, Guid.NewGuid(), new Uri("http://one.com"), "description", true, true),
                                                                            SubStatusCodes.TransportGenerated410));
            uriToException.Add(new Uri("http://two.com"), new GoneException(new TransportException(TransportErrorCode.ConnectFailed, null, Guid.NewGuid(), new Uri("http://one.com"), "description", true, true),
                                                                SubStatusCodes.TransportGenerated410));

            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            MockAddressResolver mockAddressResolver = new MockAddressResolver(oldPhysicalUris, newPhysicalUris);
            ISessionContainer sessionContainer = Mock.Of<ISessionContainer>();


            MockTransportClient mockTransportClient = new MockTransportClient(null, uriToException, cancellationTokenSource);
            MockServiceConfigurationReader mockServiceConfigurationReader = new MockServiceConfigurationReader(consistencyLevel);
            MockAuthorizationTokenProvider mockAuthorizationTokenProvider = new MockAuthorizationTokenProvider();

            ReplicatedResourceClient replicatedResourceClient = new ReplicatedResourceClient(
                addressResolver: mockAddressResolver,
                sessionContainer: sessionContainer,
                protocol: Protocol.Tcp,
                transportClient: mockTransportClient,
                serviceConfigReader: mockServiceConfigurationReader,
                authorizationTokenProvider: mockAuthorizationTokenProvider,
                enableReadRequestsFallback: false,
                useMultipleWriteLocations: false,
                detectClientConnectivityIssues: false,
                disableRetryWithRetryPolicy: false,
                enableReplicaValidation: false);

            INameValueCollection headers = new DictionaryNameValueCollection();
            headers.Set(HttpConstants.HttpHeaders.ConsistencyLevel, ConsistencyLevel.BoundedStaleness.ToString());

            using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Read,
                ResourceType.Document,
                "dbs/OVJwAA==/colls/OVJwAOcMtA0=/docs/OVJwAOcMtA0BAAAAAAAAAA==/",
                AuthorizationTokenType.PrimaryMasterKey,
                headers))
            {
                try
                {
                    await replicatedResourceClient.InvokeAsync(request, cancellationToken: cancellationTokenSource.Token);
                    Assert.Fail("Expected to throw OperationCanceledException");
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    Assert.Fail($"Expected to throw OperationCanceledException: {ex}");
                }
            }

            Assert.IsTrue(cancellationTokenSource.IsCancellationRequested);
            await ReplicatedResourceClientTests.VerifyBackGroundAddressResfreshAsync(mockAddressResolver);
        }

        private async Task StoreClient_ValidateCancellationTokenDoesNotCancelAddressRefreshInternalAsync(ConsistencyLevel consistencyLevel)
        {
            List<string> newPhysicalUris = new List<string>();
            newPhysicalUris.Add("http://one.com/");
            newPhysicalUris.Add("http://two.com/");

            Dictionary<Uri, Exception> uriToException = new Dictionary<Uri, Exception>();
            uriToException.Add(new Uri("http://one.com"), new GoneException(new TransportException(TransportErrorCode.ConnectFailed, null, Guid.NewGuid(), new Uri("http://one.com"), "description", true, true),
                                                                            SubStatusCodes.TransportGenerated410));
            uriToException.Add(new Uri("http://two.com"), new GoneException(new TransportException(TransportErrorCode.ConnectFailed, null, Guid.NewGuid(), new Uri("http://one.com"), "description", true, true),
                                                                            SubStatusCodes.TransportGenerated410));

            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

            MockAddressResolver mockAddressResolver = new MockAddressResolver(newPhysicalUris, newPhysicalUris);
            ISessionContainer sessionContainer = Mock.Of<ISessionContainer>();
            MockTransportClient mockTransportClient = new MockTransportClient(null, uriToException, cancellationTokenSource);
            MockServiceConfigurationReader mockServiceConfigurationReader = new MockServiceConfigurationReader(consistencyLevel);
            MockAuthorizationTokenProvider mockAuthorizationTokenProvider = new MockAuthorizationTokenProvider();
            StoreClient storeClient = new StoreClient(
                mockAddressResolver,
                sessionContainer,
                mockServiceConfigurationReader,
                mockAuthorizationTokenProvider,
                Protocol.Tcp,
                mockTransportClient);

            INameValueCollection headers = new DictionaryNameValueCollection();

            using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Read,
                ResourceType.Document,
                "dbs/OVJwAA==/colls/OVJwAOcMtA0=/docs/OVJwAOcMtA0BAAAAAAAAAA==/",
                AuthorizationTokenType.PrimaryMasterKey,
                headers))
            {
                try
                {
                    await storeClient.ProcessMessageAsync(request, cancellationTokenSource.Token);
                    Assert.Fail("Expected to throw OperationCanceledException");
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    Assert.Fail($"Expected to throw OperationCanceledException: {ex}");
                }
            }

            Assert.IsTrue(cancellationTokenSource.IsCancellationRequested);
            await ReplicatedResourceClientTests.VerifyBackGroundAddressResfreshAsync(mockAddressResolver);
        }

        private static async Task VerifyBackGroundAddressResfreshAsync(MockAddressResolver mockAddressResolver)
        {
            // The background refresh may take some time to start.
            // Loop until the request is verified or it times out.
            Stopwatch stopwatch = Stopwatch.StartNew();
            do
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(50));
                    Assert.IsTrue(mockAddressResolver.numberOfRefreshes > 0);
                    return;
                }
                catch (Exception)
                {
                    if (stopwatch.Elapsed > TimeSpan.FromSeconds(1))
                    {
                        throw;
                    }
                }
            } while (true);
        }

        private class MockAddressResolver : IAddressResolverExtension
        {
            private readonly List<AddressInformation> oldAddressInformations;
            private readonly List<AddressInformation> newAddressInformations;

            public int numberOfRefreshes { get; set; }

            public int numberOfSetOpenConnectionHandlerCallsMadeSoFar { get; set; }

            public int numberOfOpenConnectionCallsMadeSoFar { get; set; }

            public MockAddressResolver(List<string> oldPhysicalUris, List<string> newPhysicalUris)
            {
                this.numberOfRefreshes = 0;
                this.numberOfOpenConnectionCallsMadeSoFar = 0;
                this.numberOfSetOpenConnectionHandlerCallsMadeSoFar = 0;
                this.oldAddressInformations = new List<AddressInformation>();

                for (int i = 0; i < oldPhysicalUris.Count; i++)
                {
                    this.oldAddressInformations.Add(new AddressInformation(
                        isPrimary: i == 0,
                        isPublic: true,
                        physicalUri: oldPhysicalUris[i],
                        protocol: Protocol.Tcp,
                        isAuxiliary: false));
                }

                this.newAddressInformations = new List<AddressInformation>();
                for (int i = 0; i < newPhysicalUris.Count; i++)
                {
                    this.newAddressInformations.Add(new AddressInformation(
                        isPrimary: i == 0,
                        isPublic: true,
                        physicalUri: newPhysicalUris[i],
                        protocol: Protocol.Tcp,
                        isAuxiliary: false));
                }
            }

            public Task<PartitionAddressInformation> ResolveAsync(DocumentServiceRequest request, bool forceRefreshPartitionAddresses, CancellationToken cancellationToken)
            {
                request.RequestContext.ResolvedPartitionKeyRange = new PartitionKeyRange() { Id = "0" };
                if (forceRefreshPartitionAddresses)
                {
                    this.numberOfRefreshes++;
                }

                return Task.FromResult<PartitionAddressInformation>(new PartitionAddressInformation(this.oldAddressInformations.ToArray()));
            }

            public Task UpdateAsync(Documents.Rntbd.ServerKey serverKey, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public Task UpdateAsync(IReadOnlyList<AddressCacheToken> addressCacheTokens, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public Task OpenConnectionsToAllReplicasAsync(
                string databaseName,
                string containerLinkUri,
                CancellationToken cancellationToken = default)
            {
                this.numberOfOpenConnectionCallsMadeSoFar++;
                return Task.CompletedTask;
            }

            public void SetOpenConnectionsHandler(IOpenConnectionsHandler openConnectionHandler)
            {
                this.numberOfSetOpenConnectionHandlerCallsMadeSoFar++;
            }
        }

        private class MockTransportClient : Documents.TransportClient
        {
            private Dictionary<Uri, StoreResponse> uriToStoreResponseMap;
            private Dictionary<Uri, Exception> uriToExceptionMap;
            private CancellationTokenSource cancellationTokenSource;

            public MockTransportClient(
                Dictionary<Uri, StoreResponse> uriToStoreResponseMap,
                Dictionary<Uri, Exception> uriToExceptionMap,
                CancellationTokenSource cancellationTokenSource)
            {
                this.uriToStoreResponseMap = uriToStoreResponseMap;
                this.uriToExceptionMap = uriToExceptionMap;
                this.cancellationTokenSource = cancellationTokenSource;
            }

            internal override Task<StoreResponse> InvokeStoreAsync(Uri physicalAddress, ResourceOperation resourceOperation, DocumentServiceRequest request)
            {
                if (this.uriToStoreResponseMap != null && this.uriToStoreResponseMap.ContainsKey(physicalAddress))
                {
                    return Task.FromResult<StoreResponse>(this.uriToStoreResponseMap[physicalAddress]);
                }

                if (this.uriToExceptionMap != null && this.uriToExceptionMap.ContainsKey(physicalAddress))
                {
                    cancellationTokenSource.Cancel();
                    throw uriToExceptionMap[physicalAddress];
                }

                throw new InvalidOperationException();
            }

            internal override Task OpenConnectionAsync(
                Uri physicalAddress)
            {
                return Task.CompletedTask;
            }
        }

        private class MockServiceConfigurationReader : IServiceConfigurationReader
        {
            private readonly ConsistencyLevel consistencyLevel;
            public MockServiceConfigurationReader(ConsistencyLevel consistencyLevel)
            {
                this.consistencyLevel = consistencyLevel;
            }

            public string DatabaseAccountId
            {
                get { return "localhost"; }
            }

            public Uri DatabaseAccountApiEndpoint { get; private set; }

            public ReplicationPolicy UserReplicationPolicy
            {
                get { return new ReplicationPolicy(); }
            }

            public ReplicationPolicy SystemReplicationPolicy
            {
                get { return new ReplicationPolicy(); }
            }

            public ConsistencyLevel DefaultConsistencyLevel => this.consistencyLevel;

            public ReadPolicy ReadPolicy
            {
                get { return new ReadPolicy(); }
            }

            public string PrimaryMasterKey
            {
                get { return "key"; }
            }

            public string SecondaryMasterKey
            {
                get { return "key"; }
            }

            public string PrimaryReadonlyMasterKey
            {
                get { return "key"; }
            }

            public string SecondaryReadonlyMasterKey
            {
                get { return "key"; }
            }

            public string ResourceSeedKey
            {
                get { return "seed"; }
            }

            public string SubscriptionId
            {
                get { return Guid.Empty.ToString(); }
            }

            public bool EnableNRegionSynchronousCommit
            {
                get { return false; }
            }

            public Task InitializeAsync()
            {
                return Task.FromResult(true);
            }
        }

        private class MockAuthorizationTokenProvider : IAuthorizationTokenProvider
        {
            public ValueTask<(string token, string payload)> GetUserAuthorizationAsync(
                string resourceAddress,
                string resourceType,
                string requestVerb,
                INameValueCollection headers,
                AuthorizationTokenType tokenType)
            {
                return new ValueTask<(string token, string payload)>(("authtoken!", null));
            }

            public Task AddSystemAuthorizationHeaderAsync(DocumentServiceRequest request, string federationId, string verb, string resourceId)
            {
                request.Headers[HttpConstants.HttpHeaders.XDate] = DateTime.UtcNow.ToString("r", CultureInfo.InvariantCulture);
                request.Headers[HttpConstants.HttpHeaders.Authorization] = "authtoken!";
                return Task.FromResult(0);
            }
        }
    }
}