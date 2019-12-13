namespace Cosmos.Samples.Encryption
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Cosmos.Samples.Shared;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json;

    // ----------------------------------------------------------------------------------------------------------
    // Prerequisites - 
    // 
    // 1. An Azure Cosmos account - 
    //    https://docs.microsoft.com/en-us/azure/cosmos-db/create-cosmosdb-resources-portal
    //
    // 2. Microsoft.Azure.Cosmos NuGet package - 
    //    http://www.nuget.org/packages/Microsoft.Azure.Cosmos/ 
    // ----------------------------------------------------------------------------------------------------------
    // Sample - demonstrates the basic usage of client-side encryption support in the Cosmos DB SDK.
    // ----------------------------------------------------------------------------------------------------------

    public class Program
    {
        private const string databaseId = "samples";
        private const string containerId = "encryption";

        private const string myEncryptionKeyName = "theDataEncryptionKey";

        private static readonly JsonSerializer Serializer = new JsonSerializer();

        private static Database database = null;

        // <Main>
        public static async Task Main(string[] args)
        {
            try
            {
                // Read the Cosmos endpointUrl and authorizationKey from configuration.
                // These values are available from the Azure Management Portal on the Cosmos Account Blade under "Keys".
                // Keep these values in a safe and secure location. Together they provide administrative access to your Cosmos account.
                IConfigurationRoot configuration = new ConfigurationBuilder()
                    .AddJsonFile("appSettings.json")
                    .Build();

                using (CosmosClient client = Program.CreateClientInstance(configuration))
                {
                    await Program.InitializeAsync(client, configuration);
                    await Program.RunDemoAsync(client);
                    await Program.CleanupAsync();
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

        private static CosmosClient CreateClientInstance(IConfigurationRoot configuration)
        {
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

            // Application credentials for authentication with Azure Key Vault.
            // This application must have keys/wrapKey and keys/unwrapKey permissions
            // on the keys that will be used for encryption.
            string clientId = configuration["ClientId"];
            if (string.IsNullOrEmpty(clientId))
            {
                throw new ArgumentNullException("Please specify a valid clientId in the appSettings.json");
            }

            string clientSecret = configuration["ClientSecret"];
            if (string.IsNullOrEmpty(clientSecret))
            {
                throw new ArgumentNullException("Please specify a valid clientSecret in the appSettings.json");
            }

            CosmosClient client = new CosmosClientBuilder(endpoint, authKey)
                .WithKeyWrapProvider(new AzureKeyVaultKeyWrapProvider(clientId, clientSecret))
                .Build();
            return client;
        }

        /// <summary>
        /// Administrative operations - create the database, container, and generate the necessary data encryption keys.
        /// These are initializations and are expected to be invoked only once - do not invoke these before every item request.
        /// </summary>
        private static async Task InitializeAsync(CosmosClient client, IConfigurationRoot configuration)
        {
            Program.database = await client.CreateDatabaseIfNotExistsAsync(Program.databaseId);

            // Delete the existing container to prevent create item conflicts.
            using (await Program.database.GetContainer(Program.containerId).DeleteContainerStreamAsync())
            { }

            Console.WriteLine("The demo will create a 1000 RU/s container, press any key to continue.");
            Console.ReadKey();

            // Create a container with the appropriate partition key definition (we choose the "AccountNumber" property here) and throughput (we choose 1000 here).
            await Program.database.DefineContainer(Program.containerId, "/AccountNumber").CreateAsync(throughput: 1000);


            // Master key identifier: https://{keyvault-name}.vault.azure.net/{object-type}/{object-name}/{object-version}
            string masterKeyUrl = configuration["masterKeyUrl"];
            if (string.IsNullOrEmpty(masterKeyUrl))
            {
                throw new ArgumentException("Please specify a valid masterKeyUrl in the appSettings.json");
            }

            AzureKeyVaultKeyWrapMetadata wrapMetadata = new AzureKeyVaultKeyWrapMetadata(masterKeyUrl);

            /// Generates an encryption key, wraps it using the key wrap metadata provided
            /// with the key wrapping provider configured on the client
            /// and saves the wrapped encryption key as an asynchronous operation in the Azure Cosmos service.
            await database.CreateDataEncryptionKeyAsync(myEncryptionKeyName, wrapMetadata, clientCacheTimeToLive: TimeSpan.FromHours(1));
        }

        private static async Task RunDemoAsync(CosmosClient client)
        {
            Database database = client.GetDatabase(Program.databaseId);
            Container container = database.GetContainer(Program.containerId);

            string orderId = Guid.NewGuid().ToString();
            string account = "Account1";
            SalesOrder order = Program.GetSalesOrderSample(account, orderId);

            // Save the sales order into the container - all properties marked with the Encrypt attribute on the SalesOrder class
            // are encrypted using the encryption key referenced below before sending to the Azure Cosmos DB service.
            await container.CreateItemAsync(
                order,
                new PartitionKey(order.AccountNumber),
                new ItemRequestOptions
                {
                    EncryptionOptions = new EncryptionOptions
                    {
                        EncryptionKey = database.GetDataEncryptionKey(Program.myEncryptionKeyName)
                    }
                });

            // Read the item back - decryption happens automatically as the data contains the wrapped form of the encryption key and
            // metadata in order to unwrap it.
            ItemResponse<SalesOrder> readResponse = await container.ReadItemAsync<SalesOrder>(orderId, new PartitionKey(account));
            SalesOrder readOrder = readResponse.Resource;
        }

        private static SalesOrder GetSalesOrderSample(string account, string orderId)
        {
            SalesOrder salesOrder = new SalesOrder
            {
                Id = orderId,
                AccountNumber = account,
                PurchaseOrderNumber = "PO18009186470",
                OrderDate = new DateTime(2005, 7, 1),
                SubTotal = 419.4589m,
                TaxAmount = 12.5838m,
                Freight = 472.3108m,
                TotalDue = 985.018m,
                Items = new SalesOrderDetail[]
                {
                    new SalesOrderDetail
                    {
                        OrderQty = 1,
                        ProductId = 760,
                        UnitPrice = 419.4589m,
                        LineTotal = 419.4589m
                    }
                },
            };

            // Set the "ttl" property to auto-expire sales orders in 30 days 
            salesOrder.TimeToLive = 60 * 60 * 24 * 30;

            return salesOrder;
        }


        private static async Task CleanupAsync()
        {
            if (Program.database != null)
            {
                await Program.database.DeleteAsync();
            }
        }
    }
}
