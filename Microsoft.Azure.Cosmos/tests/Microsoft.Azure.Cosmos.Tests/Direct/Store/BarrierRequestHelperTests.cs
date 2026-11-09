//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Reflection;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    /// <summary>
    /// Test for <see cref="IDocumentClient"/> class.
    /// </summary>
    [TestClass]
    public class BarrierRequestHelperTests
    {
        /// <summary>
        /// Verifies default delays between barrier requests in ConsistencyWriter
        /// </summary>
        [TestMethod]
        [Owner("fabianm")]
        public void ValidateConsistencyWriterDefaultBarrierRequestDelays()
        {
            // Get default delays via reflection
            TimeSpan[] delays = (TimeSpan[])typeof(ConsistencyWriter)
                .GetField(
                    "defaultBarrierRequestDelays",
                    BindingFlags.NonPublic | BindingFlags.Static)
                .GetValue(null);

            Assert.AreEqual(34, delays.Length);
            Assert.AreEqual(delays[0], TimeSpan.FromMilliseconds(10));
            Assert.AreEqual(delays[3], TimeSpan.FromMilliseconds(10));
            Assert.AreEqual(delays[4], TimeSpan.FromMilliseconds(30));
            Assert.AreEqual(delays[33], TimeSpan.FromMilliseconds(30));
        }

        [TestMethod]
        [Owner("amudumba")]
        [DataRow(null, DisplayName = "SDKSupportedCapabilities header not set")]
        [DataRow("3", DisplayName = "SDKSupportedCapabilities header set")]
        public async Task ValidateSDKSupportedCapabilitiesHeaderIsCopied(string sdkCapabilitiesValue)
        {
            // Arrange
            Mock<IAuthorizationTokenProvider> mockAuthorizationTokenProvider = new Mock<IAuthorizationTokenProvider>();
            mockAuthorizationTokenProvider
                .Setup(provider => provider.GetUserAuthorizationAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<INameValueCollection>(),
                    It.IsAny<AuthorizationTokenType>()))
                .ReturnsAsync(("mockToken", "mockPayload"));

            DocumentServiceRequest originalRequest = DocumentServiceRequest.Create(
                OperationType.Create, ResourceType.Document,
                AuthorizationTokenType.PrimaryMasterKey);
            originalRequest.ResourceId = "1-MxAPlgMgA=";
            originalRequest.RequestContext = new DocumentServiceRequestContext();

            if (sdkCapabilitiesValue != null)
            {
                originalRequest.Headers[HttpConstants.HttpHeaders.SDKSupportedCapabilities] = sdkCapabilitiesValue;
            }

            // Act
            DocumentServiceRequest barrierRequest = await BarrierRequestHelper.CreateAsync(
                originalRequest,
                mockAuthorizationTokenProvider.Object,
                targetLsn: null,
                targetGlobalCommittedLsn: null,
                includeRegionContext: false);

            // Assert
            Assert.IsNotNull(barrierRequest);
            Assert.AreEqual(sdkCapabilitiesValue, barrierRequest.Headers[HttpConstants.HttpHeaders.SDKSupportedCapabilities]);
        }

        [TestMethod]
        [Owner("dikshibahl")]
        [DataRow(true, 1, null, DisplayName = "Include Region Context with Location Index")]
        [DataRow(false, 1, null, DisplayName = "Exclude Region Context with Location Index")]
        [DataRow(true, null, "https://mock-region.documents.azure.com", DisplayName = "Include Region Context with Uri")]
        [DataRow(false, null, "https://mock-region.documents.azure.com", DisplayName = "Exclude Region Context with Uri")]
        public async Task ValidateRegionContextBasedOnIncludeRegionContextFlag(bool includeRegionContext, int? locationIndex, string locationUri)
        {
            // Arrange
            Mock<IAuthorizationTokenProvider> mockAuthorizationTokenProvider = new Mock<IAuthorizationTokenProvider>();
            mockAuthorizationTokenProvider
                .Setup(provider => provider.GetUserAuthorizationAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<INameValueCollection>(),
                    It.IsAny<AuthorizationTokenType>()))
                .ReturnsAsync(("mockToken", "mockPayload"));

            DocumentServiceRequest originalRequest = DocumentServiceRequest.Create(
                OperationType.Create, ResourceType.Document,
                AuthorizationTokenType.PrimaryMasterKey);
            originalRequest.ResourceId = "1-MxAPlgMgA=";

            // Create and configure the RequestContext
            DocumentServiceRequestContext requestContext = new DocumentServiceRequestContext();
            if (includeRegionContext)
            {
                if (locationIndex.HasValue)
                {
                    requestContext.RouteToLocation(locationIndex.Value, usePreferredLocations: false); // Set location index
                }
                if (!string.IsNullOrEmpty(locationUri))
                {
                    requestContext.RouteToLocation(new Uri(locationUri)); // Set location endpoint
                }
            }

            originalRequest.RequestContext = requestContext;


            long? targetGlobalCommittedLsn = 67890;

            // Act
            DocumentServiceRequest barrierRequest = await BarrierRequestHelper.CreateAsync(
                originalRequest,
                mockAuthorizationTokenProvider.Object,
                null,
                targetGlobalCommittedLsn,
                includeRegionContext);

            // Assert
            Assert.IsNotNull(barrierRequest);
            Assert.AreEqual("mockToken", barrierRequest.Headers[HttpConstants.HttpHeaders.Authorization]);
            Assert.AreEqual(targetGlobalCommittedLsn.ToString(), barrierRequest.Headers[HttpConstants.HttpHeaders.TargetGlobalCommittedLsn]);
            if (includeRegionContext)
            {
                Assert.AreEqual(requestContext.LocationIndexToRoute, barrierRequest.RequestContext.LocationIndexToRoute);
                Assert.AreEqual(requestContext.LocationEndpointToRoute, barrierRequest.RequestContext.LocationEndpointToRoute);
            }
            else
            {
                Assert.IsNull(barrierRequest.RequestContext.LocationIndexToRoute);
                Assert.IsNull(barrierRequest.RequestContext.LocationEndpointToRoute);
            }

            mockAuthorizationTokenProvider.Verify(provider => provider.GetUserAuthorizationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<INameValueCollection>(),
                It.IsAny<AuthorizationTokenType>()), Times.Once);
        }

        /// <summary>
        /// Verifies default delays between barrier requests in QuorumReader
        /// </summary>
        [TestMethod]
        [Owner("fabianm")]
        public void ValidateQuorumReaderDefaultBarrierRequestDelays()
        {
            // Get default delays via reflection
            TimeSpan[] delays = (TimeSpan[])typeof(QuorumReader)
                .GetField(
                    "defaultBarrierRequestDelays",
                    BindingFlags.NonPublic | BindingFlags.Static)
                .GetValue(null);

            Assert.AreEqual(40, delays.Length);
            Assert.AreEqual(delays[0], TimeSpan.FromMilliseconds(5));
            Assert.AreEqual(delays[5], TimeSpan.FromMilliseconds(5));
            Assert.AreEqual(delays[6], TimeSpan.FromMilliseconds(10));
            Assert.AreEqual(delays[9], TimeSpan.FromMilliseconds(10));
            Assert.AreEqual(delays[10], TimeSpan.FromMilliseconds(30));
            Assert.AreEqual(delays[39], TimeSpan.FromMilliseconds(30));
        }

        /// <summary>
        /// Verifies logic determining delay between two barrier
        /// request in ConsistencyWriter and QuorumReader
        /// </summary>
        [TestMethod]
        [Owner("fabianm")]
        [DynamicData(nameof(GetValidateDelayBetweenBarrierRequestsTestData), DynamicDataSourceType.Method)]
        public void ValidateDelayBetweenBarrierRequests(
            TimeSpan previousHeadRequestLatency,
            Object statusCodesOfResponsesArray,
            TimeSpan minDelay,
            TimeSpan expectedDelay)
        {
            StatusCodePair[] statusCodesOfResponses = statusCodesOfResponsesArray as StatusCodePair[];
            ReferenceCountedDisposable<StoreResult>[] responses = new ReferenceCountedDisposable<StoreResult>[statusCodesOfResponses.Length];
            for (int i = 0; i < statusCodesOfResponses.Length; i++)
            {
                StoreResponse storeResponse = new StoreResponse()
                {
                    Status = (int)statusCodesOfResponses[i].StatusCode,
                };

                DictionaryNameValueCollection headers = new DictionaryNameValueCollection();
                if (statusCodesOfResponses[i].SubStatusCode.HasValue)
                {
                    headers[WFConstants.BackendHeaders.SubStatus] = ((int)statusCodesOfResponses[i].SubStatusCode.Value).ToString();
                }
                storeResponse.Headers = headers;

                responses[i] = StoreResult.CreateForTesting(storeResponse);
            }

            bool delayEnabled = BarrierRequestHelper.ShouldDelayBetweenHeadRequests(
                previousHeadRequestLatency,
                responses,
                minDelay,
                out TimeSpan actualDelay);

            Assert.AreEqual(expectedDelay > TimeSpan.Zero, delayEnabled);
            Assert.AreEqual(expectedDelay, actualDelay);
        }

        private static IEnumerable<object[]> GetValidateDelayBetweenBarrierRequestsTestData()
        {
            return new List<object[]>()
            {
                new object[]{
                    TimeSpan.Zero,
                    (Object)(new StatusCodePair[] { new StatusCodePair( HttpStatusCode.NoContent, SubStatusCodes.Unknown ) }),
                    TimeSpan.FromMilliseconds(30),
                    TimeSpan.FromMilliseconds(30) },
                new object[]{
                    TimeSpan.Zero,
                    (Object)(Array.Empty<StatusCodePair>()),
                    TimeSpan.FromMilliseconds(30),
                    TimeSpan.FromMilliseconds(30) },
                new object[]{
                    TimeSpan.Zero,
                    (Object)(new StatusCodePair[] { new StatusCodePair( HttpStatusCode.NoContent, null ) }),
                    TimeSpan.FromMilliseconds(30),
                    TimeSpan.FromMilliseconds(30) },
                new object[]{
                    TimeSpan.FromMilliseconds(100),
                    (Object)(new StatusCodePair[] { new StatusCodePair( HttpStatusCode.NoContent, null ) }),
                    TimeSpan.FromMilliseconds(30),
                    TimeSpan.FromMilliseconds(30) },
                new object[]{
                    TimeSpan.Zero,
                    (Object)(new StatusCodePair[] { new StatusCodePair( HttpStatusCode.NoContent, SubStatusCodes.MissedTargetGlobalCommittedLsnOver10000 ) }),
                    TimeSpan.FromMilliseconds(30),
                    TimeSpan.FromMilliseconds(1000) },
                new object[]{
                    TimeSpan.FromMilliseconds(700),
                    (Object)(new StatusCodePair[] { new StatusCodePair( HttpStatusCode.NoContent, SubStatusCodes.MissedTargetGlobalCommittedLsnOver10000 ) }),
                    TimeSpan.FromMilliseconds(30),
                    TimeSpan.FromMilliseconds(300) },
                new object[]{
                    TimeSpan.FromMilliseconds(700),
                    (Object)(new StatusCodePair[] { new StatusCodePair( HttpStatusCode.NoContent, SubStatusCodes.MissedTargetGlobalCommittedLsnOver1000 ) }),
                    TimeSpan.FromMilliseconds(30),
                    TimeSpan.FromMilliseconds(30) },
                new object[]{
                    TimeSpan.FromMilliseconds(700),
                    (Object)(new StatusCodePair[] { new StatusCodePair( HttpStatusCode.NoContent, SubStatusCodes.MissedTargetLsnOver10000 ) }),
                    TimeSpan.FromMilliseconds(8),
                    TimeSpan.FromMilliseconds(8) },
                new object[]{
                    TimeSpan.FromMilliseconds(10),
                    (Object)(new StatusCodePair[] { new StatusCodePair( HttpStatusCode.NoContent, SubStatusCodes.MissedTargetGlobalCommittedLsnOver1000 ) }),
                    TimeSpan.FromMilliseconds(30),
                    TimeSpan.FromMilliseconds(90) },
                new object[]{
                    TimeSpan.FromMilliseconds(1),
                    (Object)(new StatusCodePair[] { new StatusCodePair( HttpStatusCode.NoContent, SubStatusCodes.MissedTargetGlobalCommittedLsnOver100 ) }),
                    TimeSpan.FromMilliseconds(5),
                    TimeSpan.FromMilliseconds(9) },
            };
        }

        private struct StatusCodePair
        {
            public StatusCodePair(HttpStatusCode statusCode, SubStatusCodes? subStatusCode)
            {
                this.StatusCode = statusCode;
                this.SubStatusCode = subStatusCode;
            }

            public HttpStatusCode StatusCode;
            public SubStatusCodes? SubStatusCode;
        }
    }
}