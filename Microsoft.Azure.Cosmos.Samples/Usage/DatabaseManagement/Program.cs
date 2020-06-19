namespace Cosmos.Samples.Shared
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Configuration;

    internal class Program
    {
        //Read configuration
        private static readonly string databaseId = "samples";

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
                    await Program.RunDatabaseDemo(client);
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
        /// Run basic database meta data operations as a console application.
        /// </summary>
        /// <returns></returns>
        // <RunDatabaseDemo>
        private static async Task RunDatabaseDemo(CosmosClient client)
        {
            // An object containing relevant information about the response
            DatabaseResponse databaseResponse = await client.CreateDatabaseIfNotExistsAsync(databaseId, 10000);

            // A client side reference object that allows additional operations like ReadAsync
            Database database = databaseResponse;

            // The response from Azure Cosmos
            DatabaseProperties properties = databaseResponse;

            Console.WriteLine($"\n1. Create a database resource with id: {properties.Id} and last modified time stamp: {properties.LastModified}");
            Console.WriteLine($"\n2. Create a database resource request charge: {databaseResponse.RequestCharge} and Activity Id: {databaseResponse.ActivityId}");

            // Read the database from Azure Cosmos
            DatabaseResponse readResponse = await database.ReadAsync();
            Console.WriteLine($"\n3. Read a database: {readResponse.Resource.Id}");

            await readResponse.Database.CreateContainerAsync("testContainer", "/pk");

            // Get the current throughput for the database
            int? throughputResponse = await database.ReadThroughputAsync();
            if (throughputResponse.HasValue)
            {
                Console.WriteLine($"\n4. Read a database throughput: {throughputResponse}");

                // Update the current throughput for the database
                await database.ReplaceThroughputAsync(11000);
            }

            Console.WriteLine("\n5. Reading all databases resources for an account");
            using (FeedIterator<DatabaseProperties> iterator = client.GetDatabaseQueryIterator<DatabaseProperties>())
            {
                while(iterator.HasMoreResults)
                {
                    foreach (DatabaseProperties db in await iterator.ReadNextAsync())
                    {
                        Console.WriteLine(db.Id);
                    }
                }
            }

            // Delete the database from Azure Cosmos.
            await database.DeleteAsync();
            Console.WriteLine($"\n6. Database {database.Id} deleted.");
        }
        // </RunDatabaseDemo>
    }
}
