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
    // Sample - demonstrates the basic usage of the CosmosClient.
    // ----------------------------------------------------------------------------------------------------------

    public class Program
    {
        private static readonly JsonSerializer Serializer = new JsonSerializer();
        private static CosmosClient client;
        private static Database database = null;
        private static int itemsToCreate;
        private static int itemSize;
        private static int runtimeInSeconds;
        private static bool shouldCleanupOnStart;
        private static bool shouldCleanupOnFinish;
        private static int numWorkers;
        private static bool isChangeFeed;

        public static async Task Main(string[] args)
        {
            try
            {
                Container container = await Program.Initialize();

                if (Program.shouldCleanupOnStart)
                {
                    await Program.CreateItemsConcurrentlyAsync(container);
                }

                List<Memory<byte>> results = await Program.RunQuery(container);

                Stream outputStream = Console.OpenStandardOutput();
                foreach (Memory<byte> result in results)
                {
                    outputStream.Write(result.Span);
                    Console.WriteLine();
                }
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

        private static async Task<List<Memory<byte>>> RunQuery(Container container)
        {
            List<Memory<byte>> results = new List<Memory<byte>>();
            FeedIterator feedIterator;

            if (Program.isChangeFeed)
            {
                feedIterator = container.GetChangeFeedStreamIterator(
                changeFeedRequestOptions: new ChangeFeedRequestOptions()
                {
                    StartTime = DateTime.MinValue.ToUniversalTime(),
                    MaxItemCount = 10000
                });
            }
            else
            {
                feedIterator = container.GetItemQueryStreamIterator(
                    "SELECT * FROM C",
                    requestOptions: new QueryRequestOptions()
                    {
                        MaxItemCount = 10000
                    });
            }

            while (feedIterator.HasMoreResults)
            {
                await Program.ReadNextQueryResultsAsync(feedIterator, results);
            }

            return results;
        }

        private static async Task ReadNextQueryResultsAsync(FeedIterator feedIterator, List<Memory<byte>> results)
        {
            using (ResponseMessage responseMessage = await feedIterator.ReadNextAsync())
            {
                if (responseMessage.IsSuccessStatusCode)
                {
                    if (responseMessage.Content == null)
                    {
                        throw new InvalidOperationException("Unexpected empty Content in successful response");
                    }

                    Stream seekableContent;
                    if (responseMessage.Content is MemoryStream memoryStream
                        && memoryStream.TryGetBuffer(out ArraySegment<byte> buffer))
                    {
                        seekableContent = responseMessage.Content;
                    }
                    else
                    {
                        MemoryStream memoryStreamCopy = new MemoryStream();
                        await responseMessage.Content.CopyToAsync(memoryStreamCopy);
                        seekableContent = memoryStreamCopy;
                        memoryStreamCopy.TryGetBuffer(out buffer);
                    }

                    Memory<byte> bufferMemory = buffer.AsMemory<byte>();

                    ReaderState readerState = ReaderState.Initial;
                    using (JsonTextReader jsonTextReader = new JsonTextReader(new StreamReader(seekableContent)))
                    {
                        while (jsonTextReader.Read())
                        {
                            if (readerState == ReaderState.Initial)
                            {
                                if (jsonTextReader.TokenType == JsonToken.PropertyName
                                    && jsonTextReader.Depth == 1
                                    && (jsonTextReader.Value as string) == "Documents")
                                {
                                    jsonTextReader.Read();
                                    if (jsonTextReader.TokenType != JsonToken.StartArray)
                                    {
                                        throw new InvalidOperationException("Unexpected json");
                                    }

                                    readerState = ReaderState.WithinDocuments;
                                }
                            }
                            else if (readerState == ReaderState.WithinDocuments)
                            {
                                if (jsonTextReader.TokenType == JsonToken.EndArray && jsonTextReader.Depth == 1)
                                {
                                    readerState = ReaderState.AfterDocuments;
                                }
                                else if (jsonTextReader.TokenType == JsonToken.StartObject && jsonTextReader.Depth == 2)
                                {
                                    if (jsonTextReader.LineNumber != 1)
                                    {
                                        // todo: this is actually an assumption we should handle instead.
                                        throw new InvalidOperationException("Unexpected multi-line json");
                                    }

                                    int start = jsonTextReader.LinePosition - 1;
                                    jsonTextReader.Skip();
                                    int end = jsonTextReader.LinePosition - 1;
                                    results.Add(bufferMemory.Slice(start, end - start + 1));
                                }
                                else
                                {
                                    throw new InvalidOperationException("Unexpected json");
                                }
                            }
                        }
                    }
                }
            }
        }

        enum ReaderState
        {
            Initial = 0,
            WithinDocuments = 1,
            AfterDocuments
        }

        private static async Task CreateItemsConcurrentlyAsync(Container container)
        {
            Console.WriteLine($"Starting creation of {itemsToCreate} items in a limit of {runtimeInSeconds} seconds using {numWorkers} workers.");

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

                foreach (KeyValuePair<HttpStatusCode, int> countForStatus in countsByStatus)
                {
                    Console.WriteLine(countForStatus.Key + " " + countForStatus.Value);
                }
            }

            int created = countsByStatus.SingleOrDefault(x => x.Key == HttpStatusCode.Created).Value;
            Console.WriteLine($"Inserted {created} items in {(stopwatch.ElapsedMilliseconds - startMilliseconds) / 1000} seconds");
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

            Program.itemsToCreate = int.Parse(string.IsNullOrEmpty(configuration["ItemsToCreate"]) ? "1000" : configuration["ItemsToCreate"]);
            Program.itemSize = int.Parse(string.IsNullOrEmpty(configuration["ItemSize"]) ? "1024" : configuration["ItemSize"]);
            Program.runtimeInSeconds = int.Parse(string.IsNullOrEmpty(configuration["RuntimeInSeconds"]) ? "30" : configuration["RuntimeInSeconds"]);
            Program.numWorkers = int.Parse(string.IsNullOrEmpty(configuration["NumWorkers"]) ? "1" : configuration["NumWorkers"]);

            Program.shouldCleanupOnFinish = bool.Parse(string.IsNullOrEmpty(configuration["ShouldCleanupOnFinish"]) ? "false" : configuration["ShouldCleanupOnFinish"]);
            Program.shouldCleanupOnStart = bool.Parse(string.IsNullOrEmpty(configuration["ShouldCleanupOnStart"]) ? "false" : configuration["ShouldCleanupOnStart"]);
            int collectionThroughput = int.Parse(string.IsNullOrEmpty(configuration["CollectionThroughput"]) ? "30000" : configuration["CollectionThroughput"]);

            Program.isChangeFeed = bool.Parse(string.IsNullOrEmpty(configuration["IsChangeFeed"]) ? "false" : configuration["IsChangeFeed"]);

            Program.client = GetClientInstance(endpointUrl, authKey);
            Program.database = client.GetDatabase(databaseName);
            Container container = Program.database.GetContainer(containerName);
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
                throw ex;
            }

            Console.WriteLine("Running demo for container {0} with a CosmosClient.", containerName);

            return container;
        }

        private static CosmosClient GetClientInstance(
            string endpoint,
            string authKey) =>
        // </Initialization>
            new CosmosClient(endpoint, authKey);
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
            //Console.ReadKey();

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
                long maxStoredItemsPossible = maxStoredSizeInBytes / (long)numWorkers / (long)itemSize;
                this.documentsToImportInBatch = new Queue<KeyValuePair<PartitionKey, MemoryStream>>();
                this.padding = this.itemSize > 300 ? new string('x', this.itemSize - 300) : string.Empty;

                for (long j = 0; j < Math.Min((long)itemCount, maxStoredItemsPossible); j++)
                {
                    MemoryStream value = this.CreateNextDocItem(out PartitionKey partitionKeyValue);
                    this.documentsToImportInBatch.Enqueue(new KeyValuePair<PartitionKey, MemoryStream>(partitionKeyValue, value));
                }
            }

            private MemoryStream CreateNextDocItem(out PartitionKey partitionKeyValue)
            {
                string partitionKey = Guid.NewGuid().ToString();
                string id = Guid.NewGuid().ToString();

                // This is for simplicity rather than efficiency
                JObject jObject = new JObject();
                jObject.Add("pk", partitionKey);
                jObject.Add("id", id);
                for (int i = 0; i < 100; i++)
                {
                    jObject.Add("prop" + i, string.Empty);
                }
                // MyDocument myDocument = new MyDocument() { id = id, pk = partitionKey, other = padding };
                string value = JsonConvert.SerializeObject(jObject);
                partitionKeyValue = new PartitionKey(partitionKey);

                return new MemoryStream(Encoding.UTF8.GetBytes(value));
            }

            public MemoryStream GetNextDocItem(out PartitionKey partitionKeyValue)
            {
                if (this.documentsToImportInBatch.Count > 0)
                {
                    KeyValuePair<PartitionKey, MemoryStream> pair = this.documentsToImportInBatch.Dequeue();
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

