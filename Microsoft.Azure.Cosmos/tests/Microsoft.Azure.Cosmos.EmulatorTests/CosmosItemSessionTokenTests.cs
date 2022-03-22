//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Cosmos;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using PartitionKey = Documents.PartitionKey;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using System.IO;
    using System.Net;

    [TestClass]
    public class CosmosItemSessionTokenTests : BaseCosmosClientHelper
    {
        private Container Container = null;
        private ContainerProperties containerSettings = null;

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit(validateSinglePartitionKeyRangeCacheCall: true);
            string PartitionKey = "/pk";
            this.containerSettings = new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: PartitionKey);
            ContainerResponse response = await this.database.CreateContainerAsync(
                this.containerSettings,
                cancellationToken: this.cancellationToken);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Container);
            Assert.IsNotNull(response.Resource);
            this.Container = response;
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        public async Task CreateDropItemTest()
        {
            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
            this.ResetSessionToken();
            Assert.IsNull(await this.GetLSNFromSessionContainer(new PartitionKey(testItem.pk)));
            ItemResponse<ToDoActivity> response = await this.Container.CreateItemAsync<ToDoActivity>(item: testItem);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Resource);
            Assert.IsNotNull(response.Diagnostics);
            long? lsnAfterCreate = await this.GetLSNFromSessionContainer(new PartitionKey(testItem.pk));
            Assert.IsNotNull(lsnAfterCreate);
            CosmosTraceDiagnostics diagnostics = (CosmosTraceDiagnostics)response.Diagnostics;
            Assert.IsFalse(diagnostics.IsGoneExceptionHit());
            Assert.IsFalse(string.IsNullOrEmpty(diagnostics.ToString()));
            Assert.IsTrue(diagnostics.GetClientElapsedTime() > TimeSpan.Zero);

            response = await this.Container.ReadItemAsync<ToDoActivity>(testItem.id, new Cosmos.PartitionKey(testItem.pk));
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Resource);
            Assert.IsNotNull(response.Diagnostics);
            Assert.IsFalse(string.IsNullOrEmpty(response.Diagnostics.ToString()));
            Assert.IsTrue(response.Diagnostics.GetClientElapsedTime() > TimeSpan.Zero);
            long? lsnAfterRead = await this.GetLSNFromSessionContainer(new PartitionKey(testItem.pk));
            Assert.IsNotNull(lsnAfterRead);
            Assert.AreEqual(lsnAfterCreate.Value, lsnAfterRead.Value);

            Assert.IsNotNull(response.Headers.GetHeaderValue<string>(Documents.HttpConstants.HttpHeaders.MaxResourceQuota));
            Assert.IsNotNull(response.Headers.GetHeaderValue<string>(Documents.HttpConstants.HttpHeaders.CurrentResourceQuotaUsage));
            ItemResponse<ToDoActivity> deleteResponse = await this.Container.DeleteItemAsync<ToDoActivity>(partitionKey: new Cosmos.PartitionKey(testItem.pk), id: testItem.id);
            Assert.IsNotNull(deleteResponse);
            Assert.IsNotNull(response.Diagnostics);
            Assert.IsFalse(string.IsNullOrEmpty(response.Diagnostics.ToString()));
            Assert.IsTrue(response.Diagnostics.GetClientElapsedTime() > TimeSpan.Zero);
            long? lsnAfterDelete = await this.GetLSNFromSessionContainer(new PartitionKey(testItem.pk));
            Assert.IsNotNull(lsnAfterDelete);
            Assert.IsTrue(lsnAfterDelete.Value > lsnAfterCreate.Value);
        }

        [TestMethod]
        public async Task ReplaceItemStreamTest()
        {
            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
            this.ResetSessionToken();
            Assert.IsNull(await this.GetLSNFromSessionContainer(new PartitionKey(testItem.pk)));

            using (Stream stream = TestCommon.SerializerCore.ToStream<ToDoActivity>(testItem))
            {
                //Create the item
                using (ResponseMessage response = await this.Container.CreateItemStreamAsync(partitionKey: new Cosmos.PartitionKey(testItem.pk), streamPayload: stream))
                {
                    Assert.IsNotNull(response);
                    Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
                }
            }

            long? lsnAfterCreate = await this.GetLSNFromSessionContainer(new PartitionKey(testItem.pk));
            Assert.IsNotNull(lsnAfterCreate);

            this.ResetSessionToken();
            Assert.IsNull(await this.GetLSNFromSessionContainer(new PartitionKey(testItem.pk)));

            using (Stream stream = TestCommon.SerializerCore.ToStream<ToDoActivity>(testItem))
            {
                //Replace a non-existing item. It should fail, and not throw an exception.
                using (ResponseMessage response = await this.Container.ReplaceItemStreamAsync(
                    partitionKey: new Cosmos.PartitionKey("SomeNonExistingId"),
                    id: "SomeNonExistingId",
                    streamPayload: stream))
                {
                    Assert.IsFalse(response.IsSuccessStatusCode);
                    Assert.IsNotNull(response);
                    Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode, response.ErrorMessage);

                    // Session token should be captured for NotFound with SubStatusCode 0
                    long? lsnAfterNotFound = await this.GetLSNFromSessionContainer(new PartitionKey(testItem.pk));
                    Assert.IsNotNull(lsnAfterNotFound);
                    Assert.AreEqual(lsnAfterCreate.Value, lsnAfterNotFound.Value);
                }
            }

            this.ResetSessionToken();
            Assert.IsNull(await this.GetLSNFromSessionContainer(new PartitionKey(testItem.pk)));
            
            //Updated the taskNum field
            testItem.taskNum = 9001;
            using (Stream stream = TestCommon.SerializerCore.ToStream<ToDoActivity>(testItem))
            {
                using (ResponseMessage response = await this.Container.ReplaceItemStreamAsync(partitionKey: new Cosmos.PartitionKey(testItem.pk), id: testItem.id, streamPayload: stream))
                {
                    Assert.IsNotNull(response);
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                }

                long? lsnAfterReplace= await this.GetLSNFromSessionContainer(new PartitionKey(testItem.pk));
                Assert.IsNotNull(lsnAfterReplace);
                Assert.IsTrue(lsnAfterReplace.Value > lsnAfterCreate.Value);

                using (ResponseMessage deleteResponse = await this.Container.DeleteItemStreamAsync(partitionKey: new Cosmos.PartitionKey(testItem.pk), id: testItem.id))
                {
                    Assert.IsNotNull(deleteResponse);
                    Assert.AreEqual(deleteResponse.StatusCode, HttpStatusCode.NoContent);
                }

                long? lsnAfterDelete = await this.GetLSNFromSessionContainer(new PartitionKey(testItem.pk));
                Assert.IsNotNull(lsnAfterDelete);
                Assert.IsTrue(lsnAfterDelete.Value > lsnAfterReplace.Value);
            }
        }

        [TestMethod]
        public async Task UpsertItemTest()
        {
            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
            this.ResetSessionToken();
            Assert.IsNull(await this.GetLSNFromSessionContainer(new PartitionKey(testItem.pk)));

            ItemResponse<ToDoActivity> response = await this.Container.UpsertItemAsync(testItem, partitionKey: new Cosmos.PartitionKey(testItem.pk));
            Assert.IsNotNull(response);
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
            Assert.IsNotNull(response.Headers.Session);
            long? lsnAfterCreate = await this.GetLSNFromSessionContainer(new PartitionKey(testItem.pk));
            Assert.IsNotNull(lsnAfterCreate);

            this.ResetSessionToken();
            Assert.IsNull(await this.GetLSNFromSessionContainer(new PartitionKey(testItem.pk)));

            //Updated the taskNum field
            testItem.taskNum = 9001;
            response = await this.Container.UpsertItemAsync(testItem, partitionKey: new Cosmos.PartitionKey(testItem.pk));

            Assert.IsNotNull(response);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.IsNotNull(response.Headers.Session);

            long? lsnAfterUpsert = await this.GetLSNFromSessionContainer(new PartitionKey(testItem.pk));
            Assert.IsNotNull(lsnAfterUpsert);
            Assert.IsTrue(lsnAfterUpsert.Value > lsnAfterCreate.Value);
        }

        [TestMethod]
        public async Task NoSessionTokenCaptureForThrottledUpsertRequestsTest()
        {
            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
            this.ResetSessionToken();
            Assert.IsNull(await this.GetLSNFromSessionContainer(new PartitionKey(testItem.pk)));

            ItemResponse<ToDoActivity> response = await this.Container.CreateItemAsync<ToDoActivity>(item: testItem);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Resource);
            Assert.IsNotNull(response.Diagnostics);
            string sessionTokenHeaderValue = response.Headers[HttpConstants.HttpHeaders.SessionToken];
            Assert.IsNotNull(sessionTokenHeaderValue);
            
            long? lsnAfterCreate = await this.GetLSNFromSessionContainer(new PartitionKey(testItem.pk));
            Assert.IsNotNull(lsnAfterCreate);

            this.ResetSessionToken();
            Assert.IsNull(await this.GetLSNFromSessionContainer(new PartitionKey(testItem.pk)));

            Container throttledContainer = TransportClientHelper.GetContainerWithIntercepter(
                this.database.Id,
                this.Container.Id,
                (uri, resourceOperation, documentServiceRequest) => { },
                useGatewayMode: false,
                (uri, resourceOperation, documentServiceRequest) =>
                {
                    StoreResponse throttledResponse = TransportClientHelper.ReturnThrottledStoreResponseOnItemOperation(
                        uri, resourceOperation, documentServiceRequest, Guid.NewGuid(), string.Empty);

                    throttledResponse.Headers.Add(HttpConstants.HttpHeaders.SessionToken, sessionTokenHeaderValue);

                    return throttledResponse;
                },
                this.cosmosClient.DocumentClient.sessionContainer);

            try
            {
                //Updated the taskNum field
                testItem.taskNum = 9001;
                response = await throttledContainer.UpsertItemAsync(testItem, partitionKey: new Cosmos.PartitionKey(testItem.pk));
            }
            catch (CosmosException cosmosException)
            {
                Assert.AreEqual(HttpStatusCode.TooManyRequests, cosmosException.StatusCode);
            }

            long? lsnAfterThrottledRequest = await this.GetLSNFromSessionContainer(new PartitionKey(testItem.pk));
            Assert.IsNull(lsnAfterThrottledRequest);
        }

        private async Task<string> GetPKRangeIdForPartitionKey(
            PartitionKey pkValue)
        {
            DocumentFeedResponse<PartitionKeyRange> pkRanges = await this.cosmosClient.DocumentClient.ReadPartitionKeyRangeFeedAsync(
                UriFactory.CreateDocumentCollectionUri(this.Container.Database.Id, this.Container.Id));
            List<string> maxExclusiveBoundaries = pkRanges.Select(pkRange => pkRange.MaxExclusive).ToList();

            string effectivePK1 = pkValue.InternalKey.GetEffectivePartitionKeyString(this.containerSettings.PartitionKey);
            int pkIndex = 0;
            while (pkIndex < maxExclusiveBoundaries.Count && string.Compare(effectivePK1, maxExclusiveBoundaries[pkIndex]) >= 0)
            {
                ++pkIndex;
            }

            if (pkIndex == maxExclusiveBoundaries.Count)
            {
                throw new Exception("Failed to find the range");
            }

            return pkIndex.ToString(CultureInfo.InvariantCulture);
        }

        private async Task<Nullable<long>> GetLSNFromSessionContainer(PartitionKey pkValue)
        {
            string path = $"dbs/{this.Container.Database.Id}/colls/{this.Container.Id}";
            string pkRangeId = await this.GetPKRangeIdForPartitionKey(pkValue);
            DocumentServiceRequest dummyRequest = new DocumentServiceRequest(
                OperationType.Read,
                ResourceType.Document,
                path,
                body: null,
                AuthorizationTokenType.PrimaryMasterKey,
                headers: null);

            ISessionToken sessionToken = this.cosmosClient.DocumentClient.sessionContainer.ResolvePartitionLocalSessionToken(
                dummyRequest,
                pkRangeId);

            return sessionToken?.LSN;
        }

        private void ResetSessionToken()
        {
            string path = $"dbs/{this.Container.Database.Id}/colls/{this.Container.Id}";
            this.cosmosClient.DocumentClient.sessionContainer.ClearTokenByCollectionFullname(path);

        }
    }
}