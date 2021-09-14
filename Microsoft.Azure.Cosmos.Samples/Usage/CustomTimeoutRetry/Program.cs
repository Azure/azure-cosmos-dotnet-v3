namespace Cosmos.Samples.CustomTimeoutRetry
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
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
    //
    // Sample - demonstrates custom timeout and retry behavior for SDK operations
    //
    // 1. turn off default 429 (Too Many Requests) retry behavior 
    //
    // 2. customize timeout and number of retries for each Cosmos DB request
    //
    // 3. graceful cancellation + cleanup of timed-out requests
    //
    // 4. short-circuit of additional retries if able to harvest results or errors from timed-out prior attempts
    //
    // 5. retry for common error codes + honor RetryAfter response headers (if present)
    //      https://docs.microsoft.com/en-us/azure/cosmos-db/sql/troubleshoot-dot-net-sdk?tabs=diagnostics-v3#common-error-status-codes-
    //
    // 6. provide hooks for harvesting CosmosDiagnostics from Response<T>, whenever available
    //      https://docs.microsoft.com/en-us/azure/cosmos-db/sql/troubleshoot-dot-net-sdk-slow-request#capture-diagnostics
    //
    //-----------------------------------------------------------------------------------------------------------

    public class Program
    {
        private const string DatabaseName = "customtimeoutretry";
        private const string ContainerName = "items";

        private const int ItemsToInsert = 100000;
        private const int Retries = 3;

        private static readonly TimeSpan PointReadTimeout = TimeSpan.FromMilliseconds(100);
        private static readonly TimeSpan QueryTimeout = TimeSpan.FromMilliseconds(500);

        private static readonly QueryDefinition QueryDefinition = new QueryDefinition("SELECT * FROM items i WHERE i.State = 'UT'");
        private static readonly int QueryBatchSize = 500;

        static async Task Main()
        {
            Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));

            try
            {
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

                Console.WriteLine("Ensure test data exists.");

                await BulkInsert(endpoint, authKey);

                // TODO: other settings needed to disable default retry behavior?
                CosmosClientOptions options = new CosmosClientOptions
                {
                    MaxRetryAttemptsOnRateLimitedRequests = 0   // we'll control these ourselves instead of relying on SDK defaults
                };

                using CosmosClient cosmosClient = new CosmosClient(endpoint, authKey, options);

                Database database = cosmosClient.GetDatabase(Program.DatabaseName);

                Container container = database.GetContainer(Program.ContainerName);

                Console.WriteLine();
                Console.WriteLine("Demonstrate retryable, paged queries.");
                Console.WriteLine();

                (string id, string pk) pointReadInfo = await Query(container);

                Console.WriteLine();
                Console.WriteLine("Demonstrate retryable point reads.");
                Console.WriteLine();

                await PointRead(pointReadInfo, container);
            }
            finally
            {
                Console.WriteLine();
                Console.WriteLine("End of demo, press any key to exit.");
                Console.WriteLine();
                Console.ReadKey();
            }
        }

        private static async Task PointRead((string id, string pk) pointReadInfo, Container container)
        {
            // define retryable operation
            async Task<Response<Item>> DoPointRead(CancellationToken ct)
            {
                return await container.ReadItemAsync<Item>(pointReadInfo.id, new PartitionKey(pointReadInfo.pk), null, ct);
            }

            // invoke operation
            Response<Item> response = await Retryable.ExecuteAsync(DoPointRead, Program.PointReadTimeout, Retries, OnDiagnostics);

            // $$$$$
            Console.WriteLine($"{response.Resource.State} -> {response.Resource.FirstName} {response.Resource.LastName}");
        }

        private static async Task<(string id, string pk)> Query(Container container)
        {
            string? continuationToken = null;

            // define retryable operation
            async Task<Response<IEnumerable<Item>>> DoQuery(CancellationToken ct)
            {
                // TODO:
                // careful observers will note here we create a new iterator for each page of query results, which is not the ideal pattern
                //  the issue with the ideal pattern is that it conflicts with our retry logic
                //
                //  - retry logic uses cooperative cancellation (via CancellationToken) and when canceling a request, Cosmos SDK will sometimes still get a valid value
                //  - the retry logic tracks prior (timed out) query attempts and uses any results from those before trying new attempts (if results happen to appear after cancellation is requested)
                //  - the iterator is stateful, and each call to ReadNextAsync() modifies the iterator state
                //  - a prior attempt (call to ReadNextAsync()) which timed out but eventually receives results and mutates the iterator will be in conflict with
                //     another (current) attempt (call to ReadNextAsync()) which also mutates the iterator. This results in an error.
                //
                //  the solution to the above is to use a separate iterator instance for each retry... not as efficient as using a single iterator, but as long
                //   as you honor the continuation token, you get all query result pages
                //
                //  other options don't seem much fun either:
                //
                //  1. define the full query drain as the retryable operation (get all pages within scope of atomic retryable operation), and buffer all results of all pages back to caller (please, no)
                //  2. same as last, but push an Action delegate to act on each page of results as they arrive... the problem here is that
                //      in the face of retries you have to restart at first page which you may have already processed on the last pass (before you eventually timed out while processing page N)
                //
                //  so what i've got here seems "least bad" but curious if others see better options?
                //

                QueryRequestOptions options = new QueryRequestOptions { MaxItemCount = Program.QueryBatchSize };

                using FeedIterator<Item> iterator = container.GetItemQueryIterator<Item>(Program.QueryDefinition, continuationToken, options);

                return await iterator.ReadNextAsync(ct);
            }

            (string id, string pk) result = default;

            // invoke operation
            while (true)
            {
                // get a page of results
                FeedResponse<Item> response = (FeedResponse<Item>) await Retryable.ExecuteAsync(DoQuery, Program.QueryTimeout, Retries, OnDiagnostics);

                // party on, wayne
                foreach (Item item in response)
                {
                    Console.WriteLine($"{item.FirstName} {item.LastName}");
                    result = (item.Id, item.State);
                }

                // if we're done, we're done...
                if (string.IsNullOrWhiteSpace(response.ContinuationToken))
                {
                    break;
                }

                // ...if we're not, we're not
                continuationToken = response.ContinuationToken;
            }

            return result;
        }

        private static void OnDiagnostics(CosmosDiagnostics diagnostics, Func<TimeSpan, bool> filter)
        {
            if (diagnostics != null && filter(diagnostics.GetClientElapsedTime()))
            {
                Debug.WriteLine(diagnostics.ToString());    // write to log, etc.
            }
        }

        private static async Task BulkInsert(string endpoint, string authKey)
        {
            // using bulk-optimized client here for one-off insert operations

            CosmosClientOptions options = new CosmosClientOptions
            {
                AllowBulkExecution = true
            };

            using CosmosClient cosmosClient = new CosmosClient(endpoint, authKey, options);

            Database database = await cosmosClient.CreateDatabaseIfNotExistsAsync(Program.DatabaseName);

            try
            {
                Container container = await database.CreateContainerAsync(Program.ContainerName, "/State", 10000);

                IReadOnlyCollection<Item> itemsToInsert = Program.GetItemsToInsert();

                Task[] tasks = itemsToInsert.Select(item => container.CreateItemAsync(item, new PartitionKey(item.State))
                        .ContinueWith(itemResponse =>
                        {
                            if (!itemResponse.IsCompletedSuccessfully)
                            {
                                Debug.Assert(itemResponse.Exception != null);

                                AggregateException innerException = itemResponse.Exception.Flatten();

                                if (innerException.InnerExceptions.FirstOrDefault(innerEx => innerEx is CosmosException) is CosmosException cosmosException)
                                {
                                    Debug.WriteLine($"Received {cosmosException.StatusCode} ({cosmosException.Message}).");
                                }
                                else
                                {
                                    Debug.WriteLine($"Exception {innerException.InnerExceptions.FirstOrDefault()}.");
                                }
                            }
                        })).ToArray();

                await Task.WhenAll(tasks);

                Debug.WriteLine("Bulk insert complete.");
            }
            catch (CosmosException ex)
            {
                if (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    return; // container already exists
                }
            }
        }

        private static IReadOnlyCollection<Item> GetItemsToInsert()
        {
            return new Bogus.Faker<Item>()
                .StrictMode(true)
                .RuleFor(o => o.Id, f => Guid.NewGuid().ToString())
                .RuleFor(o => o.FirstName, f => f.Name.FirstName())
                .RuleFor(o => o.LastName, f => f.Name.LastName())
                .RuleFor(o => o.State, (f, o) => f.Address.StateAbbr())
                .RuleFor(o => o.Address, (f, o) => $"{f.Address.StreetAddress()} {f.Address.City()}, {o.State} {f.Address.ZipCode()}")
                .RuleFor(o => o.Email, (f, o) => f.Internet.Email(firstName: o.FirstName, lastName: o.LastName))
                .Generate(ItemsToInsert);
        }
    }

    public class Item
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}
