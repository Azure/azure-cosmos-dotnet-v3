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
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using JsonReader = Json.JsonReader;
    using JsonWriter = Json.JsonWriter;

    [TestClass]
    public class SynchronizationContextTests : BaseCosmosClientHelper
    {
        private Container Container = null;

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit();
            string PartitionKey = "/status";
            ContainerProperties containerSettings = new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: PartitionKey);
            ContainerResponse response = await this.database.CreateContainerAsync(
                containerSettings,
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
        [Timeout(30000)]
        public void VerifySynchronizationContextDoesNotLock()
        {
            SynchronizationContext prevContext = SynchronizationContext.Current;
            try
            {
                TestSynchronizationContext syncContext = new TestSynchronizationContext();
                SynchronizationContext.SetSynchronizationContext(syncContext);

                Cosmos.Database database = this.cosmosClient.GetDatabase(this.database.Id);
                Container container = this.cosmosClient.GetContainer(this.database.Id, this.Container.Id);

                syncContext.Post(_ => {
                    database.ReadStreamAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                    database.ReadAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                    ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
                    ItemResponse<ToDoActivity> response = container.CreateItemAsync<ToDoActivity>(item: testItem).ConfigureAwait(false).GetAwaiter().GetResult();
                    Assert.IsNotNull(response);

                    container.GetItemLinqQueryable<ToDoActivity>(
                        allowSynchronousQueryExecution: true,
                        requestOptions: new QueryRequestOptions()
                        {
                        }).ToList();

                    ItemResponse<ToDoActivity> deleteResponse = container.DeleteItemAsync<ToDoActivity>(partitionKey: new Cosmos.PartitionKey(testItem.status), id: testItem.id).ConfigureAwait(false).GetAwaiter().GetResult();
                    Assert.IsNotNull(deleteResponse);
                }, state: null);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(prevContext);
            }
        }

        public class TestSynchronizationContext : SynchronizationContext
        {
            private object locker = new object();

            public override void Post(SendOrPostCallback d, object state)
            {
                lock (locker)
                {
                    d(state);
                }
            }
        }
    }
}
