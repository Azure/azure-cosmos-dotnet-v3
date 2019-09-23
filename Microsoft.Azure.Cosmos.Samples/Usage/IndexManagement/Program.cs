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
            //await Program.UseLazyIndexing();

            // 3. Exclude specified document paths from the index
            //await Program.ExcludePathsFromIndex();

            // 4. Use range indexes on strings
            //await Program.UsingRangeIndexes();

            // 5. Perform an index transform
            //await Program.PerformIndexTransformations();

            // Uncomment to delete container!
            // await Program.DeleteContainer();
        }
        // </RunIndexDemo>

        private static async Task ExplicitlyExcludeFromIndex(CosmosClient client)
        {
            string collectionId = string.Format(CultureInfo.InvariantCulture, "{0}-ExplicitlyExcludeFromIndex", Program.containerId);

            // Create a collection with default index policy(i.e.automatic = true)
            ContainerResponse response = await Program.database.CreateContainerAsync(collectionId, Program.partitionKey);
            Console.WriteLine("Container {0} created with index policy \n{1}", collectionId, JsonConvert.SerializeObject(response.Resource.IndexingPolicy));
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

        private static async Task Setup(CosmosClient client)
        {
            database = await client.CreateDatabaseIfNotExistsAsync(databaseId);
        }
    }

    public class ToDoActivity
    {
        public string id = null;
        public string activityId = null;
        public string status = null;
    }
}
