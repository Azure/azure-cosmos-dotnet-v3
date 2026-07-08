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
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    /// <summary>
    /// Unit tests for <see cref="MetadataRequestThrottleRetryPolicy"/>.
    /// </summary>
    [TestClass]
    public class MetadataRequestThrottleRetryPolicyTests
    {
        private static Mock<IGlobalEndpointManager> CreateMockEndpointManager(Uri primaryEndpoint)
        {
            Mock<IGlobalEndpointManager> mock = new();
            mock.Setup(gem => gem.ResolveServiceEndpoint(It.IsAny<DocumentServiceRequest>()))
                .Returns(primaryEndpoint);
            // Default PreferredLocationCount = 0 → maxRetries = Max(1,0) = 1
            mock.Setup(gem => gem.PreferredLocationCount).Returns(0);
            return mock;
        }

        /// <summary>
        /// Creates a real (non-mocked) <see cref="GlobalEndpointManager"/> backed by a real
        /// <see cref="LocationCache"/>, so that <see cref="IGlobalEndpointManager.ResolveServiceEndpoint"/>
        /// faithfully resolves endpoints from the actual preferred-location list, the way it does in
        /// production. Used to verify the actual endpoint sequence resolved across retries — and that
        /// the shared read-endpoint ordering is left unmodified — rather than a mocked call-count-based
        /// sequence.
        /// </summary>
        private static GlobalEndpointManager CreateRealGlobalEndpointManager(
            IReadOnlyList<Uri> regionEndpoints,
            IReadOnlyList<string> regionNames)
        {
            Collection<AccountRegion> readLocations = new();
            for (int i = 0; i < regionEndpoints.Count; i++)
            {
                readLocations.Add(new AccountRegion() { Name = regionNames[i], Endpoint = regionEndpoints[i].ToString() });
            }

            AccountProperties databaseAccount = new()
            {
                EnableMultipleWriteLocations = false,
                ReadLocationsInternal = readLocations,
                WriteLocationsInternal = new Collection<AccountRegion>() { readLocations[0] },
            };

            Mock<IDocumentClientInternal> mockedClient = new();
            mockedClient.Setup(owner => owner.ServiceEndpoint).Returns(regionEndpoints[0]);
            mockedClient.Setup(owner => owner.GetDatabaseAccountInternalAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(databaseAccount);

            ConnectionPolicy connectionPolicy = new()
            {
                EnableEndpointDiscovery = true,
                UseMultipleWriteLocations = false,
            };
            foreach (string regionName in regionNames)
            {
                connectionPolicy.PreferredLocations.Add(regionName);
            }

            GlobalEndpointManager endpointManager = new(mockedClient.Object, connectionPolicy);
            endpointManager.InitializeAccountPropertiesAndStartBackgroundRefresh(databaseAccount);
            return endpointManager;
        }

        private static DocumentServiceRequest CreatePkRangesRequest(string collectionRid = "test-collection")
        {
            Documents.Collections.INameValueCollection headers = new Documents.Collections.RequestNameValueCollection();
            headers.Set(HttpConstants.HttpHeaders.PageSize, "10");
            headers.Set(HttpConstants.HttpHeaders.A_IM, HttpConstants.A_IMHeaderValues.IncrementalFeed);

            return DocumentServiceRequest.Create(
                OperationType.ReadFeed,
                collectionRid,
                Documents.ResourceType.PartitionKeyRange,
                AuthorizationTokenType.PrimaryMasterKey,
                headers);
        }

        [TestMethod]
        [Owner("dkunda")]
        [DataRow(true, DisplayName = "Test with a 503 response message.")]
        [DataRow(false, DisplayName = "Test with a 503 CosmosException.")]
        public async Task ShouldRetryAsync_With503_FirstAttempt_ShouldRetryOnNextRegionWithoutMarkingEndpointUnavailable(
            bool useResponseMessage)
        {
            // Arrange — first regional failure should retry on the next preferred region,
            // without marking the failing endpoint unavailable in the shared LocationCache
            // (that would deprioritize the region for ALL read traffic, not just metadata).
            ShouldRetryResult retryResult;
            Uri primaryServiceEndpoint = new("https://default-endpoint-region1.net/");

            DocumentServiceRequest request = CreatePkRangesRequest();

            Mock<IGlobalEndpointManager> mockedGlobalEndpointManager = CreateMockEndpointManager(
                primaryServiceEndpoint);

            MetadataRequestThrottleRetryPolicy policy = new(mockedGlobalEndpointManager.Object, 0);
            policy.OnBeforeSendRequest(request);

            // Act.
            if (useResponseMessage)
            {
                Headers responseHeaders = new()
                {
                    SubStatusCode = SubStatusCodes.TransportGenerated503
                };

                ResponseMessage responseMessage = new(
                    statusCode: HttpStatusCode.ServiceUnavailable,
                    requestMessage: null,
                    headers: responseHeaders,
                    cosmosException: null,
                    trace: NoOpTrace.Singleton);

                retryResult = await policy.ShouldRetryAsync(responseMessage, default);
            }
            else
            {
                CosmosException exception = CosmosExceptionFactory.CreateServiceUnavailableException(
                                                message: "Service Unavailable at the moment.",
                                                headers: new Headers()
                                                {
                                                    ActivityId = System.Diagnostics.Trace.CorrelationManager.ActivityId.ToString(),
                                                    SubStatusCode = SubStatusCodes.TransportGenerated503
                                                },
                                                trace: NoOpTrace.Singleton,
                                                innerException: null);

                retryResult = await policy.ShouldRetryAsync(exception, default);
            }

            // Assert — should retry in another region.
            Assert.IsNotNull(retryResult);
            Assert.IsTrue(
                retryResult.ShouldRetry,
                "First regional failure should retry on the next preferred region.");

            mockedGlobalEndpointManager.Verify(
                gem => gem.MarkEndpointUnavailableForRead(It.IsAny<Uri>()),
                Times.Never,
                "MetadataRequestThrottleRetryPolicy should not mark the region unavailable — that would affect all read traffic, not just this metadata request.");
        }

        [TestMethod]
        [Owner("dkunda")]
        public async Task ShouldRetryAsync_With503_AfterExhaustingRetries_ShouldReturnNoRetry()
        {
            // Arrange — exhaust the metadata-level retries so the exception
            // propagates to the operation-level retry policy.
            Uri primaryServiceEndpoint = new("https://default-endpoint-region1.net/");

            DocumentServiceRequest request = CreatePkRangesRequest();

            Mock<IGlobalEndpointManager> mockedGlobalEndpointManager = CreateMockEndpointManager(
                primaryServiceEndpoint);

            MetadataRequestThrottleRetryPolicy policy = new(mockedGlobalEndpointManager.Object, 0);
            policy.OnBeforeSendRequest(request);

            CosmosException exception = CosmosExceptionFactory.CreateServiceUnavailableException(
                                            message: "Service Unavailable at the moment.",
                                            headers: new Headers()
                                            {
                                                ActivityId = System.Diagnostics.Trace.CorrelationManager.ActivityId.ToString(),
                                                SubStatusCode = SubStatusCodes.TransportGenerated503
                                            },
                                            trace: NoOpTrace.Singleton,
                                            innerException: null);

            // Act — first call retries, second call exhausts the budget.
            ShouldRetryResult firstResult = await policy.ShouldRetryAsync(exception, default);
            Assert.IsTrue(firstResult.ShouldRetry, "First attempt should retry.");

            policy.OnBeforeSendRequest(request);
            ShouldRetryResult secondResult = await policy.ShouldRetryAsync(exception, default);

            // Assert.
            Assert.IsNotNull(secondResult);
            Assert.IsFalse(
                secondResult.ShouldRetry,
                "After exhausting retries, should return NoRetry so the exception propagates to the operation-level retry policy.");

            mockedGlobalEndpointManager.Verify(
                gem => gem.MarkEndpointUnavailableForRead(It.IsAny<Uri>()),
                Times.Never,
                "MetadataRequestThrottleRetryPolicy should not mark any endpoint unavailable, on any regional failure.");
        }

        [TestMethod]
        [Owner("dkunda")]
        [DataRow((int)HttpStatusCode.InternalServerError, (int)SubStatusCodes.Unknown, DisplayName = "500 InternalServerError")]
        [DataRow((int)HttpStatusCode.Gone, (int)SubStatusCodes.LeaseNotFound, DisplayName = "410/LeaseNotFound")]
        [DataRow((int)HttpStatusCode.Forbidden, (int)SubStatusCodes.DatabaseAccountNotFound, DisplayName = "403/DatabaseAccountNotFound")]
        public async Task ShouldRetryAsync_WithRegionalFailureStatusCodes_ShouldRetryOnNextRegionWithoutMarkingEndpointUnavailable(
            int statusCode,
            int subStatusCode)
        {
            // Arrange.
            Uri primaryServiceEndpoint = new("https://default-endpoint-region1.net/");

            DocumentServiceRequest request = CreatePkRangesRequest();

            Mock<IGlobalEndpointManager> mockedGlobalEndpointManager = CreateMockEndpointManager(
                primaryServiceEndpoint);

            MetadataRequestThrottleRetryPolicy policy = new(mockedGlobalEndpointManager.Object, 0);
            policy.OnBeforeSendRequest(request);

            Headers responseHeaders = new()
            {
                SubStatusCode = (SubStatusCodes)subStatusCode
            };

            ResponseMessage responseMessage = new(
                statusCode: (HttpStatusCode)statusCode,
                requestMessage: null,
                headers: responseHeaders,
                cosmosException: null,
                trace: NoOpTrace.Singleton);

            // Act.
            ShouldRetryResult retryResult = await policy.ShouldRetryAsync(responseMessage, default);

            // Assert — first failure should retry.
            Assert.IsNotNull(retryResult);
            Assert.IsTrue(retryResult.ShouldRetry,
                "Regional failure status codes should retry on the next preferred region.");

            mockedGlobalEndpointManager.Verify(
                gem => gem.MarkEndpointUnavailableForRead(It.IsAny<Uri>()),
                Times.Never,
                "MetadataRequestThrottleRetryPolicy should not mark the region unavailable on regional failure.");
        }

        [TestMethod]
        [Owner("dkunda")]
        public async Task ShouldRetryAsync_WithHttpRequestException_ShouldRetryOnNextRegionWithoutMarkingEndpointUnavailable()
        {
            // Arrange.
            Uri primaryServiceEndpoint = new("https://default-endpoint-region1.net/");

            DocumentServiceRequest request = CreatePkRangesRequest();

            Mock<IGlobalEndpointManager> mockedGlobalEndpointManager = CreateMockEndpointManager(
                primaryServiceEndpoint);

            MetadataRequestThrottleRetryPolicy policy = new(mockedGlobalEndpointManager.Object, 0);
            policy.OnBeforeSendRequest(request);

            // Act.
            HttpRequestException httpException = new("Connection refused");
            ShouldRetryResult retryResult = await policy.ShouldRetryAsync(httpException, default);

            // Assert — first failure should retry.
            Assert.IsNotNull(retryResult);
            Assert.IsTrue(retryResult.ShouldRetry,
                "HttpRequestException should retry on the next preferred region.");

            mockedGlobalEndpointManager.Verify(
                gem => gem.MarkEndpointUnavailableForRead(It.IsAny<Uri>()),
                Times.Never,
                "MetadataRequestThrottleRetryPolicy should not mark the region unavailable when HttpRequestException indicates the region is unreachable.");
        }

        [TestMethod]
        [Owner("dkunda")]
        public async Task ShouldRetryAsync_WithNonUserOperationCanceledException_ShouldRetryOnNextRegionWithoutMarkingEndpointUnavailable()
        {
            // Arrange.
            Uri primaryServiceEndpoint = new("https://default-endpoint-region1.net/");

            DocumentServiceRequest request = CreatePkRangesRequest();

            Mock<IGlobalEndpointManager> mockedGlobalEndpointManager = CreateMockEndpointManager(
                primaryServiceEndpoint);

            MetadataRequestThrottleRetryPolicy policy = new(mockedGlobalEndpointManager.Object, 0);
            policy.OnBeforeSendRequest(request);

            // Act — use a non-cancelled token so this is NOT user-initiated cancellation.
            OperationCanceledException oce = new("Request timed out");
            ShouldRetryResult retryResult = await policy.ShouldRetryAsync(oce, CancellationToken.None);

            // Assert — first failure should retry.
            Assert.IsNotNull(retryResult);
            Assert.IsTrue(retryResult.ShouldRetry,
                "Non-user OperationCanceledException should retry on the next preferred region.");

            mockedGlobalEndpointManager.Verify(
                gem => gem.MarkEndpointUnavailableForRead(It.IsAny<Uri>()),
                Times.Never,
                "MetadataRequestThrottleRetryPolicy should not mark the region unavailable when a non-user OperationCanceledException occurs.");
        }

        [TestMethod]
        [Owner("dkunda")]
        public async Task ShouldRetryAsync_WithUserCancelledOperationCanceledException_ShouldDelegateToThrottlingPolicy()
        {
            // Arrange.
            Uri primaryServiceEndpoint = new("https://default-endpoint-region1.net/");

            DocumentServiceRequest request = CreatePkRangesRequest();

            Mock<IGlobalEndpointManager> mockedGlobalEndpointManager = CreateMockEndpointManager(
                primaryServiceEndpoint);

            MetadataRequestThrottleRetryPolicy policy = new(mockedGlobalEndpointManager.Object, 0);
            policy.OnBeforeSendRequest(request);

            // Act — use a cancelled token to simulate user-initiated cancellation.
            using CancellationTokenSource cts = new();
            cts.Cancel();
            OperationCanceledException oce = new("User cancelled", cts.Token);
            ShouldRetryResult retryResult = await policy.ShouldRetryAsync(oce, cts.Token);

            // Assert.
            Assert.IsNotNull(retryResult);
            Assert.IsFalse(retryResult.ShouldRetry,
                "User-cancelled OperationCanceledException should not retry.");

            mockedGlobalEndpointManager.Verify(
                gem => gem.MarkEndpointUnavailableForRead(It.IsAny<Uri>()),
                Times.Never,
                "Endpoint should NOT be marked unavailable for user-initiated cancellations.");
        }

        [TestMethod]
        [Owner("dkunda")]
        public async Task ShouldRetryAsync_WithThrottling429_ShouldDelegateToThrottlingRetryPolicy()
        {
            // Arrange.
            Uri primaryServiceEndpoint = new("https://default-endpoint-region1.net/");

            DocumentServiceRequest request = CreatePkRangesRequest();

            Mock<IGlobalEndpointManager> mockedGlobalEndpointManager = CreateMockEndpointManager(
                primaryServiceEndpoint);

            MetadataRequestThrottleRetryPolicy policy = new(mockedGlobalEndpointManager.Object, 3);
            policy.OnBeforeSendRequest(request);

            Headers responseHeaders = new()
            {
                SubStatusCode = SubStatusCodes.Unknown
            };

            ResponseMessage responseMessage = new(
                statusCode: HttpStatusCode.TooManyRequests,
                requestMessage: null,
                headers: responseHeaders,
                cosmosException: null,
                trace: NoOpTrace.Singleton);

            // Act.
            ShouldRetryResult retryResult = await policy.ShouldRetryAsync(responseMessage, default);

            // Assert.
            Assert.IsNotNull(retryResult);
            Assert.IsTrue(retryResult.ShouldRetry,
                "429 throttling should be handled by the underlying throttling retry policy and should retry.");

            mockedGlobalEndpointManager.Verify(
                gem => gem.MarkEndpointUnavailableForRead(It.IsAny<Uri>()),
                Times.Never,
                "Endpoint should NOT be marked unavailable for throttling — this is not a regional failure.");
        }

        [TestMethod]
        [Owner("dkunda")]
        public async Task ShouldRetryAsync_With503_MultipleRegions_ShouldCycleThroughRegionsAndExhaust()
        {
            // Arrange — simulate 3 preferred regions. Each region fails with 503,
            // and the policy should cycle through all regions before exhausting retries.
            Uri region1 = new("https://region1.documents.azure.com/");
            Uri region2 = new("https://region2.documents.azure.com/");
            Uri region3 = new("https://region3.documents.azure.com/");

            DocumentServiceRequest request = CreatePkRangesRequest();

            int resolveCallCount = 0;
            Mock<IGlobalEndpointManager> mockedGlobalEndpointManager = new();
            mockedGlobalEndpointManager.Setup(gem => gem.ResolveServiceEndpoint(It.IsAny<DocumentServiceRequest>()))
                .Returns(() =>
                {
                    resolveCallCount++;
                    return resolveCallCount switch
                    {
                        1 => region1,
                        2 => region2,
                        3 => region3,
                        _ => region3,
                    };
                });
            // 3 preferred locations → maxRetries = Max(1, 3) = 3
            mockedGlobalEndpointManager.Setup(gem => gem.PreferredLocationCount).Returns(3);

            MetadataRequestThrottleRetryPolicy policy = new(mockedGlobalEndpointManager.Object, 0);

            CosmosException exception = CosmosExceptionFactory.CreateServiceUnavailableException(
                message: "Service Unavailable",
                headers: new Headers()
                {
                    ActivityId = System.Diagnostics.Trace.CorrelationManager.ActivityId.ToString(),
                    SubStatusCode = SubStatusCodes.TransportGenerated503
                },
                trace: NoOpTrace.Singleton,
                innerException: null);

            // Act & Assert — cycle through 3 regions, each returning ShouldRetry.
            policy.OnBeforeSendRequest(request);
            ShouldRetryResult result1 = await policy.ShouldRetryAsync(exception, default);
            Assert.IsTrue(result1.ShouldRetry, "First failure (region1) should retry.");

            policy.OnBeforeSendRequest(request);
            ShouldRetryResult result2 = await policy.ShouldRetryAsync(exception, default);
            Assert.IsTrue(result2.ShouldRetry, "Second failure (region2) should retry.");

            policy.OnBeforeSendRequest(request);
            ShouldRetryResult result3 = await policy.ShouldRetryAsync(exception, default);
            Assert.IsTrue(result3.ShouldRetry, "Third failure (region3) should retry.");

            // 4th attempt — retries exhausted, should return NoRetry.
            policy.OnBeforeSendRequest(request);
            ShouldRetryResult result4 = await policy.ShouldRetryAsync(exception, default);
            Assert.IsFalse(result4.ShouldRetry,
                "After exhausting all 3 regions, should return NoRetry so exception propagates to operation-level retry.");

            mockedGlobalEndpointManager.Verify(
                gem => gem.MarkEndpointUnavailableForRead(It.IsAny<Uri>()),
                Times.Never,
                "MetadataRequestThrottleRetryPolicy should never mark any endpoint unavailable — only its local retry-location index advances.");
        }

        /// <summary>
        /// Regression test using a REAL <see cref="LocationCache"/> (via <see cref="GlobalEndpointManager"/>),
        /// instead of a mock. Verifies two things: (1) the policy still visits every preferred region
        /// exactly once across its first N retries by advancing its local retry-location index — no
        /// healthy region skipped, no region revisited early — and (2) it does so WITHOUT calling
        /// <see cref="IGlobalEndpointManager.MarkEndpointUnavailableForRead"/>, so the shared
        /// <see cref="LocationCache"/> read-endpoint ordering is left completely unchanged afterward
        /// (i.e., no side effect on unrelated document/query traffic to the same account).
        /// See PR #5780 review discussion: an earlier version of this policy called
        /// <c>MarkEndpointUnavailableForRead</c>, which deprioritizes the region for ALL reads for up
        /// to 5 minutes — a disproportionate blast radius for a single metadata-request failure.
        /// </summary>
        [TestMethod]
        [Owner("dkunda")]
        public async Task ShouldRetryAsync_With503_MultipleRegions_RealLocationCache_VisitsEveryRegionOnceWithoutMarkingUnavailable()
        {
            // Arrange — 3 preferred regions backed by a real LocationCache/GlobalEndpointManager.
            Uri region1 = new("https://location1.documents.azure.com/");
            Uri region2 = new("https://location2.documents.azure.com/");
            Uri region3 = new("https://location3.documents.azure.com/");

            using GlobalEndpointManager endpointManager = CreateRealGlobalEndpointManager(
                regionEndpoints: new[] { region1, region2, region3 },
                regionNames: new[] { "location1", "location2", "location3" });

            ReadOnlyCollection<Uri> readEndpointsBeforeFailures = endpointManager.ReadEndpoints;

            DocumentServiceRequest request = CreatePkRangesRequest();

            // 3 preferred locations → maxRetries = Max(1, 3) = 3
            MetadataRequestThrottleRetryPolicy policy = new(endpointManager, 0);

            CosmosException exception = CosmosExceptionFactory.CreateServiceUnavailableException(
                message: "Service Unavailable",
                headers: new Headers()
                {
                    ActivityId = System.Diagnostics.Trace.CorrelationManager.ActivityId.ToString(),
                    SubStatusCode = SubStatusCodes.TransportGenerated503
                },
                trace: NoOpTrace.Singleton,
                innerException: null);

            List<Uri> resolvedSequence = new();

            // Act — retry until the policy exhausts its budget, recording the endpoint used on each attempt.
            ShouldRetryResult retryResult;
            int attempts = 0;
            const int maxAttempts = 10; // safety bound; a real bug (infinite loop) would trip this.
            do
            {
                policy.OnBeforeSendRequest(request);
                resolvedSequence.Add(endpointManager.ResolveServiceEndpoint(request));

                retryResult = await policy.ShouldRetryAsync(exception, default);
                attempts++;
            }
            while (retryResult.ShouldRetry && attempts < maxAttempts);

            Assert.IsFalse(retryResult.ShouldRetry, "Retries should eventually be exhausted.");

            // Assert — the first 3 attempts (one per distinct region) must visit every preferred
            // region exactly once: no healthy region skipped, no failed region revisited early.
            CollectionAssert.AreEquivalent(
                new[] { region1, region2, region3 },
                resolvedSequence.Take(3).ToArray(),
                "The first 3 attempts should visit every preferred region exactly once — none skipped, none revisited early.");

            // Assert — the shared LocationCache's read-endpoint ordering is completely unaffected by
            // these metadata-level failures, proving no global markdown side effect leaked out to
            // affect unrelated (e.g. document/query) traffic.
            CollectionAssert.AreEqual(
                readEndpointsBeforeFailures,
                endpointManager.ReadEndpoints,
                "MetadataRequestThrottleRetryPolicy must not mutate the shared LocationCache's read-endpoint ordering.");
        }
    }
}