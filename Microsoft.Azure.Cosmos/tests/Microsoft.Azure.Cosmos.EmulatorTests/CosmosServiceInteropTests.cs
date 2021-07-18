//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Runtime.Serialization;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Cosmos;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using JsonReader = Json.JsonReader;
    using JsonSerializer = Json.JsonSerializer;
    using JsonWriter = Json.JsonWriter;
    using PartitionKey = Documents.PartitionKey;
    using static Microsoft.Azure.Cosmos.SDK.EmulatorTests.TransportClientHelper;

    [TestClass]
    public class CosmosServiceInteropTests : BaseCosmosClientHelper
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
                throughput: 70000,
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

        [DataTestMethod]
        public async Task QuerySinglePartitionItemStreamTest()
        {
            IList<ToDoActivity> deleteList = await ToDoActivity.CreateRandomItems(
                this.Container,
                pkCount: 1,
                perPKItemCount: 200,
                randomPartitionKey: true);

            ToDoActivity find = deleteList.First();

            QueryDefinition sql = new QueryDefinition("select * from r");
            ContainerCore containerInternal = (ContainerCore)this.Container;

            QueryOptimizedIterator queryOptimizedIterator = new QueryOptimizedIterator(
                containerInternal,
                containerInternal.queryClient,
                sql,
                new QueryRequestOptions()
                {
                    MaxItemCount = 100,
                    PartitionKey = new Cosmos.PartitionKey(find.pk),
                },
                containerInternal.ClientContext);

            while (queryOptimizedIterator.HasMoreResults)
            {
                using ResponseMessage response = await queryOptimizedIterator.ReadNextAsync();
                Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
            }
        }

        [DataTestMethod]
        public async Task QuerySinglePartitionItemStreamTest2()
        {
            IList<ToDoActivity> deleteList = await ToDoActivity.CreateRandomItems(
                this.Container,
                pkCount: 1,
                perPKItemCount: 5,
                randomPartitionKey: true);

            ToDoActivity find = deleteList.First();

            QueryDefinition sql = new QueryDefinition(@"SELECT COUNT(1) AS Count
                FROM child IN Families.children");
            using FeedIterator queryIterator = this.Container.GetItemQueryStreamIterator(
                sql,
                requestOptions: new QueryRequestOptions()
                {
                    MaxItemCount = 100,
                    PartitionKey = new Cosmos.PartitionKey(find.pk),
                    //ForceGatewayQueryPlan = true,
                });

            while (queryIterator.HasMoreResults)
            {
                using ResponseMessage response = await queryIterator.ReadNextAsync();
                using StreamReader streamReader = new StreamReader(response.Content);
                string result = await streamReader.ReadToEndAsync();
                Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
                string diagnostics = response.Diagnostics.ToString();
            }

            using FeedIterator queryOptimizedIterator = this.Container.GetItemOptimizedQueryStreamIterator(
                sql,
                requestOptions: new QueryRequestOptions()
                {
                    MaxItemCount = 100,
                    PartitionKey = new Cosmos.PartitionKey(find.pk),
                    //ForceGatewayQueryPlan = true,
                });

            while (queryOptimizedIterator.HasMoreResults)
            {
                using ResponseMessage response = await queryOptimizedIterator.ReadNextAsync();
                using StreamReader streamReader = new StreamReader(response.Content);
                string result = await streamReader.ReadToEndAsync();
                Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
                string diagnostics = response.Diagnostics.ToString();
            }
        }
    }
}