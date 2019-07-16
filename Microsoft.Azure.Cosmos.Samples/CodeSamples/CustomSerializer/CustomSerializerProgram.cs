namespace Cosmos.Samples.Shared
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json.Linq;

    class CustomSerializerProgram
    {
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
                CustomSerializerProgram program = new CustomSerializerProgram();
                await program.RunDemo(endpoint, authKey);

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

        public async Task RunDemo(string endpoint, string key)
        {
            CosmosClientOptions options = new CosmosClientOptions()
            {
                Serializer = new JsonSerializerIgnoreNull(),
            };

            using (CosmosClient client = new CosmosClient(endpoint, key, options))
            {
                Database db = null;
                try
                {
                    db = await client.CreateDatabaseIfNotExistsAsync("CustomSerializerDemo");
                    Container container = await db.CreateContainerIfNotExistsAsync(
                        id: "ContainerDemo",
                        partitionKeyPath: "/pk");

                    dynamic testItem = new { id = "MyTestItemWithIgnoreNull" + Guid.NewGuid(), pk = "ItExists", description = (string)null };
                    ItemResponse<dynamic> itemResponse = await container.CreateItemAsync<dynamic>(testItem, new PartitionKey(testItem.pk));
                    dynamic responseObject = itemResponse.Resource;
                    if (responseObject["description"] != null)
                    {
                        throw new InvalidOperationException("Description was not ignored");
                    }

                    Console.WriteLine($"Item created: {responseObject}");
                }
                finally
                {
                    if(db != null)
                    {
                        await db.DeleteAsync();
                    }
                }
            }
        }
    }
}
