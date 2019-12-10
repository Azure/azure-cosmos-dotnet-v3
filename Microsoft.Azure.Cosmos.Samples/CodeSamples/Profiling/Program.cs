namespace Cosmos.Samples.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json;

    /// <summary>
    /// This class shows the different ways to profile CosmosDB.
    /// </summary>
    internal class Program
    {
        //Read configuration
        private static readonly string CosmosDatabaseId = "samples";
        private static readonly string containerId = "profiling-samples";

        // Sample Data
        private static readonly Family AndersonFamily = new Family
        {
            Id = "AndersonFamily",
            LastName = "Anderson",
            Parents = new Parent[]
                {
                    new Parent { FirstName = "Thomas" },
                    new Parent { FirstName = "Mary Kay"}
                },
            Children = new Child[]
                {
                    new Child
                    {
                        FirstName = "Henriette Thaulow",
                        Gender = "female",
                        Grade = 5,
                        Pets = new []
                        {
                            new Pet { GivenName = "Fluffy" }
                        }
                    }
                },
            Address = new Address { State = "WA", County = "King", City = "Seattle" },
            IsRegistered = true,
            RegistrationDate = DateTime.UtcNow.AddDays(-1)
        };

        private static readonly Family WakefieldFamily = new Family
        {
            Id = "WakefieldFamily",
            LastName = "Wakefield",
            Parents = new[] {
                    new Parent { FamilyName= "Wakefield", FirstName= "Robin" },
                    new Parent { FamilyName= "Miller", FirstName= "Ben" }
                },
            Children = new Child[] {
                    new Child
                    {
                        FamilyName= "Merriam",
                        FirstName= "Jesse",
                        Gender= "female",
                        Grade= 8,
                        Pets= new Pet[] {
                            new Pet { GivenName= "Goofy" },
                            new Pet { GivenName= "Shadow" }
                        }
                    },
                    new Child
                    {
                        FirstName= "Lisa",
                        Gender= "female",
                        Grade= 1
                    }
                },
            Address = new Address { State = "NY", County = "Manhattan", City = "NY" },
            IsRegistered = false,
            RegistrationDate = DateTime.UtcNow.AddDays(-30)
        };

        private static Database cosmosDatabase = null;

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

                //Read the Cosmos endpointUrl and authorizationKey from configuration
                //These values are available from the Azure Management Portal on the Cosmos Account Blade under "Keys"
                //NB > Keep these values in a safe & secure location. Together they provide Administrative access to your Cosmos account
                using (CosmosClient client = new CosmosClient(endpoint, authKey))
                {
                    await Program.RunDemoAsync(client);
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

        // <RunDemoAsync>
        private static async Task RunDemoAsync(CosmosClient client)
        {
            cosmosDatabase = await client.CreateDatabaseIfNotExistsAsync(CosmosDatabaseId);
            Container container = await Program.GetOrCreateContainerAsync(cosmosDatabase, containerId);

            await Program.ProfilePointOperations(container);
            await Program.ProfileFeedOperations(container);

            await container.DeleteContainerAsync();
        }
        // </RunDemoAsync>

        // <ProfilePointOperation>
        private static async Task ProfilePointOperations(Container container)
        {
            await ProfilePointOperation<Family>(
                description: "Point Write",
                pointOperation: () => container.CreateItemAsync<Family>(item: AndersonFamily, partitionKey: new PartitionKey(AndersonFamily.LastName)));

            await ProfilePointOperation<Family>(
                description: "Point Read",
                pointOperation: () => container.ReadItemAsync<Family>(id: AndersonFamily.Id, partitionKey: new PartitionKey(AndersonFamily.LastName)));
        }

        private static async Task ProfilePointOperation<T>(string description, Func<Task<ItemResponse<T>>> pointOperation)
        {
            Console.WriteLine(description);
            ItemResponse<T> response = await pointOperation();
            Console.WriteLine(response.Diagnostics);
            Console.WriteLine();
        }
        // </ProfilePointOperation>

        // <ProfilePointOperation>
        private static async Task ProfileFeedOperations(Container container)
        {
            await ProfileFeedOperation(
                description: "Query",
                feedIterator: container.GetItemQueryStreamIterator(queryDefinition: new QueryDefinition("SELECT * FROM c")));

            await ProfileFeedOperation(
                description: "ReadFeed",
                feedIterator: container.GetItemQueryStreamIterator());
        }

        private static async Task ProfileFeedOperation(string description, FeedIterator feedIterator)
        {
            Console.WriteLine($"Description: {description}");
            int iteration = 0;
            while (feedIterator.HasMoreResults)
            {
                Console.WriteLine($"Iteration: {iteration++}");
                ResponseMessage responseMessage = await feedIterator.ReadNextAsync();
                Console.WriteLine(responseMessage.Diagnostics);
            }

            Console.WriteLine();
        }
        // </ProfilePointOperation>

        /// <summary>
        /// Get a DocuemntContainer by id, or create a new one if one with the id provided doesn't exist.
        /// </summary>
        /// <param name="id">The id of the CosmosContainer to search for, or create.</param>
        /// <returns>The matched, or created, CosmosContainer object</returns>
        // <GetOrCreateContainerAsync>
        private static async Task<Container> GetOrCreateContainerAsync(Database database, string containerId)
        {
            ContainerProperties containerProperties = new ContainerProperties(id: containerId, partitionKeyPath: "/LastName");

            return await database.CreateContainerIfNotExistsAsync(
                containerProperties: containerProperties,
                throughput: 400);
        }
        // </GetOrCreateContainerAsync>

        internal sealed class Parent
        {
            public string FamilyName { get; set; }
            public string FirstName { get; set; }
        }

        internal sealed class Child
        {
            public string FamilyName { get; set; }
            public string FirstName { get; set; }
            public string Gender { get; set; }
            public int Grade { get; set; }
            public Pet[] Pets { get; set; }
        }

        internal sealed class Pet
        {
            public string GivenName { get; set; }
        }

        internal sealed class Address
        {
            public string State { get; set; }
            public string County { get; set; }
            public string City { get; set; }
        }

        internal sealed class Family
        {
            [JsonProperty(PropertyName = "id")]
            public string Id { get; set; }

            public string LastName { get; set; }

            public Parent[] Parents { get; set; }

            public Child[] Children { get; set; }

            public Address Address { get; set; }

            public bool IsRegistered { get; set; }

            public DateTime RegistrationDate { get; set; }

            public string PartitionKey => this.LastName;

            public static string PartitionKeyPath => "/LastName";
        }
    }
}

