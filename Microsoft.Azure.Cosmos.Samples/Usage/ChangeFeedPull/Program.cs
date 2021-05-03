namespace Cosmos.Samples.ChangeFeedPull
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json;

    class Program
    {
        private static readonly string partitionKeyValuePrefix = DateTime.UtcNow.ToString("MMddHHmm-");
        private readonly ConcurrentQueue<ChangeSet> changeQueue = new ConcurrentQueue<ChangeSet>();

        public static async Task Main(string[] _)
        {
            try
            {
                IConfigurationRoot configuration = new ConfigurationBuilder()
                    .AddJsonFile("appSettings.json")
                    .Build();

                string endpoint = configuration["EndPointUrl"];
                string authKey = configuration["AuthorizationKey"];

                using (CosmosClient sourceClient = new CosmosClientBuilder(endpoint, authKey)
                    .WithThrottlingRetryOptions(TimeSpan.FromSeconds(1), maxRetryAttemptsOnThrottledRequests: 0)
                    .Build())
                using (CosmosClient targetClient = new CosmosClientBuilder(endpoint, authKey)
                    .WithThrottlingRetryOptions(TimeSpan.FromSeconds(1), maxRetryAttemptsOnThrottledRequests: 0)
                    .WithBulkExecution(true)
                    .Build())
                {
                    Container source = sourceClient.GetContainer("samples", "bulktesttwocore");
                    Container target = targetClient.GetContainer("samples", "target");
                    Console.WriteLine("Starting...");
                    await new Program().RunAsync(source, target);
                }
            }
            finally
            {
                Console.WriteLine("End of demo, press any key to exit.");
                Console.ReadKey();
            }
        }


        private Task RunAsync(Container source, Container target)
        {
            return Task.WhenAll(
                Task.Run(() => this.ReadSourceAsync(source)),
                Task.Run(() => this.WriteTargetAsync(target)));
        }

        private async Task ReadSourceAsync(Container source)
        {
            FeedIterator<MyDocument> iterator = source
                .GetChangeFeedIterator<MyDocument>(
                changeFeedRequestOptions: new ChangeFeedRequestOptions()
                {
                    StartTime = DateTime.MinValue.ToUniversalTime(),
                    MaxItemCount = 200
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

                        this.changeQueue.Enqueue(new ChangeSet(response.Resource, response.ContinuationToken));
                    }
                }
                catch (CosmosException ex)
                {
                    Console.WriteLine(ex.StatusCode + " " + ex.Message);
                }
            }
        }

        private async Task WriteTargetAsync(Container target)
        {
            while (true)
            {
                if (!this.changeQueue.TryDequeue(out ChangeSet changeSet))
                {
                    Console.Write(".");
                    await Task.Delay(10);
                    continue;
                }

                List<Task> tasks = new List<Task>();
                foreach (MyDocument doc in changeSet.Documents)
                {
                    MyDocument transformedDoc = Transform(doc);
                    tasks.Add(target.UpsertItemAsync(transformedDoc, new PartitionKey(transformedDoc.PK)));
                }

                await Task.WhenAll(tasks);
            }
        }

        private static MyDocument Transform(MyDocument doc)
        {
            MyDocument returnValue = new MyDocument(doc);
            returnValue.PK = Program.partitionKeyValuePrefix + returnValue.PK;
            return returnValue;
        }

        private class MyDocument
        {
            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("pk")]
            public string PK { get; set; }

            [JsonProperty("arr")]
            public List<string> Arr { get; set; }

            [JsonProperty("other")]
            public string Other { get; set; }

            [JsonProperty(PropertyName = "_ts")]
            public int LastModified { get; set; }

            [JsonProperty(PropertyName = "_rid")]
            public string ResourceId { get; set; }

            public MyDocument(MyDocument other)
            {
                this.Id = other.Id;
                this.PK = other.PK;
                this.Other = other.Other;
                this.Arr = other.Arr;
            }
        }

        private struct ChangeSet
        {
            public ChangeSet(IEnumerable<MyDocument> documents, string continuationToken)
            {
                this.Documents = documents;
                this.ContinuationToken = continuationToken;
            }

            public IEnumerable<MyDocument> Documents { get; }
            public string ContinuationToken { get; }
        }
    }
}