namespace Cosmos.Samples.Shared
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json;

    // ----------------------------------------------------------------------------------------------------------
    // Prerequisites - 
    // 
    // 1. An Azure Cosmos account - 
    //    https://docs.microsoft.com/azure/cosmos-db/create-cosmosdb-resources-portal

    // ----------------------------------------------------------------------------------------------------------
    // Sample - demonstrates the basic usage of the CosmosClient by performing a high volume of operations
    // ----------------------------------------------------------------------------------------------------------

    public class Program
    {
        private class Configuration
        {
            public string EndpointUrl { get; set; }
            public string AuthorizationKey { get; set; }
            public string DatabaseName { get; set; }
            public string ContainerName { get; set; }

            public bool ShouldRecreateContainerOnStart { get; set; }
            public int ThroughputToProvision { get; set; }
            public bool IsSharedThroughput { get; set; }
            public bool IsAutoScale { get; set; }
            public bool ShouldContainerIndexAllProperties { get; set; }

            public int ItemsToCreate { get; set; }
            public int ItemSize { get; set; }
            public int ItemPropertyCount { get; set; }
            public int PartitionKeyCount { get; set; }

            public int RequestsPerSecond { get; set; }
            public int WarmUpRequestCount { get; set; }
            public int MaxInFlightRequestCount { get; set; }

            public int PreSerializedItemCount { get; set; }
            public int MaxRuntimeInSeconds { get; set; }
            public int NumWorkers { get; set; }
            public bool IsGatewayMode { get; set; }
            public bool AllowBulkExecution { get; set; }
            public bool OmitContentInWriteResponse { get; set; }
            public bool ShouldDeleteContainerOnFinish { get; set; }
        }

        private static Configuration configuration;
        private static CosmosClient client;
        private static readonly string partitionKeyValuePrefix = DateTime.UtcNow.ToString("MMddHHmm-");

        public static async Task Main(string[] args)
        {
            Container container = null;
            try
            {
                container = await Program.InitializeAsync(args);
                await Program.CreateItemsConcurrentlyAsync(container);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: {0}", ex);
            }
            finally
            {
                if (configuration.ShouldDeleteContainerOnFinish)
                {
                    await Program.CleanupContainerAsync(container);
                }

                client.Dispose();

                Console.WriteLine("End of demo.");
            }
        }

        private static async Task CreateItemsConcurrentlyAsync(Container container)
        {
            Console.WriteLine($"Starting creation of {configuration.ItemsToCreate} items of about {configuration.ItemSize} bytes each"
            + $" within {configuration.MaxRuntimeInSeconds} seconds using {configuration.NumWorkers} workers.");

            DataSource dataSource = new DataSource(configuration.ItemsToCreate, configuration.PreSerializedItemCount, 
                configuration.ItemPropertyCount, configuration.ItemSize, configuration.PartitionKeyCount);
            Console.WriteLine("Datasource initialized; starting ingestion");

            ConcurrentDictionary<HttpStatusCode, int> countsByStatus = new ConcurrentDictionary<HttpStatusCode, int>();
            ConcurrentBag<TimeSpan> latencies = new ConcurrentBag<TimeSpan>();
            long totalRequestCharge = 0;

            int taskTriggeredCounter = 0;
            int taskCompleteCounter = 0;
            int actualWarmupRequestCount = 0;

            int itemsToCreatePerWorker = (int)Math.Ceiling((double)(configuration.ItemsToCreate / configuration.NumWorkers));
            List<Task> workerTasks = new List<Task>();

            const int ticksPerMillisecond = 10000;
            const int ticksPerSecond = 1000 * ticksPerMillisecond;
            int eachTicks = configuration.RequestsPerSecond <= 0 ? 0 : (int)(ticksPerSecond / configuration.RequestsPerSecond);
            long usageTicks = 0;

            if(configuration.MaxInFlightRequestCount == -1)
            {
                configuration.MaxInFlightRequestCount = int.MaxValue;
            }

            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(configuration.MaxRuntimeInSeconds * 1000);
            CancellationToken cancellationToken = cancellationTokenSource.Token;
            Stopwatch stopwatch = Stopwatch.StartNew();
            Stopwatch latencyStopwatch = new Stopwatch();

            ItemRequestOptions itemRequestOptions = null;
            if(configuration.OmitContentInWriteResponse)
            {
                itemRequestOptions = new ItemRequestOptions()
                {
                    EnableContentResponseOnWrite = false
                };
            }

            for (int workerIndex = 0; workerIndex < configuration.NumWorkers; workerIndex++)
            {
                int workerIndexLocal = workerIndex;
                workerTasks.Add(Task.Run(async () =>
                {
                    int docCounter = 0;
                    bool isErrorPrinted = false;

                    while (!cancellationToken.IsCancellationRequested && docCounter < itemsToCreatePerWorker)
                    {
                        docCounter++;

                        MemoryStream stream = dataSource.GetNextItem(out PartitionKey partitionKeyValue);

                        long elapsedTicks = stopwatch.ElapsedTicks;
                        if (usageTicks < elapsedTicks)
                        {
                            usageTicks = elapsedTicks;
                        }

                        usageTicks += eachTicks;
                        if (usageTicks - elapsedTicks > ticksPerSecond)
                        {
                            await Task.Delay((int)((usageTicks - elapsedTicks - ticksPerSecond) / ticksPerMillisecond));
                        }

                        if(taskTriggeredCounter - taskCompleteCounter > configuration.MaxInFlightRequestCount)
                        {
                            await Task.Delay((int)((taskTriggeredCounter - taskCompleteCounter - configuration.MaxInFlightRequestCount) / configuration.MaxInFlightRequestCount));
                        }

                        _ = container.UpsertItemStreamAsync(stream, partitionKeyValue, itemRequestOptions, cancellationToken)
                            .ContinueWith((Task<ResponseMessage> task) =>
                            {
                                if (task.IsCompletedSuccessfully)
                                {
                                    if (stream != null) { stream.Dispose(); }

                                    using (ResponseMessage responseMessage = task.Result)
                                    {
                                        countsByStatus.AddOrUpdate(responseMessage.StatusCode, 1, (_, old) => old + 1);
                                        if (responseMessage.StatusCode < HttpStatusCode.BadRequest)
                                        {
                                            Interlocked.Add(ref totalRequestCharge, (int)(responseMessage.Headers.RequestCharge * 100));
                                            if(latencyStopwatch.IsRunning)
                                            {
                                                latencies.Add(responseMessage.Diagnostics.GetClientElapsedTime());
                                            }
                                        }
                                        else
                                        {
                                            if ((int)responseMessage.StatusCode != 429 && !isErrorPrinted)
                                            {
                                                Console.WriteLine(responseMessage.ErrorMessage);
                                                Console.WriteLine(responseMessage.Diagnostics.ToString());
                                                isErrorPrinted = true;
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    Exception ex = task.Exception;
                                }

                                task.Dispose();
                                if (Interlocked.Increment(ref taskCompleteCounter) >= configuration.ItemsToCreate)
                                {
                                    stopwatch.Stop();
                                    latencyStopwatch.Stop();
                                }
                            });

                        Interlocked.Increment(ref taskTriggeredCounter);
                    }
                }));
            }

            while (taskCompleteCounter < configuration.ItemsToCreate)
            {
                Console.Write($"In progress. Triggered: {taskTriggeredCounter} Processed: {taskCompleteCounter}, Pending: {configuration.ItemsToCreate - taskCompleteCounter}");
                int nonFailedCount = 0;
                foreach (KeyValuePair<HttpStatusCode, int> countForStatus in countsByStatus)
                {
                    Console.Write(", " + countForStatus.Key + ": " + countForStatus.Value);
                    if(countForStatus.Key < HttpStatusCode.BadRequest)
                    {
                        nonFailedCount += countForStatus.Value;
                    }
                }

                if(actualWarmupRequestCount == 0 && nonFailedCount >= configuration.WarmUpRequestCount)
                {
                    actualWarmupRequestCount = nonFailedCount;
                    latencyStopwatch.Start();
                }

                long elapsedSeconds = latencyStopwatch.ElapsedMilliseconds / 1000;
                Console.Write($", RPS: {(elapsedSeconds == 0 ? -1 : (nonFailedCount - actualWarmupRequestCount) / elapsedSeconds)}");
                Console.WriteLine();

                if (cancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine($"Could not handle {configuration.ItemsToCreate} items in {configuration.MaxRuntimeInSeconds} seconds.");
                    break;
                }

                await Task.Delay(1000);
            }

            long elapsedFinal = latencyStopwatch.ElapsedMilliseconds / 1000;
            int nonFailedCountFinal = countsByStatus.Where(x => x.Key < HttpStatusCode.BadRequest).Sum(p => p.Value);
            int nonFailedCountFinalForLatency = nonFailedCountFinal - actualWarmupRequestCount;
            Console.WriteLine($"Successfully handled {nonFailedCountFinal} items; handled {nonFailedCountFinalForLatency} in {elapsedFinal} seconds at {(elapsedFinal == 0 ? -1 : nonFailedCountFinalForLatency / elapsedFinal)} items/sec.");

            Console.WriteLine("Counts by StatusCode:");
            Console.WriteLine(string.Join(',', countsByStatus.Select(countForStatus => countForStatus.Key + ": " + countForStatus.Value)));

            List<TimeSpan> latenciesList = latencies.ToList();
            latenciesList.Sort();
            int nonWarmupRequestCount = latenciesList.Count;
            if(nonWarmupRequestCount > 0)
            {
                Console.WriteLine("Latencies (non-failed):"
                + $"   P90: {latenciesList[(int)(nonWarmupRequestCount * 0.90)].TotalMilliseconds}"
                + $"   P99: {latenciesList[(int)(nonWarmupRequestCount * 0.99)].TotalMilliseconds}"
                + $"   P99.9: {latenciesList[(int)(nonWarmupRequestCount * 0.999)].TotalMilliseconds}"
                + $"   Max: {latenciesList[nonWarmupRequestCount - 1].TotalMilliseconds}");
            }

            Console.WriteLine("Average RUs (non-failed): " + (totalRequestCharge / (100.0 * nonFailedCountFinal)));
        }

        private static async Task<Container> InitializeAsync(string[] args)
        {
            IConfigurationRoot configurationRoot = new ConfigurationBuilder()
                    .AddJsonFile("appSettings.json")
                    .AddCommandLine(args)
                    .Build();

            Program.configuration = new Configuration();
            configurationRoot.Bind(Program.configuration);

            Program.client = GetClientInstance(configuration.EndpointUrl, configuration.AuthorizationKey);
            Container container = client.GetDatabase(configuration.DatabaseName).GetContainer(configuration.ContainerName);
            if (configuration.ShouldRecreateContainerOnStart)
            {
                container = await Program.RecreateContainerAsync(client, configuration.DatabaseName, configuration.ContainerName, configuration.ShouldContainerIndexAllProperties, configuration.ThroughputToProvision, configuration.IsSharedThroughput, configuration.IsAutoScale);
                await Task.Delay(5000);
            }

            try
            {
                await container.ReadContainerAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in reading collection: {0}", ex);
                throw;
            }

            return container;
        }

        private static CosmosClient GetClientInstance(
            string endpoint,
            string authKey)
        {
            return new CosmosClient(endpoint, authKey, new CosmosClientOptions()
            {
                ConnectionMode = configuration.IsGatewayMode ? ConnectionMode.Gateway : ConnectionMode.Direct,
                AllowBulkExecution = configuration.AllowBulkExecution,
                MaxRetryAttemptsOnRateLimitedRequests = 0,
                PortReuseMode = PortReuseMode.ReuseUnicastPort
            });
        }

        private static async Task CleanupContainerAsync(Container container)
        {
            if (container != null)
            {
                try
                {
                    await container.DeleteContainerStreamAsync();
                }
                catch(Exception)
                {
                }
            }
        }

        private static async Task<Container> RecreateContainerAsync(
            CosmosClient client,
            string databaseName, 
            string containerName,
            bool shouldIndexAllProperties,
            int throughputToProvision,
            bool isSharedThroughput,
            bool isAutoScale)
        {
            ThroughputProperties throughputProperties = isAutoScale
                ? ThroughputProperties.CreateAutoscaleThroughput(throughputToProvision)
                : ThroughputProperties.CreateManualThroughput(throughputToProvision);
            (string desiredThroughputType, int desiredThroughputValue) = GetThroughputTypeAndValue(throughputProperties);

            Database database = await client.CreateDatabaseIfNotExistsAsync(databaseName, isSharedThroughput ? throughputProperties : null);
            ThroughputProperties existingDatabaseThroughput = await database.ReadThroughputAsync(null);

            if(isSharedThroughput && existingDatabaseThroughput != null)
            {
                (string existingThroughputType, int existingThroughputValue) = GetThroughputTypeAndValue(existingDatabaseThroughput);
                if (existingThroughputType == desiredThroughputType)
                {
                    if (existingThroughputValue != desiredThroughputValue)
                    {
                        Console.WriteLine($"Setting database {existingThroughputType} throughput to ${desiredThroughputValue}");
                        await database.ReplaceThroughputAsync(throughputProperties);
                    }
                }
                else
                {
                    throw new Exception($"Cannot set desired database throughput; existing {existingThroughputType} throughput is {existingThroughputValue}.");
                }
            }

            Console.WriteLine("Deleting old container if it exists.");
            await Program.CleanupContainerAsync(database.GetContainer(containerName));

            if (!isSharedThroughput)
            {
                Console.WriteLine($"Creating container with {desiredThroughputType} throughput {desiredThroughputValue}...");
            }
            else
            {
                Console.WriteLine("Creating container");
            }

            ContainerBuilder containerBuilder = database.DefineContainer(containerName, "/pk");

            if(!shouldIndexAllProperties)
            {
                containerBuilder.WithIndexingPolicy()
                    .WithIndexingMode(IndexingMode.Consistent)
                    .WithIncludedPaths()
                        .Path("/")
                        .Attach()
                    .WithExcludedPaths()
                        .Path("/other/*")
                        .Attach()
                .Attach();
            }

            return await containerBuilder.CreateAsync(isSharedThroughput ? null : throughputProperties);
        }

        private static (string, int) GetThroughputTypeAndValue(ThroughputProperties throughputProperties)
        {
            string type = throughputProperties.AutoscaleMaxThroughput.HasValue ? "auto-scale" : "manual";
            int value = throughputProperties.AutoscaleMaxThroughput ?? throughputProperties.Throughput.Value;
            return (type, value);
        }

        private class MyDocument
        {
            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("pk")]
            public string PK { get; set; }

            [JsonProperty("arr")]
            public List<string> Arr {get; set;}

            [JsonProperty("other")]
            public string Other { get; set; }

            [JsonProperty(PropertyName = "_ts")]
            public int LastModified { get; set; }

            [JsonProperty(PropertyName = "_rid")]
            public string ResourceId { get; set; }
        }

        private class DataSource
        {
            private readonly List<string> additionalProperties = new List<string>();
            private readonly int itemSize;
            private readonly int partitionKeyCount;
            private static readonly JsonSerializerSettings JsonSerializerSettings = new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore };
            private readonly string padding = string.Empty;
            private readonly PartitionKey[] partitionKeys;
            private readonly ConcurrentDictionary<PartitionKey, ConcurrentBag<MemoryStream>> itemsByPK;
            private int itemIndex = 0;

            public DataSource(int itemCount, int preSerializedItemCount, int itemPropertyCount, int itemSize, int partitionKeyCount)
            {
                this.partitionKeyCount = Math.Min(partitionKeyCount, itemCount);
                this.itemSize = itemSize;
                this.itemsByPK = new ConcurrentDictionary<PartitionKey, ConcurrentBag<MemoryStream>>();
                this.partitionKeys = new PartitionKey[this.partitionKeyCount];

                // Determine padding length - setup initial values so we can create a sample doc
                this.padding = string.Empty;

                // Setup properties - reduce some for standard properties like PK and Id we are adding
                for(int i = 0; i < itemPropertyCount - 10; i++)
                {
                    this.additionalProperties.Add(i.ToString());
                }

                // Find length and keep some bytes for the system generated properties
                int currentLen = (int)this.CreateNextDocItem(partitionKeyValuePrefix + "0").Length + 250;
                this.padding = this.itemSize > currentLen ? new string('x', this.itemSize - currentLen) : string.Empty;

                int pkIndex = this.partitionKeyCount;
                preSerializedItemCount = Math.Min(preSerializedItemCount, itemCount);
                for (int i = 0; i < Math.Max(preSerializedItemCount, this.partitionKeyCount); i++)
                {
                    if(pkIndex == this.partitionKeyCount)
                    {
                        pkIndex = 0;
                    }

                    string partitionKeyValue = partitionKeyValuePrefix + pkIndex;
                    if(i == pkIndex)
                    {
                        this.partitionKeys[pkIndex] = new PartitionKey(partitionKeyValue);
                        if(i < preSerializedItemCount)
                        {
                            this.itemsByPK.TryAdd(this.partitionKeys[pkIndex], new ConcurrentBag<MemoryStream>());
                        }
                    }

                    if(i < preSerializedItemCount)
                    {
                        this.itemsByPK[this.partitionKeys[pkIndex]].Add(this.CreateNextDocItem(partitionKeyValue));
                    }

                    pkIndex++;
                }
            }

            public MemoryStream CreateNextDocItem(string partitionKey, string id = null)
            {
                if(id == null) { id = Guid.NewGuid().ToString(); }

                MyDocument myDocument = new MyDocument()
                {
                    Id = id,
                    PK = partitionKey,
                    Arr = this.additionalProperties,
                    Other = this.padding
                };
                string value = JsonConvert.SerializeObject(myDocument, JsonSerializerSettings);
                return new MemoryStream(Encoding.UTF8.GetBytes(value));
            }

            public MemoryStream GetNextItem(out PartitionKey partitionKey)
            {
                int incremented = Interlocked.Increment(ref this.itemIndex);
                int currentPKIndex = incremented % this.partitionKeyCount;
                partitionKey = this.partitionKeys[currentPKIndex];

                if(this.itemsByPK.TryGetValue(partitionKey, out ConcurrentBag<MemoryStream> itemsForPK)
                && itemsForPK.TryTake(out MemoryStream result))
                {
                    return result;
                }
                else
                {
                    return this.CreateNextDocItem(partitionKeyValuePrefix + currentPKIndex, incremented.ToString());
                }
            }
        }
    }
}