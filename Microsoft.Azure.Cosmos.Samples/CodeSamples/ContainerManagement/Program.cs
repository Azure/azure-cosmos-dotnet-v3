namespace Cosmos.Samples.Shared
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Configuration;

    class Program
    {
        //Read configuration
        private static readonly string databaseId = "samples";
        private static readonly string containerId = "container-samples";
        private static readonly string partitionKey = "/activityId";

        private static CosmosDatabase database = null;

        // Async main requires c# 7.1 which is set in the csproj with the LangVersion attribute 
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
                    await Program.RunContainerDemo(client);
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

        /// <summary>
        /// Run through basic container access methods as a console app demo.
        /// </summary>
        /// <returns></returns>
        private static async Task RunContainerDemo(CosmosClient client)
        {
            // Create the database if necessary
            await Program.Setup(client);

            CosmosContainer simpleContainer = await Program.CreateContainer();

            await Program.CreateContainerWithCustomIndexingPolicy();

            await Program.CreateContainerWithTtlExpiration();

            await Program.GetAndChangeContainerPerformance(simpleContainer);

            await Program.ReadContainerProperties();

            await Program.ListContainersInDatabase();

            // Uncomment to delete container!
            // await Program.DeleteContainer();
        }

        private static async Task Setup(CosmosClient client)
        {
            database = await client.Databases.CreateDatabaseIfNotExistsAsync(databaseId);
        }

        private static async Task<CosmosContainer> CreateContainer()
        {
            // Set throughput to the minimum value of 400 RU/s
            CosmosContainerResponse simpleContainer = await database.Containers.CreateContainerIfNotExistsAsync(
                id: containerId,
                partitionKeyPath: partitionKey,
                throughput: 400);

            Console.WriteLine($"\n1.1. Created container :{simpleContainer.Container.Id}");
            return simpleContainer;
        }

        private static async Task CreateContainerWithCustomIndexingPolicy()
        {
            // Create a container with custom index policy (lazy indexing)
            // We cover index policies in detail in IndexManagement sample project
            CosmosContainerSettings containerSettings = new CosmosContainerSettings(
                id: "SampleContainerWithCustomIndexPolicy",
                partitionKeyPath: partitionKey);
            containerSettings.IndexingPolicy.IndexingMode = IndexingMode.Lazy;

            CosmosContainer containerWithLazyIndexing = await database.Containers.CreateContainerIfNotExistsAsync(
                containerSettings,
                throughput: 400);

            Console.WriteLine($"1.2. Created Container {containerWithLazyIndexing.Id}, with custom index policy \n");

            await containerWithLazyIndexing.DeleteAsync();
        }

        private static async Task CreateContainerWithTtlExpiration()
        {
            CosmosContainerSettings settings = new CosmosContainerSettings
                (id: "TtlExpiryContainer",
                partitionKeyPath: partitionKey);
            settings.DefaultTimeToLive = (int)TimeSpan.FromDays(1).TotalSeconds; //expire in 1 day

            CosmosContainerResponse ttlEnabledContainerResponse = await database.Containers.CreateContainerIfNotExistsAsync(
                containerSettings: settings);
            CosmosContainerSettings returnedSettings = ttlEnabledContainerResponse;

            Console.WriteLine($"\n1.3. Created Container \n{returnedSettings.Id} with TTL expiration of {returnedSettings.DefaultTimeToLive}");

            await ttlEnabledContainerResponse.Container.DeleteAsync();
        }

        private static async Task GetAndChangeContainerPerformance(CosmosContainer simpleContainer)
        {

            //*********************************************************************************************
            // Get configured performance (reserved throughput) of a CosmosContainer
            //**********************************************************************************************
            int? throughput = await simpleContainer.ReadProvisionedThroughputAsync();

            Console.WriteLine($"\n2. Found throughput \n{throughput.Value}\nusing container's id \n{simpleContainer.Id}");

            //******************************************************************************************************************
            // Change performance (reserved throughput) of CosmosContainer
            //    Let's change the performance of the container to 500 RU/s
            //******************************************************************************************************************

            await simpleContainer.ReplaceProvisionedThroughputAsync(500);

            Console.WriteLine("\n3. Replaced throughput. Throughput is now 500.\n");

            // Get the offer again after replace
            int? throughputAfterReplace = await simpleContainer.ReadProvisionedThroughputAsync();

            Console.WriteLine($"3. Found throughput \n{throughputAfterReplace.Value}\n using container's ResourceId {simpleContainer.Id}.\n");
        }

        private static async Task ReadContainerProperties()
        {
            //*************************************************
            // Get a CosmosContainer by its Id property
            //*************************************************
            CosmosContainer container = database.Containers[containerId];
            CosmosContainerSettings containerSettings = await container.ReadAsync();

            Console.WriteLine($"\n4. Found Container \n{containerSettings.Id}\n");
        }

        /// <summary>
        /// List the container within a database by calling the GetContainerIterator (scan) API.
        /// </summary>
        /// <returns></returns>
        private static async Task ListContainersInDatabase()
        {
            Console.WriteLine("\n5. Reading all CosmosContainer resources for a database");

            CosmosResultSetIterator<CosmosContainerSettings> resultSetIterator = database.Containers.GetContainerIterator();
            while (resultSetIterator.HasMoreResults)
            {
                foreach (CosmosContainerSettings container in await resultSetIterator.FetchNextSetAsync())
                {
                    Console.WriteLine(container.Id);
                }
            }
        }

        /// <summary>
        /// Delete a container
        /// </summary>
        /// <param name="simpleContainer"></param>
        private static async Task DeleteContainer()
        {
            await database.Containers[containerId].DeleteAsync();
            Console.WriteLine("\n6. Deleted Container\n");
        }
    }

    public class ToDoActivity
    {
        public string id = null;
        public string activityId = null;
        public string status = null;
    }
}
