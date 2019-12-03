namespace Cosmos.Samples.Shared
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
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
    //    https://azure.microsoft.com/en-us/itemation/articles/itemdb-create-account/
    //
    // 2. Microsoft.Azure.Cosmos NuGet package - 
    //    http://www.nuget.org/packages/Microsoft.Azure.Cosmos/ 
    // ----------------------------------------------------------------------------------------------------------
    // Sample - demonstrates the basic usage of the CosmosClient bulk mode by performing a high volume of operations
    // ----------------------------------------------------------------------------------------------------------

    public class Program
    {
        private const string databaseId = "samples";
        private const string containerId = "bulk-support";
        private static readonly JsonSerializer Serializer = new JsonSerializer();

        private static Database database = null;

        // Async main requires c# 7.1 which is set in the csproj with the LangVersion attribute
        // <Main>
        public static async Task Main(string[] args)
        {
            try
            {
                // Make sure this is >= 2 * physical_partition_count * (2MB / docSize) when useBulk is true
                // for best perf.
                int concurrency = args.Length > 0 ? int.Parse(args[0]) : 20000;
                int docSize = args.Length > 1 ? int.Parse(args[1]) : 1024;
                int runtimeInSeconds = args.Length > 2 ? int.Parse(args[2]) : 20;
                bool useBulk = args.Length > 3 ? bool.Parse(args[3]) : true;
                int workerCount = args.Length > 4 ? int.Parse(args[4]) : 1;
                int containers = args.Length > 5 ? int.Parse(args[5]) : 1;
                int type = args.Length > 6 ? int.Parse(args[6]) : 0;

                // Read the Cosmos endpointUrl and authorisationKeys from configuration
                // These values are available from the Azure Management Portal on the Cosmos Account Blade under "Keys"
                // Keep these values in a safe & secure location. Together they provide Administrative access to your Cosmos account
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

                //CosmosClient bulkClient = Program.GetBulkClientInstance(endpoint, authKey, useBulk);
                // Create the require container, can be done with any client
                //await Program.InitializeAsync(bulkClient, throughput: 6000);

                Console.WriteLine("Running demo with a {0}CosmosClient...", useBulk ? "Bulk enabled " : string.Empty);
                // Execute inserts for 30 seconds on a Bulk enabled client

                if (type == 0)
                {
                    await Program.CreateItemsConcurrentlyAsync(endpoint, authKey, useBulk, concurrency, docSize, runtimeInSeconds, workerCount, containers);
                }
                else
                {
                    await Program.RunBatchstreamerImportAsync(endpoint, authKey, useBulk, concurrency, docSize, runtimeInSeconds, workerCount, containers);
                }

                //bulkClient.Dispose();
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
                //await Program.CleanupAsync();
                Console.WriteLine("End of demo, press any key to exit.");
                Console.ReadKey();
            }
        }
        // </Main>

        private static CosmosClient GetBulkClientInstance(
            string endpoint,
            string authKey,
            bool useBulk) =>
        // </Initialization>
            new Microsoft.Azure.Cosmos.Fluent.CosmosClientBuilder(endpoint, authKey).WithBulkExecution(useBulk).Build();
        // </Initialization>

        private static async Task CreateItemsConcurrentlyAsync(
            string endpoint,
            string authKey,
            bool useBulk,
            int concurrency,
            int docSize,
            int runtimeInSeconds,
            int workerCount,
            int containers)
        {
            DataSource dataSource = new DataSource(concurrency, docSize, workerCount);
            Console.WriteLine("CreateItemsConcurrentlyAsync");
            Console.WriteLine($"Initiating creates of items of about {docSize} bytes with {workerCount} workers each maintaining {concurrency} in-progress items for {runtimeInSeconds} seconds.");
            int created = await CreateItemsAsync(endpoint, authKey, useBulk, dataSource, concurrency, docSize, workerCount, runtimeInSeconds, containers);
            Console.WriteLine($"Inserted {created} items.");
        }

        private static async Task RunBatchstreamerImportAsync(
            string endpoint,
            string authKey,
            bool useBulk,
            int concurrency,
            int docSize,
            int runtimeInSeconds,
            int workerCount,
            int containers)
        {
            CosmosClient client = Program.GetBulkClientInstance(endpoint, authKey, useBulk);
            //List<Container> conatiners = new List<Container>();
            //for (int i = 0; i < containers; i++)
            //{
            //    Container container = client.GetContainer(Program.databaseId, Program.containerId);
            //    conatiners.Add(container);
            //}
            Container container = client.GetContainer(Program.databaseId, Program.containerId);
            DataSource dataSource = new DataSource(concurrency, docSize, workerCount);
            Console.WriteLine("RunBatchstreamerImportAsync");
            Console.WriteLine($"Initiating creates of items of about {docSize} bytes with {workerCount} workers each maintaining {concurrency} in-progress items for {runtimeInSeconds} seconds.");
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(runtimeInSeconds * 1000);
            CancellationToken cancellationToken = cancellationTokenSource.Token;

            int localDocCounter = 0;
            List<Task<ResponseMessage>> workerTasks = new List<Task<ResponseMessage>>();
            var watch = System.Diagnostics.Stopwatch.StartNew();

            while (watch.ElapsedMilliseconds < runtimeInSeconds * 1000)
            {
                localDocCounter = localDocCounter + 1;
                MemoryStream stream = dataSource.GetNextItemStream(out PartitionKey partitionKeyValue, 0);

                workerTasks.Add(container.CreateItemStreamAsync(stream, partitionKeyValue, cancellationToken: cancellationToken));
            }

            Console.WriteLine("Basic divison done for " + localDocCounter + " in seconds: " + watch.ElapsedMilliseconds / 1000);
            ResponseMessage[] response = await Task.WhenAll(workerTasks);

            Console.WriteLine("Everthing completed for: " + localDocCounter + " and response has lenthg: " + response.Length);

            int docCount = 0;
            int successfullCount = 0;
            while (docCount < response.Length)
            {
                ResponseMessage resp = response[docCount];
                if (resp.IsSuccessStatusCode)
                {
                    successfullCount = successfullCount + 1;
                }
                docCount = docCount + 1;
            }
            Console.WriteLine("Total inserted docs in: " + successfullCount + " in seconds: " + watch.ElapsedMilliseconds / 1000);

        }

        private static async Task<int> CreateItemsAsync(
            string endpoint,
            string authKey,
            bool useBulk,
            DataSource dataSource,
            int concurrency,
            int docSize,
            int workerCount,
            int runtimeInSeconds,
            int containers)
        {
            CosmosClient client = Program.GetBulkClientInstance(endpoint, authKey, useBulk);
            Container container = client.GetContainer(Program.databaseId, Program.containerId);
            ConcurrentDictionary<HttpStatusCode, int> countsByStatus = new ConcurrentDictionary<HttpStatusCode, int>();
            List<Task> workerTasks = new List<Task>();
            Random random = new Random();

            Console.WriteLine("Starting job");
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(runtimeInSeconds * 1000);
            CancellationToken cancellationToken = cancellationTokenSource.Token;
            try
            {
                for (int i = 0; i < workerCount; i++)
                {
                    int tmpI = i;
                    workerTasks.Add(Task.Run(() =>
                    {
                        while (!cancellationToken.IsCancellationRequested)
                        {
                            MemoryStream stream = dataSource.GetNextItemStream(out PartitionKey partitionKeyValue, tmpI);
                            _ = container.CreateItemStreamAsync(stream, partitionKeyValue)
                                .ContinueWith((Task<ResponseMessage> task) =>
                                {
                                    dataSource.DoneWithItemStream(stream);
                                    HttpStatusCode resultCode = task.Result.StatusCode;
                                    countsByStatus.AddOrUpdate(resultCode, 1, (_, old) => old + 1);
                                    task.Dispose();
                                });
                        }
                    }));
                }

                await Task.WhenAll(workerTasks);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            finally
            {
                foreach (var countForStatus in countsByStatus)
                {
                    Console.WriteLine(countForStatus.Key + " " + countForStatus.Value);
                }
                client.Dispose();
            }

            return countsByStatus.SingleOrDefault(x => x.Key == HttpStatusCode.Created).Value;
        }

        // <Model>
        private class MyDocument
        {
            public string id { get; set; }

            public string pk { get; set; }

            public string other { get; set; }
        }
        // </Model>

        private static async Task CleanupAsync()
        {
            if (Program.database != null)
            {
                await Program.database.DeleteAsync();
            }
        }

        private static async Task InitializeAsync(CosmosClient client, int throughput)
        {
            Program.database = await client.CreateDatabaseIfNotExistsAsync(Program.databaseId);

            // Delete the existing container to prevent create item conflicts
            using (await database.GetContainer(containerId).DeleteContainerStreamAsync())
            { }

            // We create a partitioned collection here which needs a partition key. Partitioned collections
            // can be created with very high values of provisioned throughput and used to store 100's of GBs of data. 
            Console.WriteLine($"The demo will create a {throughput} RU/s container, press any key to continue.");
            Console.ReadKey();

            // Create with a throughput of 20000 RU/s - this demo is about throughput so it needs a higher degree of RU/s to show volume
            // Indexing Policy to exclude all attributes to maximize RU/s usage
            await database.DefineContainer(containerId, "/pk")
                    .WithIndexingPolicy()
                        .WithIndexingMode(IndexingMode.Consistent)
                        .WithIncludedPaths()
                            .Attach()
                        .WithExcludedPaths()
                            .Path("/*")
                            .Attach()
                    .Attach()
                .CreateAsync(throughput);
        }

        private class DataSource
        {
            private int docSize;
            private MemoryStreamPool pool;
            private byte[] sample;
            private int idIndex = -1, pkIndex = -1;
            private static List<Stack<KeyValuePair<PartitionKey, MemoryStream>>> documentsToImportInBatch;

            public DataSource(int initialPoolSize, int docSize, int workerCount)
            {
                this.pool = new MemoryStreamPool(initialPoolSize * workerCount, docSize);
                this.docSize = docSize;
                documentsToImportInBatch = new List<Stack<KeyValuePair<PartitionKey, MemoryStream>>>();
                Console.WriteLine("Creating a stack pool of " + initialPoolSize * workerCount + " items");
                for (int i = 0; i < workerCount; i++)
                {
                    Stack<KeyValuePair<PartitionKey, MemoryStream>> stk = new Stack<KeyValuePair<PartitionKey, MemoryStream>>();
                    for (int j = 0; j < initialPoolSize; j++)
                    {
                        MemoryStream value = CreateNextItemStream(out PartitionKey partitionKeyValue);
                        stk.Push(new KeyValuePair<PartitionKey, MemoryStream>(partitionKeyValue, value));
                    }
                    documentsToImportInBatch.Add(stk);
                }

                Console.WriteLine("Created a stack pool of " + documentsToImportInBatch.Count + " items");
            }

            public MemoryStream CreateNextItemStream(out PartitionKey partitionKey)
            {
                // Actual implementation would possibly read from a file or other source.

                string partitionKeyValue = Guid.NewGuid().ToString();
                string id = Guid.NewGuid().ToString();

                if (this.sample == null)
                {
                    // Leave about 100 bytes for pk, id and 200 bytes for system properties.
                    string padding = this.docSize > 300 ? new string('x', this.docSize - 300) : string.Empty;
                    MyDocument myDocument = new MyDocument() { id = id, pk = partitionKeyValue, other = padding };
                    string str = JsonConvert.SerializeObject(myDocument);
                    this.sample = Encoding.UTF8.GetBytes(str);
                    this.idIndex = str.IndexOf(id);
                    this.pkIndex = str.IndexOf(partitionKeyValue);
                }

                MemoryStream stream = this.pool.Take();
                byte[] buffer = stream.GetBuffer();
                if (buffer[0] == '\0')
                {
                    sample.CopyTo(buffer.AsSpan());
                    stream.SetLength(sample.Length);
                }

                Memory<byte> mem = buffer.AsMemory().Slice(this.idIndex, id.Length);
                Encoding.UTF8.GetBytes(id).CopyTo(mem);

                mem = buffer.AsMemory().Slice(this.pkIndex, partitionKeyValue.Length);
                Encoding.UTF8.GetBytes(partitionKeyValue).CopyTo(mem);

                stream.Position = 0;
                partitionKey = new PartitionKey(partitionKeyValue);
                return stream;
            }

            public MemoryStream GetNextItemStream(out PartitionKey partitionKeyValue, int i)
            {
                if (documentsToImportInBatch[i].Count > 0)
                {
                    var pair = documentsToImportInBatch[i].Pop();
                    partitionKeyValue = pair.Key;
                    return pair.Value;
                }
                else
                {
                    var value = CreateNextItemStream(out PartitionKey pkValue);
                    partitionKeyValue = pkValue;
                    return value;
                }

            }

            public Stack<KeyValuePair<PartitionKey, MemoryStream>> GetStack(int i)
            {
                return documentsToImportInBatch[i];

            }

            public void DoneWithItemStream(MemoryStream stream)
            {
                this.pool.Return(stream);
            }
        }

        /// <summary>
        /// Simplistic implementation of a pool for MemoryStream to avoid repeated allocations.
        /// Expects that the requirement is for MemoryStreams all with fixed and similar buffer size,
        /// and that the buffer is publicly visible.
        /// </summary>
        private class MemoryStreamPool
        {
            private int streamSize;

            private ConcurrentBag<MemoryStream> freeStreams;


            public MemoryStreamPool(int initialPoolSize, int bufferSize)
            {
                this.streamSize = bufferSize;
                this.freeStreams = new ConcurrentBag<MemoryStream>();

                for (int i = 0; i < initialPoolSize; i++)
                {
                    this.freeStreams.Add(this.GetNewStream());
                }
            }

            public MemoryStream Take()
            {
                if (this.freeStreams.TryTake(out MemoryStream stream))
                {
                    return stream;
                }

                return this.GetNewStream();
            }

            public void Return(MemoryStream stream)
            {
                this.freeStreams.Add(stream);
            }

            private MemoryStream GetNewStream()
            {
                byte[] buffer = new byte[this.streamSize];
                buffer[this.streamSize - 1] = 0;
                return new MemoryStream(buffer, index: 0, count: streamSize, writable: true, publiclyVisible: true);
            }
        }
    }
}

