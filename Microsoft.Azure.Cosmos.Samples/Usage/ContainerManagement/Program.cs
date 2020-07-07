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
        private static readonly string autoscaleContainerId = "autoscale-container-samples";
        private static readonly string partitionKeyPath = "/activityId";

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

            await Program.CreateAndUpdateAutoscaleContainer();

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
                partitionKeyPath: partitionKeyPath,
                throughput: 400);

            Console.WriteLine($"{Environment.NewLine}1.1. Created container :{simpleContainer.Container.Id}");
            return simpleContainer;
        }
        // </CreateContainer>

        // <CreateAndUpdateAutoscaleContainer>
        private static async Task CreateAndUpdateAutoscaleContainer()
        {
            // Set autoscale throughput to the maximum value of 10000 RU/s
            ContainerProperties containerProperties = new ContainerProperties(autoscaleContainerId, partitionKeyPath);

            Container autoscaleContainer = await database.CreateContainerIfNotExistsAsync(
                containerProperties: containerProperties,
                throughputProperties: ThroughputProperties.CreateAutoscaleThroughput(autoscaleMaxThroughput: 10000));

            Console.WriteLine($"{Environment.NewLine}1.2. Created autoscale container :{autoscaleContainer.Id}");

            //*********************************************************************************************
            // Get configured performance of a CosmosContainer
            //**********************************************************************************************
            ThroughputResponse throughputResponse = await autoscaleContainer.ReadThroughputAsync(requestOptions: null);

            Console.WriteLine($"{Environment.NewLine}1.2.1. Found autoscale throughput {Environment.NewLine}The current throughput: {throughputResponse.Resource.Throughput} Max throughput: {throughputResponse.Resource.AutoscaleMaxThroughput} " +
                $"using container's id: {autoscaleContainer.Id}");

            //*********************************************************************************************
            // Get the current throughput configured for a Container
            //**********************************************************************************************
            int? currentThroughput = await autoscaleContainer.ReadThroughputAsync();

            Console.WriteLine($"{Environment.NewLine}1.2.2. Found autoscale throughput {Environment.NewLine}The current throughput: {currentThroughput} using container's id: {autoscaleContainer.Id}");

            //******************************************************************************************************************
            // Change performance (reserved throughput) of CosmosContainer
            //    Let's change the performance of the autoscale container to a maximum throughput of 15000 RU/s
            //******************************************************************************************************************
            ThroughputResponse throughputUpdateResponse = await autoscaleContainer.ReplaceThroughputAsync(ThroughputProperties.CreateAutoscaleThroughput(15000));

            Console.WriteLine($"{Environment.NewLine}1.2.3. Replaced autoscale throughput. {Environment.NewLine}The current throughput: {throughputUpdateResponse.Resource.Throughput} Max throughput: {throughputUpdateResponse.Resource.AutoscaleMaxThroughput} " +
                $"using container's id: {autoscaleContainer.Id}");

            // Get the offer again after replace
            throughputResponse = await autoscaleContainer.ReadThroughputAsync(requestOptions: null);

            Console.WriteLine($"{Environment.NewLine}1.2.4. Found autoscale throughput {Environment.NewLine}The current throughput: {throughputResponse.Resource.Throughput} Max throughput: {throughputResponse.Resource.AutoscaleMaxThroughput} " +
                $"using container's id: {autoscaleContainer.Id}{Environment.NewLine}");

            // Delete the container
            await autoscaleContainer.DeleteContainerAsync();
        }
        // </CreateAndUpdateAutoscaleContainer>

        // <CreateContainerWithCustomIndexingPolicy>
        private static async Task CreateContainerWithCustomIndexingPolicy()
        {
            // Create a container with custom index policy (consistent indexing)
            // We cover index policies in detail in IndexManagement sample project
            ContainerProperties containerProperties = new ContainerProperties(
                id: "SampleContainerWithCustomIndexPolicy",
                partitionKeyPath: partitionKeyPath);
            containerProperties.IndexingPolicy.IndexingMode = IndexingMode.Consistent;

            Container containerWithConsistentIndexing = await database.CreateContainerIfNotExistsAsync(
                containerProperties,
                throughput: 400);

            Console.WriteLine($"1.3 Created Container {containerWithConsistentIndexing.Id}, with custom index policy {Environment.NewLine}");

            await containerWithConsistentIndexing.DeleteContainerAsync();
        }
        // </CreateContainerWithCustomIndexingPolicy>

        // <CreateContainerWithTtlExpiration>
        private static async Task CreateContainerWithTtlExpiration()
        {
            ContainerProperties properties = new ContainerProperties
                (id: "TtlExpiryContainer",
                partitionKeyPath: partitionKeyPath);
            properties.DefaultTimeToLive = (int)TimeSpan.FromDays(1).TotalSeconds; //expire in 1 day

            ContainerResponse ttlEnabledContainerResponse = await database.CreateContainerIfNotExistsAsync(
                containerProperties: properties);
            ContainerProperties returnedProperties = ttlEnabledContainerResponse;

            Console.WriteLine($"{Environment.NewLine}1.4 Created Container {Environment.NewLine}{returnedProperties.Id} with TTL expiration of {returnedProperties.DefaultTimeToLive}");

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

            Console.WriteLine($"{Environment.NewLine}2. Found throughput {Environment.NewLine}{throughputResponse}{Environment.NewLine}using container's id {Environment.NewLine}{simpleContainer.Id}");

            //******************************************************************************************************************
            // Change performance (reserved throughput) of CosmosContainer
            //    Let's change the performance of the container to 500 RU/s
            //******************************************************************************************************************

            await simpleContainer.ReplaceThroughputAsync(500);

            Console.WriteLine($"{Environment.NewLine}3. Replaced throughput. Throughput is now 500.{Environment.NewLine}");

            // Get the offer again after replace
            throughputResponse = await simpleContainer.ReadThroughputAsync();

            Console.WriteLine($"3. Found throughput {Environment.NewLine}{throughputResponse}{Environment.NewLine} using container's ResourceId {simpleContainer.Id}.{Environment.NewLine}");
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

            Console.WriteLine($"{Environment.NewLine}4. Found Container {Environment.NewLine}{containerProperties.Id}{Environment.NewLine}");
        }
        // </ReadContainerProperties>

        /// <summary>
        /// List the container within a database by calling the GetContainerIterator (scan) API.
        /// </summary>
        /// <returns></returns>
        // <ListContainersInDatabase>
        private static async Task ListContainersInDatabase()
        {
            Console.WriteLine($"{Environment.NewLine}5. Reading all CosmosContainer resources for a database");

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
            Console.WriteLine($"{Environment.NewLine}6. Deleted Container{Environment.NewLine}");
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
