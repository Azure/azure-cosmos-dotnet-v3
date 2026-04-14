//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class InferenceServiceTests
    {
        private static readonly Uri TestEndpoint = new Uri("https://test.dbinference.azure.com/inference/semanticReranking");

        [TestMethod]
        public async Task SemanticRerankAsync_BadRequest_ThrowsCosmosExceptionWithResponseBody()
        {
            string expectedErrorBody = "{\"error\":{\"code\":\"BadRequest\",\"message\":\"Invalid document format in request.\"}}";

            MockMessageHandler mockHandler = new MockMessageHandler(
                HttpStatusCode.BadRequest,
                expectedErrorBody);

            Mock<AuthorizationTokenProvider> mockAuth = InferenceServiceTests.CreateMockAuthorizationTokenProvider();

            using InferenceService service = new InferenceService(mockHandler, TestEndpoint, mockAuth.Object);

            CosmosException exception = await Assert.ThrowsExceptionAsync<CosmosException>(
                () => service.SemanticRerankAsync(
                    rerankContext: "test query",
                    documents: new List<string> { "doc1", "doc2" }));

            Assert.AreEqual(HttpStatusCode.BadRequest, exception.StatusCode);
            Assert.IsTrue(
                exception.Message.Contains("Invalid document format in request."),
                $"Exception message should contain the response body details. Actual: {exception.Message}");
            Assert.AreEqual(expectedErrorBody, exception.ResponseBody);
        }

        [TestMethod]
        public async Task SemanticRerankAsync_InternalServerError_ThrowsCosmosExceptionWithResponseBody()
        {
            string expectedErrorBody = "{\"error\":{\"code\":\"InternalError\",\"message\":\"An unexpected error occurred.\"}}";

            MockMessageHandler mockHandler = new MockMessageHandler(
                HttpStatusCode.InternalServerError,
                expectedErrorBody);

            Mock<AuthorizationTokenProvider> mockAuth = InferenceServiceTests.CreateMockAuthorizationTokenProvider();

            using InferenceService service = new InferenceService(mockHandler, TestEndpoint, mockAuth.Object);

            CosmosException exception = await Assert.ThrowsExceptionAsync<CosmosException>(
                () => service.SemanticRerankAsync(
                    rerankContext: "test query",
                    documents: new List<string> { "doc1", "doc2" }));

            Assert.AreEqual(HttpStatusCode.InternalServerError, exception.StatusCode);
            Assert.IsTrue(
                exception.Message.Contains("An unexpected error occurred."),
                $"Exception message should contain the response body details. Actual: {exception.Message}");
            Assert.AreEqual(expectedErrorBody, exception.ResponseBody);
        }

        [TestMethod]
        public async Task SemanticRerankAsync_SuccessResponse_ReturnsResult()
        {
            string successBody = "{\"Scores\":[{\"document\":\"doc1\",\"score\":0.95,\"index\":0}],\"latency\":{\"total_ms\":10},\"token_usage\":{\"total\":100}}";

            MockMessageHandler mockHandler = new MockMessageHandler(
                HttpStatusCode.OK,
                successBody);

            Mock<AuthorizationTokenProvider> mockAuth = InferenceServiceTests.CreateMockAuthorizationTokenProvider();

            using InferenceService service = new InferenceService(mockHandler, TestEndpoint, mockAuth.Object);

            SemanticRerankResult result = await service.SemanticRerankAsync(
                rerankContext: "test query",
                documents: new List<string> { "doc1", "doc2" });

            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.RerankScores.Count);
            Assert.AreEqual(0.95, result.RerankScores[0].Score);
            Assert.AreEqual(0, result.RerankScores[0].Index);
        }

        [TestMethod]
        public async Task SemanticRerankAsync_Timeout_ThrowsRequestTimeoutException()
        {
            // DelayedMessageHandler delays for 10 seconds, but the default inference timeout is 5 seconds
            DelayedMessageHandler delayedHandler = new DelayedMessageHandler(
                delay: TimeSpan.FromSeconds(10),
                statusCode: HttpStatusCode.OK,
                responseContent: "{}");

            Mock<AuthorizationTokenProvider> mockAuth = InferenceServiceTests.CreateMockAuthorizationTokenProvider();

            using InferenceService service = new InferenceService(delayedHandler, TestEndpoint, mockAuth.Object);

            CosmosException exception = await Assert.ThrowsExceptionAsync<CosmosException>(
                () => service.SemanticRerankAsync(
                    rerankContext: "test query",
                    documents: new List<string> { "doc1", "doc2" }));

            Assert.AreEqual(HttpStatusCode.RequestTimeout, exception.StatusCode);
            Assert.IsTrue(
                exception.Message.Contains("Inference Service Request Timeout"),
                $"Expected timeout message. Actual: {exception.Message}");
        }

        private static Mock<AuthorizationTokenProvider> CreateMockAuthorizationTokenProvider()
        {
            Mock<AuthorizationTokenProvider> mockAuth = new Mock<AuthorizationTokenProvider>();
            mockAuth.Setup(a => a.AddAuthorizationHeaderAsync(
                    It.IsAny<INameValueCollection>(),
                    It.IsAny<Uri>(),
                    It.IsAny<string>(),
                    It.IsAny<AuthorizationTokenType>()))
                .Returns(new ValueTask());
            return mockAuth;
        }

        /// <summary>
        /// A simple HttpMessageHandler mock that returns a fixed response.
        /// </summary>
        private class MockMessageHandler : HttpMessageHandler
        {
            private readonly HttpStatusCode statusCode;
            private readonly string responseContent;

            public MockMessageHandler(HttpStatusCode statusCode, string responseContent)
            {
                this.statusCode = statusCode;
                this.responseContent = responseContent;
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                HttpResponseMessage response = new HttpResponseMessage(this.statusCode)
                {
                    Content = new StringContent(this.responseContent, Encoding.UTF8, "application/json")
                };

                return Task.FromResult(response);
            }
        }

        /// <summary>
        /// An HttpMessageHandler that introduces a configurable delay before responding,
        /// used to test timeout behavior without fault injection.
        /// </summary>
        private class DelayedMessageHandler : HttpMessageHandler
        {
            private readonly TimeSpan delay;
            private readonly HttpStatusCode statusCode;
            private readonly string responseContent;

            public DelayedMessageHandler(TimeSpan delay, HttpStatusCode statusCode, string responseContent)
            {
                this.delay = delay;
                this.statusCode = statusCode;
                this.responseContent = responseContent;
            }

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                await Task.Delay(this.delay, cancellationToken);

                HttpResponseMessage response = new HttpResponseMessage(this.statusCode)
                {
                    Content = new StringContent(this.responseContent, Encoding.UTF8, "application/json")
                };

                return response;
            }
        }
    }
}
