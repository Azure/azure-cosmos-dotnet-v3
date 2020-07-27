namespace Cosmos.Samples.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json;

    /// <summary>
    /// ----------------------------------------------------------------------------------------------------------
    /// Prerequisites - 
    /// 
    /// 1. An Azure Cosmos account - 
    ///    https://docs.microsoft.com/azure/cosmos-db/create-cosmosdb-resources-portal
    ///
    /// 2. Microsoft.Azure.Cosmos NuGet package - 
    ///    http://www.nuget.org/packages/Microsoft.Azure.Cosmos/ 
    /// ----------------------------------------------------------------------------------------------------------
    /// Sample - Demonstrates the basic CRUD operations on Container that is migrated from Non-Partitioned mode to
    /// Partitioned mode.
    ///
    /// These include the following operations:
    ///    1. Document CRUD operations in the same logical partition as pre-migration
    ///    2. Document CRUD operations with a partition key value on the migrated container
    ///    3. Migration of documents inserted without partition key into a logical parition with a valid partition key value
    ///
    ///
    /// Note: This sample is written for V3 SDK and since V3 SDK doesn't allow creating a container without partition key,
    ///       this sample uses REST API to perform such operation.
    /// ----------------------------------------------------------------------------------------------------------
    /// </summary>

    public class Program
    {
        private static readonly string PreNonPartitionedMigrationApiVersion = "2018-09-17";
        private static readonly string utc_date = DateTime.UtcNow.ToString("r");
        private static readonly JsonSerializer Serializer = new JsonSerializer();

        private static string databaseId = null;
        private static string containerId = null;

        public class DeviceInformationItem
        {
            [JsonProperty(PropertyName = "id")]
            public string Id { get; set; }

            [JsonProperty(PropertyName = "deviceId")]
            public string DeviceId { get; set; }

            [JsonProperty(PropertyName = "_partitionKey", NullValueHandling = NullValueHandling.Ignore)]
            public string PartitionKey { get; set; }
        }

        // <Main>
        public static async Task Main(string[] args)
        {
            try
            {
                databaseId = "deviceInformation" + Guid.NewGuid().ToString();
                containerId = "device-samples" + Guid.NewGuid().ToString();

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

                using (CosmosClient client = new CosmosClient(endpoint, authKey))
                {
                    Database database = await client.CreateDatabaseIfNotExistsAsync(databaseId);

                    // Create the container using REST API without a partition key definition
                    await Program.CreateNonPartitionedContainerAsync(endpoint, authKey);

                    Container container = database.GetContainer(containerId);

                    // Read back the same container and verify that partition key path is populated
                    // Partition key is returned when read from V3 SDK.
                    ContainerResponse containerResposne = await container.ReadContainerAsync();
                    if (containerResposne.Resource.PartitionKeyPath != null)
                    {
                        Console.WriteLine("Container Partition Key path {0}", containerResposne.Resource.PartitionKeyPath);
                    }
                    else
                    {
                        throw new Exception("Unexpected error : Partition Key is not populated in a migrated collection");
                    }

                    Console.WriteLine("--Demo Item operations with no partition key--");
                    await Program.ItemOperationsWithNonePartitionKeyValue(container);

                    Console.WriteLine("--Demo Item operations with valid partition key--");
                    await Program.ItemOperationsWithValidPartitionKeyValue(container);

                    Console.WriteLine("--Demo migration of items inserted with no partition key to items with a partition key--");
                    await Program.MigratedItemsFromNonePartitionKeyToValidPartitionKeyValue(container);

                    // Clean up the database -- for rerunning the sample
                    await database.DeleteAsync();
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
        /// The function demonstrates the Item CRUD operation using the NonePartitionKeyValue
        /// NonePartitionKeyValue represents the information that the current item doesn't have a value for partitition key
        /// All items inserted pre-migration are grouped into this logical partition and can be accessed by providing this value
        /// for the partitionKey parameter
        /// New item CRUD could be performed using this NonePartitionKeyValue to target the same logical partition
        /// </summary>
        // <ItemOperationsWithNonePartitionKeyValue>
        private static async Task ItemOperationsWithNonePartitionKeyValue(Container container)
        {
            string itemid = Guid.NewGuid().ToString();
            DeviceInformationItem itemWithoutPK = GetDeviceWithNoPartitionKey(itemid);

            // Insert a new item with NonePartitionKeyValue
            ItemResponse<DeviceInformationItem> createResponse = await container.CreateItemAsync<DeviceInformationItem>(
             item: itemWithoutPK,
             partitionKey: PartitionKey.None);
            Console.WriteLine("Creating Item {0} Status Code {1}", itemid, createResponse.StatusCode);

            // Read an existing item with NonePartitionKeyValue
            ItemResponse<DeviceInformationItem> readResponse = await container.ReadItemAsync<DeviceInformationItem>(
                id: itemid,
                partitionKey: PartitionKey.None
                );
            Console.WriteLine("Reading Item {0} Status Code {1}", itemid, readResponse.StatusCode);

            // Replace the content of existing item with NonePartitionKeyValue
            itemWithoutPK.DeviceId = Guid.NewGuid().ToString();
            ItemResponse<DeviceInformationItem> replaceResponse = await container.ReplaceItemAsync<DeviceInformationItem>(
                 item: itemWithoutPK,
                 id: itemWithoutPK.Id,
                 partitionKey: PartitionKey.None
                 );
            Console.WriteLine("Replacing Item {0} Status Code {1}", itemid, replaceResponse.StatusCode);

            // Delete an item with NonePartitionKeyValue.
            ItemResponse<DeviceInformationItem> deleteResponse = await container.DeleteItemAsync<DeviceInformationItem>(
                id: itemid,
                partitionKey: PartitionKey.None
                );
            Console.WriteLine("Deleting Item {0} Status Code {1}", itemid, deleteResponse.StatusCode);
        }
        // </ItemOperationsWithNonePartitionKeyValue>

        /// <summary>
        /// The function demonstrates CRUD operations on the migrated collection supplying a value for the partition key
        /// <summary>
        // <ItemOperationsWithValidPartitionKeyValue>
        private static async Task ItemOperationsWithValidPartitionKeyValue(Container container)
        {
            string itemid = Guid.NewGuid().ToString();
            string partitionKey = "a";
            DeviceInformationItem itemWithPK = GetDeviceWithPartitionKey(itemid, partitionKey);

            // Insert a new item
            ItemResponse<DeviceInformationItem> createResponse = await container.CreateItemAsync<DeviceInformationItem>(
             partitionKey: new PartitionKey(partitionKey),
             item: itemWithPK);
            Console.WriteLine("Creating Item {0} with Partition Key Status Code {1}", itemid, createResponse.StatusCode);

            // Read the item back
            ItemResponse<DeviceInformationItem> readResponse = await container.ReadItemAsync<DeviceInformationItem>(
                partitionKey: new PartitionKey(partitionKey),
                id: itemid);
            Console.WriteLine("Reading Item {0} with Partition Key Status Code {1}", itemid, readResponse.StatusCode);

            // Replace the content of the item
            itemWithPK.DeviceId = Guid.NewGuid().ToString();
            ItemResponse<DeviceInformationItem> replaceResponse = await container.ReplaceItemAsync<DeviceInformationItem>(
                 partitionKey: new PartitionKey(partitionKey),
                 id: itemWithPK.Id,
                 item: itemWithPK);
            Console.WriteLine("Replacing Item {0} with Partition Key Status Code {1}", itemid, replaceResponse.StatusCode);

            // Delete the item.
            ItemResponse<DeviceInformationItem> deleteResponse = await container.DeleteItemAsync<DeviceInformationItem>(
                partitionKey: new PartitionKey(partitionKey),
                id: itemid);
            Console.WriteLine("Deleting Item {0} with Partition Key Status Code {1}", itemid, deleteResponse.StatusCode);
        }
        // </ItemOperationsWithValidPartitionKeyValue>

        /// <summary>
        ///  The function demonstrates migrating documents that were inserted without a value for partition key, and those inserted
        ///  pre-migration to other logical partitions, those with a value for partition key.
        /// </summary>
        // <MigratedItemsFromNonePartitionKeyToValidPartitionKeyValue>
        private static async Task MigratedItemsFromNonePartitionKeyToValidPartitionKeyValue(Container container)
        {
            // Pre-create a few items in the container to demo the migration
            const int ItemsToCreate = 4;
            // Insert a few items with no Partition Key
            for (int i = 0; i < ItemsToCreate; i++)
            {
                string itemid = Guid.NewGuid().ToString();
                DeviceInformationItem itemWithoutPK = GetDeviceWithNoPartitionKey(itemid);
                ItemResponse<DeviceInformationItem> createResponse = await container.CreateItemAsync<DeviceInformationItem>(
                partitionKey: PartitionKey.None,
                item: itemWithoutPK);
            }

            // Query items on the container that have no partition key value by supplying NonePartitionKeyValue
            // The operation is made in batches to not lose work in case of partial execution
            int resultsFetched = 0;
            QueryDefinition sql = new QueryDefinition("select * from r");
            using (FeedIterator<DeviceInformationItem> setIterator = container.GetItemQueryIterator<DeviceInformationItem>(
                sql,
                requestOptions: new QueryRequestOptions()
                {
                    PartitionKey = PartitionKey.None,
                    MaxItemCount = 2
                }))
            {
                while (setIterator.HasMoreResults)
                {
                    FeedResponse<DeviceInformationItem> queryResponse = await setIterator.ReadNextAsync();
                    resultsFetched += queryResponse.Count();

                    // For the items returned with NonePartitionKeyValue
                    IEnumerator<DeviceInformationItem> iter = queryResponse.GetEnumerator();
                    while (iter.MoveNext())
                    {
                        DeviceInformationItem item = iter.Current;
                        if (item.DeviceId != null)
                        {
                            // Using existing deviceID for partition key
                            item.PartitionKey = item.DeviceId;
                            Console.WriteLine("Migrating item {0} to Partition {1}", item.Id, item.DeviceId);
                            // Re-Insert into container with a partition key
                            // This could result in exception if the same item was inserted in a previous run of the program on existing container
                            // and the program stopped before the delete.
                            ItemResponse<DeviceInformationItem> createResponseWithPk = await container.CreateItemAsync<DeviceInformationItem>(
                             partitionKey: new PartitionKey(item.PartitionKey),
                             item: item);

                            // Deleting item from fixed container with CosmosContainerSettings.NonePartitionKeyValue.
                            ItemResponse<DeviceInformationItem> deleteResponseWithoutPk = await container.DeleteItemAsync<DeviceInformationItem>(
                             partitionKey: PartitionKey.None,
                             id: item.Id);
                        }
                    }
                }
            }
        }
        // </MigratedItemsFromNonePartitionKeyToValidPartitionKeyValue>

        private static DeviceInformationItem GetDeviceWithPartitionKey(string itemId, string partitionKey)
        {
            return new DeviceInformationItem
            {
                Id = itemId,
                DeviceId = Guid.NewGuid().ToString(),
                PartitionKey = partitionKey
            };
        }

        private static DeviceInformationItem GetDeviceWithNoPartitionKey(string itemId)
        {
            return new DeviceInformationItem
            {
                Id = itemId,
                DeviceId = Guid.NewGuid().ToString(),
            };
        }

        private static async Task CreateNonPartitionedContainerAsync(string endpoint, string authKey)
        {
            // Creating non partition Container, REST api used instead of .NET SDK as creation without a partition key is not supported anymore.
            Console.WriteLine("Creating container without a partition key");
            HttpClient client = new System.Net.Http.HttpClient();
            Uri baseUri = new Uri(endpoint);
            string verb = "POST";
            string resourceType = "colls";
            string resourceId = string.Format("dbs/{0}", Program.databaseId);
            string resourceLink = string.Format("dbs/{0}/colls", Program.databaseId);
            client.DefaultRequestHeaders.Add("x-ms-date", Program.utc_date);
            client.DefaultRequestHeaders.Add("x-ms-version", Program.PreNonPartitionedMigrationApiVersion);

            string authHeader = GenerateMasterKeyAuthorizationSignature(verb, resourceId, resourceType, authKey, "master", "1.0");

            client.DefaultRequestHeaders.Add("authorization", authHeader);
            string containerDefinition = "{\n  \"id\": \"" + Program.containerId + "\"\n}";
            StringContent containerContent = new StringContent(containerDefinition);
            Uri requestUri = new Uri(baseUri, resourceLink);
            var response = await client.PostAsync(requestUri.ToString(), containerContent);
            Console.WriteLine("Create container response {0}", response.StatusCode);
        }

        private static string GenerateMasterKeyAuthorizationSignature(string verb, string resourceId, string resourceType, string key, string keyType, string tokenVersion)
        {
            System.Security.Cryptography.HMACSHA256 hmacSha256 = new System.Security.Cryptography.HMACSHA256 { Key = Convert.FromBase64String(key) };

            string payLoad = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}\n{1}\n{2}\n{3}\n{4}\n",
                    verb.ToLowerInvariant(),
                    resourceType.ToLowerInvariant(),
                    resourceId,
                    utc_date.ToLowerInvariant(),
                    ""
            );

            byte[] hashPayLoad = hmacSha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payLoad));
            string signature = Convert.ToBase64String(hashPayLoad);

            return System.Web.HttpUtility.UrlEncode(string.Format(System.Globalization.CultureInfo.InvariantCulture, "type={0}&ver={1}&sig={2}",
                keyType,
                tokenVersion,
                signature));
        }
    }
}

