//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class GatewaySessionTokenTests : BaseCosmosClientHelper
    {
        private ContainerInternal Container = null;
        private const string PartitionKey = "/pk";

        [TestInitialize]
        public async Task TestInitialize()
        {
            this.cosmosClient = TestCommon.CreateCosmosClient(useGateway: true);
            this.database = await this.cosmosClient.CreateDatabaseAsync(
                   id: Guid.NewGuid().ToString());
            ContainerResponse response = await this.database.CreateContainerAsync(
                        new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: PartitionKey),
                        throughput: 20000,
                        cancellationToken: this.cancellationToken);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Container);
            Assert.IsNotNull(response.Resource);
            this.Container = (ContainerInlineCore)response;

            // Create items with different
            for (int i = 0; i < 500; i++)
            {
                ToDoActivity item = ToDoActivity.CreateRandomToDoActivity();
                item.pk = "Status" + i.ToString();
                item.id = i.ToString();
                ItemResponse<ToDoActivity> itemResponse = await this.Container.CreateItemAsync(item);
                Assert.AreEqual(HttpStatusCode.Created, itemResponse.StatusCode);
            }
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        public async Task TestGatewayModelSession()
        {
            ContainerProperties containerProperties = await this.Container.GetCachedContainerPropertiesAsync();

            ISessionContainer sessionContainer = this.cosmosClient.DocumentClient.sessionContainer;
            string docLink = "dbs/" + this.database.Id + "/colls/" + containerProperties.Id + "/docs/3";
            Documents.Collections.INameValueCollection headers = new StoreRequestNameValueCollection();
            headers.Set(HttpConstants.HttpHeaders.PartitionKey, "[\"Status3\"]");

            DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.Read, ResourceType.Document, docLink, AuthorizationTokenType.PrimaryMasterKey, headers);
            string globalSessionToken = sessionContainer.ResolveGlobalSessionToken(request);
            Assert.IsTrue(globalSessionToken.Split(',').Length > 1);

            await GatewayStoreModel.ApplySessionTokenAsync(request,
                                                           Cosmos.ConsistencyLevel.Session,
                                                           sessionContainer,
                                                           await this.cosmosClient.DocumentClient.GetPartitionKeyRangeCacheAsync(),
                                                           await this.cosmosClient.DocumentClient.GetCollectionCacheAsync());

            string sessionToken = request.Headers[HttpConstants.HttpHeaders.SessionToken];
            Assert.IsTrue(!string.IsNullOrEmpty(sessionToken) && sessionToken.Split(',').Length == 1);
        }

        [TestMethod]
        public async Task GatewaySameSessionTokenTest()
        {
            string createSessionToken = null;
            GatewaySessionTokenTests.HttpClientHandlerHelper httpClientHandler = new HttpClientHandlerHelper
            {
                ResponseCallBack = (result) =>
                {
                    HttpResponseMessage response = result.Result;
                    if (response.StatusCode != HttpStatusCode.Created)
                    {
                        return response;
                    }

                    response.Headers.TryGetValues("x-ms-session-token", out IEnumerable<string> sessionTokens);
                    foreach (string singleToken in sessionTokens)
                    {
                        createSessionToken = singleToken;
                        break;
                    }
                    return response;
                }
            };

            using (CosmosClient client = TestCommon.CreateCosmosClient(builder => builder
                .WithConnectionModeGateway()
                .WithConsistencyLevel(Cosmos.ConsistencyLevel.Session)
                .WithHttpClientFactory(() => new HttpClient(httpClientHandler))))
            {
                Container container = client.GetContainer(this.database.Id, this.Container.Id);

                ToDoActivity item = ToDoActivity.CreateRandomToDoActivity("Status1001", "1001");
                ItemResponse<ToDoActivity> itemResponse = await container.CreateItemAsync(item);

                // Read back the created Item and check if the session token is identical.
                string docLink = "dbs/" + this.database.Id + "/colls/" + this.Container.Id + "/docs/1001";
                Documents.Collections.INameValueCollection headers = new StoreRequestNameValueCollection();
                headers.Set(HttpConstants.HttpHeaders.PartitionKey, "[\"Status1001\"]");

                DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.Read, ResourceType.Document, docLink, AuthorizationTokenType.PrimaryMasterKey, headers);
                await GatewayStoreModel.ApplySessionTokenAsync(request,
                                                               Cosmos.ConsistencyLevel.Session,
                                                               client.DocumentClient.sessionContainer,
                                                               await client.DocumentClient.GetPartitionKeyRangeCacheAsync(),
                                                               await client.DocumentClient.GetCollectionCacheAsync());

                string readSessionToken = request.Headers[HttpConstants.HttpHeaders.SessionToken];
                Assert.AreEqual(readSessionToken, createSessionToken);
            }
        }

        private class HttpClientHandlerHelper : DelegatingHandler
        {
            public HttpClientHandlerHelper() : base(new HttpClientHandler())
            {
            }

            public Func<Task<HttpResponseMessage>, HttpResponseMessage> ResponseCallBack { get; set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return base.SendAsync(request, cancellationToken).ContinueWith(this.ResponseCallBack);
            }
        }
    }
}
