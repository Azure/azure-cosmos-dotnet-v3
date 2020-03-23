namespace Cosmos.Samples.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Configuration;

    // ----------------------------------------------------------------------------------------------------------
    // Prerequisites - 
    // 
    // 1. An Azure Cosmos account - 
    //    https://docs.microsoft.com/azure/cosmos-db/create-cosmosdb-resources-portal
    //
    // 2. Microsoft.Azure.Cosmos NuGet package - 
    //    http://www.nuget.org/packages/Microsoft.Azure.Cosmos/ 
    // ----------------------------------------------------------------------------------------------------------
    // Sample - demostrates how to migrate from Bulk Executor Library to V3 SDK with Bulk support
    // ----------------------------------------------------------------------------------------------------------

    public class Program
    {
        private const int DocumentsToInsert = 1000;
        private const string DatabaseName = "bulkMigration";
        private const string ContainerName = "bulkMigration";
        private static CosmosClient client;
        private static Database database = null;

        public static async Task Main(string[] args)
        {
            try
            {
                // Intialize container or create a new container.
                Container container = await Program.Initialize();

                List<MyDocument> documentsToWorkWith = new List<MyDocument>(100);
                for (int i = 0; i < DocumentsToInsert; i++)
                {
                    documentsToWorkWith.Add(
                        new MyDocument()
                        {
                            id = Guid.NewGuid().ToString(),
                            pk = Guid.NewGuid().ToString()
                        });
                }

                // Bulk import
                await Program.CreateItemsConcurrentlyAsync(container, documentsToWorkWith);

                // Bulk update
                await Program.UpdateItemsConcurrentlyAsync(container, documentsToWorkWith);

                // Bulk delete
                await Program.DeleteItemsConcurrentlyAsync(container, documentsToWorkWith);
            }
            finally
            {
                await Program.CleanupAsync();
                client.Dispose();

                Console.WriteLine("End of demo, press any key to exit.");
                Console.ReadKey();
            }
        }

        // <Model>
        public class MyDocument
        {
            public string id { get; set; }

            public string pk { get; set; }

            public int operationCounter { get; set; } = 0;
        }
        // </Model>

        private static CosmosClient GetBulkClientInstance(
            string endpoint,
            string authKey) =>
        // </Initialization>
            new CosmosClient(endpoint, authKey, new CosmosClientOptions() { AllowBulkExecution = true });
        // </Initialization>

        public static async Task CreateItemsConcurrentlyAsync(
            Container container,
            IReadOnlyList<MyDocument> documentsToWorkWith)
        {
            // <BulkImport>
            List<Task<OperationResponse<MyDocument>>> operations = new List<Task<OperationResponse<MyDocument>>>(documentsToWorkWith.Count);
            foreach (MyDocument document in documentsToWorkWith)
            {
                operations.Add(container.CreateItemAsync(document, new PartitionKey(document.pk)).CaptureOperationResponse(document));
            }
            // </BulkImport>

            BulkOperationResponse<MyDocument> bulkOperationResponse = await ExecuteTasksAsync(operations);
            Console.WriteLine($"Bulk create operation finished in {bulkOperationResponse.TotalTimeTaken}");
            Console.WriteLine($"Consumed {bulkOperationResponse.TotalRequestUnitsConsumed} RUs in total");
            Console.WriteLine($"Created {bulkOperationResponse.SuccessfulDocuments} documents");
            Console.WriteLine($"Failed {bulkOperationResponse.Failures.Count} documents");
            if (bulkOperationResponse.Failures.Count > 0)
            {
                Console.WriteLine($"First failed sample document {bulkOperationResponse.Failures[0].Item1.id} - {bulkOperationResponse.Failures[0].Item2}");
            }
        }

        public static async Task UpdateItemsConcurrentlyAsync(
            Container container,
            IReadOnlyList<MyDocument> documentsToWorkWith)
        {
            // <BulkUpdate>
            List<Task<OperationResponse<MyDocument>>> operations = new List<Task<OperationResponse<MyDocument>>>(documentsToWorkWith.Count);
            foreach (MyDocument document in documentsToWorkWith)
            {
                document.operationCounter++;
                operations.Add(container.ReplaceItemAsync(document, document.id, new PartitionKey(document.pk)).CaptureOperationResponse(document));
            }
            // </BulkUpdate>

            BulkOperationResponse<MyDocument> bulkOperationResponse = await ExecuteTasksAsync(operations);
            Console.WriteLine($"Bulk update operation finished in {bulkOperationResponse.TotalTimeTaken}");
            Console.WriteLine($"Consumed {bulkOperationResponse.TotalRequestUnitsConsumed} RUs in total");
            Console.WriteLine($"Created {bulkOperationResponse.SuccessfulDocuments} documents");
            Console.WriteLine($"Failed {bulkOperationResponse.Failures.Count} documents");
            if (bulkOperationResponse.Failures.Count > 0)
            {
                Console.WriteLine($"First failed sample document {bulkOperationResponse.Failures[0].Item1.id} - {bulkOperationResponse.Failures[0].Item2}");
            }
        }

        public static async Task DeleteItemsConcurrentlyAsync(
            Container container,
            IReadOnlyList<MyDocument> documentsToWorkWith)
        {
            // <BulkDelete>
            List<Task<OperationResponse<MyDocument>>> operations = new List<Task<OperationResponse<MyDocument>>>(documentsToWorkWith.Count);
            foreach (MyDocument document in documentsToWorkWith)
            {
                document.operationCounter++;
                operations.Add(container.DeleteItemAsync<MyDocument>(document.id, new PartitionKey(document.pk)).CaptureOperationResponse(document));
            }
            // </BulkDelete>

            BulkOperationResponse<MyDocument> bulkOperationResponse = await ExecuteTasksAsync(operations);
            Console.WriteLine($"Bulk update operation finished in {bulkOperationResponse.TotalTimeTaken}");
            Console.WriteLine($"Consumed {bulkOperationResponse.TotalRequestUnitsConsumed} RUs in total");
            Console.WriteLine($"Created {bulkOperationResponse.SuccessfulDocuments} documents");
            Console.WriteLine($"Failed {bulkOperationResponse.Failures.Count} documents");
            if (bulkOperationResponse.Failures.Count > 0)
            {
                Console.WriteLine($"First failed sample document {bulkOperationResponse.Failures[0].Item1.id} - {bulkOperationResponse.Failures[0].Item2}");
            }
        }

        private static async Task<BulkOperationResponse<T>> ExecuteTasksAsync<T>(IReadOnlyList<Task<OperationResponse<T>>> tasks)
        {
            // <WhenAll>
            Stopwatch stopwatch = Stopwatch.StartNew();
            await Task.WhenAll(tasks);
            stopwatch.Stop();

            return new BulkOperationResponse<T>()
            {
                TotalTimeTaken = stopwatch.Elapsed,
                TotalRequestUnitsConsumed = tasks.Sum(task => task.Result.RequestUnitsConsumed),
                SuccessfulDocuments = tasks.Count(task => task.Result.IsSuccessful),
                Failures = tasks.Where(task => !task.Result.IsSuccessful).Select(task => (task.Result.Item, task.Result.CosmosException)).ToList()
            };
            // </WhenAll>
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

            Program.client = GetBulkClientInstance(endpointUrl, authKey);
            Program.database = client.GetDatabase(DatabaseName);
            Container container = await CreateFreshContainerAsync(client, DatabaseName, ContainerName, 10000);

            try
            {
                await container.ReadContainerAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in reading collection: {0}", ex.Message);
                throw ex;
            }

            Console.WriteLine("Running migration demo for container {0} with a Bulk enabled CosmosClient.", ContainerName);

            return container;
        }

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
            int throughput)
        {
            Program.database = await client.CreateDatabaseIfNotExistsAsync(databaseName);

            // We create a partitioned collection here which needs a partition key. Partitioned collections
            // can be created with very high values of provisioned throughput and used to store 100's of GBs of data. 
            Console.WriteLine($"The demo will create a container, press any key to continue.");
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
                .CreateIfNotExistsAsync(throughput);

            return container;
        }
    }

    // </ResponseType>
    public class BulkOperationResponse<T>
    {
        public TimeSpan TotalTimeTaken { get; set; }
        public int SuccessfulDocuments { get; set; } = 0;
        public double TotalRequestUnitsConsumed { get; set; } = 0;

        public IReadOnlyList<(T, Exception)> Failures { get; set; }
    }
    // </ResponseType>

    // <OperationResult>
    public class OperationResponse<T>
    {
        public T Item { get; set; }
        public double RequestUnitsConsumed { get; set; } = 0;
        public bool IsSuccessful { get; set; }
        public Exception CosmosException { get; set; }
    }
    // </OperationResult>

    public static class TaskExtensions
    {
        // <CaptureOperationResult>
        public static Task<OperationResponse<T>> CaptureOperationResponse<T>(this Task<ItemResponse<T>> task, T item)
        {
            return task.ContinueWith(itemResponse =>
            {
                if (itemResponse.IsCompletedSuccessfully)
                {
                    return new OperationResponse<T>()
                    {
                        Item = item,
                        IsSuccessful = true,
                        RequestUnitsConsumed = task.Result.RequestCharge
                    };
                }

                AggregateException innerExceptions = itemResponse.Exception.Flatten();
                CosmosException cosmosException = innerExceptions.InnerExceptions.FirstOrDefault(innerEx => innerEx is CosmosException) as CosmosException;
                if (cosmosException != null)
                {
                    return new OperationResponse<T>()
                    {
                        Item = item,
                        RequestUnitsConsumed = cosmosException.RequestCharge,
                        IsSuccessful = false,
                        CosmosException = cosmosException
                    };
                }

                return new OperationResponse<T>()
                {
                    Item = item,
                    IsSuccessful = false,
                    CosmosException = innerExceptions.InnerExceptions.FirstOrDefault()
                };
            });
        }
        // </CaptureOperationResult>
    }
}

