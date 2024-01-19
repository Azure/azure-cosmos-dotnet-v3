//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Threading;
    using System.Net;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Cosmos.Tests;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using System.Collections.Generic;

    /// <summary>
    /// Tests for <see cref="GatewayAccountReader"/>.
    /// </summary>
    [TestClass]
    public class GatewayAccountReaderTests
    {
        [TestMethod]
        public async Task GatewayAccountReader_MessageHandler()
        {
            HttpMessageHandler messageHandler = new CustomMessageHandler();
            HttpClient staticHttpClient = new HttpClient(messageHandler);

            GatewayAccountReader accountReader = new GatewayAccountReader(
                serviceEndpoint: new Uri("https://localhost"),
                cosmosAuthorization: Mock.Of<AuthorizationTokenProvider>(),
                connectionPolicy: new ConnectionPolicy(),
                httpClient: MockCosmosUtil.CreateCosmosHttpClient(() => staticHttpClient));

            DocumentClientException exception = await Assert.ThrowsExceptionAsync<DocumentClientException>(() => accountReader.InitializeReaderAsync());
            Assert.AreEqual(HttpStatusCode.Conflict, exception.StatusCode);
        }

        [TestMethod]
        public async Task DocumentClient_BuildHttpClientFactory_WithHandler()
        {
            HttpMessageHandler messageHandler = new CustomMessageHandler();
            ConnectionPolicy connectionPolicy = new ConnectionPolicy()
            {
                HttpClientFactory = () => new HttpClient(messageHandler)
            };

            CosmosHttpClient httpClient = CosmosHttpClientCore.CreateWithConnectionPolicy(
                apiType: ApiType.None,
                eventSource: DocumentClientEventSource.Instance,
                connectionPolicy: connectionPolicy,
                httpMessageHandler: null,
                sendingRequestEventArgs: null,
                receivedResponseEventArgs: null);

            Assert.IsNotNull(httpClient);

            using (ITrace trace = Trace.GetRootTrace(nameof(DocumentClient_BuildHttpClientFactory_WithHandler)))
            {
                IClientSideRequestStatistics stats = new ClientSideRequestStatisticsTraceDatum(DateTime.UtcNow, trace);
                HttpResponseMessage response = await httpClient.GetAsync(
                    uri: new Uri("https://localhost"),
                    additionalHeaders: new RequestNameValueCollection(),
                    resourceType: ResourceType.Document,
                    timeoutPolicy: HttpTimeoutPolicyDefault.InstanceShouldThrow503OnTimeout,
                    clientSideRequestStatistics: stats,
                    cancellationToken: default);

                Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode);
            }
        }

        [TestMethod]
        public void DocumentClient_BuildHttpClientFactory_WithFactory()
        {
            HttpClient staticHttpClient = new HttpClient();

            Mock<Func<HttpClient>> mockFactory = new Mock<Func<HttpClient>>();
            mockFactory.Setup(f => f()).Returns(staticHttpClient);

            ConnectionPolicy connectionPolicy = new ConnectionPolicy()
            {
                HttpClientFactory = mockFactory.Object
            };

            CosmosHttpClient httpClient = CosmosHttpClientCore.CreateWithConnectionPolicy(
                apiType: ApiType.None,
                eventSource: DocumentClientEventSource.Instance,
                connectionPolicy: connectionPolicy,
                httpMessageHandler: null,
                sendingRequestEventArgs: null,
                receivedResponseEventArgs: null);

            Assert.IsNotNull(httpClient);

            Mock.Get(mockFactory.Object)
                .Verify(f => f(), Times.Once);
        }

        [TestMethod]
        public async Task InitializeReaderAsync_WhenInvokedWithRegionalEndpoints_ShouldRetryWhenPrimaryEndpointFails()
        {
            HttpMessageHandler messageHandler = new CustomMessageHandler();
            HttpClient staticHttpClient = new HttpClient(messageHandler);

            Mock<HttpClient> mockedHttpClient = new();

            ConnectionPolicy connectionPolicy = new()
            {
                EnablePartitionLevelFailover = true,
                ConnectionMode = ConnectionMode.Direct,
            };

            connectionPolicy.SetRegionalEndpoints(
                new List<string>()
                {
                    "https://dkppaf1.documents-test.windows-int.net:443/",
                    "https://dkppaf2.documents-test.windows-int.net:443/",
                    "https://dkppaf6.documents-test.windows-int.net:443/",
                });

            GatewayAccountReader accountReader = new GatewayAccountReader(
                serviceEndpoint: new Uri("https://localhost"),
                cosmosAuthorization: Mock.Of<AuthorizationTokenProvider>(),
                connectionPolicy: connectionPolicy,
                httpClient: MockCosmosUtil.CreateCosmosHttpClient(() => staticHttpClient));

            AggregateException exception = await Assert.ThrowsExceptionAsync<AggregateException>(() => accountReader.InitializeReaderAsync());
        }

        public class CustomMessageHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Conflict) { RequestMessage = request, Content = new StringContent("Notfound") });
            }
        }
    }
}
