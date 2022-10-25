﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Linq;
    using System.Threading;
    using Microsoft.Azure.Cosmos.Linq;
    using Microsoft.Azure.Cosmos.Services.Management.Tests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

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
                        Logger.LogLine("1");
                        Cosmos.Database database = client.CreateDatabaseAsync(databaseId).GetAwaiter().GetResult();
                        Logger.LogLine("a");
                        database = client.CreateDatabaseIfNotExistsAsync(databaseId).GetAwaiter().GetResult();
                        Logger.LogLine("b");
                        database.ReadStreamAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                        Logger.LogLine("c");
                        database.ReadAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                        Logger.LogLine("2");
                        QueryDefinition databaseQuery = new QueryDefinition("select * from T where T.id = @id").WithParameter("@id", databaseId);
                        FeedIterator<DatabaseProperties> databaseIterator = client.GetDatabaseQueryIterator<DatabaseProperties>(databaseQuery);
                        while (databaseIterator.HasMoreResults)
                        {
                            databaseIterator.ReadNextAsync().GetAwaiter().GetResult();
                        }
                        Logger.LogLine("3");
                        Container container = database.CreateContainerAsync(Guid.NewGuid().ToString(), "/pk").GetAwaiter().GetResult();
                        container = database.CreateContainerIfNotExistsAsync(container.Id, "/pk").GetAwaiter().GetResult();
                        Logger.LogLine("4");
                        ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
                        ItemResponse<ToDoActivity> response = container.CreateItemAsync<ToDoActivity>(item: testItem).ConfigureAwait(false).GetAwaiter().GetResult();
                        Assert.IsNotNull(response);
                        string diagnostics = response.Diagnostics.ToString();
                        Assert.IsTrue(diagnostics.Contains("Synchronization Context"));
                        Logger.LogLine("5");
                        using CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

                        try
                        {
                            ToDoActivity tempItem = ToDoActivity.CreateRandomToDoActivity();
                            CancellationToken cancellationToken = cancellationTokenSource.Token;
                            cancellationTokenSource.Cancel();
                            container.CreateItemAsync<ToDoActivity>(item: tempItem, cancellationToken: cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
                            Assert.Fail("Should have thrown a cancellation token");

                        }
                        catch (CosmosOperationCanceledException oe)
                        {
                            string exception = oe.ToString();
                            Assert.IsTrue(exception.Contains("Synchronization Context"));
                        }
                        Logger.LogLine("6");
                        // Test read feed
                        container.GetItemLinqQueryable<ToDoActivity>(
                            allowSynchronousQueryExecution: true,
                            requestOptions: new QueryRequestOptions()
                            {
                            }).ToList();

                        FeedIterator feedIterator = container
                            .GetItemLinqQueryable<ToDoActivity>()
                            .ToStreamIterator();

                        while (feedIterator.HasMoreResults)
                        {
                            feedIterator.ReadNextAsync().GetAwaiter().GetResult();
                        }
                        Logger.LogLine("7");
                        FeedIterator<ToDoActivity> feedIteratorTyped = container.GetItemLinqQueryable<ToDoActivity>()
                            .ToFeedIterator<ToDoActivity>();

                        while (feedIteratorTyped.HasMoreResults)
                        {
                            feedIteratorTyped.ReadNextAsync().GetAwaiter().GetResult();
                        }
                        Logger.LogLine("8");
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

                        Logger.LogLine("9");

                        FeedIterator<ToDoActivity> queryIteratorTyped = container.GetItemLinqQueryable<ToDoActivity>()
                            .Where(item => item.id != "").ToFeedIterator<ToDoActivity>();

                        while (queryIteratorTyped.HasMoreResults)
                        {
                            queryIteratorTyped.ReadNextAsync().GetAwaiter().GetResult();
                        }

                        Logger.LogLine("10");

                        double costAsync = container.GetItemLinqQueryable<ToDoActivity>()
                            .Select(x => x.cost).SumAsync().GetAwaiter().GetResult();

                        double cost = container.GetItemLinqQueryable<ToDoActivity>(
                            allowSynchronousQueryExecution: true).Select(x => x.cost).Sum();
                        Logger.LogLine("11");
                        ItemResponse<ToDoActivity> deleteResponse = container.DeleteItemAsync<ToDoActivity>(partitionKey: new Cosmos.PartitionKey(testItem.pk), id: testItem.id).ConfigureAwait(false).GetAwaiter().GetResult();
                        Assert.IsNotNull(deleteResponse);
                        Logger.LogLine("12");
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
                lock (locker)
                {
                    d(state);
                }
            }
        }
    }
}
