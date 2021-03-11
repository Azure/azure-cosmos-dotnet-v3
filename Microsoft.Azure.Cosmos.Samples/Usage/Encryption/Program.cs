namespace Cosmos.Samples.Encryption
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Cosmos.Samples.Shared;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Encryption;
    using Microsoft.Data.Encryption.Cryptography;
    using Microsoft.Extensions.Configuration;

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
        private const string encrypteddatabaseId = "encryptedDb";
        private const string encryptedcontainerId = "encryptedData";

        private static CosmosClient client = null;

        private static Container containerWithEncryption = null;

        // <Main>
#pragma warning disable IDE0060 // Remove unused parameter
        public static async Task Main(string[] args)
#pragma warning restore IDE0060 // Remove unused parameter
        {
            try
            {
                // Read the Cosmos endpointUrl and authorizationKey from configuration.
                // These values are available from the Azure Management Portal on the Cosmos Account Blade under "Keys".
                // Keep these values in a safe and secure location. Together they provide administrative access to your Cosmos account.
                IConfigurationRoot configuration = new ConfigurationBuilder()
                    .AddJsonFile("appSettings.json")
                    .Build();

                Program.client = Program.CreateClientInstance(configuration);
                await Program.AdminSetupAsync(client);
                await Program.RunDemoAsync();
            }
            catch (CosmosException cre)
            {
                Console.WriteLine(cre.ToString());
            }
            catch (Exception e)
            {
                Exception baseException = e.GetBaseException();
                Console.WriteLine("Message: {0} Error: {1}", baseException.Message, e);
            }
            finally
            {
                Console.WriteLine("End of demo, press any key to exit.");
                Console.ReadKey();
                await Program.CleanupAsync();
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

            CosmosClient encryptionCosmosClient = new CosmosClient(endpoint, authKey);

            // enable encryption support on the cosmos client.
            return encryptionCosmosClient.WithEncryption(new TestEncryptionKeyStoreProvider());
        }

        /// <summary>
        /// Administrative operations - create the database, container, and generate the necessary client encryption keys.
        /// These are initializations and are expected to be invoked only once - do not invoke these before every item request.
        /// </summary>
        private static async Task AdminSetupAsync(CosmosClient client)
        {
            Database database = await client.CreateDatabaseIfNotExistsAsync(Program.encrypteddatabaseId);

            // Delete the existing container to prevent create item conflicts.
            using (await database.GetContainer(Program.encryptedcontainerId).DeleteContainerStreamAsync())
            { }

            Console.WriteLine("The demo will create a 1000 RU/s container, press any key to continue.");
            Console.ReadKey();

            // Create the Client Encryption Keys for Encrypting the configured Paths.
            await database.CreateClientEncryptionKeyAsync(
                    "key1",
                    DataEncryptionKeyAlgorithm.AEAD_AES_256_CBC_HMAC_SHA256,
                    new EncryptionKeyWrapMetadata("key1", "metadata1"));

            await database.CreateClientEncryptionKeyAsync(
                    "key2",
                    DataEncryptionKeyAlgorithm.AEAD_AES_256_CBC_HMAC_SHA256,
                    new EncryptionKeyWrapMetadata("key2", "metadata2"));

            // Configure the required Paths to be Encrypted with appropriate settings.
            ClientEncryptionIncludedPath path1 = new ClientEncryptionIncludedPath()
            {
                Path = "/SubTotal",
                ClientEncryptionKeyId = "key1",
                EncryptionType = EncryptionType.Deterministic.ToString(),
                EncryptionAlgorithm = DataEncryptionKeyAlgorithm.AEAD_AES_256_CBC_HMAC_SHA256.ToString()
            };

            // non primitive data type.Leaves get encrypted.
            ClientEncryptionIncludedPath path2 = new ClientEncryptionIncludedPath()
            {
                Path = "/Items",
                ClientEncryptionKeyId = "key2",
                EncryptionType = EncryptionType.Deterministic.ToString(),
                EncryptionAlgorithm = DataEncryptionKeyAlgorithm.AEAD_AES_256_CBC_HMAC_SHA256.ToString()
            };

            ClientEncryptionIncludedPath path3 = new ClientEncryptionIncludedPath()
            {
                Path = "/OrderDate",
                ClientEncryptionKeyId = "key1",
                EncryptionType = EncryptionType.Deterministic.ToString(),
                EncryptionAlgorithm = DataEncryptionKeyAlgorithm.AEAD_AES_256_CBC_HMAC_SHA256.ToString()
            };

            // Create a container with the appropriate partition key definition (we choose the "AccountNumber" property here) and throughput (we choose 1000 here).
            // Configure the Client Encryption Key Policy with required paths to be encrypted.
            await database.DefineContainer(Program.encryptedcontainerId, "/AccountNumber")
                .WithClientEncryptionPolicy()
                .WithIncludedPath(path1)
                .WithIncludedPath(path2)
                .WithIncludedPath(path3)
                .Attach()
                .CreateAsync(throughput: 1000);

            // gets a Container with Encryption Support.
            containerWithEncryption = await database.GetContainer(Program.encryptedcontainerId).InitializeEncryptionAsync();                               
        }

        private static async Task RunDemoAsync()
        {
            SalesOrder order1 = Program.GetSalesOrderSample("Account1", Guid.NewGuid().ToString());
            SalesOrder order2 = Program.GetSalesOrderSample("Account2", Guid.NewGuid().ToString());

            // Save the sales order into the container - all properties configured with Encryption Policy on the SalesOrder class
            // are encrypted using the encryption key per the policy configured for the path before sending to the Azure Cosmos DB service.
            await Program.containerWithEncryption.CreateItemAsync(
                order1,
                new PartitionKey(order1.AccountNumber));

            // Read the item back - decryption happens automatically based on the Encryption Policy configured for the Container.
            ItemResponse<SalesOrder> readResponse = await Program.containerWithEncryption.ReadItemAsync<SalesOrder>(order1.Id, new PartitionKey(order1.AccountNumber));
            SalesOrder readOrder = readResponse.Resource;

            Console.WriteLine("Creating Document 1: SubTotal : {0} After roundtripping post Decryption: {1}", order1.SubTotal, readOrder.SubTotal);

            order2.SubTotal = 552.4589m;
            await Program.containerWithEncryption.CreateItemAsync(
               order2,
               new PartitionKey(order2.AccountNumber));

            // Read the item back - decryption happens automatically based on the Encryption Policy configured for the Container.
            readResponse = await Program.containerWithEncryption.ReadItemAsync<SalesOrder>(order2.Id, new PartitionKey(order2.AccountNumber));
            readOrder = readResponse.Resource;

            Console.WriteLine("Creating Document 2: SubTotal : {0} After roundtripping post Decryption: {1}", order2.SubTotal, readOrder.SubTotal);

            // Query Demo.
            // Here SubTotal and OrderDate are encrypted properties.
            QueryDefinition withEncryptedParameter = containerWithEncryption.CreateQueryDefinition(
                    "SELECT * FROM c where c.SubTotal = @SubTotal AND c.OrderDate = @OrderDate");

            await withEncryptedParameter.AddParameterAsync(
                    "@SubTotal",
                    order2.SubTotal,
                    "/SubTotal");

            await withEncryptedParameter.AddParameterAsync(
                    "@OrderDate",
                    order2.OrderDate,
                    "/OrderDate");

            FeedIterator<SalesOrder> queryResponseIterator;
            queryResponseIterator = containerWithEncryption.GetItemQueryIterator<SalesOrder>(withEncryptedParameter);

            FeedResponse<SalesOrder> readDocs = await queryResponseIterator.ReadNextAsync();
            Console.WriteLine("1) Query result: SELECT * FROM c where c.SubTotal = {0} AND c.OrderDate = {1}. Total Documents : {2} ", order2.SubTotal, order2.OrderDate, readDocs.Count);

            withEncryptedParameter = new QueryDefinition(
                    "SELECT c.SubTotal FROM c");

            queryResponseIterator = containerWithEncryption.GetItemQueryIterator<SalesOrder>(withEncryptedParameter);

            readDocs = await queryResponseIterator.ReadNextAsync();
            Console.WriteLine("2) Query result: SELECT c.SubTotal FROM c. Total Documents : {0} ", readDocs.Count);
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
                    },

                    new SalesOrderDetail
                    {
                        OrderQty = 2,
                        ProductId = 761,
                        UnitPrice = 420.4589m,
                        LineTotal = 420.4589m
                    }
                },
            };

            // Set the "ttl" property to auto-expire sales orders in 30 days 
            salesOrder.TimeToLive = 60 * 60 * 24 * 30;

            return salesOrder;
        }


        private static async Task CleanupAsync()
        {
            if (Program.client != null)
            {
                await Program.client.GetDatabase(encrypteddatabaseId).DeleteStreamAsync();
                client.Dispose();
            }
        }
    }

    internal class TestEncryptionKeyStoreProvider : EncryptionKeyStoreProvider
    {
        public override string ProviderName => "TESTKEYSTORE_VAULT";

        public override byte[] UnwrapKey(string masterKeyPath, KeyEncryptionKeyAlgorithm encryptionAlgorithm, byte[] encryptedKey)
        {
            byte[] plainkey = encryptedKey.Select(b => (byte)(b - 1)).ToArray();
            return plainkey;
        }

        public override byte[] WrapKey(string masterKeyPath, KeyEncryptionKeyAlgorithm encryptionAlgorithm, byte[] key)
        {
            byte[] encryptedkey = key.Select(b => (byte)(b + 1)).ToArray();
            return encryptedkey;
        }

        public override byte[] Sign(string masterKeyPath, bool allowEnclaveComputations)
        {
            return null;
        }

        public override bool Verify(string masterKeyPath, bool allowEnclaveComputations, byte[] signature)
        {
            return true;
        }
    }
}
