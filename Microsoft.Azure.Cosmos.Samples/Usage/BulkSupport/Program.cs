namespace Cosmos.Samples.Shared
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Runtime;
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
                int maxDocs = args.Length > 0 ? int.Parse(args[0]) : 1000000;
                int docSize = args.Length > 1 ? int.Parse(args[1]) : 540;
                int runtimeInSeconds = args.Length > 2 ? int.Parse(args[2]) : 30;
                bool useBulk = args.Length > 3 ? bool.Parse(args[3]) : true;
                int workerCount = args.Length > 4 ? int.Parse(args[4]) : 1;

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

                CosmosClient bulkClient = Program.GetBulkClientInstance(endpoint, authKey, useBulk);
                // Create the require container, can be done with any client
                //await Program.InitializeAsync(bulkClient, throughput: 6000);

                Console.WriteLine("Running demo with a {0}CosmosClient...", useBulk ? "Bulk enabled " : string.Empty);
                // Execute inserts for 30 seconds on a Bulk enabled client
                await Program.CreateItemsConcurrentlyAsync(bulkClient, maxDocs, docSize, runtimeInSeconds, workerCount);

                bulkClient.Dispose();
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
            new CosmosClientBuilder(endpoint, authKey).WithBulkExecution(useBulk).Build();
        // </Initialization>

        private static async Task CreateItemsConcurrentlyAsync(
            CosmosClient client, 
            int maxDocs, 
            int docSize, 
            int runtimeInSeconds,
            int workerCount)
        {
            DataSource dataSource = new DataSource(maxDocs, docSize);

            Container container = client.GetContainer(Program.databaseId, Program.containerId);
            Console.WriteLine($"Initiating creates of up to {maxDocs} items of about {docSize} bytes each using {workerCount} workers for {runtimeInSeconds} seconds.");
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(runtimeInSeconds * 1000);
            CancellationToken cancellationToken = cancellationTokenSource.Token;
            int created = await CreateItemsAsync(container, dataSource, docSize, workerCount, cancellationToken);
            Console.WriteLine($"Inserted {created} items.");
        }

        private static async Task<int> CreateItemsAsync(
            Container container,
            DataSource dataSource,
            int docSize,
            int workerCount,
            CancellationToken cancellationToken)
        {
            ConcurrentDictionary<HttpStatusCode, int> countsByStatus = new ConcurrentDictionary<HttpStatusCode, int>();

            try
            {
                List<Task> workerTasks = new List<Task>();
                for (int i = 0; i < workerCount; i++)
                {
                    workerTasks.Add(Task.Run(() =>
                    {
                        while (!cancellationToken.IsCancellationRequested)
                        {
                            MemoryStream stream = dataSource.GetNextItemStream(out string partitionKeyValue);
                            _ = container.CreateItemStreamAsync(stream, new PartitionKey(partitionKeyValue))
                                .ContinueWith((Task<ResponseMessage> task) =>
                                {
                                    dataSource.DoneWithItemStream(stream);
                                    HttpStatusCode resultCode = task.Result.StatusCode;
                                    if (task.Result.Content != null) { task.Result.Content.Dispose(); }
                                    task.Dispose();
                                    countsByStatus.AddOrUpdate(resultCode, 1, (_, old) => old + 1);
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

            public DataSource(int initialPoolSize, int docSize)
            {
                this.pool = new MemoryStreamPool(initialPoolSize, docSize);
                this.docSize = docSize;
            }

            public MemoryStream GetNextItemStream(out string partitionKeyValue)
            {
                // Actual implementation would possibly read from a file or other source.

                partitionKeyValue = Guid.NewGuid().ToString();
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
                if(buffer[0] == '\0')
                {
                    sample.CopyTo(buffer.AsSpan());
                    stream.SetLength(sample.Length);
                }

                Memory<byte> mem = buffer.AsMemory().Slice(this.idIndex, id.Length);
                Encoding.UTF8.GetBytes(id).CopyTo(mem);

                mem = buffer.AsMemory().Slice(this.pkIndex, partitionKeyValue.Length);
                Encoding.UTF8.GetBytes(partitionKeyValue).CopyTo(mem);

                stream.Position = 0;
                return stream;
            }

            public void DoneWithItemStream(MemoryStream stream)
            {
                this.pool.Return(stream);
            }
        }

        /// <summary>
        /// MemoryStreams cannot be used after they are disposed.
        /// However, disposal is not a requirement for MemoryStreams.
        /// Use a derived implementation to allow re-use since the SDK
        /// currently disposes the input streams while disposing the response.
        /// </summary>
        private class PoolableMemoryStream : MemoryStream
        {
           public PoolableMemoryStream(byte[] buffer, int index, int count, bool writable, bool publiclyVisible)
                : base(buffer, index, count, writable, publiclyVisible)
            {
            }

            protected override void Dispose(bool disposing)
            {
                // Don't invoke dispose on base. MemoryStream need not be disposed.
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

                for(int i = 0; i< initialPoolSize;i++)
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

                // Ensure allocation
                buffer[this.streamSize - 1] = 0;

                return new PoolableMemoryStream(buffer, index: 0, count: streamSize, writable: true, publiclyVisible: true);
            }
        }
    }
}

