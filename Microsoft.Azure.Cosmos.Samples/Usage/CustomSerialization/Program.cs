namespace Cosmos.Samples.Shared
{
    using System;
    using System.IO;
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
    // Sample - demonstrates the basic serialization options for Azure Cosmos Client
    // ----------------------------------------------------------------------------------------------------------

    public class Program
    {
        private static readonly string databaseId = "samples";
        private static readonly string containerId = "item-serialization-samples";
        private static readonly JsonSerializer Serializer = new JsonSerializer();

        //Reusable instance of ItemClient which represents the connection to a Cosmos endpoint
        private static Database database = null;
        private static Container container = null;

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
                    await Program.Initialize(client);
                    await Program.RunBasicSerializationOptions(endpoint, authKey);
                    await Program.Cleanup();
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

        private static async Task RunBasicSerializationOptions(string endpoint, string authKey)
        {
            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                SerializerOptions = new CosmosSerializationOptions()
                {
                    IgnoreNullValues = true,
                    Indented = false,
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                }
            };

            dynamic testItem = new { Id = "CapitalId", TestNull = (string)null, Pk = "12345" };

            using (CosmosClient customSerializationClient = new CosmosClient(endpoint, authKey, clientOptions))
            {
                Container containerWithCustomSerialization = customSerializationClient.GetContainer(Program.databaseId, Program.containerId);
                ItemResponse<dynamic> itemResponse = await containerWithCustomSerialization.CreateItemAsync<dynamic>(
                    item: testItem,
                    partitionKey: new PartitionKey(testItem.Pk));

                using (ResponseMessage response = await containerWithCustomSerialization.ReadItemStreamAsync(
                    testItem.Id,
                    new PartitionKey(testItem.Pk)))
                {
                    using (StreamReader sr = new StreamReader(response.Content))
                    {
                        string jsonOfTestItem = await sr.ReadToEndAsync();
                        Console.WriteLine($"The JSON string of the test item. All the properties are lower camel case, null is ignore, the text is indented. String: {jsonOfTestItem}");
                    }
                }
            }

        }

        private static async Task Cleanup()
        {
            if (database != null)
            {
                await database.DeleteAsync();
            }
        }

        private static async Task Initialize(CosmosClient client)
        {
            database = await client.CreateDatabaseIfNotExistsAsync(databaseId);

            // Delete the existing container to prevent create item conflicts
            using (await database.GetContainer(containerId).DeleteContainerStreamAsync())
            { }

            // We create a partitioned collection here which needs a partition key. Partitioned collections
            // can be created with very high values of provisioned throughput (up to Throughput = 250,000)
            // and used to store up to 250 GB of data. You can also skip specifying a partition key to create
            // single partition collections that store up to 10 GB of data.
            // For this demo, we create a collection to store SalesOrders. We set the partition key to the account
            // number so that we can retrieve all sales orders for an account efficiently from a single partition,
            // and perform transactions across multiple sales order for a single account number. 
            ContainerProperties containerProperties = new ContainerProperties(containerId, partitionKeyPath: "/pk");

            // Create with a throughput of 1000 RU/s
            container = await database.CreateContainerIfNotExistsAsync(
                containerProperties,
                throughput: 1000);
        }
    }
}

