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
        private static int itemSize;
        private static int maxRuntimeInSeconds;
        private static bool shouldCleanupOnFinish;
        private static int numWorkers;

        // Async main requires c# 7.1 which is set in the csproj with the LangVersion attribute
        // <Main>
        public static async Task Main(string[] args)
        {
            try
            {
                // Intialize container or create a new container.
                Container container = await Program.Initialize();

                // Running ingestion on a container.
                await Program.CreateItemsConcurrentlyAsync(container);
            }
            catch (CosmosException cre)
            {
                Console.WriteLine(cre.ToString());
            }
            catch (Exception e)
            {
                Exception baseException = e.GetBaseException();
                Console.WriteLine("Error: {0}, Message: {1}", e.Message, baseException.Message);
            }
            finally
            {
                if (Program.shouldCleanupOnFinish)
                {
                    await Program.CleanupAsync();
                }

                client.Dispose();

                Console.WriteLine("End of demo, press any key to exit.");
                //Console.ReadKey();
            }
        }
        // </Main>

        private static async Task CreateItemsConcurrentlyAsync(Container container)
        {
            Console.WriteLine($"Starting creation of {Program.itemsToCreate} items of about {Program.itemSize} bytes"
            + $" in a limit of {maxRuntimeInSeconds} seconds using {numWorkers} workers.");

            ConcurrentDictionary<HttpStatusCode, int> countsByStatus = new ConcurrentDictionary<HttpStatusCode, int>();
            ConcurrentBag<TimeSpan> latencies = new ConcurrentBag<TimeSpan>();
            long totalRequestCharge = 0;

            int taskCompleteCounter = 0;
            int taskTriggeredCounter = 0;

            DataSource dataSource = new DataSource(itemsToCreate, itemSize);
            Console.WriteLine("Datasource initialized.");

            Console.Read();
            
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(maxRuntimeInSeconds * 1000);
            CancellationToken cancellationToken = cancellationTokenSource.Token;

            Stopwatch stopwatch = Stopwatch.StartNew();
            long startMilliseconds = stopwatch.ElapsedMilliseconds;

            try
            {
                int itemsToCreatePerWorker = itemsToCreate / numWorkers + 1;

                List<Task> workerTasks = new List<Task>();
                for (int i = 0; i < numWorkers; i++)
                {
                    workerTasks.Add(Task.Run(() =>
                    {
                        int docCounter = 0;

                        while (!cancellationToken.IsCancellationRequested && docCounter < itemsToCreatePerWorker)
                        {
                            docCounter++;

                            MemoryStream stream = dataSource.GetNextDocItem(out PartitionKey partitionKeyValue);
                            _ = container.CreateItemStreamAsync(stream, partitionKeyValue, null, cancellationToken)
                                .ContinueWith((Task<ResponseMessage> task) =>
                                {
                                    if (task.IsCompletedSuccessfully)
                                    {
                                        if (stream != null) { stream.Dispose(); }

                                        ResponseMessage responseMessage = task.Result;
                                        countsByStatus.AddOrUpdate(responseMessage.StatusCode, 1, (_, old) => old + 1);
                                        Interlocked.Add(ref totalRequestCharge, (int)(responseMessage.Headers.RequestCharge * 100));
                                        latencies.Add(responseMessage.Diagnostics.GetClientElapsedTime());
                                        responseMessage.Dispose();
                                    }

                                    task.Dispose();
                                    Interlocked.Increment(ref taskCompleteCounter);
                                });

                            Interlocked.Increment(ref taskTriggeredCounter);
                        }
                    }));
                }

                // await Task.WhenAll(workerTasks);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not insert {itemsToCreate * numWorkers} items in {maxRuntimeInSeconds} seconds.");
                Console.WriteLine(ex);
            }
            finally
            {
                while (itemsToCreate > taskCompleteCounter)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        Console.WriteLine($"Could not insert {itemsToCreate} items in {maxRuntimeInSeconds} seconds.");
                        break;
                    }

                    Console.WriteLine($"In progress. Triggered: {taskTriggeredCounter} Processed: {taskCompleteCounter}, Pending: {itemsToCreate - taskCompleteCounter}");
                    await Task.Delay(1000);
                }

                foreach (var countForStatus in countsByStatus)
                {
                    Console.WriteLine(countForStatus.Key + " " + countForStatus.Value);
                }
            }

            int created = countsByStatus.SingleOrDefault(x => x.Key == HttpStatusCode.Created).Value;
            long elapsed = (stopwatch.ElapsedMilliseconds - startMilliseconds) /1000;
            Console.WriteLine($"Inserted {created} items in {elapsed} seconds at {created/elapsed} items/sec.");

            List<TimeSpan> latenciesList = latencies.ToList();
            latenciesList.Sort();
            int requestCount = latenciesList.Count;
            Console.WriteLine("Latencies:"
            + $" P90:{latenciesList[(int)(requestCount * 0.90)].TotalMilliseconds}"
            + $" P99:{latenciesList[(int)(requestCount * 0.99)].TotalMilliseconds}"
            + $" P99.9:{latenciesList[(int)(requestCount * 0.999)].TotalMilliseconds}"
            + $" Max:{latenciesList[requestCount - 1].TotalMilliseconds}");

            Console.WriteLine("Average RUs:" + totalRequestCharge / (100 * taskCompleteCounter));
        }

        // <Model>
        private class MyDocument
        {  
            private static Inner inner = new Inner();

            public string id { get; set; }

            public string pk { get; set; }

            public string other { get; set; }

            public Inner i0 { get { return inner; } }
            public Inner i1 { get { return inner; } }
        }

        private class Inner
        {
            public string p0 { get { return "abcdefghijklmnopqrstuvwxy"; } }
            public string p1 { get { return "abcdefghijklmnopqrstuvwxy"; } }
            public string p2 { get { return "abcdefghijklmnopqrstuvwxy"; } }
            public string p3 { get { return "abcdefghijklmnopqrstuvwxy"; } }
            public string p4 { get { return "abcdefghijklmnopqrstuvwxy"; } }
            public string p5 { get { return "abcdefghijklmnopqrstuvwxy"; } }
            public string p6 { get { return "abcdefghijklmnopqrstuvwxy"; } }
            public string p7 { get { return "abcdefghijklmnopqrstuvwxy"; } }
            public string p8 { get { return "abcdefghijklmnopqrstuvwxy"; } }
            public string p9 { get { return "abcdefghijklmnopqrstuvwxy"; } }
        }
        // </Model>

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

            // Important: Needed to regulate the main execution/ingestion job.
            Program.itemsToCreate = int.Parse(string.IsNullOrEmpty(configuration["ItemsToCreate"]) ? "1000" : configuration["ItemsToCreate"]);
            Program.itemSize = int.Parse(string.IsNullOrEmpty(configuration["ItemSize"]) ? "1024" : configuration["ItemSize"]);
            Program.maxRuntimeInSeconds = int.Parse(string.IsNullOrEmpty(configuration["MaxRuntimeInSeconds"]) ? "30" : configuration["MaxRuntimeInSeconds"]);
            Program.numWorkers = int.Parse(string.IsNullOrEmpty(configuration["NumWorkers"]) ? "1" : configuration["numWorkers"]);

            Program.shouldCleanupOnFinish = bool.Parse(string.IsNullOrEmpty(configuration["ShouldCleanupOnFinish"]) ? "false" : configuration["ShouldCleanupOnFinish"]);
            bool shouldCleanupOnStart = bool.Parse(string.IsNullOrEmpty(configuration["ShouldCleanupOnStart"]) ? "false" : configuration["ShouldCleanupOnStart"]);
            int collectionThroughput = int.Parse(string.IsNullOrEmpty(configuration["CollectionThroughput"]) ? "30000" : configuration["CollectionThroughput"]);

            Program.client = GetClientInstance(endpointUrl, authKey);
            Program.database = client.GetDatabase(databaseName);
            Container container = Program.database.GetContainer(containerName); ;
            if (shouldCleanupOnStart)
            {
                container = await Program.CreateFreshContainerAsync(client, databaseName, containerName, collectionThroughput);
            }

            try
            {
                await container.ReadContainerAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in reading collection: {0}", ex.Message);
                throw ex;
            }

            Console.WriteLine("Running demo for container {0} with a CosmosClient.", containerName);

            return container;
        }

        private static CosmosClient GetClientInstance(
            string endpoint,
            string authKey) =>
        // </Initialization>
            new CosmosClient(endpoint, authKey, new CosmosClientOptions() 
            {
                AllowBulkExecution = false,
                MaxRetryAttemptsOnRateLimitedRequests = 0
            });
        // </Initialization>

        private static async Task CleanupAsync()
        {
            if (Program.database != null)
            {
                await Program.database.DeleteAsync();
            }
        }

        private static async Task<Container> CreateFreshContainerAsync(CosmosClient client, string databaseName, string containerName, int throughput)
        {
            Program.database = await client.CreateDatabaseIfNotExistsAsync(databaseName);

            try
            {
                Console.WriteLine("Deleting old container if it exists.");
                await database.GetContainer(containerName).DeleteContainerStreamAsync();
            }
            catch(Exception) {
                // Do nothing
            }

            // We create a partitioned collection here which needs a partition key. Partitioned collections
            // can be created with very high values of provisioned throughput and used to store 100's of GBs of data. 
            Console.WriteLine($"The demo will create a {throughput} RU/s container, press any key to continue.");
            //Console.ReadKey();

            // Indexing Policy to exclude all attributes to maximize RU/s usage
            Container container = await database.DefineContainer(containerName, "/pk")
                    // .WithIndexingPolicy()
                    //     .WithIndexingMode(IndexingMode.Consistent)
                    //     .WithIncludedPaths()
                    //         .Attach()
                    //     .WithExcludedPaths()
                    //         .Path("/*")
                    //         .Attach()
                    // .Attach()
                .CreateAsync(throughput);

            return container;
        }

        private class DataSource
        {
            private const long maxStoredSizeInBytes = 50 * 1024 * 1024;
            private readonly int itemSize;
            private ConcurrentQueue<KeyValuePair<PartitionKey, MemoryStream>> items;
            private string padding = string.Empty;

            public DataSource(int itemCount, int itemSize)
            {
                this.itemSize = itemSize;
                long maxStoredItemsPossible = maxStoredSizeInBytes / itemSize;
                items = new ConcurrentQueue<KeyValuePair<PartitionKey, MemoryStream>>();
                this.padding = this.itemSize > 300 ? new string('x', this.itemSize - 300) : string.Empty;

                for (long j = 0; j < Math.Min((long)itemCount, maxStoredItemsPossible); j++)
                {
                    MemoryStream value = this.CreateNextDocItem(out PartitionKey partitionKeyValue);
                    items.Enqueue(new KeyValuePair<PartitionKey, MemoryStream>(partitionKeyValue, value));
                }
            }

            private MemoryStream CreateNextDocItem(out PartitionKey partitionKeyValue)
            {
                string partitionKey = Guid.NewGuid().ToString();
                string id = Guid.NewGuid().ToString();

                MyDocument myDocument = new MyDocument() { 
                    id = id, 
                    pk = partitionKey, 
                    other = this.padding };
                string value = JsonConvert.SerializeObject(myDocument);
                partitionKeyValue = new PartitionKey(partitionKey);

                return new MemoryStream(Encoding.UTF8.GetBytes(value));
            }

            public MemoryStream GetNextDocItem(out PartitionKey partitionKeyValue)
            {
                if(this.items.TryDequeue(out KeyValuePair<PartitionKey, MemoryStream> pair))
                {
                    partitionKeyValue = pair.Key;
                    return pair.Value;
                }
                else
                {
                    MemoryStream value = this.CreateNextDocItem(out PartitionKey pkValue);
                    partitionKeyValue = pkValue;
                    return value;
                }
            }
        }
    }
}

