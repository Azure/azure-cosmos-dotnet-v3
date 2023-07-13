//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Linq;
    using Microsoft.Azure.Cosmos.Resource.Settings;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.Azure.Cosmos.Telemetry.Models;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestClass]
    public class SynchronizationContextTests
    {
        [DataTestMethod]
        [DataRow(false, DisplayName = "SynchronizationContextTests - Client Telemetry disabled")]
        [DataRow(true, DisplayName = "SynchronizationContextTests - Client Telemetry enabled")]
        [Timeout(30000)]
        public void VerifySynchronizationContextDoesNotLock(bool withClientTelemetry)
        {
            string databaseId = Guid.NewGuid().ToString();
            SynchronizationContext prevContext = SynchronizationContext.Current;
            try
            {
                TestSynchronizationContext syncContext = new TestSynchronizationContext();
                SynchronizationContext.SetSynchronizationContext(syncContext);
                syncContext.Post(_ =>
                {

                    HttpClientHandlerHelper httpHandler = new HttpClientHandlerHelper
                    {
                        RequestCallBack = (request, cancellation) =>
                        {
                            if (request.RequestUri.AbsoluteUri.Equals("http://dummy.test.com"))
                            {
                                HttpResponseMessage result = new HttpResponseMessage(HttpStatusCode.OK);
                                

                                return Task.FromResult(result);
                            }
                            else if (request.RequestUri.AbsoluteUri.Contains(Paths.ClientConfigPathSegment))
                            {
                                HttpResponseMessage result = new HttpResponseMessage(HttpStatusCode.OK);
                                
                                AccountClientConfigProperties clientConfigProperties = new AccountClientConfigProperties
                                {
                                    ClientTelemetryConfiguration = new ClientTelemetryConfiguration
                                    {
                                        IsEnabled = withClientTelemetry,
                                        Endpoint = withClientTelemetry? "http://dummy.test.com" : null
                                    }
                                };

                                string payload = JsonConvert.SerializeObject(clientConfigProperties);
                                result.Content = new StringContent(payload, Encoding.UTF8, "application/json");

                                return Task.FromResult(result);
                            }
                            else if (request.RequestUri.AbsoluteUri.Equals(VmMetadataApiHandler.vmMetadataEndpointUrl.AbsoluteUri))
                            {
                                HttpResponseMessage result = new HttpResponseMessage(HttpStatusCode.OK);
                                
                                return Task.FromResult(result);
                            }
                            return null;
                        }
                    };
                    
                    using (CosmosClient client = TestCommon.CreateCosmosClient(
                        customizeClientBuilder: builder => builder.WithHttpClientFactory(() => new HttpClient(httpHandler))))
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

                        Container container = database.CreateContainerAsync(Guid.NewGuid().ToString(), "/pk").GetAwaiter().GetResult();
                        container = database.CreateContainerIfNotExistsAsync(container.Id, "/pk").GetAwaiter().GetResult();

                        ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
                        ItemResponse<ToDoActivity> response = container.CreateItemAsync<ToDoActivity>(item: testItem).ConfigureAwait(false).GetAwaiter().GetResult();
                        Assert.IsNotNull(response);
                        string diagnostics = response.Diagnostics.ToString();
                        Assert.IsTrue(diagnostics.Contains("Synchronization Context"));

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
                        
                        ItemResponse<ToDoActivity> deleteResponse = container.DeleteItemAsync<ToDoActivity>(partitionKey: new Cosmos.PartitionKey(testItem.pk), id: testItem.id).ConfigureAwait(false).GetAwaiter().GetResult();
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
            private readonly object locker = new object();

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
