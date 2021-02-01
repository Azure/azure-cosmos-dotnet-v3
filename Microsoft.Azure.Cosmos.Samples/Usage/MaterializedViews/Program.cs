namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json;

    public class Program
    {
        private const string DatabaseName = "db";
        private const string SourceContainerName = "src";
        private const string MaterializedViewName = "mv2";
        private const string SourcePartitionKeyPath = "/city";
        private const string MaterializedViewPartitionKeyPath = "/country";

        class Employee
        {
            [JsonProperty(PropertyName = "id")]
            public string Id { get; set; }

            [JsonProperty(PropertyName = "city")]
            public string City { get; set; }

            [JsonProperty(PropertyName = "country")]
            public string Country { get; set; }

            public override string ToString()
            {
                return $"id:{this.Id} city:{this.City} country:{this.Country}";
            }
        }

        class MaterializedViewDefinition
        {
            [JsonProperty(PropertyName = "sourceCollectionRid")]
            public string SourceCollectionRid
            {
                get; set;
            }

            [JsonProperty(PropertyName = "definition")]
            public string Definition
            {
                get; set;
            }
        }


        class ExtendedContainerProperties : ContainerProperties
        {
            public ExtendedContainerProperties(string id, string partitionKeyPath)
                : base(id, partitionKeyPath)
            {
            }

            [JsonProperty(PropertyName = "allowMaterializedViews", NullValueHandling = NullValueHandling.Ignore)]
            public bool AllowMaterializedViews { get; set; }

            [JsonProperty(PropertyName = "materializedViewDefinition", NullValueHandling = NullValueHandling.Ignore)]
            public MaterializedViewDefinition MaterializedViewDefinition { get; set; }
        }

        public static async Task Main()
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

                CosmosClient client = new CosmosClient(endpoint, authKey);
                Database db = await client.CreateDatabaseIfNotExistsAsync(DatabaseName);
                ContainerResponse srcResponse = await db.CreateContainerIfNotExistsAsync(
                    new ExtendedContainerProperties(SourceContainerName, SourcePartitionKeyPath)
                    {
                        AllowMaterializedViews = true
                    });
                Container src = srcResponse.Container;

                Employee item1 = new Employee()
                {
                    Id = "Ab",
                    City = "Bengaluru",
                    Country = "India"
                };

                await src.UpsertItemAsync(item1);

                Container mv = await db.CreateContainerIfNotExistsAsync(
                    new ExtendedContainerProperties(MaterializedViewName, MaterializedViewPartitionKeyPath)
                    {
                        MaterializedViewDefinition = new MaterializedViewDefinition()
                        {
                            SourceCollectionRid = srcResponse.Resource.SelfLink.Split("/")[3],
                            Definition = "SELECT * FROM c"
                        }
                    });

                Employee item2 = new Employee()
                {
                    Id = "He",
                    City = "Bengaluru",
                    Country = "India"
                };

                await src.UpsertItemAsync(item2);

                await Task.Delay(TimeSpan.FromSeconds(15));

                FeedIterator<Employee> iterator = mv.GetItemQueryIterator<Employee>();
                while (iterator.HasMoreResults)
                {
                    FeedResponse<Employee> employees = await iterator.ReadNextAsync();
                    foreach (Employee employee in employees)
                    {
                        Console.WriteLine(employee);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }
}