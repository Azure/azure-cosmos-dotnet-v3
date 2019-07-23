namespace Cosmos.Samples.Shared
{
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Configuration;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    internal class CustomSerializerProgram
    {
        private Container Container;

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
            JsonSerializerIgnoreNull jsonSerializerIgnore = new JsonSerializerIgnoreNull();
            CosmosClientOptions options = new CosmosClientOptions()
            {
                Serializer = jsonSerializerIgnore,
            };

            using (CosmosClient client = new CosmosClient(endpoint, key, options))
            {
                Database db = null;
                try
                {
                    db = await client.CreateDatabaseIfNotExistsAsync("CustomSerializerDemo");
                    this.Container = await db.CreateContainerIfNotExistsAsync(
                        id: "ContainerDemo",
                        partitionKeyPath: "/ponumber");

                    SalesOrder salesOrder = new SalesOrder()
                    {
                        Id = "1234SalesOrder",
                        PurchaseOrderNumber = "1234PurchaseOrderNumber"
                    };

                    SalesOrder salesOrder2 = new SalesOrder()
                    {
                        Id = "789SalesOrder",
                        PurchaseOrderNumber = "789PurchaseOrderNumber"
                    };

                    ItemResponse<SalesOrder> itemResponse = await this.Container.CreateItemAsync<SalesOrder>(salesOrder, new PartitionKey(salesOrder.PurchaseOrderNumber));
                    itemResponse = await this.Container.CreateItemAsync<SalesOrder>(salesOrder2, new PartitionKey(salesOrder2.PurchaseOrderNumber));
                    

                    Console.WriteLine($"Item created: {itemResponse.Resource}");

                    int beforeFromCount = jsonSerializerIgnore.FromCount;
                    IEnumerable<SalesOrder> salesOrders = await SearchPersons();
                    int afterFromCount = jsonSerializerIgnore.FromCount;

                    if(beforeFromCount == afterFromCount)
                    {
                        throw new ArgumentException("Nothing was desesrialized");
                    }
                }
                finally
                {
                    if (db != null)
                    {
                        await db.DeleteAsync();
                    }
                }
            }
        }

        public async Task<IEnumerable<SalesOrder>> SearchPersons()
        {
            QueryDefinition qd = new QueryDefinition("select * from T");

            List<SalesOrder> ret = new List<SalesOrder>();

            FeedIterator<SalesOrder> feed = this.Container.GetItemQueryIterator<SalesOrder>(qd);

            while (feed.HasMoreResults)
            {
                FeedResponse<SalesOrder> curr = await feed.ReadNextAsync();

                ret.AddRange(curr);
            }

            return ret;
        }
    }
}
