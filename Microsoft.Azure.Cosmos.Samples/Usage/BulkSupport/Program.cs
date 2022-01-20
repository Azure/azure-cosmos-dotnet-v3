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
    using Newtonsoft.Json.Linq;

    // ----------------------------------------------------------------------------------------------------------
    // Prerequisites - 
    // 
    // 1. An Azure Cosmos account - 
    //    https://docs.microsoft.com/azure/cosmos-db/create-cosmosdb-resources-portal
    //
    // 2. Microsoft.Azure.Cosmos NuGet package - 
    //    http://www.nuget.org/packages/Microsoft.Azure.Cosmos/ 
    // ----------------------------------------------------------------------------------------------------------
    // Sample - demonstrates the basic usage of the CosmosClient bulk mode by performing a high volume of operations
    // ----------------------------------------------------------------------------------------------------------

    public class Program
    {
        private static readonly JsonSerializer Serializer = new JsonSerializer();
        private static CosmosClient client;
        private static Database database = null;
        private static int itemsToCreate;
        private static int itemSize;
        private static int runtimeInSeconds;
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

                // Running bulk ingestion on a container.
                await Program.CreateItemsConcurrentlyAsync(container);

                await Program.RemovePropertyFromAllItemsAsync(container);
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
                Console.ReadKey();
            }
        }
        // </Main>

        private static async Task CreateItemsConcurrentlyAsync(Container container)
        {
            Console.WriteLine($"Starting creation of {itemsToCreate} items of about {itemSize} bytes each in a limit of {runtimeInSeconds} seconds using {numWorkers} workers.");

            ConcurrentDictionary<HttpStatusCode, int> countsByStatus = new ConcurrentDictionary<HttpStatusCode, int>();
            int taskCompleteCounter = 0;
            int globalDocCounter = 0;

            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(runtimeInSeconds * 1000);
            CancellationToken cancellationToken = cancellationTokenSource.Token;

            Stopwatch stopwatch = Stopwatch.StartNew();
            long startMilliseconds = stopwatch.ElapsedMilliseconds;

            try
            {
                List<Task> workerTasks = new List<Task>();
                for (int i = 0; i < numWorkers; i++)
                {
                    workerTasks.Add(Task.Run(() =>
                    {
                        DataSource dataSource = new DataSource(itemsToCreate, itemSize, numWorkers);
                        int docCounter = 0;

                        while (!cancellationToken.IsCancellationRequested && docCounter < itemsToCreate)
                        {
                            docCounter++;

                            MemoryStream stream = dataSource.GetNextDocItem(out PartitionKey partitionKeyValue);
                            _ = container.CreateItemStreamAsync(stream, partitionKeyValue, null, cancellationToken)
                                .ContinueWith((Task<ResponseMessage> task) =>
                                {
                                    Interlocked.Increment(ref taskCompleteCounter);

                                    if (task.IsCompletedSuccessfully)
                                    {
                                        if (stream != null) { stream.Dispose(); }
                                        HttpStatusCode resultCode = task.Result.StatusCode;
                                        countsByStatus.AddOrUpdate(resultCode, 1, (_, old) => old + 1);
                                        if (task.Result != null) { task.Result.Dispose(); }
                                    }
                                    task.Dispose();
                                });
                        }

                        Interlocked.Add(ref globalDocCounter, docCounter);
                    }));
                }

                await Task.WhenAll(workerTasks);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not insert {itemsToCreate * numWorkers} items in {runtimeInSeconds} seconds.");
                Console.WriteLine(ex);
            }
            finally
            {
                while (globalDocCounter > taskCompleteCounter)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        Console.WriteLine($"Could not insert {itemsToCreate * numWorkers} items in {runtimeInSeconds} seconds.");
                        break;
                    }
                    Console.WriteLine($"In progress. Processed: {taskCompleteCounter}, Pending: {globalDocCounter - taskCompleteCounter}");
                    Thread.Sleep(2000);
                }

                foreach (var countForStatus in countsByStatus)
                {
                    Console.WriteLine(countForStatus.Key + " " + countForStatus.Value);
                }
            }

            int created = countsByStatus.SingleOrDefault(x => x.Key == HttpStatusCode.Created).Value;
            Console.WriteLine($"Inserted {created} items in {(stopwatch.ElapsedMilliseconds - startMilliseconds) / 1000} seconds");
        }

        private static async Task RemovePropertyFromAllItemsAsync(Container container)
        {
            Console.WriteLine($"Starting remove property from {itemsToCreate} items");

            using FeedIterator<JObject> queryOfItemsToUpdate = container.GetItemQueryIterator<JObject>(
                "select * from T where IS_DEFINED(T.other)",
                requestOptions: new QueryRequestOptions()
                {
                    MaxBufferedItemCount = 0,
                    MaxConcurrency = 1,
                    MaxItemCount = 100
                });

            while (queryOfItemsToUpdate.HasMoreResults)
            {
                FeedResponse<JObject> items = await queryOfItemsToUpdate.ReadNextAsync();
                List<Task> tasks = new List<Task>(1000);
                foreach (JObject item in items)
                {
                    tasks.Add(Program.RemoveItemProperty(container, item));
                }

                await Task.WhenAll(tasks);
            }

            Console.WriteLine($"All items updated to remove property 'other'");
        }

        private static async Task RemoveItemProperty(
            Container container,
            JObject item)
        {
            ItemRequestOptions itemRequestOptions = new ItemRequestOptions()
            {
                EnableContentResponseOnWrite = false,
            };

            // While loop is used to handle scenarios when the item being updated was changed by a
            // different process. The item needs to be read again to get the latest version.
            while (true)
            {
                // Remove the 'other' property from the json. 
                item.Remove("other");

                string id = item["id"].Value<string>();
                string pk = item["pk"].Value<string>();

                // Setting the etag will cause an exception if the item was updated after it was read
                itemRequestOptions.IfMatchEtag = item["_etag"].Value<string>();
                try
                {
                    await container.ReplaceItemAsync<JObject>(item, id, new PartitionKey(pk), itemRequestOptions);
                    return;
                }
                catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
                {
                    // The item was updated after the query. Read the latest item and try update again.
                    Console.WriteLine($"Replace item failed at {DateTime.UtcNow} with id:{id}; pk:{pk}; Excepion: {ex}");
                    item = await container.ReadItemAsync<JObject>(id, new PartitionKey(pk));
                }
            }
        }

        // <Model>
        private class MyDocument
        {
            public string id { get; set; }

            public string pk { get; set; }

            public string other { get; set; }
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
            Program.runtimeInSeconds = int.Parse(string.IsNullOrEmpty(configuration["RuntimeInSeconds"]) ? "30" : configuration["RuntimeInSeconds"]);
            Program.numWorkers = int.Parse(string.IsNullOrEmpty(configuration["numWorkers"]) ? "1" : configuration["numWorkers"]);

            Program.shouldCleanupOnFinish = bool.Parse(string.IsNullOrEmpty(configuration["ShouldCleanupOnFinish"]) ? "false" : configuration["ShouldCleanupOnFinish"]);
            bool shouldCleanupOnStart = bool.Parse(string.IsNullOrEmpty(configuration["ShouldCleanupOnStart"]) ? "false" : configuration["ShouldCleanupOnStart"]);
            int collectionThroughput = int.Parse(string.IsNullOrEmpty(configuration["CollectionThroughput"]) ? "30000" : configuration["CollectionThroughput"]);

            Program.client = GetBulkClientInstance(endpointUrl, authKey);
            Program.database = client.GetDatabase(databaseName);
            Container container = Program.database.GetContainer(containerName); ;
            if (shouldCleanupOnStart)
            {
                container = await CreateFreshContainerAsync(client, databaseName, containerName, collectionThroughput);
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

            Console.WriteLine("Running demo for container {0} with a Bulk enabled CosmosClient.", containerName);

            return container;
        }

        private static CosmosClient GetBulkClientInstance(
            string endpoint,
            string authKey)
        {
            // </Initialization>
            return new CosmosClient(endpoint, authKey, new CosmosClientOptions() { AllowBulkExecution = true });
        }

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
            catch (Exception)
            {
                // Do nothing
            }

            // We create a partitioned collection here which needs a partition key. Partitioned collections
            // can be created with very high values of provisioned throughput and used to store 100's of GBs of data. 
            Console.WriteLine($"The demo will create a {throughput} RU/s container, press any key to continue.");
            Console.ReadKey();

            // Indexing Policy to exclude all attributes to maximize RU/s usage
            Container container = await database.DefineContainer(containerName, "/pk")
                    .WithIndexingPolicy()
                        .WithIndexingMode(IndexingMode.Consistent)
                        .WithIncludedPaths()
                            .Attach()
                        .WithExcludedPaths()
                            .Path("/*")
                            .Attach()
                    .Attach()
                .CreateAsync(throughput);

            return container;
        }

        private class DataSource
        {
            private readonly int itemSize;
            private const long maxStoredSizeInBytes = 100 * 1024 * 1024;
            private Queue<KeyValuePair<PartitionKey, MemoryStream>> documentsToImportInBatch;
            string padding = string.Empty;

            public DataSource(int itemCount, int itemSize, int numWorkers)
            {
                this.itemSize = itemSize;
                long maxStoredItemsPossible = (maxStoredSizeInBytes / (long)numWorkers) / (long)itemSize;
                documentsToImportInBatch = new Queue<KeyValuePair<PartitionKey, MemoryStream>>();
                this.padding = this.itemSize > 300 ? new string('x', this.itemSize - 300) : string.Empty;

                for (long j = 0; j < Math.Min((long)itemCount, maxStoredItemsPossible); j++)
                {
                    MemoryStream value = this.CreateNextDocItem(out PartitionKey partitionKeyValue);
                    documentsToImportInBatch.Enqueue(new KeyValuePair<PartitionKey, MemoryStream>(partitionKeyValue, value));
                }
            }

            private MemoryStream CreateNextDocItem(out PartitionKey partitionKeyValue)
            {
                string partitionKey = Guid.NewGuid().ToString();
                string id = Guid.NewGuid().ToString();

                MyDocument myDocument = new MyDocument() { id = id, pk = partitionKey, other = padding };
                string value = JsonConvert.SerializeObject(myDocument);
                partitionKeyValue = new PartitionKey(partitionKey);

                return new MemoryStream(Encoding.UTF8.GetBytes(value));
            }

            public MemoryStream GetNextDocItem(out PartitionKey partitionKeyValue)
            {
                if (documentsToImportInBatch.Count > 0)
                {
                    KeyValuePair<PartitionKey, MemoryStream> pair = documentsToImportInBatch.Dequeue();
                    partitionKeyValue = pair.Key;
                    return pair.Value;
                }
                else
                {
                    MemoryStream value = CreateNextDocItem(out PartitionKey pkValue);
                    partitionKeyValue = pkValue;
                    return value;
                }
            }
        }
    }
}

