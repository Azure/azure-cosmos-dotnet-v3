//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Routing;
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
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        public async Task TestGatewayModelSession()
        {
            // Create items with different
            for (int i = 0; i < 1000; i++)
            {
                ToDoActivity item = ToDoActivity.CreateRandomToDoActivity();
                item.pk = "Status" + i.ToString();
                item.id = i.ToString();
                ItemResponse<ToDoActivity> itemResponse = await this.Container.CreateItemAsync(item);
                Assert.AreEqual(HttpStatusCode.Created, itemResponse.StatusCode);
            }

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
    }
}
