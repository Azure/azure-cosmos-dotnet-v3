namespace Cosmos.Samples.Shared
{
    using System;
    using System.Globalization;
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
    // Sample - This sample project demonstrates how to customize and alter the index policy on a Container.
    //
    // 1. Exclude a document completely from the Index
    // 2. Use lazy (instead of consistent) indexing
    // 3. Exclude specified paths from document index
    // 4. Using range indexes
    // 5. Perform index transform
    // ----------------------------------------------------------------------------------------------------------
    // Note - 
    // 
    // Running this sample will create (and delete) multiple COntainer resources on your account. 
    // Each time a Container is created the account will be billed for 1 hour of usage based on
    // the performance tier of that account. 
    // ----------------------------------------------------------------------------------------------------------
    // See Also - 
    //
    // Cosmos.Samples.ContainerManagement - basic CRUD operations on a Container
    // ----------------------------------------------------------------------------------------------------------


    public class Program
    {
        //Read configuration
        private static readonly string databaseId = "samples";
        private static readonly string containerId = "index-samples";
        private static readonly string partitionKey = "/partitionKey";

        private static Database database = null;

        struct QueryStats
        {
            public QueryStats(int count, double requestCharge)
            {
                Count = count;
                RequestCharge = requestCharge;
            }

            public readonly int Count;
            public readonly double RequestCharge;
        };

        // Async main requires c# 7.1 which is set in the csproj with the LangVersion attribute
        // <Main>
        public static async Task Main(string[] args)
        {
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

                //Read the Cosmos endpointUrl and authorisationKeys from configuration
                //These values are available from the Azure Management Portal on the Cosmos Account Blade under "Keys"
                //NB > Keep these values in a safe & secure location. Together they provide Administrative access to your Cosmos account
                using (CosmosClient client = new CosmosClient(endpoint, authKey))
                {
                    await Program.RunIndexDemo(client);
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
                Console.WriteLine("End of demo, press any key to exit.");
                Console.ReadKey();
            }
        }
        // </Main>

        // <RunIndexDemo>
        private static async Task RunIndexDemo(CosmosClient client)
        {
            // Create the database if necessary
            await Program.Setup(client);

            // 1. Exclude a document from the index
            await Program.ExplicitlyExcludeFromIndex(client);

            // 2. Use lazy (instead of consistent) indexing
            await Program.UseLazyIndexing();

            // 3. Exclude specified document paths from the index
            await Program.ExcludePathsFromIndex();
        }
        // </RunIndexDemo>

        /// <summary>
        /// The default index policy on a Container will AUTOMATICALLY index ALL documents added.
        /// There may be scenarios where you want to exclude a specific doc from the index even though all other 
        /// documents are being indexed automatically. 
        /// This method demonstrates how to use an index directive to control this
        /// </summary>
        private static async Task ExplicitlyExcludeFromIndex(CosmosClient client)
        {
            string containerId = $"{Program.containerId}-ExplicitlyExcludeFromIndex";

            // Create a collection with default index policy(i.e.automatic = true)
            ContainerResponse response = await Program.database.CreateContainerAsync(containerId, Program.partitionKey);
            Console.WriteLine("Container {0} created with index policy \n{1}", containerId, JsonConvert.SerializeObject(response.Resource.IndexingPolicy));
            Container container = (Container)response;

            try
            {
                
                // Create a document
                // Then query on it immediately
                // Will work as this Collection is set to automatically index everything
                ItemResponse<dynamic> created = await container.CreateItemAsync<dynamic>(new { id = "doc1", partitionKey = "doc1", orderId = "order1" }, new PartitionKey("doc1"));
                Console.WriteLine("\nItem created: \n{0}", JsonConvert.SerializeObject(created.Resource));

                FeedIterator<dynamic> resultSetIterator = container.GetItemQueryIterator<dynamic>(new QueryDefinition("SELECT * FROM root r WHERE r.orderId='order1'"), requestOptions: new QueryRequestOptions { MaxItemCount = 1 });
                bool found = false;
                while (resultSetIterator.HasMoreResults)
                {
                    FeedResponse<dynamic> feedResponse = await resultSetIterator.ReadNextAsync();
                    found = feedResponse.Count > 0;
                }

                Console.WriteLine("Item found by query: {0}", found);

                // Now, create an item but this time explictly exclude it from the collection using IndexingDirective
                // Then query for that document
                // Shoud NOT find it, because we excluded it from the index
                // BUT, the document is there and doing a ReadItem by Id will prove it
                created = await container.CreateItemAsync<dynamic>(new { id = "doc2", partitionKey = "doc2", orderId = "order2" }, new PartitionKey("doc2"), new ItemRequestOptions
                {
                    IndexingDirective = IndexingDirective.Exclude
                });

                Console.WriteLine("\nItem created: \n{0}", JsonConvert.SerializeObject(created.Resource));

                resultSetIterator = container.GetItemQueryIterator<dynamic>(new QueryDefinition("SELECT * FROM root r WHERE r.orderId='order2'"), requestOptions: new QueryRequestOptions { MaxItemCount = 1 });
                found = false;
                while (resultSetIterator.HasMoreResults)
                {
                    FeedResponse<dynamic> feedResponse = await resultSetIterator.ReadNextAsync();
                    found = feedResponse.Count > 0;
                }
                Console.WriteLine("Item found by query: {0}", found);

                ItemResponse<dynamic> document = await container.ReadItemAsync<dynamic>((string)created.Resource.id, new PartitionKey("doc2"));
                Console.WriteLine("Item read by id: {0}", document != null);
            }
            finally
            {
                // Cleanup
                await container.DeleteContainerAsync();
            }
        }

        /// <summary>
        /// Azure Cosmos DB offers synchronous (consistent) and asynchronous (lazy) index updates. 
        /// By default, the index is updated synchronously on each insert, replace or delete of a document to the container. 
        /// There are times when you might want to configure certain containers to update their index asynchronously. 
        /// Lazy indexing boosts write performance and is ideal for bulk ingestion scenarios for primarily read-heavy collections
        /// It is important to note that you might get inconsistent reads whilst the writes are in progress,
        /// However once the write volume tapers off and the index catches up, then reads continue as normal
        /// 
        /// This method demonstrates how to switch IndexMode to Lazy.
        /// </summary>
        private static async Task UseLazyIndexing()
        {
            string containerId = $"{Program.containerId}-UseLazyIndexing";

            Console.WriteLine("\n2. Use lazy (instead of consistent) indexing");

            ContainerResponse response = await Program.database.CreateContainerAsync(new ContainerProperties(containerId, Program.partitionKey)
            {
                IndexingPolicy = new IndexingPolicy()
                {
                    IndexingMode = IndexingMode.Lazy
                }
            });

            Console.WriteLine("Container {0} created with index policy \n{1}", containerId, JsonConvert.SerializeObject(response.Resource.IndexingPolicy));
            Container container = (Container)response;

            // It is very difficult to demonstrate lazy indexing as you only notice the difference under sustained heavy write load
            // because we're using a small container in this demo we'd likely get throttled long before we were able to replicate sustained high throughput
            // which would give the index time to catch-up.

            await container.DeleteContainerAsync();
        }

        /// <summary>
        /// The default behavior is for DocumentDB to index every attribute in every document automatically.
        /// There are times when a document contains large amounts of information, in deeply nested structures
        /// that you know you will never search on. In extreme cases like this, you can exclude paths from the 
        /// index to save on storage cost, improve write performance and also improve read performance because the index is smaller
        ///
        /// This method demonstrates how to set IndexingPolicy.ExcludedPaths
        /// </summary>
        private static async Task ExcludePathsFromIndex()
        {
            string containerId = $"{Program.containerId}-ExcludePathsFromIndex";
            Console.WriteLine("\n3. Exclude specified paths from document index");

            ContainerProperties containerProperties = new ContainerProperties(containerId, Program.partitionKey);

            containerProperties.IndexingPolicy.IncludedPaths.Add(new IncludedPath { Path = "/*" });  // Special manadatory path of "/*" required to denote include entire tree
            containerProperties.IndexingPolicy.ExcludedPaths.Add(new ExcludedPath { Path = "/metaData/*" });   // exclude metaData node, and anything under it
            containerProperties.IndexingPolicy.ExcludedPaths.Add(new ExcludedPath { Path = "/subDoc/nonSearchable/*" });  // exclude ONLY a part of subDoc    
            containerProperties.IndexingPolicy.ExcludedPaths.Add(new ExcludedPath { Path = "/\"excludedNode\"/*" }); // exclude excludedNode node, and anything under it

            // The effect of the above IndexingPolicy is that only id, foo, and the subDoc/searchable are indexed

            ContainerResponse response = await Program.database.CreateContainerAsync(containerProperties);
            Console.WriteLine("Container {0} created with index policy \n{1}", containerId, JsonConvert.SerializeObject(response.Resource.IndexingPolicy));
            Container container = (Container)response;

            try
            {
                int numDocs = 250;
                Console.WriteLine("Creating {0} documents", numDocs);
                for (int docIndex = 0; docIndex < numDocs; docIndex++)
                {
                    dynamic dyn = new
                    {
                        id = "doc" + docIndex,
                        partitionKey = "doc" + docIndex,
                        foo = "bar" + docIndex,
                        metaData = "meta" + docIndex,
                        subDoc = new { searchable = "searchable" + docIndex, nonSearchable = "value" + docIndex },
                        excludedNode = new { subExcluded = "something" + docIndex, subExcludedNode = new { someProperty = "value" + docIndex } }
                    };
                    ItemResponse<dynamic> created = await container.CreateItemAsync<dynamic>(dyn, new PartitionKey("doc" + docIndex));
                    Console.WriteLine("Creating document with id {0}", created.Resource.id);
                }

                // Querying for a document on either metaData or /subDoc/subSubDoc/someProperty will be expensive since they do not utilize the index,
                // but instead are served from scan automatically.
                int queryDocId = numDocs / 2;
                QueryStats queryStats = await Program.GetQueryResult(container, string.Format(CultureInfo.InvariantCulture, "SELECT * FROM root r WHERE r.metaData='meta{0}'", queryDocId));
                Console.WriteLine("Query on metaData returned {0} results", queryStats.Count);
                Console.WriteLine("Query on metaData consumed {0} RUs", queryStats.RequestCharge);

                queryStats = await Program.GetQueryResult(container, string.Format(CultureInfo.InvariantCulture, "SELECT * FROM root r WHERE r.subDoc.nonSearchable='value{0}'", queryDocId));
                Console.WriteLine("Query on /subDoc/nonSearchable returned {0} results", queryStats.Count);
                Console.WriteLine("Query on /subDoc/nonSearchable consumed {0} RUs", queryStats.RequestCharge);

                queryStats = await Program.GetQueryResult(container, string.Format(CultureInfo.InvariantCulture, "SELECT * FROM root r WHERE r.excludedNode.subExcludedNode.someProperty='value{0}'", queryDocId));
                Console.WriteLine("Query on /excludedNode/subExcludedNode/someProperty returned {0} results", queryStats.Count);
                Console.WriteLine("Query on /excludedNode/subExcludedNode/someProperty cost {0} RUs", queryStats.RequestCharge);

                // Querying for a document using foo, or even subDoc/searchable > consume less RUs because they were not excluded
                queryStats = await Program.GetQueryResult(container, string.Format(CultureInfo.InvariantCulture, "SELECT * FROM root r WHERE r.foo='bar{0}'", queryDocId));
                Console.WriteLine("Query on /foo returned {0} results", queryStats.Count);
                Console.WriteLine("Query on /foo cost {0} RUs", queryStats.RequestCharge);

                queryStats = await Program.GetQueryResult(container, string.Format(CultureInfo.InvariantCulture, "SELECT * FROM root r WHERE r.subDoc.searchable='searchable{0}'", queryDocId));
                Console.WriteLine("Query on /subDoc/searchable returned {0} results", queryStats.Count);
                Console.WriteLine("Query on /subDoc/searchable cost {0} RUs", queryStats.RequestCharge);

            }
            finally
            {
                // Cleanup
                await container.DeleteContainerAsync();
            }
        }

        private static async Task<QueryStats> GetQueryResult(Container container, string query)
        {
            try
            {
                FeedIterator<dynamic> documentQuery = container.GetItemQueryIterator<dynamic>(
                    query,
                    requestOptions: 
                    new QueryRequestOptions
                    {
                        MaxItemCount = -1
                    });

                FeedResponse<dynamic> response = await documentQuery.ReadNextAsync();
                return new QueryStats(response.Count, response.RequestCharge);
            }
            catch (Exception e)
            {
                Program.LogException(e);
                return new QueryStats(0, 0.0);
            }
        }

        private static async Task Setup(CosmosClient client)
        {
            database = await client.CreateDatabaseIfNotExistsAsync(databaseId);
        }

        /// <summary>
        /// Log exception error message to the console
        /// </summary>
        /// <param name="e">The caught exception.</param>
        private static void LogException(Exception e)
        {
            ConsoleColor color = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;

            Exception baseException = e.GetBaseException();
            if (e is CosmosException)
            {
                CosmosException de = (CosmosException)e;
                Console.WriteLine("{0} error occurred: {1}, Message: {2}", de.StatusCode, de.Message, baseException.Message);
            }
            else
            {
                Console.WriteLine("Error: {0}, Message: {1}", e.Message, baseException.Message);
            }

            Console.ForegroundColor = color;
        }
    }
}
