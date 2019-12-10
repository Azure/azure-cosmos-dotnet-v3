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
        private static string containerId = "bulk-support";
        private static readonly JsonSerializer Serializer = new JsonSerializer();

        private static Database database = null;

        // Async main requires c# 7.1 which is set in the csproj with the LangVersion attribute
        // <Main>
        public static void Main(string[] args)
        {
            try
            {
                // Make sure this is >= 2 * physical_partition_count * (2MB / docSize) when useBulk is true
                // for best perf.
                int concurrency = args.Length > 0 ? int.Parse(args[0]) : 20000;
                int docSize = args.Length > 1 ? int.Parse(args[1]) : 1024;
                int runtimeInSeconds = args.Length > 2 ? int.Parse(args[2]) : 20;
                bool useBulk = args.Length > 3 ? bool.Parse(args[3]) : true;
                Program.containerId = args.Length > 4 ? args[4] : "bulk-support";
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

                Console.WriteLine("Running demo for container {0} with a {1} CosmosClient...", Program.containerId, useBulk ? "Bulk enabled " : string.Empty);
                Program.CreateItemsConcurrently(endpoint, authKey, useBulk, concurrency, docSize, runtimeInSeconds);
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

        private static void CreateItemsConcurrently(
            string endpoint,
            string authKey,
            bool useBulk,
            int concurrency,
            int docSize,
            int runtimeInSeconds)
        {
            Console.WriteLine($"Initiating creates of items of about {docSize} bytes maintaining {concurrency} in-progress items for {runtimeInSeconds} seconds.");
            DataSource dataSource = new DataSource(concurrency, docSize);

            CosmosClient client = Program.GetBulkClientInstance(endpoint, authKey, useBulk);
            Container container = client.GetContainer(Program.databaseId, Program.containerId);
            ConcurrentDictionary<HttpStatusCode, int> countsByStatus = new ConcurrentDictionary<HttpStatusCode, int>();

            Console.WriteLine("Starting job");
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(runtimeInSeconds * 1000);
            CancellationToken cancellationToken = cancellationTokenSource.Token;
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    MemoryStream stream = dataSource.GetNextDocItem(out PartitionKey partitionKeyValue);
                    _ = container.CreateItemStreamAsync(stream, partitionKeyValue, null, cancellationToken)
                        .ContinueWith((Task<ResponseMessage> task) =>
                        {
                            if (!task.IsCanceled)
                            {
                                if(stream != null) { stream.Dispose(); }
                                HttpStatusCode resultCode = task.Result.StatusCode;
                                countsByStatus.AddOrUpdate(resultCode, 1, (_, old) => old + 1);
                                if (task.Result != null) { task.Result.Dispose(); }
                                task.Dispose();
                            }
                        });
                }
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

            int created = countsByStatus.SingleOrDefault(x => x.Key == HttpStatusCode.Created).Value;
            Console.WriteLine($"Inserted {created} items.");
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
            private static Stack<KeyValuePair<PartitionKey, MemoryStream>> documentsToImportInBatch;

            public DataSource(int initialPoolSize, int docSize)
            {
                this.docSize = docSize;
                documentsToImportInBatch = new Stack<KeyValuePair<PartitionKey, MemoryStream>>();
               
                Stack<KeyValuePair<PartitionKey, MemoryStream>> stk = new Stack<KeyValuePair<PartitionKey, MemoryStream>>();
                for (int j = 0; j < initialPoolSize; j++)
                {
                    MemoryStream value = CreateNextDocItem(out PartitionKey partitionKeyValue);
                    documentsToImportInBatch.Push(new KeyValuePair<PartitionKey, MemoryStream>(partitionKeyValue, value));
                }
            }

            private MemoryStream CreateNextDocItem(out PartitionKey partitionKeyValue)
            {
                string partitionKey = Guid.NewGuid().ToString();
                string id = Guid.NewGuid().ToString();
                string padding = docSize > 300 ? new string('x', docSize - 300) : string.Empty;
                MyDocument myDocument = new MyDocument() { id = id, pk = partitionKey, other = padding };
                string value = JsonConvert.SerializeObject(myDocument);
                partitionKeyValue = new PartitionKey(partitionKey);

                return new MemoryStream(Encoding.UTF8.GetBytes(value ?? "")); ;
            }

            public MemoryStream GetNextDocItem(out PartitionKey partitionKeyValue)
            {
                if (documentsToImportInBatch.Count > 0)
                {
                    var pair = documentsToImportInBatch.Pop();
                    partitionKeyValue = pair.Key;
                    return pair.Value;
                }
                else
                {
                    var value = CreateNextDocItem(out PartitionKey pkValue);
                    partitionKeyValue = pkValue;
                    return value;
                }
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

