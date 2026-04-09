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


            ClientRetryPolicy retryPolicy = new ClientRetryPolicy(endpointManager, this.partitionKeyRangeLocationCache, new RetryOptions(), enableEndpointDiscovery, isThinClientEnabled: false);

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
                isThinClientEnabled: false);

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
            ClientRetryPolicy retryPolicy = new ClientRetryPolicy(endpointManager, this.partitionKeyRangeLocationCache, new RetryOptions(), enableEndpointDiscovery, isThinClientEnabled: false);

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
            OperationCanceledException operationCancelledException= new(message: "Operation was cancelled due to cancellation token expiry.");

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
            string originalHubRegionFlag = Environment.GetEnvironmentVariable(ConfigurationManager.HubRegionProcessingEnabled);
            Environment.SetEnvironmentVariable(ConfigurationManager.HubRegionProcessingEnabled, "True");

            try
            {
            using GlobalEndpointManager endpointManager = this.Initialize(
                useMultipleWriteLocations: !isSingleMaster,
                enableEndpointDiscovery: enableEndpointDiscovery,
                isPreferredLocationsListEmpty: false,
                enforceSingleMasterSingleWriteLocation: isSingleMaster);

            GlobalPartitionEndpointManagerCore cacheManager = new GlobalPartitionEndpointManagerCore(endpointManager);

            ClientRetryPolicy retryPolicy = new ClientRetryPolicy(
                endpointManager,
                cacheManager,
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
            Assert.IsTrue(shouldRetry.ShouldRetry, "Should retry on first 404/1002.");

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
                // For single master, after second 404/1002, the SDK sets hub header flag and retries
                Assert.IsTrue(shouldRetry.ShouldRetry, "Single master should retry after second 404/1002 with hub header flag set.");

                // Now verify the header IS present on this retry (triggered after 2x 404/1002)
                retryPolicy.OnBeforeSendRequest(request);
                headerValues = request.Headers.GetValues(HubRegionHeader);
                Assert.IsNotNull(headerValues, "Hub header MUST be present on retry after 2x 404/1002 for single master.");
                Assert.AreEqual(1, headerValues.Length, "Header should have exactly one value.");
                Assert.AreEqual(bool.TrueString, headerValues[0], "Header value should be 'True'.");
            }
            else
            {
                // For multi-master: Should retry across regions but hub header should NOT be added
                Assert.IsTrue(shouldRetry.ShouldRetry, "Multi-master should continue retrying on 404/1002 across regions.");

                retryPolicy.OnBeforeSendRequest(request);
                headerValues = request.Headers.GetValues(HubRegionHeader);
                Assert.IsNull(headerValues, "Hub header should NOT be present for multi-master account.");
            }
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.HubRegionProcessingEnabled, originalHubRegionFlag);
            }
        }

        /// <summary>
        /// Verifies complete flow: 404/1002 triggers hub header, 403/3 populates the PPAF cache
        /// with the hub region discovery entry, and subsequent requests with hub header can
        /// find the cached entry to skip the 403/3 discovery chain.
        /// </summary>
        [TestMethod]
        [Owner("aavasthy")]
        [Description("Validates full hub caching flow: 2× 404/1002 → hub header → 403/3 (populates cache via PPAF) → retry → subsequent request reuses cache to skip 403/3.")]
        public async Task ClientRetryPolicy_After404With1002Twice_Then403_3_ThenSuccess_CachesHub_AndSubsequentRequestReusesCache()
        {
            // Ensure hub region processing is enabled for this test
            string originalHubRegionFlag = Environment.GetEnvironmentVariable(ConfigurationManager.HubRegionProcessingEnabled);
            Environment.SetEnvironmentVariable(ConfigurationManager.HubRegionProcessingEnabled, "True");

            try
            {
            // Arrange
            const bool enableEndpointDiscovery = true;
            using GlobalEndpointManager endpointManager = this.Initialize(
                useMultipleWriteLocations: false,
                enableEndpointDiscovery: enableEndpointDiscovery,
                isPreferredLocationsListEmpty: false,
                enforceSingleMasterSingleWriteLocation: true);

            GlobalPartitionEndpointManagerCore cacheManager = new GlobalPartitionEndpointManagerCore(endpointManager);
            PartitionKeyRange pkRange = new PartitionKeyRange { Id = "0", MinInclusive = "", MaxExclusive = "FF" };

            // ===== STEP 1: First request (cold cache) =====
            // Flow: 404/1002 → 404/1002 → hub header → 403/3 (populates PPAF cache) → retry
            ClientRetryPolicy retryPolicy1 = new ClientRetryPolicy(
                endpointManager,
                cacheManager,
                new RetryOptions(),
                enableEndpointDiscovery,
                isThinClientEnabled: false);

            DocumentServiceRequest request1 = this.CreateRequest(isReadRequest: true, isMasterResourceType: false);
            request1.RequestContext.ResolvedPartitionKeyRange = pkRange;

            // Attempt 1: no hub header
            retryPolicy1.OnBeforeSendRequest(request1);
            Assert.IsNull(request1.Headers.GetValues(HubRegionHeader), "No hub header initially.");

            // Simulate first 404/1002
            DocumentClientException error1 = new DocumentClientException(
                message: "404/1002 #1",
                innerException: null,
                statusCode: HttpStatusCode.NotFound,
                substatusCode: SubStatusCodes.ReadSessionNotAvailable,
                requestUri: ClientRetryPolicyTests.Location1Endpoint,
                responseHeaders: new DictionaryNameValueCollection());

            ShouldRetryResult shouldRetry1 = await retryPolicy1.ShouldRetryAsync(error1, CancellationToken.None);
            Assert.IsTrue(shouldRetry1.ShouldRetry, "Should retry after first 404/1002.");

            // Attempt 2: routes to write region, no hub header yet
            retryPolicy1.OnBeforeSendRequest(request1);
            Assert.IsNull(request1.Headers.GetValues(HubRegionHeader), "No hub header on first retry.");

            // Simulate second 404/1002 → triggers addHubRegionProcessingOnlyHeader
            DocumentClientException error2 = new DocumentClientException(
                message: "404/1002 #2",
                innerException: null,
                statusCode: HttpStatusCode.NotFound,
                substatusCode: SubStatusCodes.ReadSessionNotAvailable,
                requestUri: ClientRetryPolicyTests.Location1Endpoint,
                responseHeaders: new DictionaryNameValueCollection());

            ShouldRetryResult shouldRetry2 = await retryPolicy1.ShouldRetryAsync(error2, CancellationToken.None);
            Assert.IsTrue(shouldRetry2.ShouldRetry, "Should retry after second 404/1002.");

            // Attempt 3: hub header active, routed to a region
            retryPolicy1.OnBeforeSendRequest(request1);
            string[] headerValues = request1.Headers.GetValues(HubRegionHeader);
            Assert.IsNotNull(headerValues, "Hub header MUST be present after 2x 404/1002.");
            Assert.AreEqual(bool.TrueString, headerValues[0]);
            Uri regionThatGot403 = request1.RequestContext.LocationEndpointToRoute;

            // Simulate 403/3 (WriteForbidden) — non-hub region rejects the hub-header request.
            // In the new design, the read-path 403/3 handler calls TryMarkEndpointUnavailableForPartitionKeyRange
            // which populates the PartitionKeyRangeToLocationForWrite cache via the PPAF mechanism
            // (TryAddOrUpdatePartitionFailoverInfoAndMoveToNextLocation). This marks the failed region
            // and advances Current to the next untried region.
            DocumentClientException error403 = new DocumentClientException(
                message: "403/3 WriteForbidden",
                innerException: null,
                statusCode: HttpStatusCode.Forbidden,
                substatusCode: SubStatusCodes.WriteForbidden,
                requestUri: regionThatGot403,
                responseHeaders: new DictionaryNameValueCollection());

            ShouldRetryResult shouldRetry3 = await retryPolicy1.ShouldRetryAsync(error403, CancellationToken.None);
            Assert.IsTrue(shouldRetry3.ShouldRetry, "Should retry after 403/3 to find hub.");

            // Attempt 4: hub header persists after 403/3 retry
            retryPolicy1.OnBeforeSendRequest(request1);
            headerValues = request1.Headers.GetValues(HubRegionHeader);
            Assert.IsNotNull(headerValues, "Hub header should persist through 403/3 retry.");

            // Without checkHubRegionOverrideInCache, the cache gate blocks access —
            // this ensures normal requests never get routed to hub via the cache.
            bool overrideWithoutFlag = cacheManager.TryAddPartitionLevelLocationOverride(request1);
            Assert.IsFalse(overrideWithoutFlag,
                "Without checkHubRegionOverrideInCache flag, cache should NOT override routing (prevents bombarding hub on normal requests).");

            // WITH checkHubRegionOverrideInCache: true, the 403/3-populated cache IS accessible.
            // This is the path used by ShouldRetryOnSessionNotAvailable for warm-cache requests.
            bool overrideWithFlag = cacheManager.TryAddPartitionLevelLocationOverride(request1, addHubRegionOverrideFromCache: true);
            Assert.IsTrue(overrideWithFlag,
                "With checkHubRegionOverrideInCache: true, cache should contain the hub region discovery entry populated by 403/3.");
            Uri hubRegion = request1.RequestContext.LocationEndpointToRoute;

            // ===== STEP 2: Normal request (no errors) — should NOT use hub cache =====
            ClientRetryPolicy retryPolicyNormal = new ClientRetryPolicy(
                endpointManager,
                cacheManager,
                new RetryOptions(),
                enableEndpointDiscovery,
                isThinClientEnabled: false);

            DocumentServiceRequest requestNormal = this.CreateRequest(isReadRequest: true, isMasterResourceType: false);
            requestNormal.RequestContext.ResolvedPartitionKeyRange = pkRange;

            retryPolicyNormal.OnBeforeSendRequest(requestNormal);
            Assert.IsNull(requestNormal.Headers.GetValues(HubRegionHeader),
                "Normal request should NOT have hub header.");

            // Simulate GatewayStoreModel / GlobalAddressResolver calling TryAddPartitionLevelLocationOverride
            bool normalOverride = cacheManager.TryAddPartitionLevelLocationOverride(requestNormal);
            Assert.IsFalse(normalOverride,
                "Without hub header, partition-level cache should NOT override routing (prevents bombarding hub).");

            // ===== STEP 3: Second request with errors (warm cache) =====
            // With the warm cache from STEP 1's 403/3, even the FIRST 404/1002 triggers a cache hit.
            // ShouldRetryOnSessionNotAvailable calls TryAddPartitionLevelLocationOverride(request, checkHubRegionOverrideInCache: true)
            // on every 404/1002 (not just after the second one). When the cache is warm, it finds the entry
            // immediately and sets addHubRegionProcessingOnlyHeader = true — skipping the 403/3 discovery chain entirely.
            ClientRetryPolicy retryPolicy2 = new ClientRetryPolicy(
                endpointManager,
                cacheManager,
                new RetryOptions(),
                enableEndpointDiscovery,
                isThinClientEnabled: false);

            DocumentServiceRequest request2 = this.CreateRequest(isReadRequest: true, isMasterResourceType: false);
            request2.RequestContext.ResolvedPartitionKeyRange = pkRange;

            // Fresh attempt — normal routing, no hub header
            retryPolicy2.OnBeforeSendRequest(request2);
            Assert.IsNull(request2.Headers.GetValues(HubRegionHeader),
                "Fresh request should NOT have hub header.");

            // Simulate 404/1002 #1 on request 2.
            // Inside ShouldRetryOnSessionNotAvailable, TryAddPartitionLevelLocationOverride(request, checkHubRegionOverrideInCache: true)
            // hits the warm cache from STEP 1's 403/3. addHubRegionProcessingOnlyHeader is set to true immediately.
            // The request is routed to the cached hub region and retried — no need for a second 404/1002.
            DocumentClientException error5 = new DocumentClientException(
                message: "404/1002 #1 on request 2 — warm cache hit",
                innerException: null,
                statusCode: HttpStatusCode.NotFound,
                substatusCode: SubStatusCodes.ReadSessionNotAvailable,
                requestUri: ClientRetryPolicyTests.Location1Endpoint,
                responseHeaders: new DictionaryNameValueCollection());
            ShouldRetryResult shouldRetry5 = await retryPolicy2.ShouldRetryAsync(error5, CancellationToken.None);
            Assert.IsTrue(shouldRetry5.ShouldRetry, "Should retry — warm cache routes directly to hub.");

            // Hub header is NOW active after just one 404/1002 (warm cache shortcut).
            // OnBeforeSendRequest sets the hub header because addHubRegionProcessingOnlyHeader = true.
            retryPolicy2.OnBeforeSendRequest(request2);

            string[] headerValues2 = request2.Headers.GetValues(HubRegionHeader);
            Assert.IsNotNull(headerValues2,
                "Hub header MUST be present after first 404/1002 when warm cache is available. " +
                "This proves the cache shortcut: only 1x 404/1002 needed instead of 2x.");
            Assert.AreEqual(bool.TrueString, headerValues2[0]);

            // Verify the warm cache routes to the hub region discovered in STEP 1.
            bool cachedOverride = cacheManager.TryAddPartitionLevelLocationOverride(request2, addHubRegionOverrideFromCache: true);
            Assert.IsTrue(cachedOverride, "With addHubRegionOverrideFromCache, cache should route to cached hub from STEP 1's 403/3.");
            Assert.AreEqual(hubRegion, request2.RequestContext.LocationEndpointToRoute,
                "After 1× 404/1002 with warm cache, should route to cached hub directly (skipping 403/3 discovery).");
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.HubRegionProcessingEnabled, originalHubRegionFlag);
            }
        }

        /// <summary>
        /// Verifies hub header behavior differs between single-master and multi-master accounts
        /// </summary>
        [TestMethod]
        [Owner("aavasthy")]
        [Description("Validates hub header is added only for single-master accounts after 2x 404/1002, not for multi-master.")]
        [DataRow(true, DisplayName = "Single-master account - Hub header added after 2x 404/1002")]
        [DataRow(false, DisplayName = "Multi-master account - Hub header NOT added")]
        public async Task ClientRetryPolicy_After404With1002_AddsHubHeaderOnlySingleMaster_NotMultiMaster(bool isSingleMaster)
        {
            // Ensure hub region processing is enabled for this test
            string originalHubRegionFlag = Environment.GetEnvironmentVariable(ConfigurationManager.HubRegionProcessingEnabled);
            Environment.SetEnvironmentVariable(ConfigurationManager.HubRegionProcessingEnabled, "True");

            try
            {
            // Arrange
            const bool enableEndpointDiscovery = true;
            using GlobalEndpointManager endpointManager = this.Initialize(
                useMultipleWriteLocations: !isSingleMaster,
                enableEndpointDiscovery: enableEndpointDiscovery,
                isPreferredLocationsListEmpty: false,
                enforceSingleMasterSingleWriteLocation: isSingleMaster);

            GlobalPartitionEndpointManagerCore cacheManager = new GlobalPartitionEndpointManagerCore(endpointManager);

            ClientRetryPolicy retryPolicy = new ClientRetryPolicy(
                endpointManager,
                cacheManager,
                new RetryOptions(),
                enableEndpointDiscovery,
                isThinClientEnabled: false);

            DocumentServiceRequest request = this.CreateRequest(isReadRequest: true, isMasterResourceType: false);

            DocumentClientException sessionNotAvailableError = new DocumentClientException(
                message: "404/1002",
                innerException: null,
                statusCode: HttpStatusCode.NotFound,
                substatusCode: SubStatusCodes.ReadSessionNotAvailable,
                requestUri: ClientRetryPolicyTests.Location1Endpoint,
                responseHeaders: new DictionaryNameValueCollection());

            // Act - Simulate 3 consecutive 404/1002 errors
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                retryPolicy.OnBeforeSendRequest(request);

                string[] headerValues = request.Headers.GetValues(HubRegionHeader);

                if (isSingleMaster)
                {
                    // Single-master: Hub header should be present starting from 3rd attempt
                    if (attempt <= 2)
                    {
                        Assert.IsNull(headerValues, $"Single-master: Hub header should NOT be present on attempt {attempt}.");
                    }
                    else
                    {
                        Assert.IsNotNull(headerValues, $"Single-master: Hub header MUST be present on attempt {attempt} (after 2x 404/1002).");
                        Assert.AreEqual(bool.TrueString, headerValues[0], "Hub header value should be 'True'.");
                    }
                }
                else
                {
                    // Multi-master: Hub header should NEVER be added
                    Assert.IsNull(headerValues, $"Multi-master: Hub header should NOT be present on any attempt (attempt {attempt}).");
                }

                // Simulate retry
                if (attempt < 3)
                {
                    ShouldRetryResult shouldRetry = await retryPolicy.ShouldRetryAsync(sessionNotAvailableError, CancellationToken.None);

                    if (isSingleMaster && attempt == 2)
                    {
                        // Single-master stops retrying 404/1002 after 2nd failure, but may retry for other reasons
                        Assert.IsTrue(shouldRetry.ShouldRetry, "Should trigger retry to add hub header.");
                    }
                    else if (!isSingleMaster)
                    {
                        Assert.IsTrue(shouldRetry.ShouldRetry, "Multi-master should retry across regions.");
                    }
                }
            }
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.HubRegionProcessingEnabled, originalHubRegionFlag);
            }
        }

        /// <summary>
        /// Verifies that when the hub region itself returns 404/1002 with the hub header active,
        /// the SDK surfaces NoRetry — throwing the exception to the user. The hub is the source
        /// of truth: if even the hub says "session not available", the document genuinely doesn't
        /// exist in this session and no further retry is possible.
        ///
        /// Flow:
        ///   1. Read → 404/1002 (sessionTokenRetryCount=1, no hub header)
        ///   2. Retry to write region → 404/1002 (sessionTokenRetryCount=2, sets addHubRegionProcessingOnlyHeader=true)
        ///   3. Retry with hub header → 404/1002 from hub (sessionTokenRetryCount=3, enters NoRetry path)
        ///   Result: ShouldRetry == false → CosmosException thrown to caller
        /// </summary>
        [TestMethod]
        [Owner("aavasthy")]
        [Description("Validates NoRetry when hub region returns 404/1002 with hub header active — hub is source of truth.")]
        public async Task ClientRetryPolicy_HubRegion_Returns4041002_WithHubHeader_ShouldNotRetry()
        {
            // Ensure hub region processing is enabled for this test
            string originalHubRegionFlag = Environment.GetEnvironmentVariable(ConfigurationManager.HubRegionProcessingEnabled);
            Environment.SetEnvironmentVariable(ConfigurationManager.HubRegionProcessingEnabled, "True");

            try
            {
            // Arrange: single-master account with endpoint discovery
            const bool enableEndpointDiscovery = true;
            using GlobalEndpointManager endpointManager = this.Initialize(
                useMultipleWriteLocations: false,
                enableEndpointDiscovery: enableEndpointDiscovery,
                isPreferredLocationsListEmpty: false,
                enforceSingleMasterSingleWriteLocation: true);

            GlobalPartitionEndpointManagerCore cacheManager = new GlobalPartitionEndpointManagerCore(endpointManager);
            PartitionKeyRange pkRange = new PartitionKeyRange { Id = "0", MinInclusive = "", MaxExclusive = "FF" };

            ClientRetryPolicy retryPolicy = new ClientRetryPolicy(
                endpointManager,
                cacheManager,
                new RetryOptions(),
                enableEndpointDiscovery,
                isThinClientEnabled: false);

            DocumentServiceRequest request = this.CreateRequest(isReadRequest: true, isMasterResourceType: false);
            request.RequestContext.ResolvedPartitionKeyRange = pkRange;

            // ===== Attempt 1: Read on preferred region → 404/1002 (no hub header) =====
            retryPolicy.OnBeforeSendRequest(request);

            Assert.IsNull(request.Headers.GetValues(HubRegionHeader),
                "Attempt 1: Hub header must NOT be present on the initial request.");

            DocumentClientException error1 = new DocumentClientException(
                message: "404/1002 from preferred read region",
                innerException: null,
                statusCode: HttpStatusCode.NotFound,
                substatusCode: SubStatusCodes.ReadSessionNotAvailable,
                requestUri: request.RequestContext.LocationEndpointToRoute,
                responseHeaders: new DictionaryNameValueCollection());

            ShouldRetryResult retry1 = await retryPolicy.ShouldRetryAsync(error1, CancellationToken.None);
            Assert.IsTrue(retry1.ShouldRetry,
                "Attempt 1: Should retry after first 404/1002 — routes to write region.");

            // ===== Attempt 2: Retry to write region → 404/1002 (still no hub header, but sets flag) =====
            retryPolicy.OnBeforeSendRequest(request);

            Assert.IsNull(request.Headers.GetValues(HubRegionHeader),
                "Attempt 2: Hub header must NOT be present on second attempt (flag set AFTER this error).");

            DocumentClientException error2 = new DocumentClientException(
                message: "404/1002 from write region",
                innerException: null,
                statusCode: HttpStatusCode.NotFound,
                substatusCode: SubStatusCodes.ReadSessionNotAvailable,
                requestUri: request.RequestContext.LocationEndpointToRoute,
                responseHeaders: new DictionaryNameValueCollection());

            ShouldRetryResult retry2 = await retryPolicy.ShouldRetryAsync(error2, CancellationToken.None);
            Assert.IsTrue(retry2.ShouldRetry,
                "Attempt 2: Should retry after second 404/1002 — addHubRegionProcessingOnlyHeader is now true.");

            // ===== Attempt 3: Hub header is active, sent to hub region → hub returns 404/1002 =====
            retryPolicy.OnBeforeSendRequest(request);

            string[] hubHeaderValues = request.Headers.GetValues(HubRegionHeader);
            Assert.IsNotNull(hubHeaderValues,
                "Attempt 3: Hub header MUST be present — SDK set it after 2x 404/1002.");
            Assert.AreEqual(1, hubHeaderValues.Length, "Hub header should have exactly one value.");
            Assert.AreEqual(bool.TrueString, hubHeaderValues[0], "Hub header value should be 'True'.");

            // Simulate 404/1002 from the hub region itself — document genuinely doesn't exist in session
            DocumentClientException hubError = new DocumentClientException(
                message: "404/1002 from hub region — source of truth says document not found",
                innerException: null,
                statusCode: HttpStatusCode.NotFound,
                substatusCode: SubStatusCodes.ReadSessionNotAvailable,
                requestUri: request.RequestContext.LocationEndpointToRoute,
                responseHeaders: new DictionaryNameValueCollection());

            ShouldRetryResult retryFromHub = await retryPolicy.ShouldRetryAsync(hubError, CancellationToken.None);

            // ===== CRITICAL ASSERTION: NoRetry — hub is the source of truth =====
            Assert.IsFalse(retryFromHub.ShouldRetry,
                "Hub region returned 404/1002 with hub header active. " +
                "The SDK must NOT retry — the hub is the source of truth. " +
                "This 404/1002 should surface as a CosmosException to the caller.");
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.HubRegionProcessingEnabled, originalHubRegionFlag);
            }
        }

        /// <summary>
        /// Verifies that when AZURE_COSMOS_HUB_REGION_PROCESSING_ENABLED is set to "False",
        /// the SDK reverts to the original retry behavior for read requests encountering 404/1002:
        ///   1. First 404/1002 → retry to write region (RetryLocationIndex=0, PreferredLocations=false)
        ///   2. Second 404/1002 → NoRetry (give up — original behavior before hub region caching)
        /// The entire hub region flow is skipped:
        ///   - No hub header is ever attached to any request
        ///   - The PPAF/hub cache is never populated or consulted
        ///   - The 403/3 read-path handler (hub discovery chain) is never triggered
        /// </summary>
        [TestMethod]
        [Owner("aavasthy")]
        [Description("Validates that disabling hub region processing via env var restores original 404/1002 retry behavior — no hub header, no cache, no hub discovery.")]
        public async Task ClientRetryPolicy_HubRegionDisabled_FallsBackToOriginalBehavior()
        {
            // Arrange: set env var to disable hub region processing
            string originalValue = Environment.GetEnvironmentVariable(ConfigurationManager.HubRegionProcessingEnabled);
            try
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.HubRegionProcessingEnabled, "False");

                const bool enableEndpointDiscovery = true;
                using GlobalEndpointManager endpointManager = this.Initialize(
                    useMultipleWriteLocations: false,
                    enableEndpointDiscovery: enableEndpointDiscovery,
                    isPreferredLocationsListEmpty: false,
                    enforceSingleMasterSingleWriteLocation: true);

                GlobalPartitionEndpointManagerCore cacheManager = new GlobalPartitionEndpointManagerCore(endpointManager);
                PartitionKeyRange pkRange = new PartitionKeyRange { Id = "0", MinInclusive = "", MaxExclusive = "FF" };

                ClientRetryPolicy retryPolicy = new ClientRetryPolicy(
                    endpointManager,
                    cacheManager,
                    new RetryOptions(),
                    enableEndpointDiscovery,
                    isThinClientEnabled: false);

                DocumentServiceRequest request = this.CreateRequest(isReadRequest: true, isMasterResourceType: false);
                request.RequestContext.ResolvedPartitionKeyRange = pkRange;

                DocumentClientException sessionError = new DocumentClientException(
                    message: "404/1002",
                    innerException: null,
                    statusCode: HttpStatusCode.NotFound,
                    substatusCode: SubStatusCodes.ReadSessionNotAvailable,
                    requestUri: ClientRetryPolicyTests.Location1Endpoint,
                    responseHeaders: new DictionaryNameValueCollection());

                // ===== Attempt 1: 404/1002 → should retry to write region (original behavior) =====
                retryPolicy.OnBeforeSendRequest(request);
                Assert.IsNull(request.Headers.GetValues(HubRegionHeader),
                    "Hub header must NOT be present when hub region processing is disabled.");

                ShouldRetryResult retry1 = await retryPolicy.ShouldRetryAsync(sessionError, CancellationToken.None);
                Assert.IsTrue(retry1.ShouldRetry, "First 404/1002 should retry to write region (original behavior).");

                // ===== Attempt 2: 404/1002 from write region → NoRetry (original 2-attempt limit) =====
                retryPolicy.OnBeforeSendRequest(request);
                Assert.IsNull(request.Headers.GetValues(HubRegionHeader),
                    "Hub header must NEVER appear when hub region processing is disabled — even after 2x 404/1002.");

                ShouldRetryResult retry2 = await retryPolicy.ShouldRetryAsync(sessionError, CancellationToken.None);
                Assert.IsFalse(retry2.ShouldRetry,
                    "Second 404/1002 must return NoRetry — original behavior without hub region processing. " +
                    "No hub header, no hub discovery, just the classic 2-attempt limit.");

                // ===== Verify cache was never populated =====
                // With flag disabled, TryAddPartitionLevelLocationOverride was never called with the hub path,
                // so the PPAF cache should have no hub region entry for this partition.
                bool cacheHit = cacheManager.TryAddPartitionLevelLocationOverride(request, addHubRegionOverrideFromCache: true);
                Assert.IsFalse(cacheHit,
                    "PPAF/hub cache must NOT be populated when hub region processing is disabled. " +
                    "No hub discovery occurred, so no cache entry should exist.");

                // ===== Verify 403/3 on a separate read follows write-path, not hub read-path =====
                // Create a fresh policy to simulate an independent request that gets 403/3
                ClientRetryPolicy retryPolicy2 = new ClientRetryPolicy(
                    endpointManager,
                    cacheManager,
                    new RetryOptions(),
                    enableEndpointDiscovery,
                    isThinClientEnabled: false);

                DocumentServiceRequest request2 = this.CreateRequest(isReadRequest: true, isMasterResourceType: false);
                request2.RequestContext.ResolvedPartitionKeyRange = pkRange;
                retryPolicy2.OnBeforeSendRequest(request2);

                // Simulate a 403/3 (WriteForbidden) on a read request
                // With hub enabled, this would enter the hub read-path handler.
                // With hub disabled, addHubRegionProcessingOnlyHeader is false, so it falls through
                // to the normal write-path handler which calls ShouldRetryOnEndpointFailureAsync.
                DocumentClientException writeForbidden = new DocumentClientException(
                    message: "403/3 WriteForbidden",
                    innerException: null,
                    statusCode: HttpStatusCode.Forbidden,
                    substatusCode: SubStatusCodes.WriteForbidden,
                    requestUri: ClientRetryPolicyTests.Location1Endpoint,
                    responseHeaders: new DictionaryNameValueCollection());

                ShouldRetryResult retry403 = await retryPolicy2.ShouldRetryAsync(writeForbidden, CancellationToken.None);
                // The write-path handler triggers endpoint rediscovery — it should retry
                Assert.IsTrue(retry403.ShouldRetry,
                    "403/3 on read with hub disabled should follow write-path handler (endpoint rediscovery), not hub read-path.");

                // And hub header must still NOT be present
                retryPolicy2.OnBeforeSendRequest(request2);
                Assert.IsNull(request2.Headers.GetValues(HubRegionHeader),
                    "Hub header must NOT appear even after 403/3 when hub region processing is disabled.");
            }
            finally
            {
                // Restore original env var value
                Environment.SetEnvironmentVariable(ConfigurationManager.HubRegionProcessingEnabled, originalValue);
            }
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

            ClientRetryPolicy retryPolicy = new ClientRetryPolicy(mockDocumentClientContext.GlobalEndpointManager, this.partitionKeyRangeLocationCache, new RetryOptions(), enableEndpointDiscovery: true, isThinClientEnabled: false);

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
