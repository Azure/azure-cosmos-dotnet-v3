namespace Cosmos.Samples.ChangeFeedPull
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Extensions.Configuration;

    class Program
    {
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
                    FeedIterator<MyDocument> iterator = client.GetContainer("samples", "source")
                        .GetChangeFeedIterator<MyDocument>(
                        changeFeedRequestOptions: new ChangeFeedRequestOptions()
                        {
                            StartTime = DateTime.MinValue.ToUniversalTime()
                        });

                    Database samples = client.GetDatabase("samples");
                    Container target = await samples.CreateContainerIfNotExistsAsync("target", "/pk", 24000);
                    await Task.Delay(5000);

                    await target.ReplaceThroughputAsync(40000);
                    await Task.Delay(5000);

                    Console.WriteLine("Starting...");

                    ConcurrentDictionary<Task, bool> allTasks = new ConcurrentDictionary<Task, bool>();
                    while (true)
                    {
                        FeedResponse<MyDocument> response;
                        try
                        {
                            response = await iterator.ReadNextAsync();
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
                                List<Task> currentTasks = new List<Task>();
                                foreach (MyDocument doc in response.Resource)
                                {
                                    Task t = target.CreateItemAsync(doc, new PartitionKey(doc.pk));
                                    currentTasks.Add(t);
                                    allTasks.TryAdd(t, true);
                                }

                                Task _ = Task.WhenAll(currentTasks).ContinueWith(_ =>
                                 {
                                     foreach (Task task in currentTasks)
                                     {
                                         allTasks.TryRemove(task, out bool _);
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
            }
            finally
            {
                Console.WriteLine("End of demo, press any key to exit.");
                Console.ReadKey();
            }
        }

        private class MyDocument
        {
            public string id { get; set; }

            public string pk { get; set; }

            public string other { get; set; }
        }

    }
}
