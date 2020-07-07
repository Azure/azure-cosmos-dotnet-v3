namespace Cosmos.Samples.ChangeFeedPull
{
    using System;
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

                    Container target = client.GetContainer("samples", "target");

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
                                List<Task> tasks = new List<Task>();
                                foreach (MyDocument doc in response.Resource)
                                {
                                    tasks.Add(target.CreateItemAsync(doc, new PartitionKey(doc.pk)));
                                }

                                await Task.WhenAll(tasks);
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
