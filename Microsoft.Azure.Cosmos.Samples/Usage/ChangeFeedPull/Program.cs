namespace Cosmos.Samples.ChangeFeedPull
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json;

    class Program
    {
        private static readonly string partitionKeyValuePrefix = DateTime.UtcNow.ToString("MMddHHmm-");
        private readonly List<ConcurrentQueue<ChangeSet>> changeQueues = new List<ConcurrentQueue<ChangeSet>>();
        private int readCount = 0;
        private int writtenCount = 0;
        private int minQueueLength;

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


        private async Task RunAsync(Container source, Container target)
        {
            IEnumerable<FeedRange> feedRanges = await source.GetFeedRangesAsync();
            foreach (FeedRange feedRange in feedRanges)
            {
                this.changeQueues.Add(new ConcurrentQueue<ChangeSet>());
            }

            Console.WriteLine("Feed range count: " + this.changeQueues.Count);

            Stopwatch stopwatch = Stopwatch.StartNew();
            await Task.WhenAny(
                Task.Run(() => this.ReadSourceAsync(source, feedRanges)),
                Task.Run(() => this.WriteTargetAsync(target)),
                Task.Run(() => this.PrintStatusAsync(stopwatch)));
        }

        private async Task ReadSourceAsync(Container source, IEnumerable<FeedRange> feedRanges)
        {
            List<FeedIterator<MyDocument>> iterators = new List<FeedIterator<MyDocument>>();

            foreach (FeedRange feedRange in feedRanges)
            {
                iterators.Add(source
                    .GetChangeFeedIterator<MyDocument>(
                    feedRange,
                    changeFeedRequestOptions: new ChangeFeedRequestOptions()
                    {
                        StartTime = DateTime.MinValue.ToUniversalTime(),
                        MaxItemCount = 200
                    }));
            }

            int iteratorIndex = -1;

            while (true)
            {
                try
                {
                    iteratorIndex++;
                    if (iteratorIndex == iterators.Count)
                    {
                        iteratorIndex = 0;
                    }

                    while(this.minQueueLength > 5)
                    {
                        await Task.Delay(500);
                    }

                    FeedResponse<MyDocument> response = await iterators[iteratorIndex].ReadNextAsync();
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
                            Console.Write("0");
                            continue;
                        }

                        this.changeQueues[iteratorIndex].Enqueue(new ChangeSet(response.Resource, response.ContinuationToken));
                        Interlocked.Add(ref this.readCount, response.Resource.Count());
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }
        }

        private Task WriteTargetAsync(Container target)
        {
            List<Task> tasks = new List<Task>();
            for (int index = 0; index < this.changeQueues.Count; index++)
            {
                int indexLocal = index;
                tasks.Add(Task.Run(async () =>
                {
                    while (true)
                    {
                        if (!this.changeQueues[indexLocal].TryDequeue(out ChangeSet changeSet))
                        {
                            await Task.Delay(50);
                            continue;
                        }

                        List<Task> tasks = new List<Task>();
                        foreach (MyDocument doc in changeSet.Documents)
                        {
                            MyDocument transformedDoc = Transform(doc);
                            tasks.Add(target.UpsertItemAsync(transformedDoc, new PartitionKey(transformedDoc.PK)));
                        }

                        await Task.WhenAll(tasks);
                        Interlocked.Add(ref this.writtenCount, tasks.Count);
                    }
                }));
            }

            return Task.WhenAll(tasks);
        }

        private async Task PrintStatusAsync(Stopwatch stopwatch)
        {
            while (true)
            {
                long elapsedSeconds = stopwatch.ElapsedMilliseconds / 1000;
                Console.Write($" Read RPS: {(elapsedSeconds == 0 ? -1 : this.readCount / elapsedSeconds)}");
                Console.WriteLine($" Write RPS: {(elapsedSeconds == 0 ? -1 : this.writtenCount / elapsedSeconds)}");
                Console.WriteLine("Queue lengths:" + string.Join(' ', this.changeQueues.Select(q => q.Count.ToString().PadLeft(5))));
                this.minQueueLength = this.changeQueues.Min(q => q.Count);
                await Task.Delay(1000);
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

            public MyDocument()
            { }

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