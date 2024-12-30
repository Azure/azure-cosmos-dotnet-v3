namespace Microsoft.Azure.Cosmos.Client.Tests
{
    using System;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Cosmos.Common;
    using System.Net.Http;
    using System.Reflection;
    using System.Collections.Concurrent;

    /// <summary>
    /// Tests for <see cref="ClientRetryPolicy"/>
    /// </summary>
    [TestClass]
    public sealed class ClientRetryPolicyTests
    {
        private static Uri Location1Endpoint = new Uri("https://location1.documents.azure.com");
        private static Uri Location2Endpoint = new Uri("https://location2.documents.azure.com");

        private ReadOnlyCollection<string> preferredLocations;
        private AccountProperties databaseAccount;
        private GlobalPartitionEndpointManager partitionKeyRangeLocationCache;
        private Mock<IDocumentClientInternal> mockedClient;

        /// <summary>
        /// Tests behavior of Multimaster Accounts on metadata writes where the default location is not the hub region
        /// </summary>
        [TestMethod]
        public void MultimasterMetadataWriteRetryTest()
        {
            const bool enableEndpointDiscovery = false;

            //Creates GlobalEndpointManager where enableEndpointDiscovery is False and
            //Default location is false
            using GlobalEndpointManager endpointManager = this.Initialize(
                useMultipleWriteLocations: true,
                enableEndpointDiscovery: enableEndpointDiscovery,
                isPreferredLocationsListEmpty: true,
                multimasterMetadataWriteRetryTest: true);


            ClientRetryPolicy retryPolicy = new ClientRetryPolicy(endpointManager, this.partitionKeyRangeLocationCache, new RetryOptions(), enableEndpointDiscovery, false);

            //Creates a metadata write request
            DocumentServiceRequest request = this.CreateRequest(false, true);

            Assert.IsTrue(endpointManager.IsMultimasterMetadataWriteRequest(request));

            //On first attempt should get incorrect (default/non hub) location
            retryPolicy.OnBeforeSendRequest(request);
            Assert.AreEqual(request.RequestContext.LocationEndpointToRoute, ClientRetryPolicyTests.Location2Endpoint);

            //Creation of 403.3 Error
            HttpStatusCode forbidden = HttpStatusCode.Forbidden;
            SubStatusCodes writeForbidden = SubStatusCodes.WriteForbidden;
            Exception forbiddenWriteFail = new Exception();
            Mock<INameValueCollection> nameValueCollection = new Mock<INameValueCollection>();

            DocumentClientException documentClientException = new DocumentClientException(
                message: "Multimaster Metadata Write Fail",
                innerException: forbiddenWriteFail,
                statusCode: forbidden,
                substatusCode: writeForbidden,
                requestUri: request.RequestContext.LocationEndpointToRoute,
                responseHeaders: nameValueCollection.Object);

            CancellationToken cancellationToken = new CancellationToken();

            //Tests behavior of should retry
            Task<ShouldRetryResult> shouldRetry = retryPolicy.ShouldRetryAsync(documentClientException, cancellationToken);

            Assert.IsTrue(shouldRetry.Result.ShouldRetry);

            //Now since the retry context is not null, should route to the hub region
            retryPolicy.OnBeforeSendRequest(request);
            Assert.AreEqual(request.RequestContext.LocationEndpointToRoute, ClientRetryPolicyTests.Location1Endpoint);
        }

        /// <summary>
        /// Test to validate that when 429.3092 is thrown from the service, write requests on
        /// a multi master account should be converted to 503 and retried to the next region.
        /// </summary>
        [TestMethod]
        [DataRow(true, DisplayName = "Validate retry policy with multi master write account.")]
        [DataRow(false, DisplayName = "Validate retry policy with single master write account.")]
        public async Task ShouldRetryAsync_WhenRequestThrottledWithResourceNotAvailable_ShouldThrow503OnMultiMasterWriteAndRetryOnNextRegion(
            bool isMultiMasterAccount)
        {
            // Arrange.
            const bool enableEndpointDiscovery = true;
            using GlobalEndpointManager endpointManager = this.Initialize(
                useMultipleWriteLocations: isMultiMasterAccount,
                enableEndpointDiscovery: enableEndpointDiscovery,
                isPreferredLocationsListEmpty: false,
                multimasterMetadataWriteRetryTest: true);

            await endpointManager.RefreshLocationAsync();

            ClientRetryPolicy retryPolicy = new (
                endpointManager,
                this.partitionKeyRangeLocationCache,
                new RetryOptions(),
                enableEndpointDiscovery,
                false);

            // Creates a sample write request.
            DocumentServiceRequest request = this.CreateRequest(
                isReadRequest: false,
                isMasterResourceType: false);

            // On first attempt should get (default/non hub) location.
            retryPolicy.OnBeforeSendRequest(request);
            Assert.AreEqual(request.RequestContext.LocationEndpointToRoute, ClientRetryPolicyTests.Location1Endpoint);

            // Creation of 429.3092 Error.
            HttpStatusCode throttleException = HttpStatusCode.TooManyRequests;
            SubStatusCodes resourceNotAvailable = SubStatusCodes.SystemResourceUnavailable;

            Exception innerException = new ();
            Mock<INameValueCollection> nameValueCollection = new ();
            DocumentClientException documentClientException = new (
                message: "SystemResourceUnavailable: 429 with 3092 occurred.",
                innerException: innerException,
                statusCode: throttleException,
                substatusCode: resourceNotAvailable,
                requestUri: request.RequestContext.LocationEndpointToRoute,
                responseHeaders: nameValueCollection.Object);

            // Act.
            Task<ShouldRetryResult> shouldRetry = retryPolicy.ShouldRetryAsync(
                documentClientException,
                new CancellationToken());

            // Assert.
            Assert.IsTrue(shouldRetry.Result.ShouldRetry);
            retryPolicy.OnBeforeSendRequest(request);

            if (isMultiMasterAccount)
            {
                Assert.AreEqual(
                    expected: ClientRetryPolicyTests.Location2Endpoint,
                    actual: request.RequestContext.LocationEndpointToRoute,
                    message: "The request should be routed to the next region, since the accound is a multi master write account and the request" +
                    "failed with 429.309 which got converted into 503 internally. This should trigger another retry attempt to the next region.");
            }
            else
            {
                Assert.AreEqual(
                    expected: ClientRetryPolicyTests.Location1Endpoint,
                    actual: request.RequestContext.LocationEndpointToRoute,
                    message: "Since this is asingle master account, the write request should not be retried on the next region.");
            }
        }

        /// <summary>
        /// Tests to see if different 503 substatus codes are handeled correctly
        /// </summary>
        /// <param name="testCode">The substatus code being Tested.</param>
        [DataRow((int)SubStatusCodes.Unknown)]
        [DataRow((int)SubStatusCodes.TransportGenerated503)]
        [DataTestMethod]
        public void Http503SubStatusHandelingTests(int testCode)
        {

            const bool enableEndpointDiscovery = true;
            //Create GlobalEndpointManager
            using GlobalEndpointManager endpointManager = this.Initialize(
               useMultipleWriteLocations: false,
               enableEndpointDiscovery: enableEndpointDiscovery,
               isPreferredLocationsListEmpty: true);

            //Create Retry Policy
            ClientRetryPolicy retryPolicy = new ClientRetryPolicy(endpointManager, this.partitionKeyRangeLocationCache, new RetryOptions(), enableEndpointDiscovery, false);

            CancellationToken cancellationToken = new CancellationToken();
            Exception serviceUnavailableException = new Exception();
            Mock<INameValueCollection> nameValueCollection = new Mock<INameValueCollection>();

            HttpStatusCode serviceUnavailable = HttpStatusCode.ServiceUnavailable;

            DocumentClientException documentClientException = new DocumentClientException(
               message: "Service Unavailable",
               innerException: serviceUnavailableException,
               responseHeaders: nameValueCollection.Object,
               statusCode: serviceUnavailable,
               substatusCode: (SubStatusCodes)testCode,
               requestUri: null
               );

            Task<ShouldRetryResult> retryStatus = retryPolicy.ShouldRetryAsync(documentClientException, cancellationToken);

            Assert.IsFalse(retryStatus.Result.ShouldRetry);
        }

        /// <summary>
        /// Tests to validate that when HttpRequestException is thrown while connecting to a gateway endpoint for a single master write account with PPAF enabled,
        /// a partition level failover is added and the request is retried to the next region.
        /// </summary>
        [TestMethod]
        [DataRow(true, DisplayName = "Case when partition level failover is enabled.")]
        [DataRow(false, DisplayName = "Case when partition level failover is disabled.")]

        public void HttpRequestExceptionHandelingTests(
            bool enablePartitionLevelFailover)
        {
            const bool enableEndpointDiscovery = true;
            const string suffix = "-FF-FF-FF-FF-FF-FF-FF-FF-FF-FF-FF-FF-FF-FF-FF";

            //Creates a sample write request
            DocumentServiceRequest request = this.CreateRequest(false, false);
            request.RequestContext.ResolvedPartitionKeyRange = new PartitionKeyRange() { Id = "0" , MinInclusive = "3F" + suffix, MaxExclusive = "5F" + suffix };

            //Create GlobalEndpointManager
            using GlobalEndpointManager endpointManager = this.Initialize(
               useMultipleWriteLocations: false,
               enableEndpointDiscovery: enableEndpointDiscovery,
               isPreferredLocationsListEmpty: false,
               enablePartitionLevelFailover: enablePartitionLevelFailover);

            // Capture the read locations.
            ReadOnlyCollection<Uri> readLocations = endpointManager.ReadEndpoints;

            //Create Retry Policy
            ClientRetryPolicy retryPolicy = new (
                globalEndpointManager: endpointManager,
                partitionKeyRangeLocationCache: this.partitionKeyRangeLocationCache,
                retryOptions: new RetryOptions(),
                enableEndpointDiscovery: enableEndpointDiscovery,
                isPertitionLevelFailoverEnabled: enablePartitionLevelFailover);

            CancellationToken cancellationToken = new ();
            HttpRequestException httpRequestException = new (message: "Connecting to endpoint has failed.");

            GlobalPartitionEndpointManagerCore.PartitionKeyRangeFailoverInfo partitionKeyRangeFailoverInfo = ClientRetryPolicyTests.GetPartitionKeyRangeFailoverInfoUsingReflection(
                this.partitionKeyRangeLocationCache,
                request.RequestContext.ResolvedPartitionKeyRange);

            // Validate that the partition key range failover info is not present before the http request exception was captured in the retry policy.
            Assert.IsNull(partitionKeyRangeFailoverInfo);

            retryPolicy.OnBeforeSendRequest(request);
            Task<ShouldRetryResult> retryStatus = retryPolicy.ShouldRetryAsync(httpRequestException, cancellationToken);

            Assert.IsTrue(retryStatus.Result.ShouldRetry);

            partitionKeyRangeFailoverInfo = ClientRetryPolicyTests.GetPartitionKeyRangeFailoverInfoUsingReflection(
                this.partitionKeyRangeLocationCache,
                request.RequestContext.ResolvedPartitionKeyRange);

            if (enablePartitionLevelFailover)
            {
                // Validate that the partition key range failover info to the next account region is present after the http request exception was captured in the retry policy.
                Assert.AreEqual(partitionKeyRangeFailoverInfo.Current, readLocations[1]);
            }
            else
            {
                Assert.IsNull(partitionKeyRangeFailoverInfo);
            }
        }

        [TestMethod]
        public async Task ClientRetryPolicy_Retry_SingleMaster_Read_PreferredLocationsAsync()
        {
            await this.ValidateConnectTimeoutTriggersClientRetryPolicyAsync(isReadRequest: true, useMultipleWriteLocations: false, usesPreferredLocations: true, shouldHaveRetried: true);
        }

        [TestMethod]
        public async Task ClientRetryPolicy_Retry_MultiMaster_Read_PreferredLocationsAsync()
        {
            await this.ValidateConnectTimeoutTriggersClientRetryPolicyAsync(isReadRequest: true, useMultipleWriteLocations: true, usesPreferredLocations: true, shouldHaveRetried: true);
        }

        [TestMethod]
        public async Task ClientRetryPolicy_Retry_MultiMaster_Write_PreferredLocationsAsync()
        {
            await this.ValidateConnectTimeoutTriggersClientRetryPolicyAsync(isReadRequest: false, useMultipleWriteLocations: true, usesPreferredLocations: true, shouldHaveRetried: true);
        }

        [TestMethod]
        public async Task ClientRetryPolicy_NoRetry_SingleMaster_Write_PreferredLocationsAsync()
        {
            await this.ValidateConnectTimeoutTriggersClientRetryPolicyAsync(isReadRequest: false, useMultipleWriteLocations: false, usesPreferredLocations: true, shouldHaveRetried: false);
        }

        [TestMethod]
        public async Task ClientRetryPolicy_NoRetry_SingleMaster_Read_NoPreferredLocationsAsync()
        {
            await this.ValidateConnectTimeoutTriggersClientRetryPolicyAsync(isReadRequest: true, useMultipleWriteLocations: false, usesPreferredLocations: false, shouldHaveRetried: true);
        }

        [TestMethod]
        public async Task ClientRetryPolicy_NoRetry_SingleMaster_Write_NoPreferredLocationsAsync()
        {
            await this.ValidateConnectTimeoutTriggersClientRetryPolicyAsync(isReadRequest: false, useMultipleWriteLocations: false, usesPreferredLocations: false, shouldHaveRetried: false);
        }

        [TestMethod]
        public async Task ClientRetryPolicy_NoRetry_MultiMaster_Read_NoPreferredLocationsAsync()
        {
            await this.ValidateConnectTimeoutTriggersClientRetryPolicyAsync(isReadRequest: true, useMultipleWriteLocations: true, usesPreferredLocations: false, true);
        }

        [TestMethod]
        public async Task ClientRetryPolicy_NoRetry_MultiMaster_Write_NoPreferredLocationsAsync()
        {
            await this.ValidateConnectTimeoutTriggersClientRetryPolicyAsync(isReadRequest: false, useMultipleWriteLocations: true, usesPreferredLocations: false, true);
        }

        private async Task ValidateConnectTimeoutTriggersClientRetryPolicyAsync(
            bool isReadRequest,
            bool useMultipleWriteLocations,
            bool usesPreferredLocations,
            bool shouldHaveRetried)
        {
            List<string> newPhysicalUris = new List<string>();
            newPhysicalUris.Add("https://default.documents.azure.com");
            newPhysicalUris.Add("https://location1.documents.azure.com");
            newPhysicalUris.Add("https://location2.documents.azure.com");
            newPhysicalUris.Add("https://location3.documents.azure.com");

            Dictionary<Uri, Exception> uriToException = new Dictionary<Uri, Exception>();
            uriToException.Add(new Uri("https://default.documents.azure.com"), new GoneException(new TransportException(TransportErrorCode.ConnectTimeout, innerException: null, activityId: Guid.NewGuid(), requestUri: new Uri("https://default.documents.azure.com"), sourceDescription: "description", userPayload: true, payloadSent: true), SubStatusCodes.TransportGenerated410));
            uriToException.Add(new Uri("https://location1.documents.azure.com"), new GoneException(new TransportException(TransportErrorCode.ConnectTimeout, innerException: null, activityId: Guid.NewGuid(), requestUri: new Uri("https://location1.documents.azure.com"), sourceDescription: "description", userPayload: true, payloadSent: true), SubStatusCodes.TransportGenerated410));
            uriToException.Add(new Uri("https://location2.documents.azure.com"), new GoneException(new TransportException(TransportErrorCode.ConnectTimeout, innerException: null, activityId: Guid.NewGuid(), requestUri: new Uri("https://location2.documents.azure.com"), sourceDescription: "description", userPayload: true, payloadSent: true), SubStatusCodes.TransportGenerated410));
            uriToException.Add(new Uri("https://location3.documents.azure.com"), new GoneException(new TransportException(TransportErrorCode.ConnectTimeout, innerException: null, activityId: Guid.NewGuid(), requestUri: new Uri("https://location3.documents.azure.com"), sourceDescription: "description", userPayload: true, payloadSent: true), SubStatusCodes.TransportGenerated410));

            using MockDocumentClientContext mockDocumentClientContext = this.InitializeMockedDocumentClient(useMultipleWriteLocations, !usesPreferredLocations);
            mockDocumentClientContext.GlobalEndpointManager.InitializeAccountPropertiesAndStartBackgroundRefresh(mockDocumentClientContext.DatabaseAccount);

            MockAddressResolver mockAddressResolver = new MockAddressResolver(newPhysicalUris, newPhysicalUris);
            SessionContainer sessionContainer = new SessionContainer("localhost");
            MockTransportClient mockTransportClient = new MockTransportClient(null, uriToException);
            MockServiceConfigurationReader mockServiceConfigurationReader = new MockServiceConfigurationReader();
            MockAuthorizationTokenProvider mockAuthorizationTokenProvider = new MockAuthorizationTokenProvider();

            ReplicatedResourceClient replicatedResourceClient = new ReplicatedResourceClient(
                addressResolver: mockAddressResolver,
                sessionContainer: sessionContainer,
                protocol: Protocol.Tcp,
                transportClient: mockTransportClient,
                serviceConfigReader: mockServiceConfigurationReader,
                authorizationTokenProvider: mockAuthorizationTokenProvider,
                enableReadRequestsFallback: false,
                useMultipleWriteLocations: useMultipleWriteLocations,
                detectClientConnectivityIssues: true,
                disableRetryWithRetryPolicy: false,
                enableReplicaValidation: false,
                accountConfigurationProperties: null);

            // Reducing retry timeout to avoid long-running tests
            replicatedResourceClient.GoneAndRetryWithRetryTimeoutInSecondsOverride = 1;

            this.partitionKeyRangeLocationCache = GlobalPartitionEndpointManagerNoOp.Instance;

            ClientRetryPolicy retryPolicy = new ClientRetryPolicy(mockDocumentClientContext.GlobalEndpointManager, this.partitionKeyRangeLocationCache, new RetryOptions(), enableEndpointDiscovery: true, isPertitionLevelFailoverEnabled: false);

            INameValueCollection headers = new DictionaryNameValueCollection();
            headers.Set(HttpConstants.HttpHeaders.ConsistencyLevel, ConsistencyLevel.BoundedStaleness.ToString());

            using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                isReadRequest ? OperationType.Read : OperationType.Create,
                ResourceType.Document,
                "dbs/OVJwAA==/colls/OVJwAOcMtA0=/docs/OVJwAOcMtA0BAAAAAAAAAA==/",
                AuthorizationTokenType.PrimaryMasterKey,
                headers))
            {
                int retryCount = 0;

                try
                {
                    await BackoffRetryUtility<StoreResponse>.ExecuteAsync(
                        () =>
                        {
                            retryPolicy.OnBeforeSendRequest(request);

                            if (retryCount == 1)
                            {
                                Uri expectedEndpoint = null;
                                if (usesPreferredLocations)
                                {
                                    expectedEndpoint = new Uri(mockDocumentClientContext.DatabaseAccount.ReadLocationsInternal.First(l => l.Name == mockDocumentClientContext.PreferredLocations[1]).Endpoint);
                                }
                                else
                                {
                                    if (isReadRequest)
                                    {
                                        expectedEndpoint = new Uri(mockDocumentClientContext.DatabaseAccount.ReadLocationsInternal[1].Endpoint);
                                    }
                                    else
                                    {
                                        expectedEndpoint = new Uri(mockDocumentClientContext.DatabaseAccount.WriteLocationsInternal[1].Endpoint);
                                    }
                                }

                                Assert.AreEqual(expectedEndpoint, request.RequestContext.LocationEndpointToRoute);
                            }
                            else if (retryCount > 1)
                            {
                                Assert.Fail("Should retry once");
                            }

                            retryCount++;

                            return replicatedResourceClient.InvokeAsync(request);
                        },
                        retryPolicy);

                    Assert.Fail();
                }
                catch (ServiceUnavailableException)
                {
                    if (shouldHaveRetried)
                    {
                        Assert.AreEqual(2, retryCount, $"Retry count {retryCount}, shouldHaveRetried {shouldHaveRetried} isReadRequest {isReadRequest} useMultipleWriteLocations {useMultipleWriteLocations} usesPreferredLocations {usesPreferredLocations}");
                    }
                    else
                    {
                        Assert.AreEqual(1, retryCount, $"Retry count {retryCount}, shouldHaveRetried {shouldHaveRetried} isReadRequest {isReadRequest} useMultipleWriteLocations {useMultipleWriteLocations} usesPreferredLocations {usesPreferredLocations}");
                    }
                }
            }
        }

        private static GlobalPartitionEndpointManagerCore.PartitionKeyRangeFailoverInfo GetPartitionKeyRangeFailoverInfoUsingReflection(
            GlobalPartitionEndpointManager globalPartitionEndpointManager,
            PartitionKeyRange pkRange)
        {
            FieldInfo fieldInfo = globalPartitionEndpointManager
                .GetType()
                .GetField(
                    name: "PartitionKeyRangeToLocation",
                    bindingAttr: BindingFlags.Instance | BindingFlags.NonPublic);

            if (fieldInfo != null)
            {
                Lazy<ConcurrentDictionary<PartitionKeyRange, GlobalPartitionEndpointManagerCore.PartitionKeyRangeFailoverInfo>> partitionKeyRangeToLocation = (Lazy<ConcurrentDictionary<PartitionKeyRange, GlobalPartitionEndpointManagerCore.PartitionKeyRangeFailoverInfo>>)fieldInfo.GetValue(globalPartitionEndpointManager);
                partitionKeyRangeToLocation.Value.TryGetValue(pkRange, out GlobalPartitionEndpointManagerCore.PartitionKeyRangeFailoverInfo partitionKeyRangeFailoverInfo);

                return partitionKeyRangeFailoverInfo;
            }

            return null;
        }

        private static AccountProperties CreateDatabaseAccount(
            bool useMultipleWriteLocations,
            bool enforceSingleMasterSingleWriteLocation)
        {
            Collection<AccountRegion> writeLocations = new Collection<AccountRegion>()
                {
                    { new AccountRegion() { Name = "location1", Endpoint = ClientRetryPolicyTests.Location1Endpoint.ToString() } },
                    { new AccountRegion() { Name = "location2", Endpoint = ClientRetryPolicyTests.Location2Endpoint.ToString() } },
                };

            if (!useMultipleWriteLocations
                && enforceSingleMasterSingleWriteLocation)
            {
                // Some pre-existing tests depend on the account having multiple write locations even on single master setup
                // Newer tests can correctly define a single master account (single write region) without breaking existing tests
                writeLocations = new Collection<AccountRegion>()
                {
                    { new AccountRegion() { Name = "location1", Endpoint = ClientRetryPolicyTests.Location1Endpoint.ToString() } }
                };
            }

            AccountProperties databaseAccount = new AccountProperties()
            {
                EnableMultipleWriteLocations = useMultipleWriteLocations,
                ReadLocationsInternal = new Collection<AccountRegion>()
                {
                    { new AccountRegion() { Name = "location1", Endpoint = ClientRetryPolicyTests.Location1Endpoint.ToString() } },
                    { new AccountRegion() { Name = "location2", Endpoint = ClientRetryPolicyTests.Location2Endpoint.ToString() } },
                },
                WriteLocationsInternal = writeLocations
            };

            return databaseAccount;
        }

        private GlobalEndpointManager Initialize(
            bool useMultipleWriteLocations,
            bool enableEndpointDiscovery,
            bool isPreferredLocationsListEmpty,
            bool enforceSingleMasterSingleWriteLocation = false, // Some tests depend on the Initialize to create an account with multiple write locations, even when not multi master
            ReadOnlyCollection<string> preferedRegionListOverride = null,
            bool enablePartitionLevelFailover = false,
            bool multimasterMetadataWriteRetryTest = false)
        {
            this.databaseAccount = ClientRetryPolicyTests.CreateDatabaseAccount(
                useMultipleWriteLocations,
                enforceSingleMasterSingleWriteLocation);

            if (isPreferredLocationsListEmpty)
            {
                this.preferredLocations = new List<string>().AsReadOnly();
            }
            else
            {
                // Allow for override at the test method level if needed
                this.preferredLocations = preferedRegionListOverride != null ? preferedRegionListOverride : new List<string>()
                {
                    "location1",
                    "location2"
                }.AsReadOnly();
            }

            if (!multimasterMetadataWriteRetryTest)
            {
                this.mockedClient = new Mock<IDocumentClientInternal>();
                mockedClient.Setup(owner => owner.ServiceEndpoint).Returns(ClientRetryPolicyTests.Location1Endpoint);
                mockedClient.Setup(owner => owner.GetDatabaseAccountInternalAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>())).ReturnsAsync(this.databaseAccount);
            }
            else
            {
                this.mockedClient = new Mock<IDocumentClientInternal>();
                mockedClient.Setup(owner => owner.ServiceEndpoint).Returns(ClientRetryPolicyTests.Location2Endpoint);
                mockedClient.Setup(owner => owner.GetDatabaseAccountInternalAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>())).ReturnsAsync(this.databaseAccount);
            }

            ConnectionPolicy connectionPolicy = new ConnectionPolicy()
            {
                EnableEndpointDiscovery = enableEndpointDiscovery,
                UseMultipleWriteLocations = useMultipleWriteLocations,
            };

            foreach (string preferredLocation in this.preferredLocations)
            {
                connectionPolicy.PreferredLocations.Add(preferredLocation);
            }

            GlobalEndpointManager endpointManager = new GlobalEndpointManager(this.mockedClient.Object, connectionPolicy);
            endpointManager.InitializeAccountPropertiesAndStartBackgroundRefresh(this.databaseAccount);

            if (enablePartitionLevelFailover)
            {
                this.partitionKeyRangeLocationCache = new GlobalPartitionEndpointManagerCore(endpointManager);
            }
            else
            {
                this.partitionKeyRangeLocationCache = GlobalPartitionEndpointManagerNoOp.Instance;
            }

            return endpointManager;
        }

        private DocumentServiceRequest CreateRequest(bool isReadRequest, bool isMasterResourceType)
        {
            if (isReadRequest)
            {
                return DocumentServiceRequest.Create(OperationType.Read, isMasterResourceType ? ResourceType.Database : ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);
            }
            else
            {
                return DocumentServiceRequest.Create(OperationType.Create, isMasterResourceType ? ResourceType.Database : ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);
            }
        }

        private MockDocumentClientContext InitializeMockedDocumentClient(
            bool useMultipleWriteLocations,
            bool isPreferredLocationsListEmpty)
        {
            AccountProperties databaseAccount = new AccountProperties()
            {
                EnableMultipleWriteLocations = useMultipleWriteLocations,
                ReadLocationsInternal = new Collection<AccountRegion>()
                {
                    { new AccountRegion() { Name = "location1", Endpoint = new Uri("https://location1.documents.azure.com").ToString() } },
                    { new AccountRegion() { Name = "location2", Endpoint = new Uri("https://location2.documents.azure.com").ToString() } },
                    { new AccountRegion() { Name = "location3", Endpoint = new Uri("https://location3.documents.azure.com").ToString() } },
                },
                WriteLocationsInternal = new Collection<AccountRegion>()
                {
                    { new AccountRegion() { Name = "location1", Endpoint = new Uri("https://location1.documents.azure.com").ToString() } },
                    { new AccountRegion() { Name = "location2", Endpoint = new Uri("https://location2.documents.azure.com").ToString() } },
                    { new AccountRegion() { Name = "location3", Endpoint = new Uri("https://location3.documents.azure.com").ToString() } },
                }
            };

            MockDocumentClientContext mockDocumentClientContext = new MockDocumentClientContext();
            mockDocumentClientContext.DatabaseAccount = databaseAccount;

            mockDocumentClientContext.PreferredLocations = isPreferredLocationsListEmpty ? new List<string>().AsReadOnly() : new List<string>()
            {
                "location1",
                "location3"
            }.AsReadOnly();

            mockDocumentClientContext.LocationCache = new LocationCache(
                mockDocumentClientContext.PreferredLocations,
                new Uri("https://default.documents.azure.com"),
                true,
                10,
                useMultipleWriteLocations);

            mockDocumentClientContext.LocationCache.OnDatabaseAccountRead(mockDocumentClientContext.DatabaseAccount);

            Mock<IDocumentClientInternal> mockedClient = new Mock<IDocumentClientInternal>();
            mockedClient.Setup(owner => owner.ServiceEndpoint).Returns(new Uri("https://default.documents.azure.com"));
            mockedClient.Setup(owner => owner.GetDatabaseAccountInternalAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>())).ReturnsAsync(mockDocumentClientContext.DatabaseAccount);

            ConnectionPolicy connectionPolicy = new ConnectionPolicy()
            {
                UseMultipleWriteLocations = useMultipleWriteLocations,
            };

            foreach (string preferredLocation in mockDocumentClientContext.PreferredLocations)
            {
                connectionPolicy.PreferredLocations.Add(preferredLocation);
            }

            mockDocumentClientContext.DocumentClientInternal = mockedClient.Object;
            mockDocumentClientContext.GlobalEndpointManager = new GlobalEndpointManager(mockDocumentClientContext.DocumentClientInternal, connectionPolicy);
            return mockDocumentClientContext;
        }
        private class MockDocumentClientContext : IDisposable
        {
            public IDocumentClientInternal DocumentClientInternal { get; set; }
            public GlobalEndpointManager GlobalEndpointManager { get; set; }
            public LocationCache LocationCache { get; set; }
            public ReadOnlyCollection<string> PreferredLocations { get; set; }
            public AccountProperties DatabaseAccount { get; set; }

            public void Dispose()
            {
                this.GlobalEndpointManager.Dispose();
            }
        }

        private class MockAddressResolver : IAddressResolverExtension
        {
            private List<AddressInformation> oldAddressInformations;
            private List<AddressInformation> newAddressInformations;

            public int NumberOfRefreshes { get; set; }

            public MockAddressResolver(List<string> oldPhysicalUris, List<string> newPhysicalUris)
            {
                this.NumberOfRefreshes = 0;
                this.oldAddressInformations = new List<AddressInformation>();

                for (int i = 0; i < oldPhysicalUris.Count; i++)
                {
                    this.oldAddressInformations.Add(new AddressInformation(
                        isPrimary: i == 0,
                        isPublic: true,
                        physicalUri: oldPhysicalUris[i],
                        protocol: Protocol.Tcp));
                }

                this.newAddressInformations = new List<AddressInformation>();
                for (int i = 0; i < newPhysicalUris.Count; i++)
                {
                    this.newAddressInformations.Add(new AddressInformation(
                        isPrimary: i == 0,
                        isPublic: true,
                        physicalUri: newPhysicalUris[i],
                        protocol: Protocol.Tcp));
                }
            }

            public Task<PartitionAddressInformation> ResolveAsync(DocumentServiceRequest request, bool forceRefreshPartitionAddresses, CancellationToken cancellationToken)
            {
                List<AddressInformation> addressInformations = new List<AddressInformation>();
                request.RequestContext.ResolvedPartitionKeyRange = new PartitionKeyRange() { Id = "0" };
                if (forceRefreshPartitionAddresses)
                {
                    this.NumberOfRefreshes++;
                    return Task.FromResult<PartitionAddressInformation>(new PartitionAddressInformation(this.newAddressInformations.ToArray()));
                }

                return Task.FromResult<PartitionAddressInformation>(new PartitionAddressInformation(this.oldAddressInformations.ToArray()));
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
                throw new NotImplementedException();
            }

            public Task UpdateAsync(Documents.Rntbd.ServerKey serverKey, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public void SetOpenConnectionsHandler(
                IOpenConnectionsHandler openConnectionHandler)
            {
                throw new NotImplementedException();
            }
        }

        private class MockTransportClient : TransportClient
        {
            private Dictionary<Uri, StoreResponse> uriToStoreResponseMap;
            private Dictionary<Uri, Exception> uriToExceptionMap;

            public MockTransportClient(
                Dictionary<Uri, StoreResponse> uriToStoreResponseMap,
                Dictionary<Uri, Exception> uriToExceptionMap)
            {
                this.uriToStoreResponseMap = uriToStoreResponseMap;
                this.uriToExceptionMap = uriToExceptionMap;
            }

            internal override Task<StoreResponse> InvokeStoreAsync(Uri physicalAddress, ResourceOperation resourceOperation, DocumentServiceRequest request)
            {
                if (this.uriToStoreResponseMap != null && this.uriToStoreResponseMap.ContainsKey(physicalAddress))
                {
                    return Task.FromResult<StoreResponse>(this.uriToStoreResponseMap[physicalAddress]);
                }

                if (this.uriToExceptionMap != null && this.uriToExceptionMap.ContainsKey(physicalAddress))
                {
                    throw this.uriToExceptionMap[physicalAddress];
                }

                throw new InvalidOperationException();
            }
        }

        private class MockServiceConfigurationReader : IServiceConfigurationReader
        {

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

            public ConsistencyLevel DefaultConsistencyLevel
            {
                get { return ConsistencyLevel.BoundedStaleness; }
            }

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
