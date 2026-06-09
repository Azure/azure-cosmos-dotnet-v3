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
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Cosmos.Common;
    using System.Net.Http;
    using System.Reflection;
    using System.Collections.Concurrent;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Cosmos.Tracing;

    /// <summary>
    /// Tests for <see cref="ClientRetryPolicy"/>
    /// </summary>
    [TestClass]
    public sealed class ClientRetryPolicyTests
    {
        private static Uri Location1Endpoint = new Uri("https://location1.documents.azure.com");
        private static Uri Location2Endpoint = new Uri("https://location2.documents.azure.com");

        private const string HubRegionHeader = "x-ms-cosmos-hub-region-processing-only";
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
        /// Tests to see if different 503 substatus and other similar status codes are handeled correctly
        /// </summary>
        /// <param name="testCode">The substatus code being Tested.</param>
        [DataRow((int)StatusCodes.ServiceUnavailable, (int)SubStatusCodes.Unknown, "ServiceUnavailable")]
        [DataRow((int)StatusCodes.ServiceUnavailable, (int)SubStatusCodes.TransportGenerated503, "ServiceUnavailable")]
        [DataRow((int)StatusCodes.InternalServerError, (int)SubStatusCodes.Unknown, "InternalServerError")]
        [DataRow((int)StatusCodes.Gone, (int)SubStatusCodes.LeaseNotFound, "LeaseNotFound")]
        [DataRow((int)StatusCodes.Forbidden, (int)SubStatusCodes.DatabaseAccountNotFound, "DatabaseAccountNotFound")]
        [DataTestMethod]
        public void Http503LikeSubStatusHandelingTests(int statusCode, int SubStatusCode, string message)
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

            HttpStatusCode serviceUnavailable = (HttpStatusCode)statusCode;

            DocumentClientException documentClientException = new DocumentClientException(
               message: message,
               innerException: serviceUnavailableException,
               responseHeaders: nameValueCollection.Object,
               statusCode: serviceUnavailable,
               substatusCode: (SubStatusCodes)SubStatusCode,
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
                isThinClientEnabled: false);

            CancellationToken cancellationToken = new ();
            HttpRequestException httpRequestException = new (message: "Connecting to endpoint has failed.");

            GlobalPartitionEndpointManagerCore.PartitionKeyRangeFailoverInfo partitionKeyRangeFailoverInfo = ClientRetryPolicyTests.GetPartitionKeyRangeFailoverInfoUsingReflection(
                this.partitionKeyRangeLocationCache,
                request.RequestContext.ResolvedPartitionKeyRange,
                isReadOnlyOrMultiMasterWriteRequest: false);

            // Validate that the partition key range failover info is not present before the http request exception was captured in the retry policy.
            Assert.IsNull(partitionKeyRangeFailoverInfo);

            retryPolicy.OnBeforeSendRequest(request);
            Task<ShouldRetryResult> retryStatus = retryPolicy.ShouldRetryAsync(httpRequestException, cancellationToken);

            Assert.IsTrue(retryStatus.Result.ShouldRetry);

            partitionKeyRangeFailoverInfo = ClientRetryPolicyTests.GetPartitionKeyRangeFailoverInfoUsingReflection(
                this.partitionKeyRangeLocationCache,
                request.RequestContext.ResolvedPartitionKeyRange,
                isReadOnlyOrMultiMasterWriteRequest: false);

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

        /// <summary>
        /// Test to validate that when an OperationCanceledException is thrown during the retry attempt, for a single master write account with PPAF enabled,
        /// a partition level failover is applied and the subsequent requests will be retried on the next region for the faulty partition.
        /// </summary>
        [TestMethod]
        [DataRow(true, true, DisplayName = "Read Request - Case when partition level failover is enabled.")]
        [DataRow(false, true, DisplayName = "Write Request - Case when partition level failover is enabled.")]
        [DataRow(true, false, DisplayName = "Read Request - Case when partition level failover is disabled.")]
        [DataRow(false, false, DisplayName = "Write Request - Case when partition level failover is disabled.")]
        public void CosmosOperationCancelledExceptionHandelingTests(
            bool isReadOnlyRequest,
            bool enablePartitionLevelFailover)
        {
            int requestThreshold = isReadOnlyRequest ? 10 : 5;
            const bool enableEndpointDiscovery = true;
            const string suffix = "-FF-FF-FF-FF-FF-FF-FF-FF-FF-FF-FF-FF-FF-FF-FF";

            //Creates a sample write request
            DocumentServiceRequest request = this.CreateRequest(isReadOnlyRequest, false);
            request.RequestContext.ResolvedPartitionKeyRange = new PartitionKeyRange() { Id = "0", MinInclusive = "3F" + suffix, MaxExclusive = "5F" + suffix };

            //Create GlobalEndpointManager
            using GlobalEndpointManager endpointManager = this.Initialize(
               useMultipleWriteLocations: false,
               enableEndpointDiscovery: enableEndpointDiscovery,
               isPreferredLocationsListEmpty: false,
               enablePartitionLevelFailover: enablePartitionLevelFailover);

            // Capture the read locations.
            ReadOnlyCollection<Uri> readLocations = endpointManager.ReadEndpoints;

            //Create Retry Policy
            ClientRetryPolicy retryPolicy = new(
                globalEndpointManager: endpointManager,
                partitionKeyRangeLocationCache: this.partitionKeyRangeLocationCache,
                retryOptions: new RetryOptions(),
                enableEndpointDiscovery: enableEndpointDiscovery,
                isThinClientEnabled: false);

            CancellationToken cancellationToken = new();
            OperationCanceledException operationCancelledException = new(message: "Operation was cancelled due to cancellation token expiry.");

            GlobalPartitionEndpointManagerCore.PartitionKeyRangeFailoverInfo partitionKeyRangeFailoverInfo = ClientRetryPolicyTests.GetPartitionKeyRangeFailoverInfoUsingReflection(
                this.partitionKeyRangeLocationCache,
                request.RequestContext.ResolvedPartitionKeyRange,
                isReadOnlyOrMultiMasterWriteRequest: isReadOnlyRequest);

            // Validate that the partition key range failover info is not present before the http request exception was captured in the retry policy.
            Assert.IsNull(partitionKeyRangeFailoverInfo);

            Task<ShouldRetryResult> retryStatus;

            // With cancellation token expiry, the retry policy should not failover the offending partition
            // until the write threshold is met.
            for (int i=0; i< requestThreshold; i++)
            {
                retryPolicy.OnBeforeSendRequest(request);
                retryStatus = retryPolicy.ShouldRetryAsync(operationCancelledException, cancellationToken);
            }

            retryStatus = retryPolicy.ShouldRetryAsync(operationCancelledException, cancellationToken);
            Assert.IsFalse(retryStatus.Result.ShouldRetry);

            partitionKeyRangeFailoverInfo = ClientRetryPolicyTests.GetPartitionKeyRangeFailoverInfoUsingReflection(
                this.partitionKeyRangeLocationCache,
                request.RequestContext.ResolvedPartitionKeyRange,
                isReadOnlyOrMultiMasterWriteRequest: isReadOnlyRequest);

            if (enablePartitionLevelFailover)
            {
                // Validate that the partition key range failover info to the next account region is present after the http request exception was captured in the retry policy.
                Assert.IsNotNull(partitionKeyRangeFailoverInfo);
                Assert.AreEqual(partitionKeyRangeFailoverInfo.Current, readLocations[1]);
            }
            else
            {
                Assert.IsNull(partitionKeyRangeFailoverInfo);
            }
        }

        /// <summary>
        /// Regression for PR #5913 / ICM 21000001041367: the OperationCanceledException
        /// raised by the new inter-attempt Task.Delay(ct) inside
        /// CosmosHttpClientCore.ProcessMessageAsync must NOT be classified as a
        /// region-down signal by ClientRetryPolicy. Pure caller-cancellation must not
        /// invoke <see cref="GlobalEndpointManager.MarkEndpointUnavailableForRead"/>
        /// (nor the write variant) — those are reserved for genuine endpoint failures
        /// (HttpRequestException, 503, etc.).
        ///
        /// Uses a CallBase Moq spy over <see cref="GlobalEndpointManager"/> so the
        /// real ClientRetryPolicy flow runs end-to-end and the assertion is a true
        /// behavioral pin: any future change that wires OCE handling into the
        /// endpoint-marking path will fail this test.
        /// </summary>
        [TestMethod]
        [DataRow(true, DisplayName = "PPAF enabled")]
        [DataRow(false, DisplayName = "PPAF disabled (customer scenario)")]
        public async Task OperationCanceledException_DoesNotMarkEndpointUnavailable(
            bool enablePartitionLevelFailover)
        {
            const bool enableEndpointDiscovery = true;

            this.databaseAccount = ClientRetryPolicyTests.CreateDatabaseAccount(
                useMultipleWriteLocations: false,
                enforceSingleMasterSingleWriteLocation: false);
            this.preferredLocations = new List<string>() { "location1", "location2", "location3", "location4" }.AsReadOnly();

            Mock<IDocumentClientInternal> mockedDocClient = new();
            mockedDocClient.Setup(c => c.ServiceEndpoint).Returns(ClientRetryPolicyTests.Location1Endpoint);
            mockedDocClient.Setup(c => c.GetDatabaseAccountInternalAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(this.databaseAccount);
            this.mockedClient = mockedDocClient;

            ConnectionPolicy connectionPolicy = new()
            {
                EnableEndpointDiscovery = enableEndpointDiscovery,
                UseMultipleWriteLocations = false,
            };
            foreach (string preferredLocation in this.preferredLocations)
            {
                connectionPolicy.PreferredLocations.Add(preferredLocation);
            }

            // CallBase=true: virtual methods (MarkEndpointUnavailableForRead, ResolveServiceEndpoint, ...)
            // execute the real implementation, so the test exercises the real routing flow, but Moq
            // also records invocations so we can Verify Times.Never.
            Mock<GlobalEndpointManager> endpointManagerSpy = new(mockedDocClient.Object, connectionPolicy, false)
            {
                CallBase = true,
            };
            using GlobalEndpointManager endpointManager = endpointManagerSpy.Object;
            endpointManager.InitializeAccountPropertiesAndStartBackgroundRefresh(this.databaseAccount);

            this.partitionKeyRangeLocationCache = enablePartitionLevelFailover
                ? new GlobalPartitionEndpointManagerCore(
                    globalEndpointManager: endpointManager,
                    isPartitionLevelFailoverEnabled: true,
                    isPartitionLevelCircuitBreakerEnabled: true)
                : GlobalPartitionEndpointManagerNoOp.Instance;

            ClientRetryPolicy retryPolicy = new(
                globalEndpointManager: endpointManager,
                partitionKeyRangeLocationCache: this.partitionKeyRangeLocationCache,
                retryOptions: new RetryOptions(),
                enableEndpointDiscovery: enableEndpointDiscovery,
                isThinClientEnabled: false);

            const string suffix = "-FF-FF-FF-FF-FF-FF-FF-FF-FF-FF-FF-FF-FF-FF-FF";
            DocumentServiceRequest request = this.CreateRequest(isReadRequest: true, isMasterResourceType: false);
            request.RequestContext.ResolvedPartitionKeyRange = new PartitionKeyRange() { Id = "0", MinInclusive = "3F" + suffix, MaxExclusive = "5F" + suffix };

            using CancellationTokenSource cts = new();
            cts.Cancel();
            OperationCanceledException oce = new(message: "Caller cancellation token expired during HTTP retry delay.", cts.Token);

            // Drive enough OCE attempts to exceed the read-request failover threshold (10).
            // Without the contract this test pins, a future regression that translates OCE
            // into an endpoint-failure signal would call MarkEndpointUnavailableForRead
            // somewhere in this loop.
            for (int i = 0; i < 12; i++)
            {
                retryPolicy.OnBeforeSendRequest(request);
                await retryPolicy.ShouldRetryAsync(oce, cts.Token);
            }

            endpointManagerSpy.Verify(
                gep => gep.MarkEndpointUnavailableForRead(It.IsAny<Uri>()),
                Times.Never,
                "Pure caller-cancellation (OperationCanceledException) must not mark the read endpoint unavailable.");
            endpointManagerSpy.Verify(
                gep => gep.MarkEndpointUnavailableForWrite(It.IsAny<Uri>()),
                Times.Never,
                "Pure caller-cancellation (OperationCanceledException) must not mark the write endpoint unavailable.");
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

        /// <summary>
        /// Test to validate that hub region header is added on 404/1002 for single master accounts only,
        /// starting from the second retry (after first retry also fails). For multi-master accounts, 
        /// the header should NOT be added.
        /// </summary>
        [TestMethod]
        [DataRow(true, true, DisplayName = "Read request on single master - Hub region header added after first retry fails")]
        [DataRow(false, true, DisplayName = "Write request on single master - Hub region header added after first retry fails")]
        [DataRow(true, false, DisplayName = "Read request on multi-master - Hub region header NOT added")]
        [DataRow(false, false, DisplayName = "Write request on multi-master - Hub region header NOT added")]
        public async Task ClientRetryPolicy_HubRegionHeader_AddedOn404_1002_BasedOnAccountType(bool isReadRequest, bool isSingleMaster)
        {
            // Arrange
            const bool enableEndpointDiscovery = true;

            using GlobalEndpointManager endpointManager = this.Initialize(
                useMultipleWriteLocations: !isSingleMaster,
                enableEndpointDiscovery: enableEndpointDiscovery,
                isPreferredLocationsListEmpty: false,
                enforceSingleMasterSingleWriteLocation: isSingleMaster);

            ClientRetryPolicy retryPolicy = new ClientRetryPolicy(
                endpointManager,
                this.partitionKeyRangeLocationCache,
                new RetryOptions(),
                enableEndpointDiscovery,
                isThinClientEnabled: false);

            DocumentServiceRequest request = this.CreateRequest(isReadRequest: isReadRequest, isMasterResourceType: false);

            // First attempt - header should not exist
            retryPolicy.OnBeforeSendRequest(request);
            Assert.IsNull(request.Headers.GetValues(HubRegionHeader), "Header should not exist on initial request before any 404/1002 error.");

            // Simulate first 404/1002 error
            DocumentClientException sessionNotAvailableException = new DocumentClientException(
                message: "Simulated 404/1002 ReadSessionNotAvailable",
                innerException: null,
                statusCode: HttpStatusCode.NotFound,
                substatusCode: SubStatusCodes.ReadSessionNotAvailable,
                requestUri: request.RequestContext.LocationEndpointToRoute,
                responseHeaders: new DictionaryNameValueCollection());

            ShouldRetryResult shouldRetry = await retryPolicy.ShouldRetryAsync(sessionNotAvailableException, CancellationToken.None);
            Assert.IsTrue(shouldRetry.ShouldRetry, "Should retry on 404/1002.");

            // First retry attempt - header should NOT be present yet
            retryPolicy.OnBeforeSendRequest(request);
            string[] headerValues = request.Headers.GetValues(HubRegionHeader);
            Assert.IsNull(headerValues, "Header should NOT be present on first retry attempt (before it fails).");

            // Simulate first retry also failing with 404/1002
            DocumentClientException sessionNotAvailableException2 = new DocumentClientException(
                message: "Simulated 404/1002 ReadSessionNotAvailable on first retry",
                innerException: null,
                statusCode: HttpStatusCode.NotFound,
                substatusCode: SubStatusCodes.ReadSessionNotAvailable,
                requestUri: request.RequestContext.LocationEndpointToRoute,
                responseHeaders: new DictionaryNameValueCollection());

            shouldRetry = await retryPolicy.ShouldRetryAsync(sessionNotAvailableException2, CancellationToken.None);

            if (isSingleMaster)
            {
                // For single master, after the second 404/1002, the hub region header flag has been set
                // and the retry policy should allow one more retry so the request can be sent with the header.
                Assert.IsTrue(shouldRetry.ShouldRetry, "Single master should retry once more after second 404/1002 so the hub region header is sent.");

                // Verify the header is now present on the retry
                retryPolicy.OnBeforeSendRequest(request);
                headerValues = request.Headers.GetValues(HubRegionHeader);
                Assert.IsNotNull(headerValues, "Hub region header should be present on retry after second 404/1002.");
                Assert.AreEqual(1, headerValues.Length, "Header should have exactly one value.");
                Assert.AreEqual(bool.TrueString, headerValues[0], "Header value should be 'True'.");
            }
            else
            {
                // For multi-master: Verify header is NOT added even on subsequent retries
                for (int retryAttempt = 2; retryAttempt <= 3; retryAttempt++)
                {
                    if (shouldRetry.ShouldRetry)
                    {
                        retryPolicy.OnBeforeSendRequest(request);
                        headerValues = request.Headers.GetValues(HubRegionHeader);
                        Assert.IsNull(headerValues, $"Header should NOT be present on retry attempt {retryAttempt} for multi-master account.");

                        // Simulate another 404/1002 or 503 to continue retry loop
                        DocumentClientException nextException = new DocumentClientException(
                            message: $"Simulated error on retry {retryAttempt}",
                            innerException: null,
                            statusCode: retryAttempt % 2 == 0 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.NotFound,
                            substatusCode: retryAttempt % 2 == 0 ? SubStatusCodes.Unknown : SubStatusCodes.ReadSessionNotAvailable,
                            requestUri: request.RequestContext.LocationEndpointToRoute,
                            responseHeaders: new DictionaryNameValueCollection());

                        shouldRetry = await retryPolicy.ShouldRetryAsync(nextException, CancellationToken.None);
                    }
                }
            }
        }

        /// <summary>
        /// End-to-end test for the hub region discovery flow on a single-master account (Direct mode):
        /// 1st request → 404/1002 (no hub header) → retry to write region
        /// 2nd request → 404/1002 (no hub header) → hub header flag set, retry
        /// 3rd request → assert hub header present → 403/3 from non-hub → retry
        /// 4th request → assert hub header present → 200 success
        /// </summary>
        [TestMethod]
        public async Task ClientRetryPolicy_HubRegionDiscovery_EndToEnd_DirectMode()
        {
            // Arrange: single-master, endpoint discovery enabled
            const bool enableEndpointDiscovery = true;

            using GlobalEndpointManager endpointManager = this.Initialize(
                useMultipleWriteLocations: false,
                enableEndpointDiscovery: enableEndpointDiscovery,
                isPreferredLocationsListEmpty: false,
                enforceSingleMasterSingleWriteLocation: true);

            ClientRetryPolicy retryPolicy = new ClientRetryPolicy(
                endpointManager,
                this.partitionKeyRangeLocationCache,
                new RetryOptions(),
                enableEndpointDiscovery,
                isThinClientEnabled: false);

            DocumentServiceRequest request = this.CreateRequest(isReadRequest: true, isMasterResourceType: false);

            // ---- Step 1: First request attempt ----
            retryPolicy.OnBeforeSendRequest(request);
            Assert.IsNull(
                request.Headers.GetValues(HubRegionHeader),
                "Hub region header should NOT be present on the initial request.");

            // Simulate 1st 404/1002
            ShouldRetryResult shouldRetry = await retryPolicy.ShouldRetryAsync(
                new DocumentClientException(
                    message: "1st 404/1002",
                    innerException: null,
                    statusCode: HttpStatusCode.NotFound,
                    substatusCode: SubStatusCodes.ReadSessionNotAvailable,
                    requestUri: request.RequestContext.LocationEndpointToRoute,
                    responseHeaders: new DictionaryNameValueCollection()),
                CancellationToken.None);

            Assert.IsTrue(shouldRetry.ShouldRetry, "Should retry after first 404/1002.");

            // ---- Step 2: Retry routed to write region ----
            retryPolicy.OnBeforeSendRequest(request);
            Assert.IsNull(
                request.Headers.GetValues(HubRegionHeader),
                "Hub region header should NOT be present on the first retry (routed to write region).");

            // Simulate 2nd 404/1002
            shouldRetry = await retryPolicy.ShouldRetryAsync(
                new DocumentClientException(
                    message: "2nd 404/1002",
                    innerException: null,
                    statusCode: HttpStatusCode.NotFound,
                    substatusCode: SubStatusCodes.ReadSessionNotAvailable,
                    requestUri: request.RequestContext.LocationEndpointToRoute,
                    responseHeaders: new DictionaryNameValueCollection()),
                CancellationToken.None);

            Assert.IsTrue(shouldRetry.ShouldRetry, "Should retry after second 404/1002 (hub header flag now set).");

            // ---- Step 3: Retry with hub region header → gets 403/3 ----
            retryPolicy.OnBeforeSendRequest(request);
            string[] headerValues = request.Headers.GetValues(HubRegionHeader);
            Assert.IsNotNull(headerValues, "Hub region header MUST be present on the retry after two consecutive 404/1002 errors.");
            Assert.AreEqual(1, headerValues.Length, "Hub region header should have exactly one value.");
            Assert.AreEqual(bool.TrueString, headerValues[0], "Hub region header value should be 'True'.");

            // Simulate 403/3 (WriteForbidden) — this happens when the request reaches a non-hub region
            shouldRetry = await retryPolicy.ShouldRetryAsync(
                new DocumentClientException(
                    message: "403/3 WriteForbidden from non-hub region",
                    innerException: null,
                    statusCode: HttpStatusCode.Forbidden,
                    substatusCode: SubStatusCodes.WriteForbidden,
                    requestUri: request.RequestContext.LocationEndpointToRoute,
                    responseHeaders: new DictionaryNameValueCollection()),
                CancellationToken.None);

            Assert.IsTrue(shouldRetry.ShouldRetry, "Should retry after 403/3 to continue hub region discovery.");

            // ---- Step 4: Retry still carries hub header → 200 success ----
            retryPolicy.OnBeforeSendRequest(request);
            headerValues = request.Headers.GetValues(HubRegionHeader);
            Assert.IsNotNull(headerValues, "Hub region header MUST persist through 403/3 retries.");
            Assert.AreEqual(bool.TrueString, headerValues[0], "Hub region header value should remain 'True'.");
        }

        /// <summary>
        /// Verifies that once the hub region header is set (after two consecutive 404/1002),
        /// it persists through subsequent retries triggered by other retriable errors
        /// (503 ServiceUnavailable, 408 RequestTimeout) and that the normal preferred-region
        /// cycling continues with the header attached.
        /// </summary>
        [TestMethod]
        public async Task ClientRetryPolicy_HubRegionHeader_PersistsThroughRetriableErrors()
        {
            const bool enableEndpointDiscovery = true;

            using GlobalEndpointManager endpointManager = this.Initialize(
                useMultipleWriteLocations: false,
                enableEndpointDiscovery: enableEndpointDiscovery,
                isPreferredLocationsListEmpty: false,
                enforceSingleMasterSingleWriteLocation: true);

            ClientRetryPolicy retryPolicy = new ClientRetryPolicy(
                endpointManager,
                this.partitionKeyRangeLocationCache,
                new RetryOptions(),
                enableEndpointDiscovery,
                isThinClientEnabled: false);

            DocumentServiceRequest request = this.CreateRequest(isReadRequest: true, isMasterResourceType: false);

            // ---- 1st 404/1002 ----
            retryPolicy.OnBeforeSendRequest(request);
            ShouldRetryResult shouldRetry = await retryPolicy.ShouldRetryAsync(
                new DocumentClientException(
                    message: "1st 404/1002",
                    innerException: null,
                    statusCode: HttpStatusCode.NotFound,
                    substatusCode: SubStatusCodes.ReadSessionNotAvailable,
                    requestUri: request.RequestContext.LocationEndpointToRoute,
                    responseHeaders: new DictionaryNameValueCollection()),
                CancellationToken.None);
            Assert.IsTrue(shouldRetry.ShouldRetry);

            // ---- 2nd 404/1002 → hub header flag gets set ----
            retryPolicy.OnBeforeSendRequest(request);
            shouldRetry = await retryPolicy.ShouldRetryAsync(
                new DocumentClientException(
                    message: "2nd 404/1002",
                    innerException: null,
                    statusCode: HttpStatusCode.NotFound,
                    substatusCode: SubStatusCodes.ReadSessionNotAvailable,
                    requestUri: request.RequestContext.LocationEndpointToRoute,
                    responseHeaders: new DictionaryNameValueCollection()),
                CancellationToken.None);
            Assert.IsTrue(shouldRetry.ShouldRetry, "Should retry so the hub header can be sent.");

            // ---- 3rd request: hub header should be present ----
            retryPolicy.OnBeforeSendRequest(request);
            string[] headerValues = request.Headers.GetValues(HubRegionHeader);
            Assert.IsNotNull(headerValues, "Hub region header must be present after two 404/1002 failures.");
            Assert.AreEqual(bool.TrueString, headerValues[0]);

            // ---- Now simulate a retriable 503 ServiceUnavailable error ----
            shouldRetry = await retryPolicy.ShouldRetryAsync(
                new DocumentClientException(
                    message: "503 ServiceUnavailable after hub header set",
                    innerException: null,
                    statusCode: HttpStatusCode.ServiceUnavailable,
                    substatusCode: SubStatusCodes.Unknown,
                    requestUri: request.RequestContext.LocationEndpointToRoute,
                    responseHeaders: new DictionaryNameValueCollection()),
                CancellationToken.None);
            Assert.IsTrue(shouldRetry.ShouldRetry, "Should retry on 503 ServiceUnavailable.");

            // ---- 4th request: hub header must STILL be present after 503 retry ----
            retryPolicy.OnBeforeSendRequest(request);
            headerValues = request.Headers.GetValues(HubRegionHeader);
            Assert.IsNotNull(headerValues, "Hub region header must persist through 503 retry.");
            Assert.AreEqual(bool.TrueString, headerValues[0], "Hub region header value should remain 'True'.");
        }

        /// <summary>
        /// When a hedge request's Properties contains a CrossRegionAvailabilityContext with
        /// ShouldAddHubRegionProcessingOnlyHeader = true, OnBeforeSendRequest should set
        /// the hub region header immediately (simulates hedge picking up primary's flag).
        /// </summary>
        [TestMethod]
        public void ClientRetryPolicy_SharedContext_HedgePicksUpHubHeaderFromSharedFlag()
        {
            // Arrange: single-master account
            const bool enableEndpointDiscovery = true;
            using GlobalEndpointManager endpointManager = this.Initialize(
                useMultipleWriteLocations: false,
                enableEndpointDiscovery: enableEndpointDiscovery,
                isPreferredLocationsListEmpty: false,
                enforceSingleMasterSingleWriteLocation: true);

            ClientRetryPolicy retryPolicy = new ClientRetryPolicy(
                endpointManager,
                this.partitionKeyRangeLocationCache,
                new RetryOptions(),
                enableEndpointDiscovery,
                isThinClientEnabled: false);

            DocumentServiceRequest request = this.CreateRequest(isReadRequest: true, isMasterResourceType: false);

            // Simulate the shared context that was injected by CrossRegionHedgingAvailabilityStrategy
            // and has already been flagged by the primary's ClientRetryPolicy after 2x 404/1002.
            CrossRegionAvailabilityContext sharedContext = new CrossRegionAvailabilityContext();
            sharedContext.ShouldAddHubRegionProcessingOnlyHeader = true;

            request.Properties = new Dictionary<string, object>
            {
                { CrossRegionAvailabilityContext.PropertyKey, sharedContext }
            };

            // Act: first call to OnBeforeSendRequest (hedge's very first attempt)
            retryPolicy.OnBeforeSendRequest(request);

            // Assert: hub region header should be set immediately
            string[] headerValues = request.Headers.GetValues(HubRegionHeader);
            Assert.IsNotNull(headerValues, "Hedge request should get hub header on first attempt when shared context flag is true.");
            Assert.AreEqual(bool.TrueString, headerValues[0]);
        }

        /// <summary>
        /// After 2× 404/1002 on a single-master account, the ClientRetryPolicy should
        /// set the shared CrossRegionAvailabilityContext flag to true (propagating to hedges).
        /// </summary>
        [TestMethod]
        public async Task ClientRetryPolicy_SharedContext_FlagSetAfterTwoSessionNotAvailable()
        {
            // Arrange: single-master account
            const bool enableEndpointDiscovery = true;
            using GlobalEndpointManager endpointManager = this.Initialize(
                useMultipleWriteLocations: false,
                enableEndpointDiscovery: enableEndpointDiscovery,
                isPreferredLocationsListEmpty: false,
                enforceSingleMasterSingleWriteLocation: true);

            ClientRetryPolicy retryPolicy = new ClientRetryPolicy(
                endpointManager,
                this.partitionKeyRangeLocationCache,
                new RetryOptions(),
                enableEndpointDiscovery,
                isThinClientEnabled: false);

            DocumentServiceRequest request = this.CreateRequest(isReadRequest: true, isMasterResourceType: false);

            // Simulate the shared context injected by CrossRegionHedgingAvailabilityStrategy
            CrossRegionAvailabilityContext sharedContext = new CrossRegionAvailabilityContext();
            Assert.IsFalse(sharedContext.ShouldAddHubRegionProcessingOnlyHeader, "Flag should start as false.");

            request.Properties = new Dictionary<string, object>
            {
                { CrossRegionAvailabilityContext.PropertyKey, sharedContext }
            };

            // First attempt
            retryPolicy.OnBeforeSendRequest(request);
            Assert.IsFalse(sharedContext.ShouldAddHubRegionProcessingOnlyHeader, "Flag should still be false after first attempt.");

            // Simulate first 404/1002
            DocumentClientException ex1 = new DocumentClientException(
                message: "ReadSessionNotAvailable",
                innerException: null,
                statusCode: HttpStatusCode.NotFound,
                substatusCode: SubStatusCodes.ReadSessionNotAvailable,
                requestUri: request.RequestContext.LocationEndpointToRoute,
                responseHeaders: new DictionaryNameValueCollection());

            ShouldRetryResult result1 = await retryPolicy.ShouldRetryAsync(ex1, CancellationToken.None);
            Assert.IsTrue(result1.ShouldRetry, "Should retry after first 404/1002.");
            Assert.IsFalse(sharedContext.ShouldAddHubRegionProcessingOnlyHeader, "Flag should still be false after first 404/1002.");

            // Second attempt
            retryPolicy.OnBeforeSendRequest(request);

            // Simulate second 404/1002
            DocumentClientException ex2 = new DocumentClientException(
                message: "ReadSessionNotAvailable",
                innerException: null,
                statusCode: HttpStatusCode.NotFound,
                substatusCode: SubStatusCodes.ReadSessionNotAvailable,
                requestUri: request.RequestContext.LocationEndpointToRoute,
                responseHeaders: new DictionaryNameValueCollection());

            ShouldRetryResult result2 = await retryPolicy.ShouldRetryAsync(ex2, CancellationToken.None);
            Assert.IsTrue(result2.ShouldRetry, "Should retry once more after second 404/1002 (hub header retry).");

            // Assert: shared context flag should now be true
            Assert.IsTrue(sharedContext.ShouldAddHubRegionProcessingOnlyHeader,
                "After 2× 404/1002 on single-master, shared context flag must be set to true for hedge propagation.");
        }

        /// <summary>
        /// When CrossRegionAvailabilityContext is null (non-hedging path), the existing
        /// local addHubRegionProcessingOnlyHeader behavior should be preserved unchanged.
        /// </summary>
        [TestMethod]
        public async Task ClientRetryPolicy_NullSharedContext_LocalFlagStillWorks()
        {
            // Arrange: single-master, no shared context (Properties is null)
            const bool enableEndpointDiscovery = true;
            using GlobalEndpointManager endpointManager = this.Initialize(
                useMultipleWriteLocations: false,
                enableEndpointDiscovery: enableEndpointDiscovery,
                isPreferredLocationsListEmpty: false,
                enforceSingleMasterSingleWriteLocation: true);

            ClientRetryPolicy retryPolicy = new ClientRetryPolicy(
                endpointManager,
                this.partitionKeyRangeLocationCache,
                new RetryOptions(),
                enableEndpointDiscovery,
                isThinClientEnabled: false);

            DocumentServiceRequest request = this.CreateRequest(isReadRequest: true, isMasterResourceType: false);
            // Properties is NOT set (non-hedging scenario)

            // First attempt - no header
            retryPolicy.OnBeforeSendRequest(request);
            Assert.IsNull(request.Headers.GetValues(HubRegionHeader), "No header on initial attempt.");

            // Simulate first 404/1002
            DocumentClientException ex1 = new DocumentClientException(
                message: "ReadSessionNotAvailable",
                innerException: null,
                statusCode: HttpStatusCode.NotFound,
                substatusCode: SubStatusCodes.ReadSessionNotAvailable,
                requestUri: request.RequestContext.LocationEndpointToRoute,
                responseHeaders: new DictionaryNameValueCollection());

            await retryPolicy.ShouldRetryAsync(ex1, CancellationToken.None);

            // Second attempt - still no header
            retryPolicy.OnBeforeSendRequest(request);
            Assert.IsNull(request.Headers.GetValues(HubRegionHeader), "No header on first retry (before second 404/1002).");

            // Simulate second 404/1002
            DocumentClientException ex2 = new DocumentClientException(
                message: "ReadSessionNotAvailable",
                innerException: null,
                statusCode: HttpStatusCode.NotFound,
                substatusCode: SubStatusCodes.ReadSessionNotAvailable,
                requestUri: request.RequestContext.LocationEndpointToRoute,
                responseHeaders: new DictionaryNameValueCollection());

            await retryPolicy.ShouldRetryAsync(ex2, CancellationToken.None);

            // Third attempt - header should be set via local flag (not shared context)
            retryPolicy.OnBeforeSendRequest(request);
            string[] headerValues = request.Headers.GetValues(HubRegionHeader);
            Assert.IsNotNull(headerValues, "Hub region header should be set via local flag on non-hedging path.");
            Assert.AreEqual(bool.TrueString, headerValues[0]);
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
                enableReplicaValidation: false);

            // Reducing retry timeout to avoid long-running tests
            replicatedResourceClient.GoneAndRetryWithRetryTimeoutInSecondsOverride = 1;

            this.partitionKeyRangeLocationCache = GlobalPartitionEndpointManagerNoOp.Instance;

            ClientRetryPolicy retryPolicy = new ClientRetryPolicy(mockDocumentClientContext.GlobalEndpointManager, this.partitionKeyRangeLocationCache, new RetryOptions(), enableEndpointDiscovery: true, false);

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

        // ─── DTX (Distributed Transaction) retry tests ───────────────────────────────

        [TestMethod]
        public async Task DtxRequest_408_ShouldRetry()
        {
            const bool enableEndpointDiscovery = true;
            using GlobalEndpointManager endpointManager = this.Initialize(
                useMultipleWriteLocations: false,
                enableEndpointDiscovery: enableEndpointDiscovery,
                isPreferredLocationsListEmpty: false,
                enforceSingleMasterSingleWriteLocation: true);

            ClientRetryPolicy policy = new ClientRetryPolicy(endpointManager, this.partitionKeyRangeLocationCache, new RetryOptions(), enableEndpointDiscovery, false);
            DocumentServiceRequest request = ClientRetryPolicyTests.CreateDtxRequest();
            policy.OnBeforeSendRequest(request);

            ResponseMessage response = new ResponseMessage(HttpStatusCode.RequestTimeout);
            ShouldRetryResult result = await policy.ShouldRetryAsync(response, CancellationToken.None);

            Assert.IsTrue(result.ShouldRetry, "DTX 408 must be retried — idempotency token guarantees safety.");
        }

        [TestMethod]
        public async Task DtxRequest_449_5352_ShouldRetry_WithDefaultRetryInterval()
        {
            const bool enableEndpointDiscovery = true;
            using GlobalEndpointManager endpointManager = this.Initialize(
                useMultipleWriteLocations: false,
                enableEndpointDiscovery: enableEndpointDiscovery,
                isPreferredLocationsListEmpty: false,
                enforceSingleMasterSingleWriteLocation: true);

            ClientRetryPolicy policy = new ClientRetryPolicy(endpointManager, this.partitionKeyRangeLocationCache, new RetryOptions(), enableEndpointDiscovery, false);
            DocumentServiceRequest request = ClientRetryPolicyTests.CreateDtxRequest();
            policy.OnBeforeSendRequest(request);

            ResponseMessage response = new ResponseMessage((HttpStatusCode)StatusCodes.RetryWith);
            response.Headers.SubStatusCodeLiteral = ((int)SubStatusCodes.DtcCoordinatorRaceConflict).ToString();

            ShouldRetryResult result = await policy.ShouldRetryAsync(response, CancellationToken.None);

            Assert.IsTrue(result.ShouldRetry, "DTX 449/5352 coordinator race conflict must be retried.");
            Assert.AreEqual(TimeSpan.FromSeconds(1), result.BackoffTime,
                "Without a Retry-After header, CRP should fall back to the standard retry interval (1s) instead of hammering the coordinator with zero-delay retries.");
        }

        [TestMethod]
        public async Task DtxRequest_449_5352_ShouldRetry_HonorsRetryAfterHeader()
        {
            const bool enableEndpointDiscovery = true;
            TimeSpan serverRetryAfter = TimeSpan.FromMilliseconds(250);
            using GlobalEndpointManager endpointManager = this.Initialize(
                useMultipleWriteLocations: false,
                enableEndpointDiscovery: enableEndpointDiscovery,
                isPreferredLocationsListEmpty: false,
                enforceSingleMasterSingleWriteLocation: true);

            ClientRetryPolicy policy = new ClientRetryPolicy(endpointManager, this.partitionKeyRangeLocationCache, new RetryOptions(), enableEndpointDiscovery, false);
            DocumentServiceRequest request = ClientRetryPolicyTests.CreateDtxRequest();
            policy.OnBeforeSendRequest(request);

            ResponseMessage response = new ResponseMessage((HttpStatusCode)StatusCodes.RetryWith);
            response.Headers.SubStatusCodeLiteral = ((int)SubStatusCodes.DtcCoordinatorRaceConflict).ToString();
            response.Headers.RetryAfterLiteral = ((long)serverRetryAfter.TotalMilliseconds).ToString();

            ShouldRetryResult result = await policy.ShouldRetryAsync(response, CancellationToken.None);

            Assert.IsTrue(result.ShouldRetry, "DTX 449/5352 must be retried.");
            Assert.AreEqual(serverRetryAfter, result.BackoffTime, "Retry delay must honor the server's Retry-After header.");
        }

        [DataTestMethod]
        [DataRow((int)SubStatusCodes.DtcLedgerFailure, DisplayName = "500/5411 LedgerFailure")]
        [DataRow((int)SubStatusCodes.DtcAccountConfigFailure, DisplayName = "500/5412 AccountConfigFailure")]
        [DataRow((int)SubStatusCodes.DtcDispatchFailure, DisplayName = "500/5413 DispatchFailure")]
        public async Task DtxRequest_500_InfraFailure_ShouldRetry(int subStatusCode)
        {
            const bool enableEndpointDiscovery = true;
            using GlobalEndpointManager endpointManager = this.Initialize(
                useMultipleWriteLocations: false,
                enableEndpointDiscovery: enableEndpointDiscovery,
                isPreferredLocationsListEmpty: false,
                enforceSingleMasterSingleWriteLocation: true);

            ClientRetryPolicy policy = new ClientRetryPolicy(endpointManager, this.partitionKeyRangeLocationCache, new RetryOptions(), enableEndpointDiscovery, false);
            DocumentServiceRequest request = ClientRetryPolicyTests.CreateDtxRequest();
            policy.OnBeforeSendRequest(request);

            ResponseMessage response = new ResponseMessage(HttpStatusCode.InternalServerError);
            response.Headers.SubStatusCodeLiteral = subStatusCode.ToString();

            ShouldRetryResult result = await policy.ShouldRetryAsync(response, CancellationToken.None);

            Assert.IsTrue(result.ShouldRetry, $"DTX 500/{subStatusCode} transient infra failure must be retried.");
        }

        [TestMethod]
        public async Task NonDtxWriteRequest_408_ShouldNotRetry()
        {
            const bool enableEndpointDiscovery = true;
            using GlobalEndpointManager endpointManager = this.Initialize(
                useMultipleWriteLocations: false,
                enableEndpointDiscovery: enableEndpointDiscovery,
                isPreferredLocationsListEmpty: false,
                enforceSingleMasterSingleWriteLocation: true);

            ClientRetryPolicy policy = new ClientRetryPolicy(endpointManager, this.partitionKeyRangeLocationCache, new RetryOptions(), enableEndpointDiscovery, false);
            // Non-DTX write (e.g., a point Create)
            DocumentServiceRequest request = this.CreateRequest(isReadRequest: false, isMasterResourceType: false);
            policy.OnBeforeSendRequest(request);

            ResponseMessage response = new ResponseMessage(HttpStatusCode.RequestTimeout);
            ShouldRetryResult result = await policy.ShouldRetryAsync(response, CancellationToken.None);

            Assert.IsFalse(result.ShouldRetry, "Non-DTX 408 must NOT be retried by ClientRetryPolicy (only marks endpoint unavailable).");
        }

        [DataTestMethod]
        [DataRow((int)SubStatusCodes.DtcLedgerFailure, DisplayName = "500/5411 LedgerFailure")]
        [DataRow((int)SubStatusCodes.DtcAccountConfigFailure, DisplayName = "500/5412 AccountConfigFailure")]
        [DataRow((int)SubStatusCodes.DtcDispatchFailure, DisplayName = "500/5413 DispatchFailure")]
        public async Task NonDtxWriteRequest_500_DtcSubStatus_ShouldNotRetry(int subStatusCode)
        {
            const bool enableEndpointDiscovery = true;
            using GlobalEndpointManager endpointManager = this.Initialize(
                useMultipleWriteLocations: false,
                enableEndpointDiscovery: enableEndpointDiscovery,
                isPreferredLocationsListEmpty: false,
                enforceSingleMasterSingleWriteLocation: true);

            ClientRetryPolicy policy = new ClientRetryPolicy(endpointManager, this.partitionKeyRangeLocationCache, new RetryOptions(), enableEndpointDiscovery, false);
            // Non-DTX write — same sub-status codes must NOT trigger a retry.
            DocumentServiceRequest request = this.CreateRequest(isReadRequest: false, isMasterResourceType: false);
            policy.OnBeforeSendRequest(request);

            ResponseMessage response = new ResponseMessage(HttpStatusCode.InternalServerError);
            response.Headers.SubStatusCodeLiteral = subStatusCode.ToString();

            ShouldRetryResult result = await policy.ShouldRetryAsync(response, CancellationToken.None);

            Assert.IsFalse(result.ShouldRetry, $"Non-DTX write 500/{subStatusCode} must NOT be retried — only DTX writes with idempotency tokens are safe.");
        }

        [TestMethod]
        public async Task DtxRequest_ExhaustsRetryBudget_ReturnsNoRetry()
        {
            const bool enableEndpointDiscovery = true;
            using GlobalEndpointManager endpointManager = this.Initialize(
                useMultipleWriteLocations: false,
                enableEndpointDiscovery: enableEndpointDiscovery,
                isPreferredLocationsListEmpty: false,
                enforceSingleMasterSingleWriteLocation: true);

            ClientRetryPolicy policy = new ClientRetryPolicy(endpointManager, this.partitionKeyRangeLocationCache, new RetryOptions(), enableEndpointDiscovery, false);
            DocumentServiceRequest request = ClientRetryPolicyTests.CreateDtxRequest();
            policy.OnBeforeSendRequest(request);

            // 408 with no body — the inner CRP loop owns this code (the body-bearing case is
            // deferred to the outer DistributedTransactionCommitter loop).
            ResponseMessage response = new ResponseMessage(HttpStatusCode.RequestTimeout);

            const int budget = 10; // matches ClientRetryPolicy.MaxDtxRetryCount
            for (int i = 0; i < budget; i++)
            {
                ShouldRetryResult retryResult = await policy.ShouldRetryAsync(response, CancellationToken.None);
                Assert.IsTrue(retryResult.ShouldRetry, $"DTX 408 retry {i + 1} of {budget} should be allowed.");
            }

            // The (budget + 1)th call must be denied.
            ShouldRetryResult finalResult = await policy.ShouldRetryAsync(response, CancellationToken.None);
            Assert.IsFalse(finalResult.ShouldRetry, $"DTX retry budget is exhausted after {budget} retries; the next call must be denied.");
        }

        [DataTestMethod]
        [Description("CRP must defer body-bearing envelope responses (408 and 449/5352) to the outer DistributedTransactionCommitter loop so the two retry budgets do not amplify each other.")]
        [DataRow((int)HttpStatusCode.RequestTimeout, 0, DisplayName = "408 with body deferred to outer loop")]
        [DataRow((int)StatusCodes.RetryWith, (int)SubStatusCodes.DtcCoordinatorRaceConflict, DisplayName = "449/5352 with body deferred to outer loop")]
        public async Task DtxRequest_WithBody_DeferredToOuterLoop(int statusCode, int subStatusCode)
        {
            const bool enableEndpointDiscovery = true;
            using GlobalEndpointManager endpointManager = this.Initialize(
                useMultipleWriteLocations: false,
                enableEndpointDiscovery: enableEndpointDiscovery,
                isPreferredLocationsListEmpty: false,
                enforceSingleMasterSingleWriteLocation: true);

            ClientRetryPolicy policy = new ClientRetryPolicy(endpointManager, this.partitionKeyRangeLocationCache, new RetryOptions(), enableEndpointDiscovery, false);
            DocumentServiceRequest request = ClientRetryPolicyTests.CreateDtxRequest();
            policy.OnBeforeSendRequest(request);

            ResponseMessage response = new ResponseMessage((HttpStatusCode)statusCode)
            {
                Content = new MemoryStream(Encoding.UTF8.GetBytes("{\"isRetriable\":true}"))
            };
            if (subStatusCode != 0)
            {
                response.Headers.SubStatusCodeLiteral = subStatusCode.ToString();
            }

            // Replay the same body-bearing response well past CRP's inner retry budget: every call must
            // return NoRetry without consuming the inner counter, so a follow-up empty-body response still
            // gets the full inner budget.
            for (int i = 0; i < 25; i++)
            {
                response.Content.Position = 0; // ResponseMessage.Content may be re-read by callers
                ShouldRetryResult retryResult = await policy.ShouldRetryAsync(response, CancellationToken.None);
                Assert.IsFalse(retryResult.ShouldRetry,
                    $"CRP must defer body-bearing response to the outer loop on call {i + 1} (no inner retry).");
            }

            ResponseMessage emptyBodyResponse = new ResponseMessage((HttpStatusCode)statusCode);
            if (subStatusCode != 0)
            {
                emptyBodyResponse.Headers.SubStatusCodeLiteral = subStatusCode.ToString();
            }

            ShouldRetryResult innerResult = await policy.ShouldRetryAsync(emptyBodyResponse, CancellationToken.None);
            Assert.IsTrue(innerResult.ShouldRetry,
                "CRP's inner retry budget must NOT have been consumed by the deferred body-bearing calls; an empty-body response should still trigger an inner retry.");
        }

        /// <summary>
        /// Hedging-Detection invariant: when a hedge arm has already been tagged with
        /// <see cref="RequestedRegionReason.Hedging"/> by
        /// <c>CrossRegionHedgingAvailabilityStrategy.CloneAndSendAsync</c>, a subsequent retry
        /// on the same cloned request (e.g. 410 Gone, 449) must NOT silently overwrite the
        /// dispatch reason with <see cref="RequestedRegionReason.OperationRetry"/> or
        /// <see cref="RequestedRegionReason.RegionFailover"/>. Doing so would erase the hedge
        /// origin from the <c>GetRequestedRegions()</c> sequence on the second dispatch of
        /// the same arm. Pins F3 review feedback on PR #5868.
        /// </summary>
        [TestMethod]
        public async Task OnBeforeSendRequest_HedgeArmRetry_PreservesHedgingReason()
        {
            // Arrange — standard 2-region client (matches the Initialize() defaults).
            const bool enableEndpointDiscovery = true;
            using GlobalEndpointManager endpointManager = this.Initialize(
                useMultipleWriteLocations: false,
                enableEndpointDiscovery: enableEndpointDiscovery,
                isPreferredLocationsListEmpty: false);

            ClientRetryPolicy retryPolicy = new ClientRetryPolicy(
                endpointManager,
                this.partitionKeyRangeLocationCache,
                new RetryOptions(),
                enableEndpointDiscovery,
                isThinClientEnabled: false);

            // A hedge-arm dispatch carries a pre-seeded Hedging tag in Properties (this
            // mirrors what CrossRegionHedgingAvailabilityStrategy.CloneAndSendAsync does
            // for requestNumber > 0).
            DocumentServiceRequest request = this.CreateRequest(isReadRequest: true, isMasterResourceType: false);
            request.Properties = new Dictionary<string, object>
            {
                {
                    Microsoft.Azure.Cosmos.Tracing.HedgingDetectionState.DispatchReasonPropertyKey,
                    RequestedRegionReason.Hedging
                }
            };

            // First attempt — retryContext is null, so OnBeforeSendRequest must not touch
            // the existing Hedging value.
            retryPolicy.OnBeforeSendRequest(request);
            Assert.IsTrue(
                request.Properties.TryGetValue(
                    Microsoft.Azure.Cosmos.Tracing.HedgingDetectionState.DispatchReasonPropertyKey,
                    out object firstAttemptReasonObj),
                "Hedging tag must survive the first OnBeforeSendRequest call (retryContext == null).");
            Assert.AreEqual(RequestedRegionReason.Hedging, firstAttemptReasonObj);

            // Simulate a 410 Gone on the hedge arm → retryContext gets populated.
            // 410/LeaseNotFound flows through ShouldRetryOnUnavailableEndpointStatusCodes
            // and yields RetryRequestOnPreferredLocations = true (RegionFailover-shaped).
            DocumentClientException leaseNotFound = new DocumentClientException(
                message: "LeaseNotFound on hedge arm",
                innerException: null,
                statusCode: HttpStatusCode.Gone,
                substatusCode: SubStatusCodes.LeaseNotFound,
                requestUri: request.RequestContext.LocationEndpointToRoute,
                responseHeaders: new DictionaryNameValueCollection());

            ShouldRetryResult retryDecision = await retryPolicy.ShouldRetryAsync(leaseNotFound, CancellationToken.None);
            Assert.IsTrue(retryDecision.ShouldRetry, "410/LeaseNotFound must trigger a CRP retry decision.");

            // Critical assertion — second OnBeforeSendRequest (retryContext != null) must
            // PRESERVE the existing Hedging tag rather than overwriting it. Without the
            // preservation guard this would flip to RegionFailover / OperationRetry, and
            // the dispatch site would record the retry of the hedge arm as a same-region
            // retry, losing the hedge origin entirely.
            retryPolicy.OnBeforeSendRequest(request);
            Assert.IsTrue(
                request.Properties.TryGetValue(
                    Microsoft.Azure.Cosmos.Tracing.HedgingDetectionState.DispatchReasonPropertyKey,
                    out object retryReasonObj),
                "Hedging tag must still be present on Properties after the hedge-arm retry.");
            Assert.AreEqual(
                RequestedRegionReason.Hedging,
                retryReasonObj,
                "Hedge-arm retry must NOT overwrite Hedging with RegionFailover / OperationRetry — see F3 on PR #5868.");
        }

        /// <summary>
        /// Companion to <see cref="OnBeforeSendRequest_HedgeArmRetry_PreservesHedgingReason"/>:
        /// when the existing tag is NOT <see cref="RequestedRegionReason.Hedging"/> (e.g. it
        /// is the OperationRetry value written by a previous retry), the policy MUST overwrite
        /// it with the new retry reason. The preservation guard is strictly scoped to
        /// <see cref="RequestedRegionReason.Hedging"/>.
        /// </summary>
        [TestMethod]
        public async Task OnBeforeSendRequest_NonHedgeRetry_OverwritesPreviousReason()
        {
            const bool enableEndpointDiscovery = true;
            using GlobalEndpointManager endpointManager = this.Initialize(
                useMultipleWriteLocations: false,
                enableEndpointDiscovery: enableEndpointDiscovery,
                isPreferredLocationsListEmpty: false);

            ClientRetryPolicy retryPolicy = new ClientRetryPolicy(
                endpointManager,
                this.partitionKeyRangeLocationCache,
                new RetryOptions(),
                enableEndpointDiscovery,
                isThinClientEnabled: false);

            DocumentServiceRequest request = this.CreateRequest(isReadRequest: true, isMasterResourceType: false);
            request.Properties = new Dictionary<string, object>
            {
                {
                    Microsoft.Azure.Cosmos.Tracing.HedgingDetectionState.DispatchReasonPropertyKey,
                    RequestedRegionReason.OperationRetry
                }
            };

            // First attempt (retryContext == null): nothing overwritten.
            retryPolicy.OnBeforeSendRequest(request);
            Assert.AreEqual(
                RequestedRegionReason.OperationRetry,
                request.Properties[Microsoft.Azure.Cosmos.Tracing.HedgingDetectionState.DispatchReasonPropertyKey]);

            // Drive a 410/LeaseNotFound → ShouldRetryOnUnavailableEndpointStatusCodes
            // sets retryContext.RetryRequestOnPreferredLocations = true.
            DocumentClientException leaseNotFound = new DocumentClientException(
                message: "LeaseNotFound",
                innerException: null,
                statusCode: HttpStatusCode.Gone,
                substatusCode: SubStatusCodes.LeaseNotFound,
                requestUri: request.RequestContext.LocationEndpointToRoute,
                responseHeaders: new DictionaryNameValueCollection());

            ShouldRetryResult retryDecision = await retryPolicy.ShouldRetryAsync(leaseNotFound, CancellationToken.None);
            Assert.IsTrue(retryDecision.ShouldRetry);

            // Now the policy SHOULD overwrite OperationRetry → RegionFailover.
            retryPolicy.OnBeforeSendRequest(request);
            Assert.AreEqual(
                RequestedRegionReason.RegionFailover,
                request.Properties[Microsoft.Azure.Cosmos.Tracing.HedgingDetectionState.DispatchReasonPropertyKey],
                "Non-Hedging tag must be overwritten by the policy's new retry reason.");
        }

        /// <summary>
        /// Pins the F3 fix on <see cref="TransportHandler.AppendDispatchedRegion"/>: when
        /// the resolved dispatch reason is <see cref="RequestedRegionReason.Hedging"/>,
        /// the dispatch site MUST NOT remove the property from
        /// <c>DocumentServiceRequest.Properties</c>. Subsequent physical retries of the
        /// same hedge arm (driven by <see cref="ClientRetryPolicy"/> via
        /// <c>OnBeforeSendRequest</c> on the same cloned <c>RequestMessage</c>) need to
        /// observe the existing Hedging tag so the preservation guard in
        /// <see cref="ClientRetryPolicy.OnBeforeSendRequest"/> can keep the reason as
        /// Hedging rather than overwriting it with OperationRetry / RegionFailover.
        ///
        /// Pins F3 review feedback on PR #5868: without this carve-out the F3 preservation
        /// guard is dead code in production because TransportHandler drains the shared
        /// Properties dictionary before the retry-driven re-entry of OnBeforeSendRequest
        /// can observe it.
        /// </summary>
        [TestMethod]
        public void AppendDispatchedRegion_HedgingReason_LeavesPropertyForRetry()
        {
            using GlobalEndpointManager endpointManager = this.Initialize(
                useMultipleWriteLocations: false,
                enableEndpointDiscovery: true,
                isPreferredLocationsListEmpty: false);

            using ITrace rootTrace = Trace.GetRootTrace("AppendDispatchedRegion_HedgingReason");
            using RequestMessage requestMessage = new RequestMessage(
                HttpMethod.Get,
                "/dbs/db/colls/coll/docs/id",
                rootTrace)
            {
                ResourceType = ResourceType.Document,
                OperationType = OperationType.Read,
            };

            DocumentServiceRequest serviceRequest = this.CreateRequest(isReadRequest: true, isMasterResourceType: false);
            serviceRequest.RequestContext.RouteToLocation(ClientRetryPolicyTests.Location1Endpoint);
            serviceRequest.Properties = new Dictionary<string, object>
            {
                {
                    HedgingDetectionState.DispatchReasonPropertyKey,
                    RequestedRegionReason.Hedging
                }
            };

            TransportHandler.AppendDispatchedRegion(requestMessage, serviceRequest, endpointManager);

            // The Hedging entry must have been recorded ...
            IReadOnlyList<RequestedRegion> regions = rootTrace.Summary.HedgingDetectionState.GetRequestedRegionsSnapshot();
            Assert.AreEqual(1, regions.Count, "AppendRequested should have recorded exactly one region.");
            Assert.AreEqual(RequestedRegionReason.Hedging, regions[0].Reason);

            // ... AND the property must remain so subsequent retries can observe it
            // (this is the F3 carve-out).
            Assert.IsTrue(
                serviceRequest.Properties.ContainsKey(HedgingDetectionState.DispatchReasonPropertyKey),
                "Hedging property must remain on Properties after AppendDispatchedRegion so the F3 preservation guard in ClientRetryPolicy can keep it on the next physical retry of this hedge arm.");
            Assert.AreEqual(
                RequestedRegionReason.Hedging,
                serviceRequest.Properties[HedgingDetectionState.DispatchReasonPropertyKey],
                "Hedging property value must be unchanged.");
        }

        /// <summary>
        /// Inverse of <see cref="AppendDispatchedRegion_HedgingReason_LeavesPropertyForRetry"/>:
        /// when the resolved reason is NOT Hedging (e.g. OperationRetry written by
        /// <see cref="ClientRetryPolicy"/> on a same-region retry), the dispatch site MUST
        /// consume the property so a subsequent dispatch without a fresh upstream tag
        /// defaults to <see cref="RequestedRegionReason.Initial"/> rather than re-using
        /// the stale OperationRetry / RegionFailover value. Pins the original F5 behavior
        /// for non-Hedging reasons.
        /// </summary>
        [TestMethod]
        public void AppendDispatchedRegion_NonHedgingReason_RemovesPropertyAfterConsume()
        {
            using GlobalEndpointManager endpointManager = this.Initialize(
                useMultipleWriteLocations: false,
                enableEndpointDiscovery: true,
                isPreferredLocationsListEmpty: false);

            using ITrace rootTrace = Trace.GetRootTrace("AppendDispatchedRegion_NonHedgingReason");
            using RequestMessage requestMessage = new RequestMessage(
                HttpMethod.Get,
                "/dbs/db/colls/coll/docs/id",
                rootTrace)
            {
                ResourceType = ResourceType.Document,
                OperationType = OperationType.Read,
            };

            DocumentServiceRequest serviceRequest = this.CreateRequest(isReadRequest: true, isMasterResourceType: false);
            serviceRequest.RequestContext.RouteToLocation(ClientRetryPolicyTests.Location1Endpoint);
            serviceRequest.Properties = new Dictionary<string, object>
            {
                {
                    HedgingDetectionState.DispatchReasonPropertyKey,
                    RequestedRegionReason.OperationRetry
                }
            };

            TransportHandler.AppendDispatchedRegion(requestMessage, serviceRequest, endpointManager);

            IReadOnlyList<RequestedRegion> regions = rootTrace.Summary.HedgingDetectionState.GetRequestedRegionsSnapshot();
            Assert.AreEqual(1, regions.Count);
            Assert.AreEqual(RequestedRegionReason.OperationRetry, regions[0].Reason);

            Assert.IsFalse(
                serviceRequest.Properties.ContainsKey(HedgingDetectionState.DispatchReasonPropertyKey),
                "Non-Hedging property must be removed after AppendDispatchedRegion so subsequent dispatches default to Initial unless explicitly re-tagged.");
        }

        /// <summary>
        /// End-to-end production-order test for F3 + F5: drives the exact sequence the
        /// hedge-arm retry path takes inside <see cref="TransportHandler.ProcessMessageAsync"/>:
        /// <list type="number">
        /// <item>Strategy seeds <c>Properties[KEY] = Hedging</c>.</item>
        /// <item><c>OnBeforeSendRequest</c> (first attempt, retryContext == null) leaves it alone.</item>
        /// <item><c>AppendDispatchedRegion</c> consumes it. With the F3 fix this LEAVES the
        /// property on Properties because the reason was Hedging.</item>
        /// <item>410 Gone -> <c>ShouldRetryAsync</c> populates <c>retryContext</c>.</item>
        /// <item><c>OnBeforeSendRequest</c> (retry, retryContext != null) MUST see Hedging
        /// on Properties and preserve it via the F3 guard.</item>
        /// <item><c>AppendDispatchedRegion</c> on the retry records the second physical
        /// dispatch as Hedging, not as RegionFailover.</item>
        /// </list>
        /// Without the F3 fix in <see cref="TransportHandler.AppendDispatchedRegion"/>,
        /// the property would be drained at step 3 and the retry would be silently
        /// recorded as RegionFailover, defeating the F3 guard in
        /// <see cref="ClientRetryPolicy.OnBeforeSendRequest"/>.
        /// </summary>
        [TestMethod]
        public async Task HedgeArmRetry_ProductionOrder_RecordsBothPhysicalAttemptsAsHedging()
        {
            using GlobalEndpointManager endpointManager = this.Initialize(
                useMultipleWriteLocations: false,
                enableEndpointDiscovery: true,
                isPreferredLocationsListEmpty: false);

            ClientRetryPolicy retryPolicy = new ClientRetryPolicy(
                endpointManager,
                this.partitionKeyRangeLocationCache,
                new RetryOptions(),
                enableEndpointDiscovery: true,
                isThinClientEnabled: false);

            using ITrace rootTrace = Trace.GetRootTrace("HedgeArmRetry_ProductionOrder");
            using RequestMessage requestMessage = new RequestMessage(
                HttpMethod.Get,
                "/dbs/db/colls/coll/docs/id",
                rootTrace)
            {
                ResourceType = ResourceType.Document,
                OperationType = OperationType.Read,
            };

            // Step 1: hedge orchestrator pre-seeds Properties[KEY] = Hedging on the
            // cloned RequestMessage before it enters the pipeline.
            requestMessage.Properties[HedgingDetectionState.DispatchReasonPropertyKey] =
                RequestedRegionReason.Hedging;

            // Hand-build a DSR whose Properties is the SAME reference as
            // requestMessage.Properties — this mirrors RequestMessage.ToDocumentServiceRequest()
            // line 302 (`serviceRequest.Properties = this.Properties`) and is essential
            // for reproducing the bug: TransportHandler.Remove on serviceRequest.Properties
            // also mutates requestMessage.Properties.
            DocumentServiceRequest serviceRequest = this.CreateRequest(isReadRequest: true, isMasterResourceType: false);
            serviceRequest.Properties = requestMessage.Properties;
            serviceRequest.RequestContext.RouteToLocation(ClientRetryPolicyTests.Location1Endpoint);

            // Step 2: first OnBeforeSendRequest (retryContext == null). The Hedging tag
            // must survive.
            retryPolicy.OnBeforeSendRequest(serviceRequest);
            Assert.AreEqual(
                RequestedRegionReason.Hedging,
                serviceRequest.Properties[HedgingDetectionState.DispatchReasonPropertyKey],
                "First OnBeforeSendRequest (retryContext == null) must not touch the Hedging tag.");

            // Step 3: AppendDispatchedRegion consumes the property (records the first
            // physical hedge dispatch). With the F3 fix the property remains.
            TransportHandler.AppendDispatchedRegion(requestMessage, serviceRequest, endpointManager);
            Assert.IsTrue(
                serviceRequest.Properties.ContainsKey(HedgingDetectionState.DispatchReasonPropertyKey),
                "F3 fix: Hedging property must survive TransportHandler.AppendDispatchedRegion so the next retry's OnBeforeSendRequest can preserve it.");

            // Step 4: 410 Gone -> ShouldRetryAsync populates retryContext.
            DocumentClientException leaseNotFound = new DocumentClientException(
                message: "LeaseNotFound on hedge arm",
                innerException: null,
                statusCode: HttpStatusCode.Gone,
                substatusCode: SubStatusCodes.LeaseNotFound,
                requestUri: serviceRequest.RequestContext.LocationEndpointToRoute,
                responseHeaders: new DictionaryNameValueCollection());
            ShouldRetryResult retryDecision = await retryPolicy.ShouldRetryAsync(leaseNotFound, CancellationToken.None);
            Assert.IsTrue(retryDecision.ShouldRetry, "410/LeaseNotFound must trigger a CRP retry decision on a hedge arm.");

            // Step 5: second OnBeforeSendRequest (retryContext != null). The F3 guard
            // MUST observe Hedging on Properties and preserve it.
            retryPolicy.OnBeforeSendRequest(serviceRequest);
            Assert.AreEqual(
                RequestedRegionReason.Hedging,
                serviceRequest.Properties[HedgingDetectionState.DispatchReasonPropertyKey],
                "F3 guard must preserve Hedging on the retry-side OnBeforeSendRequest — otherwise the hedge origin is silently overwritten with RegionFailover.");

            // Step 6: AppendDispatchedRegion on the retry records the second physical
            // attempt — also as Hedging.
            TransportHandler.AppendDispatchedRegion(requestMessage, serviceRequest, endpointManager);

            IReadOnlyList<RequestedRegion> regions = rootTrace.Summary.HedgingDetectionState.GetRequestedRegionsSnapshot();
            Assert.AreEqual(2, regions.Count, "Both physical dispatches of the hedge arm should have been recorded.");
            Assert.IsTrue(
                regions.All(r => r.Reason == RequestedRegionReason.Hedging),
                $"Both physical retries of the hedge arm must be recorded as Hedging, not RegionFailover. Actual reasons: [{string.Join(", ", regions.Select(r => r.Reason))}].");
            Assert.IsTrue(
                rootTrace.Summary.HedgingDetectionState.HedgingStarted,
                "HedgingStarted must be true after a Hedging entry has been appended.");
        }

        private static DocumentServiceRequest CreateDtxRequest()
        {
            return DocumentServiceRequest.Create(
                OperationType.CommitDistributedTransaction,
                ResourceType.DistributedTransactionBatch,
                AuthorizationTokenType.PrimaryMasterKey);
        }

        private static GlobalPartitionEndpointManagerCore.PartitionKeyRangeFailoverInfo GetPartitionKeyRangeFailoverInfoUsingReflection(
            GlobalPartitionEndpointManager globalPartitionEndpointManager,
            PartitionKeyRange pkRange,
            bool isReadOnlyOrMultiMasterWriteRequest)
        {
            string fieldName = isReadOnlyOrMultiMasterWriteRequest ? "PartitionKeyRangeToLocationForReadAndWrite" : "PartitionKeyRangeToLocationForWrite";
            FieldInfo fieldInfo = globalPartitionEndpointManager
                .GetType()
                .GetField(
                    name: fieldName,
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
            bool enablePartitionLevelCircuitBreaker = false,
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
                this.partitionKeyRangeLocationCache = new GlobalPartitionEndpointManagerCore(
                    globalEndpointManager: endpointManager,
                    isPartitionLevelFailoverEnabled: enablePartitionLevelFailover,
                    isPartitionLevelCircuitBreakerEnabled: enablePartitionLevelFailover || enablePartitionLevelCircuitBreaker);
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
