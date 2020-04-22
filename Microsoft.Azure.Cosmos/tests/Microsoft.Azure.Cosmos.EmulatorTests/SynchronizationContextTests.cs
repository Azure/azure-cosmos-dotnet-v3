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
    using System.Xml.Linq;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Linq;
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
    public class SynchronizationContextTests
    {
        [TestMethod]
        [Timeout(30000)]
        public void VerifySynchronizationContextDoesNotLock()
        {
            string databaseId = Guid.NewGuid().ToString();
            SynchronizationContext prevContext = SynchronizationContext.Current;
            try
            {
                TestSynchronizationContext syncContext = new TestSynchronizationContext();
                SynchronizationContext.SetSynchronizationContext(syncContext);
                syncContext.Post(_ =>
                {
                    using (CosmosClient client = TestCommon.CreateCosmosClient())
                    {
                        Cosmos.Database database = client.CreateDatabaseAsync(databaseId).GetAwaiter().GetResult();
                        database = client.CreateDatabaseIfNotExistsAsync(databaseId).GetAwaiter().GetResult();

                        database.ReadStreamAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                        database.ReadAsync().ConfigureAwait(false).GetAwaiter().GetResult();

                        QueryDefinition databaseQuery = new QueryDefinition("select * from T where T.id = @id").WithParameter("@id", databaseId);
                        FeedIterator<DatabaseProperties> databaseIterator = client.GetDatabaseQueryIterator<DatabaseProperties>(databaseQuery);
                        while (databaseIterator.HasMoreResults)
                        {
                            databaseIterator.ReadNextAsync().GetAwaiter().GetResult();
                        }

                        Container container = database.CreateContainerAsync(Guid.NewGuid().ToString(), "/status").GetAwaiter().GetResult();
                        container = database.CreateContainerIfNotExistsAsync(container.Id, "/status").GetAwaiter().GetResult();

                        ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
                        ItemResponse<ToDoActivity> response = container.CreateItemAsync<ToDoActivity>(item: testItem).ConfigureAwait(false).GetAwaiter().GetResult();
                        Assert.IsNotNull(response);

                        // Test read feed
                        container.GetItemLinqQueryable<ToDoActivity>(
                            allowSynchronousQueryExecution: true,
                            requestOptions: new QueryRequestOptions()
                            {
                            }).ToList();

                        FeedIterator feedIterator = container.GetItemLinqQueryable<ToDoActivity>()
                            .ToStreamIterator();

                        while (feedIterator.HasMoreResults)
                        {
                            feedIterator.ReadNextAsync().GetAwaiter().GetResult();
                        }

                        FeedIterator<ToDoActivity> feedIteratorTyped = container.GetItemLinqQueryable<ToDoActivity>()
                            .ToFeedIterator<ToDoActivity>();

                        while (feedIteratorTyped.HasMoreResults)
                        {
                            feedIteratorTyped.ReadNextAsync().GetAwaiter().GetResult();
                        }

                        // Test query
                        container.GetItemLinqQueryable<ToDoActivity>(
                            allowSynchronousQueryExecution: true,
                            requestOptions: new QueryRequestOptions()
                            {
                            }).Where(item => item.id != "").ToList();

                        FeedIterator queryIterator = container.GetItemLinqQueryable<ToDoActivity>()
                            .Where(item => item.id != "").ToStreamIterator();

                        while (queryIterator.HasMoreResults)
                        {
                            queryIterator.ReadNextAsync().GetAwaiter().GetResult();
                        }

                        FeedIterator<ToDoActivity> queryIteratorTyped = container.GetItemLinqQueryable<ToDoActivity>()
                            .Where(item => item.id != "").ToFeedIterator<ToDoActivity>();

                        while (queryIteratorTyped.HasMoreResults)
                        {
                            queryIteratorTyped.ReadNextAsync().GetAwaiter().GetResult();
                        }

                        double costAsync = container.GetItemLinqQueryable<ToDoActivity>()
                            .Select(x => x.cost).SumAsync().GetAwaiter().GetResult();

                        double cost = container.GetItemLinqQueryable<ToDoActivity>(
                            allowSynchronousQueryExecution: true).Select(x => x.cost).Sum();

                        ItemResponse<ToDoActivity> deleteResponse = container.DeleteItemAsync<ToDoActivity>(partitionKey: new Cosmos.PartitionKey(testItem.status), id: testItem.id).ConfigureAwait(false).GetAwaiter().GetResult();
                        Assert.IsNotNull(deleteResponse);
                    }
                }, state: null);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(prevContext);
                using (CosmosClient client = TestCommon.CreateCosmosClient())
                {
                    client.GetDatabase(databaseId).DeleteAsync().GetAwaiter().GetResult();
                }
            }
        }

        public class TestSynchronizationContext : SynchronizationContext
        {
            private object locker = new object();

            public override void Post(SendOrPostCallback d, object state)
            {
                lock (this.locker)
                {
                    d(state);
                }
            }
        }
    }
}
