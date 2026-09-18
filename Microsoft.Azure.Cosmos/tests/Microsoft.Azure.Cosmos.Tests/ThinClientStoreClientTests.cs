//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.FaultInjection;
    using Microsoft.Azure.Documents.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Moq.Protected;

    [TestClass]
    public class ThinClientStoreClientTests
    {
        private const string base64MockResponse =
           "9AEAAMkAAAAIvhHfD23jSaynaR+gyTZ3AAAAAQIAByFUaHUsIDEzIEZlYiAyMDI1IDE0OjI1OjI4LjAyNCBHTVQEAAgmACIwMDAwYWQzZS0wMDAwLTAyMDAtMDAwMC02N2FlNjRjMDAwMDAiDgAIVABkb2N1bWVudFNpemU9NTEyMDA7ZG9jdW1lbnRzU2l6ZT01MjQyODgwMDtkb2N1bWVudHNDb3VudD0tMTtjb2xsZWN0aW9uU2l6ZT01MjQyODgwMDsPAAhBAGRvY3VtZW50U2l6ZT0wO2RvY3VtZW50c1NpemU9MTtkb2N1bWVudHNDb3VudD04O2NvbGxlY3Rpb25TaXplPTM7EAAHBDEuMTkTAAUKAAAAAAAAABUADgzDMAzDMBxAFwAIOgBkYnMvdGhpbi1jbGllbnQtdGVzdC1kYi9jb2xscy90aGluLWNsaWVudC10ZXN0LWNvbnRhaW5lci0xGAAIDABOSDF1QUo2QU5tMD0aAAUJAAAAAAAAAB4AAgMAAAAfAAIEAAAAIQAIAQAwJgACAQAAACkABQkAAAAAAAAAMAACAAAAADUAAgEAAAA6AAUKAAAAAAAAADsABQkAAAAAAAAAPgAIBQAtMSMxMFEADkjhehSuRxBAYwAIAQAweAAF//////////89AQAAeyJpZCI6IjNiMTFiNDM2LTViMTUtNGQwZS1iZWYwLWY1MzVmNjA0MTQxYyIsInBrIjoicGsiLCJuYW1lIjoiODM2MzI0NTA2IiwiZW1haWwiOiJhYmNAZGVmLmNvbSIsImJvZHkiOiJibGFibGEiLCJfcmlkIjoiTkgxdUFKNkFObTBKQUFBQUFBQUFBQT09IiwiX3NlbGYiOiJkYnMvTkgxdUFBPT0vY29sbHMvTkgxdUFKNkFObTA9L2RvY3MvTkgxdUFKNkFObTBKQUFBQUFBQUFBQT09LyIsIl9ldGFnIjoiXCIwMDAwYWQzZS0wMDAwLTAyMDAtMDAwMC02N2FlNjRjMDAwMDBcIiIsIl9hdHRhY2htZW50cyI6ImF0dGFjaG1lbnRzLyIsIl90cyI6MTczOTQ4MjMwNH0=";

        private readonly Uri thinClientEndpoint = new("https://thinproxy.cosmos.azure.com/");

        [TestInitialize]
        public void TestInitialize()
        {
            System.Diagnostics.Trace.CorrelationManager.ActivityId = Guid.NewGuid();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            System.Diagnostics.Trace.CorrelationManager.ActivityId = Guid.Empty;
        }

        [TestMethod]
        [DataRow(HttpStatusCode.NotFound, "{\"message\":\"Sample 404 JSON error.\"}", "application/json")]
        [DataRow(HttpStatusCode.InternalServerError, "{\"message\":\"Sample 500 JSON error.\"}", "application/json")]
        [DataRow(HttpStatusCode.Forbidden, "<html><body>403 Forbidden.</body></html>", "text/html")]
        public async Task InvokeAsync_ShouldThrowDocumentClientException(HttpStatusCode statusCode, string content, string contentType)
        {
            // Arrange
            HttpResponseMessage mockResponse = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content, Encoding.UTF8, contentType)
            };

            CosmosHttpClient cosmosHttpClient = MockCosmosUtil.CreateMockCosmosHttpClientFromFunc(
                _ => Task.FromResult(mockResponse));

            Cosmos.UserAgentContainer userAgentContainer = new Microsoft.Azure.Cosmos.UserAgentContainer(0, "TestFeature", "TestRegion", "TestSuffix");

            ThinClientStoreClient thinClientStoreClient = new ThinClientStoreClient(
                httpClient: cosmosHttpClient,
                eventSource: null,
                userAgentContainer: userAgentContainer,
                serializerSettings: null,
                globalPartitionEndpointManager: GlobalPartitionEndpointManagerNoOp.Instance);

            DocumentServiceRequest request = DocumentServiceRequest.Create(
                operationType: OperationType.Read,
                resourceType: ResourceType.Document,
                resourceId: "docId",
                body: null,
                authorizationTokenType: AuthorizationTokenType.PrimaryMasterKey);

            request.Headers[HttpConstants.HttpHeaders.PartitionKey] = "[\"myPartitionKey\"]";

            Mock<ISessionContainer> sessionContainerMock = new Mock<ISessionContainer>();
            Mock<IStoreModel> storeModelMock = new Mock<IStoreModel>();
            Mock<ICosmosAuthorizationTokenProvider> tokenProviderMock = new Mock<ICosmosAuthorizationTokenProvider>();
            Mock<IRetryPolicyFactory> retryPolicyFactoryMock = new Mock<IRetryPolicyFactory>();
            TelemetryToServiceHelper telemetry = null;

            Mock<ClientCollectionCache> clientCollectionCacheMock = new Mock<ClientCollectionCache>(
                sessionContainerMock.Object,
                storeModelMock.Object,
                tokenProviderMock.Object,
                retryPolicyFactoryMock.Object,
                telemetry,
                true,
                null)
            {
                CallBase = true
            };

            clientCollectionCacheMock
                .Setup(c => c.ResolveCollectionAsync(
                    It.IsAny<DocumentServiceRequest>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<ITrace>()))
                .ReturnsAsync(this.GetMockContainerProperties());

            // Act + Assert => Should throw DocumentClientException
            await Assert.ThrowsExceptionAsync<DocumentClientException>(async () =>
                await thinClientStoreClient.InvokeAsync(
                    request,
                    ResourceType.Document,
                    new Uri("https://mock.cosmos.com/dbs/mockdb/colls/mockcoll/docs/mockdoc"),
                    this.thinClientEndpoint,
                    "mockaccount",
                    clientCollectionCacheMock.Object,
                    default));
        }

        [TestMethod]
        public async Task InvokeAsync_Rntbd200_ShouldReturnDocumentServiceResponse()
        {
            HttpResponseMessage successResponse = new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new ByteArrayContent(Convert.FromBase64String(base64MockResponse))
            };

            CosmosHttpClient cosmosHttpClient = MockCosmosUtil.CreateMockCosmosHttpClientFromFunc(
                _ => Task.FromResult(successResponse));

            Cosmos.UserAgentContainer userAgentContainer = new Microsoft.Azure.Cosmos.UserAgentContainer(0, "TestFeature", "TestRegion", "TestSuffix");

            ThinClientStoreClient thinClientStoreClient = new ThinClientStoreClient(
                httpClient: cosmosHttpClient,
                eventSource: null,
                userAgentContainer: userAgentContainer,
                serializerSettings: null,
                globalPartitionEndpointManager: GlobalPartitionEndpointManagerNoOp.Instance);

            DocumentServiceRequest request = DocumentServiceRequest.Create(
                operationType: OperationType.Read,
                resourceType: ResourceType.Document,
                resourceId: "docId",
                body: null,
                authorizationTokenType: AuthorizationTokenType.PrimaryMasterKey);

            // Add partition key
            request.Headers[HttpConstants.HttpHeaders.PartitionKey] = "[\"myPartitionKey\"]";

            Mock<ISessionContainer> sessionContainerMock = new Mock<ISessionContainer>();
            Mock<IStoreModel> storeModelMock = new Mock<IStoreModel>();
            Mock<ICosmosAuthorizationTokenProvider> tokenProviderMock = new Mock<ICosmosAuthorizationTokenProvider>();
            Mock<IRetryPolicyFactory> retryPolicyFactoryMock = new Mock<IRetryPolicyFactory>();
            TelemetryToServiceHelper telemetry = null;
            Mock<ClientCollectionCache> clientCollectionCacheMock = new Mock<ClientCollectionCache>(
                sessionContainerMock.Object,
                storeModelMock.Object,
                tokenProviderMock.Object,
                retryPolicyFactoryMock.Object,
                telemetry,
                true,
                null)
            {
                CallBase = true
            };

            clientCollectionCacheMock
                .Setup(c => c.ResolveCollectionAsync(
                    It.IsAny<DocumentServiceRequest>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<ITrace>()))
                .ReturnsAsync(this.GetMockContainerProperties());

            // Act
            DocumentServiceResponse dsr = await thinClientStoreClient.InvokeAsync(
                request,
                ResourceType.Document,
                new Uri("https://mock.cosmos.com/dbs/mockdb/colls/mockcoll/docs/mockdoc"),
                this.thinClientEndpoint,
                "mockaccount",
                clientCollectionCacheMock.Object,
                default);

            // Assert
            Assert.IsNotNull(dsr);
            Assert.AreEqual(HttpStatusCode.Created, dsr.StatusCode);
        }

        [TestMethod]
        public async Task InvokeAsync_ShouldOnlyAddUserAgentAndActivityIdHeadersToProxyRequest()
        {
            HttpResponseMessage successResponse = new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new ByteArrayContent(Convert.FromBase64String(base64MockResponse))
            };

            HttpRequestMessage capturedRequest = null;
            Mock<CosmosHttpClient> mockCosmosHttpClient = new Mock<CosmosHttpClient>();
            mockCosmosHttpClient.Setup(client => client.SendHttpAsync(
                It.IsAny<Func<ValueTask<HttpRequestMessage>>>(),
                It.IsAny<ResourceType>(),
                It.IsAny<HttpTimeoutPolicy>(),
                It.IsAny<IClientSideRequestStatistics>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<DocumentServiceRequest>()))
                .Callback<Func<ValueTask<HttpRequestMessage>>, ResourceType, HttpTimeoutPolicy, IClientSideRequestStatistics, CancellationToken, DocumentServiceRequest>(
                    async (requestFactory, _, _, _, _, _) =>
                        capturedRequest = await requestFactory())
                .ReturnsAsync(successResponse);

            Cosmos.UserAgentContainer userAgentContainer = new Microsoft.Azure.Cosmos.UserAgentContainer(0, "TestFeature", "TestRegion", "TestSuffix");

            ThinClientStoreClient thinClientStoreClient = new ThinClientStoreClient(
                httpClient: mockCosmosHttpClient.Object,
                eventSource: null,
                userAgentContainer: userAgentContainer,
                serializerSettings: null,
                globalPartitionEndpointManager: GlobalPartitionEndpointManagerNoOp.Instance);

            DocumentServiceRequest request = DocumentServiceRequest.Create(
                operationType: OperationType.Read,
                resourceType: ResourceType.Document,
                resourceId: "docId",
                body: null,
                authorizationTokenType: AuthorizationTokenType.PrimaryMasterKey);

            // Add partition key
            request.Headers[HttpConstants.HttpHeaders.PartitionKey] = "[\"myPartitionKey\"]";

            // Set up a mock partition key range in the request context
            PartitionKeyRange mockPartitionKeyRange = new PartitionKeyRange
            {
                MinInclusive = "00000000-0000-0000-0000-000000000000",
                MaxExclusive = "FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF"
            };

            // Initialize request context if needed
            if (request.RequestContext == null)
            {
                request.RequestContext = new DocumentServiceRequestContext();
            }

            // Set the partition key range in the request context
            request.RequestContext.ResolvedPartitionKeyRange = mockPartitionKeyRange;

            Mock<ISessionContainer> sessionContainerMock = new Mock<ISessionContainer>();
            Mock<IStoreModel> storeModelMock = new Mock<IStoreModel>();
            Mock<ICosmosAuthorizationTokenProvider> tokenProviderMock = new Mock<ICosmosAuthorizationTokenProvider>();
            Mock<IRetryPolicyFactory> retryPolicyFactoryMock = new Mock<IRetryPolicyFactory>();
            TelemetryToServiceHelper telemetry = null;
            Mock<ClientCollectionCache> clientCollectionCacheMock = new Mock<ClientCollectionCache>(
                sessionContainerMock.Object,
                storeModelMock.Object,
                tokenProviderMock.Object,
                retryPolicyFactoryMock.Object,
                telemetry,
                true,
                null)
            {
                CallBase = true
            };

            clientCollectionCacheMock
                .Setup(c => c.ResolveCollectionAsync(
                    It.IsAny<DocumentServiceRequest>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<ITrace>()))
                .ReturnsAsync(this.GetMockContainerProperties());

            // Act
            await thinClientStoreClient.InvokeAsync(
                request,
                ResourceType.Document,
                new Uri("https://mock.cosmos.com/dbs/mockdb/colls/mockcoll/docs/mockdoc"),
                this.thinClientEndpoint,
                "mockaccount",
                clientCollectionCacheMock.Object,
                default);

            // Assert
            Assert.IsNotNull(capturedRequest, "The request was not captured");

            System.Collections.Generic.Dictionary<string, string> requestHeaders = capturedRequest.Headers.ToDictionary(h => h.Key, h => h.Value.FirstOrDefault());

            // Only UserAgent and ActivityId should be present
            Assert.AreEqual(2, requestHeaders.Count, "Only UserAgent and ActivityId headers should be present");
            Assert.IsTrue(requestHeaders.ContainsKey(ThinClientConstants.UserAgent), "UserAgent header is missing");
            Assert.IsTrue(requestHeaders.ContainsKey(HttpConstants.HttpHeaders.ActivityId), "ActivityId header is missing");

            Assert.IsFalse(requestHeaders.ContainsKey(ThinClientConstants.ProxyStartEpk), "ProxyStartEpk header should NOT be present");
            Assert.IsFalse(requestHeaders.ContainsKey(ThinClientConstants.ProxyEndEpk), "ProxyEndEpk header should NOT be present");
        }

        [TestMethod]
        public async Task InvokeAsync_ShouldNotAddProxyEpkHeaders_WhenPartitionKeyRangeIsNull()
        {
            HttpResponseMessage successResponse = new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new ByteArrayContent(Convert.FromBase64String(base64MockResponse))
            };

            HttpRequestMessage capturedRequest = null;
            Mock<CosmosHttpClient> mockCosmosHttpClient = new Mock<CosmosHttpClient>();
            mockCosmosHttpClient.Setup(client => client.SendHttpAsync(
                It.IsAny<Func<ValueTask<HttpRequestMessage>>>(),
                It.IsAny<ResourceType>(),
                It.IsAny<HttpTimeoutPolicy>(),
                It.IsAny<IClientSideRequestStatistics>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<DocumentServiceRequest>()))
                .Callback<Func<ValueTask<HttpRequestMessage>>, ResourceType, HttpTimeoutPolicy, IClientSideRequestStatistics, CancellationToken, DocumentServiceRequest>(
                    async (requestFactory, _, _, _, _, _) =>
                        capturedRequest = await requestFactory())
                .ReturnsAsync(successResponse);

            Cosmos.UserAgentContainer userAgentContainer = new Microsoft.Azure.Cosmos.UserAgentContainer(0, "TestFeature", "TestRegion", "TestSuffix");

            ThinClientStoreClient thinClientStoreClient = new ThinClientStoreClient(
                httpClient: mockCosmosHttpClient.Object,
                eventSource: null,
                userAgentContainer: userAgentContainer,
                serializerSettings: null,
                globalPartitionEndpointManager: GlobalPartitionEndpointManagerNoOp.Instance);

            DocumentServiceRequest request = DocumentServiceRequest.Create(
                operationType: OperationType.Read,
                resourceType: ResourceType.Document,
                resourceId: "docId",
                body: null,
                authorizationTokenType: AuthorizationTokenType.PrimaryMasterKey);

            // Add partition key
            request.Headers[HttpConstants.HttpHeaders.PartitionKey] = "[\"myPartitionKey\"]";

            request.RequestContext.ResolvedPartitionKeyRange = null;
            if (request.RequestContext == null)
            {
                request.RequestContext = new DocumentServiceRequestContext();
            };

            Mock<ISessionContainer> sessionContainerMock = new Mock<ISessionContainer>();
            Mock<IStoreModel> storeModelMock = new Mock<IStoreModel>();
            Mock<ICosmosAuthorizationTokenProvider> tokenProviderMock = new Mock<ICosmosAuthorizationTokenProvider>();
            Mock<IRetryPolicyFactory> retryPolicyFactoryMock = new Mock<IRetryPolicyFactory>();
            TelemetryToServiceHelper telemetry = null;
            Mock<ClientCollectionCache> clientCollectionCacheMock = new Mock<ClientCollectionCache>(
                sessionContainerMock.Object,
                storeModelMock.Object,
                tokenProviderMock.Object,
                retryPolicyFactoryMock.Object,
                telemetry,
                true,
                null)
            {
                CallBase = true
            };

            clientCollectionCacheMock
                .Setup(c => c.ResolveCollectionAsync(
                    It.IsAny<DocumentServiceRequest>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<ITrace>()))
                .ReturnsAsync(this.GetMockContainerProperties());

            // Act
            await thinClientStoreClient.InvokeAsync(
                request,
                ResourceType.Document,
                new Uri("https://mock.cosmos.com/dbs/mockdb/colls/mockcoll/docs/mockdoc"),
                this.thinClientEndpoint,
                "mockaccount",
                clientCollectionCacheMock.Object,
                default);

            // Assert
            Assert.IsNotNull(capturedRequest, "The request was not captured");

            System.Collections.Generic.Dictionary<string, string> headers = capturedRequest.Headers.ToDictionary(h => h.Key, h => h.Value.FirstOrDefault());

            Assert.IsFalse(headers.ContainsKey(ThinClientConstants.ProxyStartEpk), "ProxyStartEpk should not be added when PKRange is null");
            Assert.IsFalse(headers.ContainsKey(ThinClientConstants.ProxyEndEpk), "ProxyEndEpk should not be added when PKRange is null");
        }

        [TestMethod]
        public void Constructor_ShouldThrowArgumentNullException_WhenUserAgentContainerIsNull()
        {
            // Arrange
            Mock<CosmosHttpClient> mockHttpClient = new Mock<CosmosHttpClient>();
            ICommunicationEventSource mockEventSource = Mock.Of<ICommunicationEventSource>();

            // Act & Assert
            ArgumentNullException ex = Assert.ThrowsException<ArgumentNullException>(() =>
                new ThinClientStoreClient(
                    httpClient: mockHttpClient.Object,
                    userAgentContainer: null,
                    eventSource: mockEventSource,
                    globalPartitionEndpointManager: GlobalPartitionEndpointManagerNoOp.Instance,
                    serializerSettings: null)
            );

            Assert.AreEqual("userAgentContainer", ex.ParamName);
            StringAssert.Contains(ex.Message, "UserAgentContainer cannot be null");
        }

        [TestMethod]
        [DataRow(false, 1, false)]
        [DataRow(false, 100, false)]
        [DataRow(true, 1, false)]
        [DataRow(true, 100, false)]
        [DataRow(false, 1, true)]
        [DataRow(false, 100, true)]
        [DataRow(true, 1, true)]
        [DataRow(true, 100, true)]
        public async Task InvokeAsync_FaultInjectionRunsOnceBeforeSending(bool isWrite, int allowedApplications, bool useHttpClientFactory)
        {
            const string ruleId = "single-proxy-injection";
            int applications = 0;
            Mock<IChaosInterceptor> interceptor = new Mock<IChaosInterceptor>();
            interceptor.Setup(i => i.OnHttpRequestCallAsync(
                It.IsAny<DocumentServiceRequest>(), It.IsAny<CancellationToken>()))
                .Returns<DocumentServiceRequest, CancellationToken>((request, token) =>
                {
                    Assert.AreEqual("true", request.Headers.Get("FAULTINJECTION_IS_PROXY"),
                        "The interceptor must know the response format before selecting a fault.");
                    applications++;
                    if (applications > allowedApplications)
                    {
                        return Task.FromResult<(bool, HttpResponseMessage)>((false, null));
                    }

                    HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                    {
                        Content = new StringContent(
                            $"{{\"code\":\"429:3200\",\"message\":\"Fault Injection Server Error, rule: {ruleId}\"}}",
                            Encoding.UTF8,
                            "application/json")
                    };
                    response.Headers.Add(WFConstants.BackendHeaders.SubStatus, "3200");
                    response.Headers.Add(ThinClientConstants.RoutedViaProxy, "1");
                    return Task.FromResult((true, response));
                });

            Mock<HttpMessageHandler> handler = new Mock<HttpMessageHandler>();
            using CosmosHttpClient httpClient = CreateFaultInjectionHttpClient(interceptor.Object, handler.Object, useHttpClientFactory);
            ThinClientStoreClient client = this.CreateFaultInjectionStoreClient(httpClient, interceptor.Object);
            using DocumentServiceRequest request = this.CreateFaultInjectionRequest(isWrite ? OperationType.Create : OperationType.Read);

            DocumentClientException exception = await Assert.ThrowsExceptionAsync<DocumentClientException>(
                () => this.InvokeFaultInjectionRequestAsync(client, request));

            Assert.AreEqual(HttpStatusCode.TooManyRequests, exception.StatusCode);
            Assert.AreEqual("3200", exception.Headers.Get(WFConstants.BackendHeaders.SubStatus));
            StringAssert.Contains(exception.Message, ruleId);
            Assert.AreEqual(1, applications);
            Assert.IsNull(request.Headers.Get("FAULTINJECTION_IS_PROXY"));
            ClientSideRequestStatisticsTraceDatum statistics =
                (ClientSideRequestStatisticsTraceDatum)request.RequestContext.ClientRequestStatistics;
            HttpResponseMessage recordedResponse = statistics.HttpResponseStatisticsList.Single().HttpResponseMessage;
            Assert.AreEqual(HttpStatusCode.TooManyRequests, recordedResponse.StatusCode);
            Assert.AreEqual("3200", recordedResponse.Headers.GetValues(WFConstants.BackendHeaders.SubStatus).Single());
            Assert.IsTrue(recordedResponse.RequestMessage.Options.TryGetValue(
                new HttpRequestOptionsKey<bool>(CosmosHttpClientCore.FaultInjectionIsProxy), out bool proxyRequest) && proxyRequest);
            Assert.IsTrue(recordedResponse.RequestMessage.Options.TryGetValue(
                new HttpRequestOptionsKey<bool>(CosmosHttpClientCore.FaultInjectionResponse), out bool injectedResponse) && injectedResponse);
            handler.Protected().Verify("SendAsync", Times.Never(),
                ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
            interceptor.Verify(i => i.OnBeforeHttpSendAsync(request, It.IsAny<CancellationToken>()), Times.Once());
            interceptor.Verify(i => i.OnAfterHttpSendAsync(request, It.IsAny<CancellationToken>()), Times.Never());

            if (allowedApplications == 1)
            {
                handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                    .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.Created)
                    {
                        Content = new ByteArrayContent(Convert.FromBase64String(base64MockResponse))
                    });

                using DocumentServiceResponse retryResponse = await this.InvokeFaultInjectionRequestAsync(client, request);
                Assert.AreEqual(HttpStatusCode.Created, retryResponse.StatusCode,
                    "A retry after a single-use fault must decode the real proxy response.");
                Assert.AreEqual(2, applications);
                Assert.IsNull(request.Headers.Get("FAULTINJECTION_IS_PROXY"));
                Assert.AreEqual(2, statistics.HttpResponseStatisticsList.Count);
                Assert.AreEqual(HttpStatusCode.Created, statistics.HttpResponseStatisticsList.Last().HttpResponseMessage.StatusCode);
                handler.Protected().Verify("SendAsync", Times.Once(),
                    ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
            }
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task InvokeAsync_UnmatchedFaultPreservesRealProxyResponse(bool useHttpClientFactory)
        {
            Mock<IChaosInterceptor> interceptor = new Mock<IChaosInterceptor>();
            interceptor.Setup(i => i.OnHttpRequestCallAsync(
                It.IsAny<DocumentServiceRequest>(), It.IsAny<CancellationToken>()))
                .Returns<DocumentServiceRequest, CancellationToken>((request, token) =>
                {
                    Assert.AreEqual("true", request.Headers.Get("FAULTINJECTION_IS_PROXY"));
                    return Task.FromResult<(bool, HttpResponseMessage)>((false, null));
                });

            using DocumentServiceRequest request = this.CreateFaultInjectionRequest(OperationType.Read);
            Mock<HttpMessageHandler> handler = new Mock<HttpMessageHandler>();
            handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .Returns<HttpRequestMessage, CancellationToken>((httpRequest, token) =>
                {
                    Assert.IsNull(request.Headers.Get("FAULTINJECTION_IS_PROXY"),
                        "The format marker must not survive the interception scope.");
                    Assert.IsFalse(httpRequest.Headers.Contains("FAULTINJECTION_IS_PROXY"));
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created)
                    {
                        Content = new ByteArrayContent(Convert.FromBase64String(base64MockResponse))
                    });
                });

            using CosmosHttpClient httpClient = CreateFaultInjectionHttpClient(interceptor.Object, handler.Object, useHttpClientFactory);
            ThinClientStoreClient client = this.CreateFaultInjectionStoreClient(httpClient, interceptor.Object);
            using DocumentServiceResponse response = await this.InvokeFaultInjectionRequestAsync(client, request);

            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
            interceptor.Verify(i => i.OnHttpRequestCallAsync(request, It.IsAny<CancellationToken>()), Times.Once());
            interceptor.Verify(i => i.OnAfterHttpSendAsync(request, It.IsAny<CancellationToken>()), Times.Once());
            handler.Protected().Verify("SendAsync", Times.Once(),
                ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task InvokeAsync_CanceledFaultDoesNotLeakProxyMarker(bool cancelBeforeSend)
        {
            using CancellationTokenSource cancellation = new CancellationTokenSource();
            Mock<IChaosInterceptor> interceptor = new Mock<IChaosInterceptor>();
            interceptor.Setup(i => i.OnBeforeHttpSendAsync(
                It.IsAny<DocumentServiceRequest>(), It.IsAny<CancellationToken>()))
                .Returns<DocumentServiceRequest, CancellationToken>((request, token) =>
                {
                    Assert.AreEqual("true", request.Headers.Get("FAULTINJECTION_IS_PROXY"));
                    if (cancelBeforeSend)
                    {
                        cancellation.Cancel();
                        throw new OperationCanceledException(cancellation.Token);
                    }

                    return Task.CompletedTask;
                });
            interceptor.Setup(i => i.OnHttpRequestCallAsync(
                It.IsAny<DocumentServiceRequest>(), It.IsAny<CancellationToken>()))
                .Returns<DocumentServiceRequest, CancellationToken>((request, token) =>
                {
                    Assert.AreEqual("true", request.Headers.Get("FAULTINJECTION_IS_PROXY"));
                    cancellation.Cancel();
                    throw new OperationCanceledException(cancellation.Token);
                });

            Mock<HttpMessageHandler> handler = new Mock<HttpMessageHandler>();
            using CosmosHttpClient httpClient = CreateFaultInjectionHttpClient(interceptor.Object, handler.Object);
            ThinClientStoreClient client = this.CreateFaultInjectionStoreClient(httpClient, interceptor.Object);
            using DocumentServiceRequest request = this.CreateFaultInjectionRequest(OperationType.Read);

            await Assert.ThrowsExceptionAsync<OperationCanceledException>(
                () => this.InvokeFaultInjectionRequestAsync(client, request, cancellation.Token));

            Assert.IsNull(request.Headers.Get("FAULTINJECTION_IS_PROXY"));
            interceptor.Verify(i => i.OnHttpRequestCallAsync(request, It.IsAny<CancellationToken>()),
                cancelBeforeSend ? Times.Never() : Times.Once());
            handler.Protected().Verify("SendAsync", Times.Never(),
                ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task SendHttpAsync_GatewayFaultPreservesHttpResponse(bool useHttpClientFactory)
        {
            const string errorMessage = "Fault Injection Server Error: Gateway test";
            Mock<IChaosInterceptor> interceptor = new Mock<IChaosInterceptor>();
            interceptor.Setup(i => i.OnHttpRequestCallAsync(
                It.IsAny<DocumentServiceRequest>(), It.IsAny<CancellationToken>()))
                .Returns<DocumentServiceRequest, CancellationToken>((request, token) =>
                {
                    Assert.IsNull(request.Headers.Get("FAULTINJECTION_IS_PROXY"));
                    return Task.FromResult((true, new HttpResponseMessage(HttpStatusCode.InternalServerError)
                    {
                        Content = new StringContent(errorMessage)
                    }));
                });

            Mock<HttpMessageHandler> handler = new Mock<HttpMessageHandler>();
            using CosmosHttpClient httpClient = CreateFaultInjectionHttpClient(interceptor.Object, handler.Object, useHttpClientFactory);
            using DocumentServiceRequest request = this.CreateFaultInjectionRequest(OperationType.Read);
            using HttpResponseMessage response = await httpClient.SendHttpAsync(
                () => new ValueTask<HttpRequestMessage>(new HttpRequestMessage(HttpMethod.Get, "https://mock.cosmos.com/")),
                ResourceType.Document,
                HttpTimeoutPolicyDefault.Instance,
                request.RequestContext.ClientRequestStatistics,
                default,
                request);

            Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.AreEqual(errorMessage, await response.Content.ReadAsStringAsync());
            Assert.IsNull(request.Headers.Get("FAULTINJECTION_IS_PROXY"));
            interceptor.Verify(i => i.OnHttpRequestCallAsync(request, It.IsAny<CancellationToken>()), Times.Once());
            handler.Protected().Verify("SendAsync", Times.Never(),
                ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
        }

        private static CosmosHttpClient CreateFaultInjectionHttpClient(
            IChaosInterceptor interceptor,
            HttpMessageHandler handler,
            bool useHttpClientFactory = false)
        {
            ConnectionPolicy connectionPolicy = new ConnectionPolicy();
            if (useHttpClientFactory)
            {
                connectionPolicy.HttpClientFactory = () => new HttpClient(handler);
            }

            return CosmosHttpClientCore.CreateWithConnectionPolicy(
                apiType: default,
                eventSource: DocumentClientEventSource.Instance,
                connectionPolicy: connectionPolicy,
                httpMessageHandler: handler,
                sendingRequestEventArgs: null,
                receivedResponseEventArgs: null,
                faultInjectionchaosInterceptor: interceptor);
        }

        private ThinClientStoreClient CreateFaultInjectionStoreClient(
            CosmosHttpClient httpClient,
            IChaosInterceptor interceptor)
        {
            return new ThinClientStoreClient(
                httpClient,
                new Cosmos.UserAgentContainer(0, "TestFeature", "TestRegion", "TestSuffix"),
                DocumentClientEventSource.Instance,
                GlobalPartitionEndpointManagerNoOp.Instance,
                chaosInterceptor: interceptor);
        }

        private DocumentServiceRequest CreateFaultInjectionRequest(OperationType operationType)
        {
            DocumentServiceRequest request = DocumentServiceRequest.Create(
                operationType: operationType,
                resourceType: ResourceType.Document,
                resourceId: "docId",
                body: null,
                authorizationTokenType: AuthorizationTokenType.PrimaryMasterKey);
            request.Headers[HttpConstants.HttpHeaders.PartitionKey] = "[\"myPartitionKey\"]";
            request.RequestContext.ClientRequestStatistics =
                new ClientSideRequestStatisticsTraceDatum(DateTime.UtcNow, NoOpTrace.Singleton);
            return request;
        }

        private Task<DocumentServiceResponse> InvokeFaultInjectionRequestAsync(
            ThinClientStoreClient client,
            DocumentServiceRequest request,
            CancellationToken cancellationToken = default)
        {
            Mock<ClientCollectionCache> cache = new Mock<ClientCollectionCache>(
                Mock.Of<ISessionContainer>(),
                Mock.Of<IStoreModel>(),
                Mock.Of<ICosmosAuthorizationTokenProvider>(),
                Mock.Of<IRetryPolicyFactory>(),
                null,
                true,
                null)
            {
                CallBase = true
            };
            cache.Setup(c => c.ResolveCollectionAsync(
                It.IsAny<DocumentServiceRequest>(), It.IsAny<CancellationToken>(), It.IsAny<ITrace>()))
                .ReturnsAsync(this.GetMockContainerProperties());

            return client.InvokeAsync(
                request,
                ResourceType.Document,
                new Uri("https://mock.cosmos.com/dbs/mockdb/colls/mockcoll/docs/mockdoc"),
                this.thinClientEndpoint,
                "mockaccount",
                cache.Object,
                cancellationToken);
        }

        private ContainerProperties GetMockContainerProperties()
        {
            ContainerProperties containerProperties = new ContainerProperties
            {
                PartitionKey = new PartitionKeyDefinition
                {
                    Paths = new Collection<string> { "/pk" }
                }
            };

            typeof(ContainerProperties)
                .GetProperty("ResourceId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(containerProperties, "-Jlvm9pqHGk=");

            return containerProperties;
        }
    }
}
