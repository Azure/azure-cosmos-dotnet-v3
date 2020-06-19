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

        private static Database database = null;

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
        // </Main>

        /// <summary>
        /// Run through basic container access methods as a console app demo.
        /// </summary>
        /// <returns></returns>
        // <RunContainerDemo>
        private static async Task RunContainerDemo(CosmosClient client)
        {
            // Create the database if necessary
            await Program.Setup(client);

            Container simpleContainer = await Program.CreateContainer();

            await Program.CreateContainerWithCustomIndexingPolicy();

            await Program.CreateContainerWithTtlExpiration();

            await Program.GetAndChangeContainerPerformance(simpleContainer);

            await Program.ReadContainerProperties();

            await Program.ListContainersInDatabase();

            // Uncomment to delete container!
            // await Program.DeleteContainer();
        }
        // </RunContainerDemo>

        private static async Task Setup(CosmosClient client)
        {
            database = await client.CreateDatabaseIfNotExistsAsync(databaseId);
        }

        // <CreateContainer>
        private static async Task<Container> CreateContainer()
        {
            // Set throughput to the minimum value of 400 RU/s
            ContainerResponse simpleContainer = await database.CreateContainerIfNotExistsAsync(
                id: containerId,
                partitionKeyPath: partitionKey,
                throughput: 400);

            Console.WriteLine($"\n1.1. Created container :{simpleContainer.Container.Id}");
            return simpleContainer;
        }
        // </CreateContainer>

        // <CreateContainerWithCustomIndexingPolicy>
        private static async Task CreateContainerWithCustomIndexingPolicy()
        {
            // Create a container with custom index policy (consistent indexing)
            // We cover index policies in detail in IndexManagement sample project
            ContainerProperties containerProperties = new ContainerProperties(
                id: "SampleContainerWithCustomIndexPolicy",
                partitionKeyPath: partitionKey);
            containerProperties.IndexingPolicy.IndexingMode = IndexingMode.Consistent;

            Container containerWithConsistentIndexing = await database.CreateContainerIfNotExistsAsync(
                containerProperties,
                throughput: 400);

            Console.WriteLine($"1.2. Created Container {containerWithConsistentIndexing.Id}, with custom index policy \n");

            await containerWithConsistentIndexing.DeleteContainerAsync();
        }
        // </CreateContainerWithCustomIndexingPolicy>

        // <CreateContainerWithTtlExpiration>
        private static async Task CreateContainerWithTtlExpiration()
        {
            ContainerProperties properties = new ContainerProperties
                (id: "TtlExpiryContainer",
                partitionKeyPath: partitionKey);
            properties.DefaultTimeToLive = (int)TimeSpan.FromDays(1).TotalSeconds; //expire in 1 day

            ContainerResponse ttlEnabledContainerResponse = await database.CreateContainerIfNotExistsAsync(
                containerProperties: properties);
            ContainerProperties returnedProperties = ttlEnabledContainerResponse;

            Console.WriteLine($"\n1.3. Created Container \n{returnedProperties.Id} with TTL expiration of {returnedProperties.DefaultTimeToLive}");

            await ttlEnabledContainerResponse.Container.DeleteContainerAsync();
        }
        // </CreateContainerWithTtlExpiration>

        // <GetAndChangeContainerPerformance>
        private static async Task GetAndChangeContainerPerformance(Container simpleContainer)
        {

            //*********************************************************************************************
            // Get configured performance (reserved throughput) of a CosmosContainer
            //**********************************************************************************************
            int? throughputResponse = await simpleContainer.ReadThroughputAsync();

            Console.WriteLine($"\n2. Found throughput \n{throughputResponse}\nusing container's id \n{simpleContainer.Id}");

            //******************************************************************************************************************
            // Change performance (reserved throughput) of CosmosContainer
            //    Let's change the performance of the container to 500 RU/s
            //******************************************************************************************************************

            await simpleContainer.ReplaceThroughputAsync(500);

            Console.WriteLine("\n3. Replaced throughput. Throughput is now 500.\n");

            // Get the offer again after replace
            throughputResponse = await simpleContainer.ReadThroughputAsync();

            Console.WriteLine($"3. Found throughput \n{throughputResponse}\n using container's ResourceId {simpleContainer.Id}.\n");
        }
        // </GetAndChangeContainerPerformance>

        // <ReadContainerProperties>
        private static async Task ReadContainerProperties()
        {
            //*************************************************
            // Get a CosmosContainer by its Id property
            //*************************************************
            Container container = database.GetContainer(containerId);
            ContainerProperties containerProperties = await container.ReadContainerAsync();

            Console.WriteLine($"\n4. Found Container \n{containerProperties.Id}\n");
        }
        // </ReadContainerProperties>

        /// <summary>
        /// List the container within a database by calling the GetContainerIterator (scan) API.
        /// </summary>
        /// <returns></returns>
        // <ListContainersInDatabase>
        private static async Task ListContainersInDatabase()
        {
            Console.WriteLine("\n5. Reading all CosmosContainer resources for a database");

            using (FeedIterator<ContainerProperties> resultSetIterator = database.GetContainerQueryIterator<ContainerProperties>())
            {
                while (resultSetIterator.HasMoreResults)
                {
                    foreach (ContainerProperties container in await resultSetIterator.ReadNextAsync())
                    {
                        Console.WriteLine(container.Id);
                    }
                }
            }
        }
        // </ListContainersInDatabase>

        /// <summary>
        /// Delete a container
        /// </summary>
        /// <param name="simpleContainer"></param>
        // <DeleteContainer>
        private static async Task DeleteContainer()
        {
            await database.GetContainer(containerId).DeleteContainerAsync();
            Console.WriteLine("\n6. Deleted Container\n");
        }
        // </DeleteContainer>
    }

    public class ToDoActivity
    {
        public string id = null;
        public string activityId = null;
        public string status = null;
    }
}
