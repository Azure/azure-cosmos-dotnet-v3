//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tests;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    /// <summary>
    /// Tests for <see cref="CancellationToken"/>  scenarios.
    /// </summary>
    [TestClass]
    public class CancellationTokenTests
    {
        [TestMethod]
        [ExpectedException(typeof(OperationCanceledException))]
        public async Task GatewayProcessMessageAsyncCancels()
        {
            using (CancellationTokenSource source = new CancellationTokenSource())
            {
                CancellationToken cancellationToken = source.Token;

                int run = 0;
                Func<HttpRequestMessage, Task<HttpResponseMessage>> sendFunc = async request =>
                {
                    string content = await request.Content.ReadAsStringAsync();
                    Assert.AreEqual("content1", content);

                    if (run == 0)
                    {
                        // We force a retry but cancel the token to verify if the retry mechanism cancels inbetween
                        source.Cancel();
                        throw new WebException("", WebExceptionStatus.ConnectFailure);
                    }

                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("Response") };
                };

                Mock<IDocumentClientInternal> mockDocumentClient = new Mock<IDocumentClientInternal>();
                mockDocumentClient.Setup(client => client.ServiceEndpoint).Returns(new Uri("https://foo"));

                GlobalEndpointManager endpointManager = new GlobalEndpointManager(mockDocumentClient.Object, new ConnectionPolicy());
                ISessionContainer sessionContainer = new SessionContainer(string.Empty);
                DocumentClientEventSource eventSource = DocumentClientEventSource.Instance;
                HttpMessageHandler messageHandler = new MockMessageHandler(sendFunc);
                GatewayStoreModel storeModel = new GatewayStoreModel(
                    endpointManager,
                    sessionContainer,
                    ConsistencyLevel.Eventual,
                    eventSource,
                    null,
                    MockCosmosUtil.CreateCosmosHttpClient(
                        () => new HttpClient(messageHandler),
                        eventSource));

                using (new ActivityScope(Guid.NewGuid()))
                {
                    using (DocumentServiceRequest request =
                    DocumentServiceRequest.Create(
                        Documents.OperationType.Query,
                        Documents.ResourceType.Document,
                        new Uri("https://foo.com/dbs/db1/colls/coll1", UriKind.Absolute),
                        new MemoryStream(Encoding.UTF8.GetBytes("content1")),
                        AuthorizationTokenType.PrimaryMasterKey,
                        null))
                    {
                        await storeModel.ProcessMessageAsync(request, cancellationToken);
                    }
                }

                Assert.Fail();
            }
        }

        [TestMethod]
        [ExpectedException(typeof(OperationCanceledException))]
        public async Task GatewayProcessMessageAsyncCancelsOnDeadline()
        {
            // Cancellation deadline is before Request timeout
            using (CancellationTokenSource source = new CancellationTokenSource(TimeSpan.FromSeconds(2)))
            {
                CancellationToken cancellationToken = source.Token;

                Func<HttpRequestMessage, Task<HttpResponseMessage>> sendFunc = async request =>
                {
                    string content = await request.Content.ReadAsStringAsync();
                    Assert.AreEqual("content1", content);

                    // Wait until CancellationTokenSource deadline expires
                    Thread.Sleep(2000);
                    // Force retries
                    throw new WebException("", WebExceptionStatus.ConnectFailure);
                };

                Mock<IDocumentClientInternal> mockDocumentClient = new Mock<IDocumentClientInternal>();
                mockDocumentClient.Setup(client => client.ServiceEndpoint).Returns(new Uri("https://foo"));

                GlobalEndpointManager endpointManager = new GlobalEndpointManager(mockDocumentClient.Object, new ConnectionPolicy());
                ISessionContainer sessionContainer = new SessionContainer(string.Empty);
                DocumentClientEventSource eventSource = DocumentClientEventSource.Instance;
                HttpMessageHandler messageHandler = new MockMessageHandler(sendFunc);
                GatewayStoreModel storeModel = new GatewayStoreModel(
                    endpointManager,
                    sessionContainer,
                    ConsistencyLevel.Eventual,
                    eventSource,
                    null,
                    MockCosmosUtil.CreateCosmosHttpClient(
                        () => new HttpClient(messageHandler),
                        eventSource));

                using (new ActivityScope(Guid.NewGuid()))
                {
                    using (DocumentServiceRequest request =
                    DocumentServiceRequest.Create(
                        Documents.OperationType.Query,
                        Documents.ResourceType.Document,
                        new Uri("https://foo.com/dbs/db1/colls/coll1", UriKind.Absolute),
                        new MemoryStream(Encoding.UTF8.GetBytes("content1")),
                        AuthorizationTokenType.PrimaryMasterKey,
                        null))
                    {
                        await storeModel.ProcessMessageAsync(request, cancellationToken);
                    }
                }
                Assert.Fail();
            }
        }

        [TestMethod]
        [ExpectedException(typeof(OperationCanceledException))]
        public async Task ClientRetryPolicyShouldCancel()
        {
            CancellationToken notCancelledToken = new CancellationToken();
            CancellationToken cancelledToken = new CancellationToken(true);

            Mock<IDocumentClientInternal> mockDocumentClient = new Mock<IDocumentClientInternal>();
            mockDocumentClient.Setup(client => client.ServiceEndpoint).Returns(new Uri("https://foo"));

            GlobalEndpointManager endpointManager = new GlobalEndpointManager(mockDocumentClient.Object, new ConnectionPolicy());
            ClientRetryPolicy clientRetryPolicy = new ClientRetryPolicy(endpointManager, true, new RetryOptions());
            DocumentClientException ex = new DocumentClientException("", (HttpStatusCode)StatusCodes.TooManyRequests, SubStatusCodes.OwnerResourceNotFound);

            ShouldRetryResult result = await clientRetryPolicy.ShouldRetryAsync(ex, notCancelledToken);
            Assert.IsTrue(result.ShouldRetry);

            await clientRetryPolicy.ShouldRetryAsync(ex, cancelledToken);
        }

        private class MockMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> sendFunc;

            public MockMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> func)
            {
                this.sendFunc = func;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return await this.sendFunc(request);
            }
        }
    }
}
