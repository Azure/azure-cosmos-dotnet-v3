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
    //
    // 2. Microsoft.Azure.Cosmos NuGet package - 
    //    http://www.nuget.org/packages/Microsoft.Azure.Cosmos/ 
    // ----------------------------------------------------------------------------------------------------------
    // Sample - demonstrates the basic usage of the CosmosClient by performing a high volume of operations
    // ----------------------------------------------------------------------------------------------------------

    public class Program
    {
        private static CosmosClient client;
        private static int itemsToCreate;
        private static int preSerializedItemCount;
        private static int itemSize;
        private static int itemPropertyCount;
        private static int maxRuntimeInSeconds;
        private static bool shouldDeleteContainerOnFinish;
        private static int numWorkers;
        private static bool allowBulkExecution;
        private static int partitionKeyCount;
        private static int requestsPerSec;
        private static readonly string partitionKeyValuePrefix = DateTime.UtcNow.ToString("MMddHHmm-");

        public static async Task Main(string[] args)
        {
            Container container = null;
            try
            {
                container = await Program.InitializeAsync();
                await Program.CreateItemsConcurrentlyAsync(container);
            }
            catch (CosmosException cre)
            {
                Console.WriteLine(cre.ToString());
            }
            catch (Exception e)
            {
                Exception baseException = e.GetBaseException();
                Console.WriteLine("Error: {0}", e);
            }
            finally
            {
                if (Program.shouldDeleteContainerOnFinish)
                {
                    await Program.CleanupContainerAsync(container);
                }

                client.Dispose();

                Console.WriteLine("End of demo.");
            }
        }

        private static async Task CreateItemsConcurrentlyAsync(Container container)
        {
            Console.WriteLine($"Starting creation of {Program.itemsToCreate} items of about {Program.itemSize} bytes each"
            + $" within {maxRuntimeInSeconds} seconds using {numWorkers} workers.");
            DataSource dataSource = new DataSource(itemsToCreate, preSerializedItemCount, itemPropertyCount, itemSize, partitionKeyCount);
            Console.WriteLine("Datasource initialized; starting ingestion");

            ConcurrentDictionary<HttpStatusCode, int> countsByStatus = new ConcurrentDictionary<HttpStatusCode, int>();
            ConcurrentBag<TimeSpan> latencies = new ConcurrentBag<TimeSpan>();
            long totalRequestCharge = 0;

            int taskTriggeredCounter = 0;
            int taskCompleteCounter = 0;

            int itemsToCreatePerWorker = (int)Math.Ceiling((double)(itemsToCreate / numWorkers));
            List<Task> workerTasks = new List<Task>();

            const int ticksPerMillisecond = 10000;
            const int ticksPerSecond = 1000 * ticksPerMillisecond;
            int eachTicks = (int)(ticksPerSecond / Program.requestsPerSec);
            long usageTicks = 0;

            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(maxRuntimeInSeconds * 1000);
            CancellationToken cancellationToken = cancellationTokenSource.Token;
            Stopwatch stopwatch = Stopwatch.StartNew();

            for (int workerIndex = 0; workerIndex < numWorkers; workerIndex++)
            {
                int workerIndexLocal = workerIndex;
                workerTasks.Add(Task.Run(async () =>
                {
                    int docCounter = 0;
                    bool isErrorPrinted = false;

                    while (!cancellationToken.IsCancellationRequested && docCounter < itemsToCreatePerWorker)
                    {
                        docCounter++;

                        MemoryStream stream = dataSource.GetNextDocItem(workerIndexLocal, out PartitionKey partitionKeyValue);

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

                        _ = container.UpsertItemStreamAsync(stream, partitionKeyValue, null, cancellationToken)
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
                                            latencies.Add(responseMessage.Diagnostics.GetClientElapsedTime());
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
                                if (Interlocked.Increment(ref taskCompleteCounter) >= itemsToCreate)
                                {
                                    stopwatch.Stop();
                                }
                            });

                        Interlocked.Increment(ref taskTriggeredCounter);
                    }
                }));
            }

            while (taskCompleteCounter < itemsToCreate)
            {
                Console.Write($"In progress. Triggered: {taskTriggeredCounter} Processed: {taskCompleteCounter}, Pending: {itemsToCreate - taskCompleteCounter}");
                int nonFailedCount = 0;
                foreach (KeyValuePair<HttpStatusCode, int> countForStatus in countsByStatus)
                {
                    Console.Write(", " + countForStatus.Key + ": " + countForStatus.Value);
                    if(countForStatus.Key < HttpStatusCode.BadRequest)
                    {
                        nonFailedCount += countForStatus.Value;
                    }
                }

                long elapsedSeconds = stopwatch.ElapsedMilliseconds / 1000;
                Console.Write($", RPS: {(elapsedSeconds == 0 ? -1 : nonFailedCount / elapsedSeconds)}");
                Console.WriteLine();

                if (cancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine($"Could not insert {itemsToCreate} items in {maxRuntimeInSeconds} seconds.");
                    break;
                }

                await Task.Delay(1000);
            }

            long elapsedFinal = stopwatch.ElapsedMilliseconds / 1000;
            int nonFailedCountFinal = countsByStatus.Where(x => x.Key < HttpStatusCode.BadRequest).Sum(p => p.Value);
            Console.WriteLine($"Successfully handled {nonFailedCountFinal} items in {elapsedFinal} seconds at {(elapsedFinal == 0 ? -1 : nonFailedCountFinal / elapsedFinal)} items/sec.");

            Console.WriteLine("Counts by StatusCode:");
            Console.WriteLine(string.Join(',', countsByStatus.Select(countForStatus => countForStatus.Key + ": " + countForStatus.Value)));

            List<TimeSpan> latenciesList = latencies.ToList();
            latenciesList.Sort();
            int requestCount = latenciesList.Count;
            Console.WriteLine("Latencies (non-failed):"
            + $"   P90: {latenciesList[(int)(requestCount * 0.90)].TotalMilliseconds}"
            + $"   P99: {latenciesList[(int)(requestCount * 0.99)].TotalMilliseconds}"
            + $"   P99.9: {latenciesList[(int)(requestCount * 0.999)].TotalMilliseconds}"
            + $"   Max: {latenciesList[requestCount - 1].TotalMilliseconds}");

            Console.WriteLine("Average RUs (non-failed): " + totalRequestCharge / (100.0 * nonFailedCountFinal));
        }

        private class MyDocument
        {
            public string id { get; set; }
            public string pk { get; set; }
            public List<string> arr {get; set;}
            public string other { get; set; }

            [JsonProperty(PropertyName = "_ts")]
            public int lastModified { get; set; }

            [JsonProperty(PropertyName = "_rid")]
            public string resourceId { get; set; }
        }

        private static async Task<Container> InitializeAsync()
        {
            IConfigurationRoot configuration = new ConfigurationBuilder()
                    .AddJsonFile("appSettings.json")
                    .Build();

            string endpointUrl = configuration["EndPointUrl"];
            if (string.IsNullOrEmpty(endpointUrl))
            {
                throw new ArgumentNullException("Please specify a valid EndPointUrl in the appSettings.json");
            }

            string authKey = configuration["AuthorizationKey"];
            if (string.IsNullOrEmpty(authKey) || string.Equals(authKey, "Super secret key"))
            {
                throw new ArgumentException("Please specify a valid AuthorizationKey in the appSettings.json");
            }

            Program.itemsToCreate = GetConfig(configuration, "ItemsToCreate", 100000);
            Program.itemSize = GetConfig(configuration, "ItemSize", 1024);
            Program.itemPropertyCount = GetConfig(configuration, "ItemPropertyCount", 10);
            Program.partitionKeyCount = GetConfig(configuration, "PartitionKeyCount", int.MaxValue);
            Program.requestsPerSec = GetConfig(configuration, "RequestsPerSec", 1000);
            Program.numWorkers = GetConfig(configuration, "NumWorkers", 1);
            Program.allowBulkExecution = GetConfig(configuration, "AllowBulkExecution", false);
            Program.preSerializedItemCount = GetConfig(configuration, "PreSerializedItemCount", 0);
            Program.maxRuntimeInSeconds = GetConfig(configuration, "MaxRuntimeInSeconds", 300);
            Program.shouldDeleteContainerOnFinish = GetConfig(configuration, "ShouldDeleteContainerOnFinish", false);

            string databaseName = GetConfig(configuration, "DatabaseName", "demodatabase");
            string containerName = GetConfig(configuration, "ContainerName", "democontainer");
            bool shouldRecreateContainerOnStart = GetConfig(configuration, "ShouldRecreateContainerOnStart", false);
            int containerThroughput = GetConfig(configuration, "ContainerThroughput", 10000);
            bool isContainerAutoScale = GetConfig(configuration, "IsContainerAutoscale", true);
            bool shouldIndexAllProperties = GetConfig(configuration, "ShouldContainerIndexAllProperties", false);

            Program.client = GetClientInstance(endpointUrl, authKey);
            Container container = client.GetDatabase(databaseName).GetContainer(containerName);
            if (shouldRecreateContainerOnStart)
            {
                container = await Program.RecreateContainerAsync(client, databaseName, containerName, shouldIndexAllProperties, containerThroughput, isContainerAutoScale);
            }

            try
            {
                await container.ReadContainerAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in reading collection: {0}", ex.Message);
                throw;
            }

            return container;
        }

        private static CosmosClient GetClientInstance(
            string endpoint,
            string authKey) =>
            new CosmosClient(endpoint, authKey, new CosmosClientOptions()
            {
                ConnectionMode = ConnectionMode.Direct,
                AllowBulkExecution = Program.allowBulkExecution,
                MaxRetryAttemptsOnRateLimitedRequests = 0,
                PortReuseMode = PortReuseMode.ReuseUnicastPort
            });

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
            bool isContainerAutoScale)
        {
            Database database = await client.CreateDatabaseIfNotExistsAsync(databaseName);

            Console.WriteLine("Deleting old container if it exists.");
            await Program.CleanupContainerAsync(database.GetContainer(containerName));

            Console.WriteLine($"Creating a {throughputToProvision} RU/s {(isContainerAutoScale ? "auto-scale" : "manual throughput")} container...");

            ContainerBuilder containerBuilder = database.DefineContainer(containerName, "/pk");

            if(!shouldIndexAllProperties)
            {
                containerBuilder.WithIndexingPolicy()
                    .WithIndexingMode(IndexingMode.Consistent)
                    .WithIncludedPaths()
                        .Path("/pk/*")
                        .Attach()
                    .WithExcludedPaths()
                        .Path("/*")
                        .Attach()
                .Attach();
            }

            ThroughputProperties throughputProperties = isContainerAutoScale 
                ? ThroughputProperties.CreateAutoscaleThroughput(throughputToProvision) 
                : ThroughputProperties.CreateManualThroughput(throughputToProvision);

            return await containerBuilder.CreateAsync(throughputProperties);
        }

        private static int GetConfig(IConfigurationRoot iConfigurationRoot, string configName, int defaultValue)
        {
            if(!string.IsNullOrEmpty(iConfigurationRoot[configName]))
            {
                return int.Parse(iConfigurationRoot[configName]);
            }

            return defaultValue;
        }

        private static bool GetConfig(IConfigurationRoot iConfigurationRoot, string configName, bool defaultValue)
        {
            if(!string.IsNullOrEmpty(iConfigurationRoot[configName]))
            {
                return bool.Parse(iConfigurationRoot[configName]);
            }

            return defaultValue;
        }

        private static string GetConfig(IConfigurationRoot iConfigurationRoot, string configName, string defaultValue)
        {
            if(!string.IsNullOrEmpty(iConfigurationRoot[configName]))
            {
                return iConfigurationRoot[configName];
            }

            return defaultValue;
        }

        private class DataSource
        {
            private readonly List<string> additionalProperties = new List<string>();
            private readonly int itemSize;
            private readonly int partitionKeyCount;
            private static readonly JsonSerializerSettings JsonSerializerSettings = new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore };
            private string padding = string.Empty;
            private PartitionKey[] partitionKeys;
            private ConcurrentDictionary<PartitionKey, ConcurrentBag<MemoryStream>> itemsByPK;
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
                int currentLen = (int)CreateNextDocItem(partitionKeyValuePrefix + "0").Length + 250;
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
                            itemsByPK.TryAdd(this.partitionKeys[pkIndex], new ConcurrentBag<MemoryStream>());
                        }
                    }

                    if(i < preSerializedItemCount)
                    {
                        itemsByPK[this.partitionKeys[pkIndex]].Add(this.CreateNextDocItem(partitionKeyValue));
                    }

                    pkIndex++;
                }
            }

            private MemoryStream CreateNextDocItem(string partitionKey, string id = null)
            {
                if(id == null) { id = Guid.NewGuid().ToString(); }

                MyDocument myDocument = new MyDocument()
                {
                    id = id,
                    pk = partitionKey,
                    arr = this.additionalProperties,
                    other = this.padding
                };
                string value = JsonConvert.SerializeObject(myDocument, JsonSerializerSettings);
                return new MemoryStream(Encoding.UTF8.GetBytes(value));
            }

            public MemoryStream GetNextDocItem(int workerIndex, out PartitionKey partitionKey)
            {
                int incremented = Interlocked.Increment(ref itemIndex);
                int currentPKIndex = incremented % partitionKeyCount;
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