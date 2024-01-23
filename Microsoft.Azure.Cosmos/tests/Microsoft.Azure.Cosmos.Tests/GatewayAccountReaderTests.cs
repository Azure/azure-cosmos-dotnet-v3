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
        [Owner("dkunda")]
        [DataRow(true, DisplayName = "Validate that when regional endpoints are provided in the connection policy, the request will be retried in the regional endpoints.")]
        [DataRow(false, DisplayName = "Validate that when regional endpoints are not provided in the connection policy, the request will be failed in the primary endpoint.")]
        public async Task InitializeReaderAsync_WhenRegionalEndpointsProvided_ShouldRetryWithRegionalEndpointsWhenPrimaryFails(
            bool regionalEndpointsProvided)
        {
            string accountPropertiesResponse = "{\r\n    \"_self\": \"\",\r\n    \"id\": \"localhost\",\r\n    \"_rid\": \"127.0.0.1\",\r\n    \"media\": \"//media/\",\r\n    \"addresses\": \"//addresses/\",\r\n    \"_dbs\": \"//dbs/\",\r\n    \"writableLocations\": [\r\n        {\r\n            \"name\": \"South Central US\",\r\n            \"databaseAccountEndpoint\": \"https://127.0.0.1:8081/\"\r\n        }" +
                "\r\n    ],\r\n    \"readableLocations\": [\r\n        {\r\n            \"name\": \"South Central US\",\r\n            \"databaseAccountEndpoint\": \"https://127.0.0.1:8081/\"\r\n        }\r\n    ],\r\n    \"enableMultipleWriteLocations\": false,\r\n    \"userReplicationPolicy\": {\r\n        \"asyncReplication\": false,\r\n        \"minReplicaSetSize\": 1,\r\n        \"maxReplicasetSize\": 4\r\n    },\r\n    \"userConsistencyPolicy\": {\r\n        " +
                "\"defaultConsistencyLevel\": \"Session\"\r\n    },\r\n    \"systemReplicationPolicy\": {\r\n        \"minReplicaSetSize\": 1,\r\n        \"maxReplicasetSize\": 4\r\n    },\r\n    \"readPolicy\": {\r\n        \"primaryReadCoefficient\": 1,\r\n        \"secondaryReadCoefficient\": 1\r\n    },\r\n    \"queryEngineConfiguration\": \"{\\\"maxSqlQueryInputLength\\\":262144,\\\"maxJoinsPerSqlQuery\\\":5," +
                "\\\"maxLogicalAndPerSqlQuery\\\":500,\\\"maxLogicalOrPerSqlQuery\\\":500,\\\"maxUdfRefPerSqlQuery\\\":10,\\\"maxInExpressionItemsCount\\\":16000,\\\"queryMaxInMemorySortDocumentCount\\\":500,\\\"maxQueryRequestTimeoutFraction\\\":0.9,\\\"sqlAllowNonFiniteNumbers\\\":false,\\\"sqlAllowAggregateFunctions\\\":true,\\\"sqlAllowSubQuery\\\":true,\\\"sqlAllowScalarSubQuery\\\":true,\\\"allowNewKeywords\\\":true,\\\"" +
                "sqlAllowLike\\\":true,\\\"sqlAllowGroupByClause\\\":true,\\\"maxSpatialQueryCells\\\":12,\\\"spatialMaxGeometryPointCount\\\":256,\\\"sqlDisableOptimizationFlags\\\":0,\\\"sqlAllowTop\\\":true,\\\"enableSpatialIndexing\\\":true}\"\r\n}";
            
            StringContent content = new(accountPropertiesResponse);
            HttpResponseMessage responseMessage = new()
            {
                StatusCode = HttpStatusCode.OK,
                Content = content,
            };

            Mock<CosmosHttpClient> mockHttpClient = new();
            mockHttpClient
                .SetupSequence(x => x.GetAsync(
                    It.IsAny<Uri>(),
                    It.IsAny<INameValueCollection>(),
                    It.IsAny<ResourceType>(),
                    It.IsAny<HttpTimeoutPolicy>(),
                    It.IsAny<IClientSideRequestStatistics>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Service is Unavailable at the Moment."))
                .ThrowsAsync(new Exception("Service is Unavailable at the Moment."))
                .ReturnsAsync(responseMessage);

            ConnectionPolicy connectionPolicy = new()
            {
                ConnectionMode = ConnectionMode.Direct,
            };

            if (regionalEndpointsProvided)
            {
                connectionPolicy.SetRegionalEndpoints(
                    new List<string>()
                    {
                    "https://testfed2.documents-test.windows-int.net:443/",
                    "https://testfed3.documents-test.windows-int.net:443/",
                    "https://testfed4.documents-test.windows-int.net:443/",
                    });
            }

            GatewayAccountReader accountReader = new GatewayAccountReader(
                serviceEndpoint: new Uri("https://testfed1.documents-test.windows-int.net:443/"),
                cosmosAuthorization: Mock.Of<AuthorizationTokenProvider>(),
                connectionPolicy: connectionPolicy,
                httpClient: mockHttpClient.Object);

            if (regionalEndpointsProvided)
            {
                AccountProperties accountProperties = await accountReader.InitializeReaderAsync();

                Assert.IsNotNull(accountProperties);
                Assert.AreEqual("localhost", accountProperties.Id);
                Assert.AreEqual("127.0.0.1", accountProperties.ResourceId);
            }
            else
            {
                Exception exception = await Assert.ThrowsExceptionAsync<Exception>(() => accountReader.InitializeReaderAsync());
                Assert.IsNotNull(exception);
                Assert.AreEqual("Service is Unavailable at the Moment.", exception.Message);
            }
        }

        [TestMethod]
        [Owner("dkunda")]
        public async Task InitializeReaderAsync_WhenRegionalEndpointsProvided_ShouldThrowAggregateExceptionWithAllEndpointsFail()
        {
            Mock<CosmosHttpClient> mockHttpClient = new();
            mockHttpClient
                .Setup(x => x.GetAsync(
                    It.IsAny<Uri>(),
                    It.IsAny<INameValueCollection>(),
                    It.IsAny<ResourceType>(),
                    It.IsAny<HttpTimeoutPolicy>(),
                    It.IsAny<IClientSideRequestStatistics>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Service is Unavailable at the Moment."));

            ConnectionPolicy connectionPolicy = new()
            {
                ConnectionMode = ConnectionMode.Direct,
            };

            connectionPolicy.SetRegionalEndpoints(
                new List<string>()
                {
                "https://testfed2.documents-test.windows-int.net:443/",
                "https://testfed3.documents-test.windows-int.net:443/",
                "https://testfed4.documents-test.windows-int.net:443/",
                });

            GatewayAccountReader accountReader = new GatewayAccountReader(
                serviceEndpoint: new Uri("https://testfed1.documents-test.windows-int.net:443/"),
                cosmosAuthorization: Mock.Of<AuthorizationTokenProvider>(),
                connectionPolicy: connectionPolicy,
                httpClient: mockHttpClient.Object);

            AggregateException exception = await Assert.ThrowsExceptionAsync<AggregateException>(() => accountReader.InitializeReaderAsync());
            Assert.IsNotNull(exception);
            Assert.AreEqual("Service is Unavailable at the Moment.", exception.InnerException.Message);
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
