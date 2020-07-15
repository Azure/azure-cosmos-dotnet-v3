namespace Cosmos.Samples.Encryption
{
    using System;
    using System.Collections.Generic;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using Cosmos.Samples.Shared;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Encryption;
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
        private const string containerId = "encryptedData";
        private const string keyContainerId = "keyContainer";
        private const string dataEncryptionKeyId1 = "theDEK1";
        private const string dataEncryptionKeyId2 = "theDEK2";
        private const string dataEncryptionKeyId3 = "theDEK3";
        private const string dataEncryptionKeyId4 = "theDEK4";

        private static readonly JsonSerializer Serializer = new JsonSerializer();

        private static CosmosClient client = null;

        private static Container containerWithEncryption = null;

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

                Program.client = Program.CreateClientInstance(configuration);
                await Program.InitializeAsync(client, configuration);
                await Program.RunDemoAsync(client);
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

            return new CosmosClientBuilder(endpoint, authKey).Build();
        }

        private static X509Certificate2 GetCertificate(string clientCertThumbprint)
        {
            X509Store store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);
            X509Certificate2Collection certs = store.Certificates.Find(X509FindType.FindByThumbprint, clientCertThumbprint, false);
            store.Close();
            
            if(certs.Count == 0)
            {
                throw new ArgumentException("Certificate with thumbprint not found in LocalMachine certificate store");
            }

            return certs[0];
        }

        /// <summary>
        /// Administrative operations - create the database, container, and generate the necessary data encryption keys.
        /// These are initializations and are expected to be invoked only once - do not invoke these before every item request.
        /// </summary>
        private static async Task InitializeAsync(CosmosClient client, IConfigurationRoot configuration)
        {
            Database database = await client.CreateDatabaseIfNotExistsAsync(Program.databaseId);

            // Delete the existing container to prevent create item conflicts.
            using (await database.GetContainer(Program.containerId).DeleteContainerStreamAsync())
            { }

            Console.WriteLine("The demo will create a 1000 RU/s container, press any key to continue.");
            Console.ReadKey();

            // Create a container with the appropriate partition key definition (we choose the "AccountNumber" property here) and throughput (we choose 1000 here).
            Container container = await database.DefineContainer(Program.containerId, "/AccountNumber").CreateAsync(throughput: 1000);

            // Application credentials for authentication with Azure Key Vault.
            // This application must have keys/wrapKey and keys/unwrapKey permissions
            // on the keys that will be used for encryption.
            string clientId = configuration["ClientId"];
            if (string.IsNullOrEmpty(clientId))
            {
                throw new ArgumentNullException("Please specify a valid ClientId in the appSettings.json");
            }

            // Certificate's public key must be at least 2048 bits.
            string clientCertThumbprint = configuration["ClientCertThumbprint"];
            if (string.IsNullOrEmpty(clientCertThumbprint))
            {
                throw new ArgumentNullException("Please specify a valid ClientCertThumbprint in the appSettings.json");
            }
            /* SANTOSH:sealed class not derivation Core Calls to init*/
            /*provides wrap/unwarp, the DEK and finally the Encrypt and Decrypt*/
            /*Get the Client Certificate by passing all relevant info like client id*/
            AzureKeyVaultCosmosEncryptor encryptor = new AzureKeyVaultCosmosEncryptor(
                clientId,
                Program.GetCertificate(clientCertThumbprint));

            await encryptor.InitializeAsync(database, Program.keyContainerId);

            Program.containerWithEncryption = container.WithEncryptor(encryptor);


            // Master key identifier: https://{keyvault-name}.vault.azure.net/{object-type}/{object-name}/{object-version}
            string masterKeyUrlFromConfig = configuration["MasterKeyUrl1"];
            if (string.IsNullOrEmpty(masterKeyUrlFromConfig))
            {
                throw new ArgumentException("Please specify a valid MasterKeyUrl in the appSettings.json");
            }

            Uri masterKeyUri1 = new Uri(masterKeyUrlFromConfig);            

            /*Another Key Vault2*/

            string masterKeyUrlFromConfig2= configuration["MasterKeyUrl2"];
            if (string.IsNullOrEmpty(masterKeyUrlFromConfig2))
            {
                throw new ArgumentException("Please specify a valid MasterKeyUrl1 in the appSettings.json");
            }

            Uri masterKeyUri2 = new Uri(masterKeyUrlFromConfig2);

            /*Another Key Vault3*/

            string masterKeyUrlFromConfig3 = configuration["MasterKeyUrl3"];
            if (string.IsNullOrEmpty(masterKeyUrlFromConfig3))
            {
                throw new ArgumentException("Please specify a valid MasterKeyUrl1 in the appSettings.json");
            }

            Uri masterKeyUri3 = new Uri(masterKeyUrlFromConfig3);

            string masterKeyUrlFromConfig4 = configuration["MasterKeyUrl4"];
            if (string.IsNullOrEmpty(masterKeyUrlFromConfig4))
            {
                throw new ArgumentException("Please specify a valid MasterKeyUrl1 in the appSettings.json");
            }

            Uri masterKeyUri4 = new Uri(masterKeyUrlFromConfig4);

            AzureKeyVaultKeyWrapMetadata wrapMetadata1 = new AzureKeyVaultKeyWrapMetadata(masterKeyUri1);
            AzureKeyVaultKeyWrapMetadata wrapMetadata2 = new AzureKeyVaultKeyWrapMetadata(masterKeyUri2);
            AzureKeyVaultKeyWrapMetadata wrapMetadata3 = new AzureKeyVaultKeyWrapMetadata(masterKeyUri3);
            AzureKeyVaultKeyWrapMetadata wrapMetadata4 = new AzureKeyVaultKeyWrapMetadata(masterKeyUri4);

            /// Generates an encryption key, wraps it using the key wrap metadata provided
            /// with the key wrapping provider configured on the client
            /// and saves the wrapped encryption key as an asynchronous operation in the Azure Cosmos service.
            await encryptor.DataEncryptionKeyContainer.CreateDataEncryptionKeyAsync(
                dataEncryptionKeyId1,
                CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized,
                wrapMetadata1);

            await encryptor.DataEncryptionKeyContainer.CreateDataEncryptionKeyAsync(
                dataEncryptionKeyId2,
                CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized,
                wrapMetadata2);

            await encryptor.DataEncryptionKeyContainer.CreateDataEncryptionKeyAsync(
                dataEncryptionKeyId3,
                CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized,
                wrapMetadata3);

            await encryptor.DataEncryptionKeyContainer.CreateDataEncryptionKeyAsync(
                dataEncryptionKeyId4,
                CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized,
                wrapMetadata4);
        }

        private static async Task RunDemoAsync(CosmosClient client)
        {   /*in items, the id value and / PK path*/
            string orderId1 = Guid.NewGuid().ToString();
            string orderId2 = Guid.NewGuid().ToString();
            string orderId3 = Guid.NewGuid().ToString();
            string orderId4 = Guid.NewGuid().ToString();
            string orderId5 = Guid.NewGuid().ToString();
            string orderId6 = Guid.NewGuid().ToString();
            string account1 = "Account1";
            string account2 = "Account2";
            string account3 = "Account3";
            string account4 = "Account4";
            string account5 = "Account5";
            string account6 = "Account6";
            SalesOrder order1 = Program.GetSalesOrderSample(account1, orderId1);
            SalesOrder order2 = Program.GetSalesOrderSample(account2, orderId2);
            SalesOrder order3 = Program.GetSalesOrderSample(account3, orderId3);
            SalesOrder order4 = Program.GetSalesOrderSample(account4, orderId4);
            SalesOrder order5 = Program.GetSalesOrderSample(account5, orderId5);
            SalesOrder order6 = Program.GetSalesOrderSample(account6, orderId6);

            // Save the sales order into the container - all properties marked with the Encrypt attribute on the SalesOrder class
            // are encrypted using the encryption key referenced below before sending to the Azure Cosmos DB service.

            Console.WriteLine("-- Staring Timer --");
            System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();

            await Program.containerWithEncryption.CreateItemAsync(
                order1,
                new PartitionKey(order1.AccountNumber),
                new EncryptionItemRequestOptions
                {
                    EncryptionOptions = new EncryptionOptions
                    {
                        DataEncryptionKeyId = Program.dataEncryptionKeyId1,
                        EncryptionAlgorithm = CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized,
                        PathsToEncrypt = new List<string> { "/TotalDue" }
                    }
                });
            
            await Program.containerWithEncryption.CreateItemAsync(
                order2,
                new PartitionKey(order2.AccountNumber),
                new EncryptionItemRequestOptions
                {
                    EncryptionOptions = new EncryptionOptions
                    {
                        DataEncryptionKeyId = Program.dataEncryptionKeyId2,
                        EncryptionAlgorithm = CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized,
                        PathsToEncrypt = new List<string> { "/OrderDate" }
                    }
                });

            await Program.containerWithEncryption.CreateItemAsync(
            order3,
            new PartitionKey(order3.AccountNumber),
            new EncryptionItemRequestOptions
            {
                EncryptionOptions = new EncryptionOptions
                {
                    DataEncryptionKeyId = Program.dataEncryptionKeyId3,
                    EncryptionAlgorithm = CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized,
                    PathsToEncrypt = new List<string> { "/Freight" }
                }
            });

            await Program.containerWithEncryption.CreateItemAsync(
            order4,
            new PartitionKey(order4.AccountNumber),
            new EncryptionItemRequestOptions
            {
                EncryptionOptions = new EncryptionOptions
                {
                    DataEncryptionKeyId = Program.dataEncryptionKeyId1,
                    EncryptionAlgorithm = CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized,
                    PathsToEncrypt = new List<string> { "/TaxAmount" }
                }
            });

            await Program.containerWithEncryption.CreateItemAsync(
            order5,
            new PartitionKey(order5.AccountNumber),
            new EncryptionItemRequestOptions
            {
                EncryptionOptions = new EncryptionOptions
                {
                    DataEncryptionKeyId = Program.dataEncryptionKeyId2,
                    EncryptionAlgorithm = CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized,
                    PathsToEncrypt = new List<string> { "/ShippedDate" }
                }
            });

            await Program.containerWithEncryption.CreateItemAsync(
            order6,
            new PartitionKey(order6.AccountNumber),
            new EncryptionItemRequestOptions
            {
                EncryptionOptions = new EncryptionOptions
                {
                    DataEncryptionKeyId = Program.dataEncryptionKeyId4,
                    EncryptionAlgorithm = CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized,
                    PathsToEncrypt = new List<string> { "/SubTotal" }
                }
            });

            // Read the item back - decryption happens automatically as the data contains the reference to the wrapped form of the encryption key and
            // metadata in order to unwrap it.
            ItemResponse<SalesOrder> readResponse1 = await Program.containerWithEncryption.ReadItemAsync<SalesOrder>(orderId1, new PartitionKey(account1));
            SalesOrder readOrder1 = readResponse1.Resource;

            Console.WriteLine("1.Total due: {0} After roundtripping: {1}", order1.TotalDue, readOrder1.TotalDue);
            
            ItemResponse<SalesOrder> readResponse2 = await Program.containerWithEncryption.ReadItemAsync<SalesOrder>(orderId2, new PartitionKey(account2));
            SalesOrder readOrder2 = readResponse2.Resource;

            Console.WriteLine("2.OrderDate: {0} After roundtripping: {1}", order2.OrderDate, readOrder2.OrderDate);

            ItemResponse<SalesOrder> readResponse3 = await Program.containerWithEncryption.ReadItemAsync<SalesOrder>(orderId3, new PartitionKey(account3));
            SalesOrder readOrder3 = readResponse3.Resource;

            Console.WriteLine("3.Freight: {0} After roundtripping: {1}", order3.Freight, readOrder3.Freight);

            ItemResponse<SalesOrder> readResponse4 = await Program.containerWithEncryption.ReadItemAsync<SalesOrder>(orderId4, new PartitionKey(account4));
            SalesOrder readOrder4 = readResponse4.Resource;

            Console.WriteLine("4.TaxAmount: {0} After roundtripping: {1}", order4.TaxAmount, readOrder4.TaxAmount);

            ItemResponse<SalesOrder> readResponse5 = await Program.containerWithEncryption.ReadItemAsync<SalesOrder>(orderId5, new PartitionKey(account5));
            SalesOrder readOrder5 = readResponse5.Resource;

            Console.WriteLine("5.ShippedDate: {0} After roundtripping: {1}", order5.ShippedDate, readOrder5.ShippedDate);

            ItemResponse<SalesOrder> readResponse6 = await Program.containerWithEncryption.ReadItemAsync<SalesOrder>(orderId6, new PartitionKey(account6));
            SalesOrder readOrder6 = readResponse6.Resource;

            Console.WriteLine("6.SubTotal: {0} After roundtripping: {1}", order6.SubTotal, readOrder6.SubTotal);
            watch.Stop();
            long elapsedMs = watch.ElapsedMilliseconds/1000;
            Console.WriteLine("Total time spent {0} seconds", elapsedMs);

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
            if (Program.client != null)
            {
                await Program.client.GetDatabase(databaseId).DeleteStreamAsync();
            }
        }
    }
}
