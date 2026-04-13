//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
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
        public async Task ShouldRetryAsync_With503_FirstAttempt_ShouldMarkUnavailableAndRetry(
            bool useResponseMessage)
        {
            // Arrange — first regional failure should mark endpoint unavailable
            // and retry on the next preferred region.
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
                gem => gem.MarkEndpointUnavailableForRead(primaryServiceEndpoint),
                Times.Once,
                "The failing endpoint should be marked unavailable for reads.");
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
                gem => gem.MarkEndpointUnavailableForRead(primaryServiceEndpoint),
                Times.Exactly(2),
                "Endpoint should be marked unavailable on every regional failure.");
        }

        [TestMethod]
        [Owner("dkunda")]
        [DataRow((int)HttpStatusCode.InternalServerError, (int)SubStatusCodes.Unknown, DisplayName = "500 InternalServerError")]
        [DataRow((int)HttpStatusCode.Gone, (int)SubStatusCodes.LeaseNotFound, DisplayName = "410/LeaseNotFound")]
        [DataRow((int)HttpStatusCode.Forbidden, (int)SubStatusCodes.DatabaseAccountNotFound, DisplayName = "403/DatabaseAccountNotFound")]
        public async Task ShouldRetryAsync_WithRegionalFailureStatusCodes_ShouldMarkUnavailableAndRetry(
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
                gem => gem.MarkEndpointUnavailableForRead(primaryServiceEndpoint),
                Times.Once,
                "Endpoint should be marked unavailable on regional failure.");
        }

        [TestMethod]
        [Owner("dkunda")]
        public async Task ShouldRetryAsync_WithHttpRequestException_ShouldMarkUnavailableAndRetry()
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
                gem => gem.MarkEndpointUnavailableForRead(primaryServiceEndpoint),
                Times.Once,
                "Endpoint should be marked unavailable when HttpRequestException indicates the region is unreachable.");
        }

        [TestMethod]
        [Owner("dkunda")]
        public async Task ShouldRetryAsync_WithNonUserOperationCanceledException_ShouldMarkUnavailableAndRetry()
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
                gem => gem.MarkEndpointUnavailableForRead(primaryServiceEndpoint),
                Times.Once,
                "Endpoint should be marked unavailable when a non-user OperationCanceledException occurs.");
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
    }
}