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
    /// This class shows the different ways to execute item feed and queries.
    /// </summary>
    /// <remarks>
    /// For help with SQL query syntax see: 
    /// https://docs.microsoft.com/en-us/azure/cosmos-db/query-cheat-sheet
    /// https://docs.microsoft.com/en-us/azure/cosmos-db/how-to-sql-query
    /// </remarks>
    internal class Program
    {
        //Read configuration
        private static readonly string CosmosDatabaseId = "samples";
        private static readonly string containerId = "query-samples";

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

                //Read the Cosmos endpointUrl and authorisationKeys from configuration
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

            await Program.CreateItems(container);

            await Program.ItemFeed(container);

            await Program.ItemStreamFeed(container);

            await Program.QueryItemsInPartitionAsStreams(container);

            await Program.QueryPartitionedContainerInParallelAsync(container);

            await Program.QueryWithSqlParameters(container);

            // Uncomment to Cleanup
            //await cosmosDatabase.DeleteAsync();
        }
        // </RunDemoAsync>

        // <ItemFeed>
        private static async Task ItemFeed(Container container)
        {
            List<Family> families = new List<Family>();

            // SQL
            FeedIterator<Family> setIterator = container.GetItemQueryIterator<Family>(requestOptions: new QueryRequestOptions { MaxItemCount =  1});
            while (setIterator.HasMoreResults)
            {
                int count = 0;
                foreach (Family item in await setIterator.ReadNextAsync())
                {
                    Assert("Should only return 1 result at a time.", count <= 1);
                    families.Add(item);
                }
            }

            Assert("Expected two families", families.ToList().Count == 2);
        }
        // </ItemFeed>

        // <ItemStreamFeed>
        private static async Task ItemStreamFeed(Container container)
        {
            int totalCount = 0;

            // SQL
            FeedIterator setIterator = container.GetItemQueryStreamIterator();
            while (setIterator.HasMoreResults)
            {
                int count = 0;
                using (ResponseMessage response = await setIterator.ReadNextAsync())
                {
                    if (response.Diagnostics != null)
                    {
                        Console.WriteLine($"ItemStreamFeed Diagnostics: {response.Diagnostics.ToString()}");
                    }

                    response.EnsureSuccessStatusCode();
                    count++;
                    using (StreamReader sr = new StreamReader(response.Content))
                    using (JsonTextReader jtr = new JsonTextReader(sr))
                    {
                        JsonSerializer jsonSerializer = new JsonSerializer();
                        dynamic array = jsonSerializer.Deserialize<dynamic>(jtr);
                        totalCount += array.Documents.Count;
                    }
                }

            }

            Assert("Expected two families", totalCount == 2);
        }
        // </ItemStreamFeed>

        // <QueryItemsInPartitionAsStreams>
        private static async Task QueryItemsInPartitionAsStreams(Container container)
        {
            // SQL
            FeedIterator setIterator = container.GetItemQueryStreamIterator(
                "SELECT F.id, F.LastName, F.IsRegistered FROM Families F",
                requestOptions: new QueryRequestOptions() {
                    PartitionKey = new PartitionKey("Anderson"),
                    MaxConcurrency = 1,
                    MaxItemCount = 1
                });

            int count = 0;
            while (setIterator.HasMoreResults)
            {
                using (ResponseMessage response = await setIterator.ReadNextAsync())
                {
                    Assert("Response failed", response.IsSuccessStatusCode);
                    count++;
                    using (StreamReader sr = new StreamReader(response.Content))
                    using (JsonTextReader jtr = new JsonTextReader(sr))
                    {
                        JsonSerializer jsonSerializer = new JsonSerializer();
                        dynamic items = jsonSerializer.Deserialize<dynamic>(jtr).Documents;
                        Assert("Expected one family", items.Count == 1);
                        dynamic item = items[0];
                        Assert($"Expected LastName: Anderson Actual: {item.LastName}", string.Equals("Anderson", item.LastName.ToString(), StringComparison.InvariantCulture));
                    }
                }
            }

            Assert("Expected 1 family", count == 1);
        }
        // </QueryItemsInPartitionAsStreams>

        // <QueryWithSqlParameters>
        private static async Task QueryWithSqlParameters(Container container)
        {
            // Query using two properties within each item. WHERE Id == "" AND Address.City == ""
            // notice here how we are doing an equality comparison on the string value of City

            QueryDefinition query = new QueryDefinition("SELECT * FROM Families f WHERE f.id = @id AND f.Address.City = @city")
                .WithParameter("@id", "AndersonFamily")
                .WithParameter("@city", "Seattle");

            List<Family> results = new List<Family>();
            FeedIterator<Family> resultSetIterator = container.GetItemQueryIterator<Family>(query, requestOptions: new QueryRequestOptions() { PartitionKey = new PartitionKey("Anderson")});
            while (resultSetIterator.HasMoreResults)
            {
                FeedResponse<Family> response = await resultSetIterator.ReadNextAsync();
                results.AddRange(response);
                if (response.Diagnostics != null)
                {
                    Console.WriteLine($"\nQueryWithSqlParameters Diagnostics: {response.Diagnostics.ToString()}");
                }
            }

            Assert("Expected only 1 family", results.Count == 1);
        }
        // </QueryWithSqlParameters>

        // <QueryPartitionedContainerInParallelAsync>
        private static async Task QueryPartitionedContainerInParallelAsync(Container container)
        {
            List<Family> familiesSerial = new List<Family>();
            string queryText = "SELECT * FROM Families";

            // 0 maximum parallel tasks, effectively serial execution
            QueryRequestOptions options = new QueryRequestOptions() { MaxBufferedItemCount = 100 };
            options.MaxConcurrency = 0;
            FeedIterator<Family> query = container.GetItemQueryIterator<Family>(
                queryText,
                requestOptions: options);
            while (query.HasMoreResults)
            {
                foreach (Family family in await query.ReadNextAsync())
                {
                    familiesSerial.Add(family);
                }
            }

            Assert("Parallel Query expected two families", familiesSerial.ToList().Count == 2);

            // 1 maximum parallel tasks, 1 dedicated asynchronous task to continuously make REST calls
            List<Family> familiesParallel1 = new List<Family>();

            options.MaxConcurrency = 1;
            query = container.GetItemQueryIterator<Family>(
                queryText,
                requestOptions: options);

            while (query.HasMoreResults)
            {
                foreach (Family family in await query.ReadNextAsync())
                {
                    familiesParallel1.Add(family);
                }
            }

            Assert("Parallel Query expected two families", familiesParallel1.ToList().Count == 2);
            AssertSequenceEqual("Parallel query returns result out of order compared to serial execution", familiesSerial, familiesParallel1);


            // 10 maximum parallel tasks, a maximum of 10 dedicated asynchronous tasks to continuously make REST calls
            List<Family> familiesParallel10 = new List<Family>();

            options.MaxConcurrency = 10;
            query = container.GetItemQueryIterator<Family>(
                queryText,
                requestOptions: options);

            while (query.HasMoreResults)
            {
                foreach (Family family in await query.ReadNextAsync())
                {
                    familiesParallel10.Add(family);
                }
            }

            Assert("Parallel Query expected two families", familiesParallel10.ToList().Count == 2);
            AssertSequenceEqual("Parallel query returns result out of order compared to serial execution", familiesSerial, familiesParallel10);
        }
        // </QueryPartitionedContainerInParallelAsync>

        /// <summary>
        /// Creates the items used in this Sample
        /// </summary>
        /// <param name="container">The selfLink property for the CosmosContainer where items will be created.</param>
        /// <returns>None</returns>
        // <CreateItems>
        private static async Task CreateItems(Container container)
        {
            Family AndersonFamily = new Family
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

            await container.UpsertItemAsync<Family>(AndersonFamily, new PartitionKey(AndersonFamily.PartitionKey));

            Family WakefieldFamily = new Family
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

            await container.UpsertItemAsync<Family>(WakefieldFamily, new PartitionKey(WakefieldFamily.PartitionKey));
        }
        // </CreateItems>

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

        private static void Assert(string message, bool condition)
        {
            if (!condition)
            {
                throw new ApplicationException(message);
            }
        }

        private static void AssertSequenceEqual(string message, List<Family> list1, List<Family> list2)
        {
            if (!string.Join(",", list1.Select(family => family.Id).ToArray()).Equals(
                string.Join(",", list1.Select(family => family.Id).ToArray())))
            {
                throw new ApplicationException(message);
            }
        }

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

