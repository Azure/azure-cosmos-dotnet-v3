//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.Scripts;
    using Microsoft.Azure.Cosmos;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestClass]
    public class AsyncEnumerationTests : BaseCosmosClientHelper
    {
        private CosmosContainer Container = null;

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit();
            string PartitionKey = "/status";
            CosmosContainerProperties containerSettings = new CosmosContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: PartitionKey);
            CosmosContainerResponse response = await this.database.CreateContainerAsync(
                containerSettings,
                cancellationToken: this.cancellationToken);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Container);
            Assert.IsNotNull(response.Value);
            this.Container = response;
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [Timeout(30000)]
        [TestMethod]
        [ExpectedException(typeof(OperationCanceledException))]
        public async Task IAsyncEnumerableCancels()
        {
            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
            await this.Container.CreateItemAsync<ToDoActivity>(item: testItem);
            ToDoActivity testItem2 = ToDoActivity.CreateRandomToDoActivity();
            await this.Container.CreateItemAsync<ToDoActivity>(item: testItem2);

            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            IAsyncEnumerable<Response> enumerable = this.Container.GetItemQueryStreamIterator(requestOptions: new QueryRequestOptions() { MaxItemCount = 1 }, cancellationToken: cancellationTokenSource.Token);
            int iterations = 0;
            await foreach (Response response in enumerable)
            {
                Assert.AreEqual((int)HttpStatusCode.OK, response.Status);
                if (iterations++ == 0)
                {
                    cancellationTokenSource.Cancel();
                }
            }

            Assert.Fail("Should had thrown");
        }

        [Timeout(30000)]
        [TestMethod]
        [ExpectedException(typeof(OperationCanceledException))]
        public async Task AsyncPageableCancels()
        {
            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
            await this.Container.CreateItemAsync<ToDoActivity>(item: testItem);
            ToDoActivity testItem2 = ToDoActivity.CreateRandomToDoActivity();
            await this.Container.CreateItemAsync<ToDoActivity>(item: testItem2);

            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            AsyncPageable<ToDoActivity> enumerable = this.Container.GetItemQueryIterator<ToDoActivity>(requestOptions: new QueryRequestOptions() { MaxItemCount = 1 }, cancellationToken: cancellationTokenSource.Token);
            int iterations = 0;
            await foreach (ToDoActivity item in enumerable)
            {
                if (iterations++ == 0)
                {
                    cancellationTokenSource.Cancel();
                }
            }

            Assert.Fail("Should had thrown");
        }

        [Timeout(30000)]
        [TestMethod]
        [ExpectedException(typeof(OperationCanceledException))]
        public async Task AsyncPageableAsPagesCancels()
        {
            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
            await this.Container.CreateItemAsync<ToDoActivity>(item: testItem);
            ToDoActivity testItem2 = ToDoActivity.CreateRandomToDoActivity();
            await this.Container.CreateItemAsync<ToDoActivity>(item: testItem2);

            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            IAsyncEnumerable<Page<ToDoActivity>> enumerable = this.Container.GetItemQueryIterator<ToDoActivity>(requestOptions: new QueryRequestOptions() { MaxItemCount = 1 }, cancellationToken: cancellationTokenSource.Token).AsPages();
            int iterations = 0;
            await foreach (Page<ToDoActivity> item in enumerable)
            {
                if (iterations++ == 0)
                {
                    cancellationTokenSource.Cancel();
                }
            }

            Assert.Fail("Should had thrown");
        }
    }
}
