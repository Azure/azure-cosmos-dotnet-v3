namespace Cosmos.Samples.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json;

    internal class UserManagementProgram
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
                using (CosmosClient client = new CosmosClient(endpoint, authKey))
                {
                    Database database = null;
                    try
                    {
                        using (await client.GetDatabase("UserManagementDemoDb").DeleteStreamAsync()) { }

                        // Get, or Create, the Database
                        database = await client.CreateDatabaseIfNotExistsAsync("UserManagementDemoDb");

                        await UserManagementProgram.RunDemoAsync(
                            client,
                            database);
                    }
                    finally
                    {
                        if (database != null)
                        {
                            await database.DeleteStreamAsync();
                        }
                    }

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
        private static async Task RunDemoAsync(
            CosmosClient client,
            Database database)
        {
            //--------------------------------------------------------------------------------------------------
            // We need Two Containers, Two Users, and some permissions for this sample,
            // So let's go ahead and set these up initially
            //--------------------------------------------------------------------------------------------------

            // Get, or Create, two separate Containers
            Container container1 = await database.CreateContainerAsync(
                id: "Container1",
                partitionKeyPath: "/AccountNumber");

            Container container2 = await database.CreateContainerAsync(
                id: "Container2",
                partitionKeyPath: "/AccountNumber");

            // Insert two documents in to col1
            SalesOrder salesOrder1 = new SalesOrder()
            {
                Id = "order1",
                AccountNumber = "partitionKey1"
            };

            await container1.CreateItemAsync<SalesOrder>(
                salesOrder1,
                new PartitionKey(salesOrder1.AccountNumber));

            SalesOrder salesOrder2 = new SalesOrder()
            {
                Id = "order2",
                AccountNumber = "pk2"
            };

            await container1.CreateItemAsync<SalesOrder>(
                salesOrder2,
                new PartitionKey(salesOrder2.AccountNumber));

            // Create a user
            User user1 = await database.CreateUserAsync("Thomas Andersen");

            // Get an existing user and permission.
            // This is a client side reference and does no verification against Cosmos DB.
            user1 = database.GetUser("Thomas Andersen");

            // Verify the user exists
            UserProperties userProperties = await user1.ReadAsync();

            //Add the read permission to the user and validate the user can 
            //read only the container it has access to
            await ValidateReadPermissions(
                client.Endpoint.OriginalString,
                database.Id,
                container1,
                user1);

            // Insert one item in to container 2
            SalesOrder salesOrder3 = new SalesOrder()
            {
                Id = "doc3",
                AccountNumber = "partitionKey"
            };

            await container2.CreateItemAsync<SalesOrder>(
                salesOrder3,
                new PartitionKey(salesOrder3.AccountNumber));

            // Create a new user
            User user2 = await database.CreateUserAsync("Robin Wakefield");

            //Add the all permission to the user for a single item and validate the user can 
            //only access the single item
            await ValidateAllPermissionsForItem(
                client.Endpoint.OriginalString,
                database.Id,
                container2,
                user2,
                salesOrder3);

            // Add read permission to user1 on container 2 so query has multiple results
            PermissionResponse permissionUser1Container2Response = await user1.CreatePermissionAsync(
                new PermissionProperties(
                    id: "permissionUser1Container2",
                    permissionMode: PermissionMode.Read,
                    container: container2));

            Permission permissionUser1Container2 = permissionUser1Container2Response;
            PermissionProperties user1Container2Properties = permissionUser1Container2Response;

            Console.WriteLine();
            Console.WriteLine($"Created {permissionUser1Container2.Id} with resource URI: {user1Container2Properties.ResourceUri}");

            // Get an existing permission and token
            permissionUser1Container2 = user1.GetPermission("permissionUser1Container2");

            // Get an existing permission properties
            user1Container2Properties = await permissionUser1Container2.ReadAsync();
            Console.WriteLine($"Read existing {permissionUser1Container2.Id} with resource URI: {user1Container2Properties.ResourceUri}");

            // All user1's permissions in a List
            List<PermissionProperties> user1Permissions = new List<PermissionProperties>();
            using (FeedIterator<PermissionProperties> feedIterator = user1.GetPermissionQueryIterator<PermissionProperties>())
            {
                while (feedIterator.HasMoreResults)
                {
                    FeedResponse<PermissionProperties> permissions = await feedIterator.ReadNextAsync();
                    user1Permissions.AddRange(permissions);
                }
            }

        }
        // </RunDemoAsync>

        // <ValidateAllPermissionsForItem>
        private static async Task ValidateAllPermissionsForItem(
            string endpoint,
            string databaseName,
            Container container,
            User user,
            SalesOrder salesOrder)
        {
            // All Permissions on Doc1 for user1
            PermissionProperties allPermissionForItem = new PermissionProperties(
                id: "permissionUserSaleOrder",
                permissionMode: PermissionMode.All,
                container: container,
                resourcePartitionKey: new PartitionKey(salesOrder.AccountNumber));

            PermissionProperties allItemPermission = await user.CreatePermissionAsync(allPermissionForItem);

            // Create a new client with the generated token
            using (CosmosClient permissionClient = new CosmosClient(endpoint, allItemPermission.Token))
            {
                Container permissionContainer = permissionClient.GetContainer(databaseName, container.Id);

                SalesOrder readSalesOrder = await permissionContainer.ReadItemAsync<SalesOrder>(
                    salesOrder.Id,
                    new PartitionKey(salesOrder.AccountNumber));
                Console.WriteLine();
                Console.WriteLine("Read item will all permission succeeded.");

                // Read sales order item
                readSalesOrder.OrderDate = DateTime.UtcNow;

                // Write sales order item
                await permissionContainer.UpsertItemAsync<SalesOrder>(
                    readSalesOrder,
                    new PartitionKey(salesOrder.AccountNumber));
                Console.WriteLine("Upsert item will all permission succeeded.");

                //try iterate items should fail because the user only has access to single partition key
                //and therefore cannot access anything outside of that partition key value.
                try
                {
                    using (FeedIterator<SalesOrder> itemIterator = permissionContainer.GetItemQueryIterator<SalesOrder>(
                        "select * from T"))
                    {
                        while (itemIterator.HasMoreResults)
                        {
                            await itemIterator.ReadNextAsync();
                            throw new ApplicationException("Should never get here");
                        }
                    }
                        
                }
                catch (CosmosException ce) when (ce.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    Console.WriteLine("Item query failed because user has access to only 1 partition");
                }
            }
        }
        // </ValidateAllPermissionsForItem>

        // <ValidateReadPermissions>
        private static async Task ValidateReadPermissions(
            string endpoint,
            string databaseName,
            Container container,
            User user)
        {
            // Read Permission on container for the user
            PermissionProperties readPermission = new PermissionProperties(
                id: "Read",
                permissionMode: PermissionMode.Read,
                container: container);

            PermissionProperties readContainerPermission = await user.CreatePermissionAsync(readPermission);

            // Create a new client with the generated token
            using (CosmosClient readClient = new CosmosClient(endpoint, readContainerPermission.Token))
            {
                Container readContainer = readClient.GetContainer(databaseName, container.Id);

                //try read items should succeed because user1 was granted Read permission on container1
                using (FeedIterator<SalesOrder> feedIterator = readContainer.GetItemQueryIterator<SalesOrder>())
                {
                    while (feedIterator.HasMoreResults)
                    {
                        FeedResponse<SalesOrder> salesOrders = await feedIterator.ReadNextAsync();
                        foreach (SalesOrder salesOrder in salesOrders)
                        {
                            Console.WriteLine(JsonConvert.SerializeObject(salesOrder));
                        }
                    }
                }

                //try iterate databases should fail because the user has no Admin rights 
                //but only read access to a single container and therefore
                //cannot access anything outside of that container.
                try
                {
                    using (FeedIterator<DatabaseProperties> databaseIterator = readClient.GetDatabaseQueryIterator<DatabaseProperties>("select T.* from T"))
                    {
                        while (databaseIterator.HasMoreResults)
                        {
                            await databaseIterator.ReadNextAsync();
                            throw new ApplicationException("Should never get here");
                        }
                    }
                }
                catch (CosmosException ce) when (ce.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    Console.WriteLine("Database query failed because user has no admin rights");
                }
            }
        }
        // </ValidateReadPermissions>
    }
}
