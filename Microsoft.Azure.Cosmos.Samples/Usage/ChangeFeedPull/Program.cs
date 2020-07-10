namespace Cosmos.Samples.ChangeFeedPull
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Extensions.Configuration;

    class Program
    {
        ConcurrentQueue<string> continuationTokenQueue = new ConcurrentQueue<string>();

        ConcurrentDictionary<string, int> pendingCountByContinuationToken = new ConcurrentDictionary<string, int>();

        PartitionKey emptyPartitionKey = new PartitionKey(string.Empty);

        ItemRequestOptions targetItemRequestOptions = new ItemRequestOptions()
        {
            EnableContentResponseOnWrite = false            
        };

        private async Task RunAsync(Container source, Container target)
        {
            FeedIterator<MyDocument> iterator = source
                .GetChangeFeedIterator<MyDocument>(
                changeFeedRequestOptions: new ChangeFeedRequestOptions()
                {
                    StartTime = DateTime.MinValue.ToUniversalTime(),
                    // MaxItemCount = 1000
                });

            while (true)
            {
                try
                {
                    FeedResponse<MyDocument> response = await iterator.ReadNextAsync();
                    if ((int)response.StatusCode == 304)
                    {
                        Console.WriteLine("304");
                        await Task.Delay(50);
                    }
                    else if ((int)response.StatusCode >= 300)
                    {
                        Console.WriteLine(response.StatusCode);
                    }
                    else
                    {
                        // shouldn't be, but anyway
                        if (response.Count == 0)
                        {
                            continue;
                        }

                        string continuationToken = response.ContinuationToken;
                        this.continuationTokenQueue.Enqueue(continuationToken);
                        this.pendingCountByContinuationToken.TryAdd(continuationToken, response.Count);

                        Task _ = Task.Run(() =>
                        {
                            foreach (MyDocument doc in response.Resource)
                            {
                                MyDocument transformedDoc = Transform(doc);

                                Task _ = target.CreateItemAsync(transformedDoc, new PartitionKey(transformedDoc.pk), this.targetItemRequestOptions)
                                    .ContinueWith(_ => this.UpdateProgressAsync(target, continuationToken));
                            }
                        });
                    }
                }
                catch (CosmosException ex)
                {
                    Console.WriteLine(ex.StatusCode + " " + ex.Message);
                }
            }

        }

        private static MyDocument Transform(MyDocument doc)
        {
            string sourceId = doc.id;
            doc.id = doc.pk;
            doc.pk = sourceId;
            return doc;
        }

        private async Task UpdateProgressAsync(Container target, string continuationToken)
        {
            int updatedPendingCountForContinuationToken = this.pendingCountByContinuationToken.AddOrUpdate(
                continuationToken,
                -1,
                (key, old) => old - 1);

            if (updatedPendingCountForContinuationToken == 0
                && this.continuationTokenQueue.TryPeek(out string firstPendingContinuationToken)
                && firstPendingContinuationToken == continuationToken)
            {
                await target.UpsertItemAsync(new
                {
                    id = "Progress",
                    pk = string.Empty,
                    cont = continuationToken
                }, this.emptyPartitionKey);

                bool wasDequeued = this.continuationTokenQueue.TryDequeue(out string dequeuedToken);
                Debug.Assert(wasDequeued);
                Debug.Assert(dequeuedToken == continuationToken);
            }
        }

        private class MyDocument
        {
            public string id { get; set; }

            public string pk { get; set; }

            public string other { get; set; }
        }

        static async Task Main(string[] args)
        {
            try
            {
                IConfigurationRoot configuration = new ConfigurationBuilder()
                    .AddJsonFile("appSettings.json")
                    .Build();

                string endpoint = configuration["EndPointUrl"];
                if (string.IsNullOrEmpty(endpoint))
                {
                    throw new ArgumentNullException("Please specify a valid endpoint in the appSettings.json");
                }

                string authKey = configuration["AuthorizationKey"];
                if (string.IsNullOrEmpty(authKey) || string.Equals(authKey, "Super secret key"))
                {
                    throw new ArgumentException("Please specify a valid AuthorizationKey in the appSettings.json");
                }

                using (CosmosClient client = new CosmosClientBuilder(endpoint, authKey)
                    .WithThrottlingRetryOptions(TimeSpan.FromSeconds(1), maxRetryAttemptsOnThrottledRequests: 0).Build())
                {
                    Database samples = client.GetDatabase("samples");
                    Container target = await samples.CreateContainerIfNotExistsAsync("target", "/pk", 24000);
                    await Task.Delay(5000);

                    await target.ReplaceThroughputAsync(40000);
                    await Task.Delay(5000);

                    Console.WriteLine("Starting...");
                    await new Program().RunAsync(client.GetContainer("samples", "temp"), target);
                }
            }
            finally
            {
                Console.WriteLine("End of demo, press any key to exit.");
                Console.ReadKey();
            }
        }
    }
}
