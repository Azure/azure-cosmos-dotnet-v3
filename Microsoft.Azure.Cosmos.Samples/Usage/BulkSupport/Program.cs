namespace Cosmos.Samples.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
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
        private const int concurrentWorkers = 3;
        private const int concurrentDocuments = 100;
        private const string databaseId = "samples";
        private const string containerId = "bulk-support";
        private static readonly JsonSerializer Serializer = new JsonSerializer();

        //Reusable instance of ItemClient which represents the connection to a Cosmos endpoint
        private static Database database = null;

        // Async main requires c# 7.1 which is set in the csproj with the LangVersion attribute
        // <Main>
        public static async Task Main(string[] args)
        {
            try
            {
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

                CosmosClient bulkClient = Program.GetBulkClientInstance(endpoint, authKey);
                // Create the require container, can be done with any client
                await Program.InitializeAsync(bulkClient);

                Console.WriteLine("Running demo with a Bulk enabled CosmosClient...");
                // Execute inserts for 30 seconds on a Bulk enabled client
                await Program.CreateItemsConcurrentlyAsync(bulkClient);
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
                await Program.CleanupAsync();
                Console.WriteLine("End of demo, press any key to exit.");
                Console.ReadKey();
            }
        }
        // </Main>

        private static CosmosClient GetBulkClientInstance(
            string endpoint,
            string authKey) =>
        // </Initialization>
            new CosmosClient(endpoint, authKey, new CosmosClientOptions() { AllowBulkExecution = true } );
        // </Initialization>

        private static async Task CreateItemsConcurrentlyAsync(CosmosClient client)
        {
            // Create concurrent workers that will insert items for 30 seconds
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(30000);
            CancellationToken cancellationToken = cancellationTokenSource.Token;

            Container container = client.GetContainer(Program.databaseId, Program.containerId);
            List<Task<int>> workerTasks = new List<Task<int>>(Program.concurrentWorkers);
            Console.WriteLine($"Initiating process with {Program.concurrentWorkers} worker threads writing groups of {Program.concurrentDocuments} items for 30 seconds.");
            for (var i = 0; i < Program.concurrentWorkers; i++)
            {
                workerTasks.Add(CreateItemsAsync(container, cancellationToken));
            }

            await Task.WhenAll(workerTasks);
            Console.WriteLine($"Inserted {workerTasks.Sum(task => task.Result)} items.");
        }

        private static async Task<int> CreateItemsAsync(
            Container container, 
            CancellationToken cancellationToken)
        {
            int itemsCreated = 0;
            string partitionKeyValue = Guid.NewGuid().ToString();
            while (!cancellationToken.IsCancellationRequested)
            {
                List<Task> tasks = new List<Task>(Program.concurrentDocuments);
                for (int i = 0; i < Program.concurrentDocuments; i++)
                {
                    string id = Guid.NewGuid().ToString();
                    MyDocument myDocument = new MyDocument() { id = id, pk = partitionKeyValue };
                    tasks.Add(
                        container.CreateItemAsync<MyDocument>(myDocument, new PartitionKey(partitionKeyValue))
                        .ContinueWith((Task<ItemResponse<MyDocument>> task) =>
                        {
                            if (!task.IsCompletedSuccessfully)
                            {
                                AggregateException innerExceptions = task.Exception.Flatten();
                                CosmosException cosmosException = innerExceptions.InnerExceptions.FirstOrDefault(innerEx => innerEx is CosmosException) as CosmosException;
                                Console.WriteLine($"Item {myDocument.id} failed with status code {cosmosException.StatusCode}");
                            }
                        }));
                }

                await Task.WhenAll(tasks);

                itemsCreated += tasks.Count(task => task.IsCompletedSuccessfully);
            }
            
            return itemsCreated;
        }

        // <Model>
        private class MyDocument
        {
            public string id { get; set; }

            public string pk { get; set; }

            public bool Updated { get; set; }
        }
        // </Model>

        private static async Task CleanupAsync()
        {
            if (Program.database != null)
            {
                await Program.database.DeleteAsync();
            }
        }

        private static async Task InitializeAsync(CosmosClient client)
        {
            Program.database = await client.CreateDatabaseIfNotExistsAsync(Program.databaseId);

            // Delete the existing container to prevent create item conflicts
            using (await database.GetContainer(containerId).DeleteContainerStreamAsync())
            { }

            // We create a partitioned collection here which needs a partition key. Partitioned collections
            // can be created with very high values of provisioned throughput (up to Throughput = 250,000)
            // and used to store up to 250 GB of data. 
            Console.WriteLine("The demo will create a 20000 RU/s container, press any key to continue.");
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
                .CreateAsync(20000);
        }
    }
}

