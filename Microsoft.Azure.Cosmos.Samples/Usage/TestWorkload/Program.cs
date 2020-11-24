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
        private static readonly JsonSerializer Serializer = new JsonSerializer();
        private static CosmosClient client;
        private static Database database = null;
        private static int itemsToCreate;
        private static int preSerializedItemCount;
        private static int itemSize;
        private static int itemPropertyCount;
        private static int maxRuntimeInSeconds;
        private static bool shouldCleanupOnFinish;
        private static int numWorkers;
        private static int partitionKeyCount;
        private const string PartitionKeyValuePrefix = "0";

        public static async Task Main(string[] args)
        {
            try
            {
                Container container = await Program.Initialize();

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
                if (Program.shouldCleanupOnFinish)
                {
                    await Program.CleanupAsync();
                }

                client.Dispose();

                Console.WriteLine("End of demo.");
            }
        }

        private static async Task CreateItemsConcurrentlyAsync(Container container)
        {
            Console.WriteLine($"Starting creation of {Program.itemsToCreate} items each with {Program.itemPropertyCount} properties of about {Program.itemSize} bytes"
            + $" in a limit of {maxRuntimeInSeconds} seconds using {numWorkers} workers.");

            ConcurrentDictionary<HttpStatusCode, int> countsByStatus = new ConcurrentDictionary<HttpStatusCode, int>();
            ConcurrentBag<TimeSpan> latencies = new ConcurrentBag<TimeSpan>();
            long totalRequestCharge = 0;

            int taskCompleteCounter = 0;
            int taskTriggeredCounter = 0;

            DataSource dataSource = new DataSource(itemsToCreate, preSerializedItemCount, itemPropertyCount, itemSize, partitionKeyCount);

            Console.WriteLine("Datasource initialized; starting ingestion");

            int itemsToCreatePerWorker = (int)Math.Ceiling((double)(itemsToCreate / numWorkers));
            List<Task> workerTasks = new List<Task>();

            SemaphoreSlim semaphore = new SemaphoreSlim(80, 80);

            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(maxRuntimeInSeconds * 1000);
            CancellationToken cancellationToken = cancellationTokenSource.Token;
            Stopwatch stopwatch = Stopwatch.StartNew();
            int[] itemsCreatedByWorker = new int[numWorkers];

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

                        await semaphore.WaitAsync();

                        _ = container.UpsertItemStreamAsync(stream, partitionKeyValue, null, cancellationToken)
                            .ContinueWith((Task<ResponseMessage> task) =>
                            {
                                semaphore.Release();
                                if (task.IsCompletedSuccessfully)
                                {
                                    if (stream != null) { stream.Dispose(); }

                                    using (ResponseMessage responseMessage = task.Result)
                                    {
                                        countsByStatus.AddOrUpdate(responseMessage.StatusCode, 1, (_, old) => old + 1);
                                        if ((int)responseMessage.StatusCode == 408 || (int)responseMessage.StatusCode >= 500)
                                        {
                                            if (!isErrorPrinted)
                                            {
                                                Console.WriteLine(responseMessage.ErrorMessage);
                                                Console.WriteLine(responseMessage.Diagnostics.ToString());
                                                isErrorPrinted = true;
                                            }
                                        }

                                        if ((int)responseMessage.StatusCode < 400)
                                        {
                                            Interlocked.Add(ref totalRequestCharge, (int)(responseMessage.Headers.RequestCharge * 100));
                                            latencies.Add(responseMessage.Diagnostics.GetClientElapsedTime());
                                            ++itemsCreatedByWorker[workerIndexLocal];
                                        }
                                    }
                                }

                                Exception ex = task.Exception;
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
                if (cancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine($"Could not insert {itemsToCreate} items in {maxRuntimeInSeconds} seconds.");
                    break;
                }

                Console.Write($"In progress. Triggered: {taskTriggeredCounter} Processed: {taskCompleteCounter}, Pending: {itemsToCreate - taskCompleteCounter}");
                foreach (KeyValuePair<HttpStatusCode, int> countForStatus in countsByStatus)
                {
                    Console.Write(", " + countForStatus.Key + ": " + countForStatus.Value);
                }

                foreach(int cx in itemsCreatedByWorker)
                {
                    Console.Write(" " + cx);
                }

                Console.WriteLine();
                await Task.Delay(1000);
            }


            int nonFailedCount = countsByStatus.Where(x => x.Key < HttpStatusCode.BadRequest).Sum(p => p.Value);
            long elapsed = stopwatch.ElapsedMilliseconds / 1000;
            Console.WriteLine($"Successfully handled {nonFailedCount} items in {elapsed} seconds at {(elapsed == 0 ? -1 : nonFailedCount / elapsed)} items/sec.");

            Console.WriteLine("Counts by StatusCode:");
            foreach (var countForStatus in countsByStatus)
            {
                Console.Write(", " + countForStatus.Key + ": " + countForStatus.Value);
            }
            Console.WriteLine();

            List<TimeSpan> latenciesList = latencies.ToList();
            latenciesList.Sort();
            int requestCount = latenciesList.Count;
            Console.WriteLine("Latencies (non-failed):"
            + $"   P90: {latenciesList[(int)(requestCount * 0.90)].TotalMilliseconds}"
            + $"   P99: {latenciesList[(int)(requestCount * 0.99)].TotalMilliseconds}"
            + $"   P99.9: {latenciesList[(int)(requestCount * 0.999)].TotalMilliseconds}"
            + $"   Max: {latenciesList[requestCount - 1].TotalMilliseconds}");

            Console.WriteLine("Average RUs (non-failed): " + totalRequestCharge / (100.0 * nonFailedCount));
        }

        private class MyDocument
        {
            public string id { get; set; }

            public string pk { get; set; }

            public List<string> arr {get; set;}
            
            public string other { get; set; }

            // private static Inner inner = new Inner();
            // public Inner i0 { get { return inner; } }
            // public Inner i1 { get { return inner; } }
        }

        private class Inner
        {
            public string p0 { get { return "abcdefghijklmnopqrstuvwxy"; } }
            public string p1 { get { return "abcdefghijklmnopqrstuvwxy"; } }
            public string p2 { get { return "abcdefghijklmnopqrstuvwxy"; } }
            public string p3 { get { return "abcdefghijklmnopqrstuvwxy"; } }
            public string p4 { get { return "abcdefghijklmnopqrstuvwxy"; } }
            //public string p5 { get { return "abcdefghijklmnopqrstuvwxy"; } }
            //public string p6 { get { return "abcdefghijklmnopqrstuvwxy"; } }
            // public string p7 { get { return "abcdefghijklmnopqrstuvwxy"; } }
            // public string p8 { get { return "abcdefghijklmnopqrstuvwxy"; } }
            // public string p9 { get { return "abcdefghijklmnopqrstuvwxy"; } }
        }

        private static async Task<Container> Initialize()
        {
            // Read the Cosmos endpointUrl and authorization keys from configuration
            // These values are available from the Azure Management Portal on the Cosmos Account Blade under "Keys"
            // Keep these values in a safe & secure location. Together they provide Administrative access to your Cosmos account
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

            string databaseName = configuration["DatabaseName"];
            if (string.IsNullOrEmpty(databaseName))
            {
                throw new ArgumentException("Please specify a valid DatabaseName in the appSettings.json");
            }

            string containerName = configuration["ContainerName"];
            if (string.IsNullOrEmpty(containerName))
            {
                throw new ArgumentException("Please specify a valid ContainerName in the appSettings.json");
            }

            Program.itemsToCreate = int.Parse(string.IsNullOrEmpty(configuration["ItemsToCreate"]) ? "1000" : configuration["ItemsToCreate"]);
            Program.itemSize = int.Parse(string.IsNullOrEmpty(configuration["ItemSize"]) ? "1024" : configuration["ItemSize"]);
            Program.itemPropertyCount = int.Parse(string.IsNullOrEmpty(configuration["ItemPropertyCount"]) ? "2" : configuration["ItemPropertyCount"]);
            Program.maxRuntimeInSeconds = int.Parse(string.IsNullOrEmpty(configuration["MaxRuntimeInSeconds"]) ? "30" : configuration["MaxRuntimeInSeconds"]);
            Program.numWorkers = int.Parse(string.IsNullOrEmpty(configuration["NumWorkers"]) ? "1" : configuration["numWorkers"]);
            Program.preSerializedItemCount = int.Parse(string.IsNullOrEmpty(configuration["PreSerializedItemCount"]) ? "0" : configuration["PreSerializedItemCount"]);
            Program.partitionKeyCount = int.Parse(string.IsNullOrEmpty(configuration["PartitionKeyCount"]) ? int.MaxValue.ToString() : configuration["PartitionKeyCount"]);

            Program.shouldCleanupOnFinish = bool.Parse(string.IsNullOrEmpty(configuration["ShouldCleanupOnFinish"]) ? "false" : configuration["ShouldCleanupOnFinish"]);

            bool shouldCleanupOnStart = bool.Parse(string.IsNullOrEmpty(configuration["ShouldCleanupOnStart"]) ? "false" : configuration["ShouldCleanupOnStart"]);
            bool shouldIndexAllProperties = bool.Parse(string.IsNullOrEmpty(configuration["ShouldIndexAllProperties"]) ? "false" : configuration["ShouldIndexAllProperties"]);
            int collectionThroughput = int.Parse(string.IsNullOrEmpty(configuration["CollectionThroughput"]) ? "30000" : configuration["CollectionThroughput"]);

            Program.client = GetClientInstance(endpointUrl, authKey);
            Program.database = client.GetDatabase(databaseName);
            Container container = Program.database.GetContainer(containerName);
            if (shouldCleanupOnStart)
            {
                container = await Program.CreateFreshContainerAsync(client, databaseName, containerName, shouldIndexAllProperties, collectionThroughput);
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

            Console.WriteLine("Running demo for container {0} with a CosmosClient.", containerName);

            return container;
        }

        private static CosmosClient GetClientInstance(
            string endpoint,
            string authKey) =>
            new CosmosClient(endpoint, authKey, new CosmosClientOptions()
            {
                AllowBulkExecution = false,
                MaxRetryAttemptsOnRateLimitedRequests = 0
            });

        private static async Task CleanupAsync()
        {
            if (Program.database != null)
            {
                await Program.database.DeleteAsync();
            }
        }

        private static async Task<Container> CreateFreshContainerAsync(
            CosmosClient client,
             string databaseName, 
             string containerName,
             bool shouldIndexAllProperties,
            int throughput)
        {
            Program.database = await client.CreateDatabaseIfNotExistsAsync(databaseName);

            try
            {
                Console.WriteLine("Deleting old container if it exists.");
                await database.GetContainer(containerName).DeleteContainerStreamAsync();
            }
            catch (Exception)
            {
                // Do nothing
            }

            // We create a partitioned collection here which needs a partition key. Partitioned collections
            // can be created with very high values of provisioned throughput and used to store 100's of GBs of data. 
            Console.WriteLine($"The demo will create a {throughput} RU/s container...");

            ContainerBuilder containerBuilder = database.DefineContainer(containerName, "/pk");

            if(shouldIndexAllProperties)
            {
                containerBuilder.WithIndexingPolicy()
                    .WithIndexingMode(IndexingMode.Consistent)
                    .WithIncludedPaths()
                        .Attach()
                    .WithExcludedPaths()
                        .Path("/*")
                        .Attach()
                .Attach();
            }

            return await containerBuilder.CreateAsync(throughput);
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
                int currentLen = (int)CreateNextDocItem(PartitionKeyValuePrefix + "0").Length + 250;
                this.padding = this.itemSize > currentLen ? new string('x', this.itemSize - currentLen) : string.Empty;

                int pkIndex = this.partitionKeyCount;
                preSerializedItemCount = Math.Min(preSerializedItemCount, itemCount);
                for (int i = 0; i < Math.Max(preSerializedItemCount, this.partitionKeyCount); i++)
                {
                    if(pkIndex == this.partitionKeyCount)
                    {
                        pkIndex = 0;
                    }

                    string partitionKeyValue = PartitionKeyValuePrefix + pkIndex;
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

            private MemoryStream CreateNextDocItem(string partitionKey)
            {
                string id = Guid.NewGuid().ToString();

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
                // int currentPKIndex = workerIndex;
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
                    return this.CreateNextDocItem(PartitionKeyValuePrefix + currentPKIndex);
                }
            }
        }
    }
}

