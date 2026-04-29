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
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;

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
                true)
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
                true)
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
                true)
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
                true)
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

        #region ThinClientQueryPlanHelper Tests

        private static readonly PartitionKeyDefinition HashPartitionKeyDefinition = new PartitionKeyDefinition()
        {
            Paths = new Collection<string>() { "/id" },
            Kind = PartitionKind.Hash,
        };

        [TestMethod]
        [DynamicData(nameof(GetQueryPlanJsonTestCases), DynamicDataSourceType.Method)]
        public void DeserializeQueryPlanResponse_ConsistentWithQueryPartitionProvider(string queryPlanJson, string description)
        {
            // Deserialize via ThinClientQueryPlanHelper (stream-based, as used in thin client mode)
            PartitionedQueryExecutionInfo thinClientResult;
            using (Stream stream = new MemoryStream(Encoding.UTF8.GetBytes(queryPlanJson)))
            {
                thinClientResult = ThinClientQueryPlanHelper.DeserializeQueryPlanResponse(
                    stream,
                    HashPartitionKeyDefinition);
            }

            // Deserialize via QueryPartitionProvider (string-based, as used in gateway/service-interop mode)
            QueryPartitionProvider queryPartitionProvider = new QueryPartitionProvider(
                new Dictionary<string, object>() { { "maxSqlQueryInputLength", 524288 } });

            PartitionedQueryExecutionInfoInternal queryInfoInternal =
                JsonConvert.DeserializeObject<PartitionedQueryExecutionInfoInternal>(
                    queryPlanJson,
                    new JsonSerializerSettings { DateParseHandling = DateParseHandling.None, MaxDepth = 64 });

            PartitionedQueryExecutionInfo providerResult = queryPartitionProvider.ConvertPartitionedQueryExecutionInfo(
                queryInfoInternal,
                HashPartitionKeyDefinition);

            // Assert: Both paths must produce identical EPK ranges
            Assert.AreEqual(providerResult.QueryRanges.Count, thinClientResult.QueryRanges.Count, description);
            for (int i = 0; i < providerResult.QueryRanges.Count; i++)
            {
                Assert.AreEqual(providerResult.QueryRanges[i].Min, thinClientResult.QueryRanges[i].Min, $"{description} - range[{i}].Min");
                Assert.AreEqual(providerResult.QueryRanges[i].Max, thinClientResult.QueryRanges[i].Max, $"{description} - range[{i}].Max");
                Assert.AreEqual(providerResult.QueryRanges[i].IsMinInclusive, thinClientResult.QueryRanges[i].IsMinInclusive, $"{description} - range[{i}].IsMinInclusive");
                Assert.AreEqual(providerResult.QueryRanges[i].IsMaxInclusive, thinClientResult.QueryRanges[i].IsMaxInclusive, $"{description} - range[{i}].IsMaxInclusive");
            }
        }

        private static IEnumerable<object[]> GetQueryPlanJsonTestCases()
        {
            // Full range (cross-partition query)
            yield return new object[]
            {
                @"{""queryInfo"":{""distinctType"":""None"",""top"":null,""offset"":null,""limit"":null,""orderBy"":[],""orderByExpressions"":[],""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[""CountIf""],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT VALUE [{\""item\"": COUNTIF(c.valid)}]\nFROM c"",""hasSelectValue"":true,""dCountInfo"":null,""hasNonStreamingOrderBy"":false},""queryRanges"":[{""min"":[],""max"":""Infinity"",""isMinInclusive"":true,""isMaxInclusive"":false}]}",
                "Full range with aggregate"
            };

            // Point query (single partition key)
            yield return new object[]
            {
                @"{""queryInfo"":{""distinctType"":""None"",""top"":null,""offset"":null,""limit"":null,""orderBy"":[""Descending""],""orderByExpressions"":[],""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":"""",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":false},""queryRanges"":[{""min"":[""testValue""],""max"":[""testValue""],""isMinInclusive"":true,""isMaxInclusive"":true}]}",
                "Point query with ORDER BY"
            };

            // HybridSearchQueryInfo
            yield return new object[]
            {
                @"{""hybridSearchQueryInfo"":{""globalStatisticsQuery"":""SELECT COUNT(1) AS documentCount, [] AS fullTextStatistics\nFROM c"",""componentQueryInfos"":[],""componentWithoutPayloadQueryInfos"":[],""projectionQueryInfo"":null,""componentWeights"":null,""skip"":null,""take"":10,""requiresGlobalStatistics"":false},""queryRanges"":[{""min"":[],""max"":""Infinity"",""isMinInclusive"":true,""isMaxInclusive"":false}]}",
                "HybridSearchQueryInfo"
            };
        }

        [TestMethod]
        public void DeserializeQueryPlanResponse_MultipleRanges_SortsOutput()
        {
            // Multiple ranges in deliberate reverse order to verify sorting
            string queryPlanJson = @"{""queryInfo"":{""distinctType"":""None"",""top"":null,""offset"":null,""limit"":null,""orderBy"":[],""orderByExpressions"":[],""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":"""",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":false},""queryRanges"":[{""min"":[""zzz""],""max"":[""zzz""],""isMinInclusive"":true,""isMaxInclusive"":true},{""min"":[""aaa""],""max"":[""aaa""],""isMinInclusive"":true,""isMaxInclusive"":true},{""min"":[""mmm""],""max"":[""mmm""],""isMinInclusive"":true,""isMaxInclusive"":true}]}";

            using Stream stream = new MemoryStream(Encoding.UTF8.GetBytes(queryPlanJson));

            PartitionedQueryExecutionInfo result = ThinClientQueryPlanHelper.DeserializeQueryPlanResponse(
                stream,
                HashPartitionKeyDefinition);

            Assert.AreEqual(3, result.QueryRanges.Count);
            for (int i = 0; i < result.QueryRanges.Count - 1; i++)
            {
                Assert.IsTrue(
                    string.Compare(result.QueryRanges[i].Min, result.QueryRanges[i + 1].Min, StringComparison.Ordinal) <= 0,
                    $"Ranges should be sorted: range[{i}].Min='{result.QueryRanges[i].Min}' should be <= range[{i + 1}].Min='{result.QueryRanges[i + 1].Min}'");
            }
        }

        [TestMethod]
        public void DeserializeQueryPlanResponse_InvalidInputs_FailsFast()
        {
            Assert.ThrowsException<ArgumentNullException>(
                () => ThinClientQueryPlanHelper.DeserializeQueryPlanResponse(null, HashPartitionKeyDefinition),
                "Null stream should throw");

            using (Stream validStream = new MemoryStream(Encoding.UTF8.GetBytes("{}")))
            {
                Assert.ThrowsException<ArgumentNullException>(
                    () => ThinClientQueryPlanHelper.DeserializeQueryPlanResponse(validStream, null),
                    "Null partitionKeyDefinition should throw");
            }

            using (Stream badJson = new MemoryStream(Encoding.UTF8.GetBytes("not valid json {{{")))
            {
                try
                {
                    ThinClientQueryPlanHelper.DeserializeQueryPlanResponse(badJson, HashPartitionKeyDefinition);
                    Assert.Fail("Malformed JSON should throw");
                }
                catch (System.Text.Json.JsonException)
                {
                    // Expected - System.Text.Json throws JsonException or a derived type for malformed JSON
                }
            }

            using (Stream nullJson = new MemoryStream(Encoding.UTF8.GetBytes("null")))
            {
                Assert.ThrowsException<FormatException>(
                    () => ThinClientQueryPlanHelper.DeserializeQueryPlanResponse(nullJson, HashPartitionKeyDefinition),
                    "JSON null should throw FormatException");
            }
        }

        #endregion

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
