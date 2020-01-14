namespace Cosmos.Samples.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Configuration;

    internal class Program
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

        private static async Task RunDemoAsync(CosmosClient client)
        {
            //--------------------------------------------------------------------------------------------------
            // We need a Database, Two Containers, Two Users, and some permissions for this sample,
            // So let's go ahead and set these up initially
            //--------------------------------------------------------------------------------------------------
            using (await client.GetDatabase("UserManagementDemoDb").DeleteStreamAsync()) { }
            using (await client.GetDatabase("UserManagementDemoDb2").DeleteStreamAsync()) { }

            // Get, or Create, the Database
            Database db = await client.CreateDatabaseIfNotExistsAsync("UserManagementDemoDb");
            Database db2 = await client.CreateDatabaseIfNotExistsAsync("UserManagementDemoDb2");

            // Get, or Create, two separate Collections
            Container container1 = await db.CreateContainerAsync(
                id: "COL1",
                partitionKeyPath: "/AccountNumber");

            Container container2 = await db.CreateContainerAsync(
                id: "COL2",
                partitionKeyPath: "/AccountNumber");

            // Insert two documents in to col1
            SalesOrder salesOrder1 = new SalesOrder()
            {
                Id = "order1",
                AccountNumber = "partitionKey1"
            };

            SalesOrder order1 = await container1.CreateItemAsync<SalesOrder>(
                salesOrder1,
                new PartitionKey(salesOrder1.AccountNumber));

            SalesOrder salesOrder2 = new SalesOrder()
            {
                Id = "order2",
                AccountNumber = "pk2"
            };

            SalesOrder order2 = await container1.CreateItemAsync<SalesOrder>(
                salesOrder2,
                new PartitionKey(salesOrder2.AccountNumber));

            // Insert one document in to col2
            SalesOrder salesOrder3 = new SalesOrder()
            {
                Id = "doc3",
                AccountNumber = "partitionKey"
            };

            SalesOrder order3 = await container2.CreateItemAsync<SalesOrder>(
                salesOrder3,
                new PartitionKey(salesOrder3.AccountNumber));

            // Create two users
            User user1 = await db.CreateUserAsync("Thomas Andersen");
            User user2 = await db.CreateUserAsync("Robin Wakefield");

            // Read Permission on container 1 for user1
            PermissionProperties readPermission = new PermissionProperties("Read", PermissionMode.Read, container1);
            PermissionProperties permissionUser1Col1 = await user1.CreatePermissionAsync(readPermission);

            // All Permissions on Doc1 for user1
            PermissionProperties permissionUser1Doc1 = new PermissionProperties(
                id: "permissionUser1Doc1",
                permissionMode: PermissionMode.All,
                container: container1,
                resourcePartitionKey: new PartitionKey(salesOrder1.AccountNumber),
                itemId: salesOrder1.Id);
            await user1.CreatePermissionAsync(permissionUser1Doc1);

            // Read Permissions on col2 for user1
            PermissionProperties permissionUser1Col2 = new PermissionProperties(
                id: "permissionUser1Col2",
                permissionMode: PermissionMode.Read,
                container: container2);
            await user1.CreatePermissionAsync(permissionUser1Col2);

            // All Permissions on col2 for user2
            PermissionProperties permissionUser2Col2 = new PermissionProperties(
               id: "permissionUser2Col2",
               permissionMode: PermissionMode.All,
               container: container2);
            await user2.CreatePermissionAsync(permissionUser1Col2);

            // All user1's permissions in a List
            List<PermissionProperties> user1Permissions = new List<PermissionProperties>();
            FeedIterator<PermissionProperties> feedIterator = user1.GetPermissionQueryIterator<PermissionProperties>();
            while (feedIterator.HasMoreResults)
            {
                FeedResponse<PermissionProperties> permissions = await feedIterator.ReadNextAsync();
                user1Permissions.AddRange(permissions);
            }

            //--------------------------------------------------------------------------------------------------
            // That takes care of the creating Users, Permissions on Resources, Linking user to permissions etc. 
            // Now let's take a look at the result of User.Id = 1 having ALL permission on a single Collection
            // but not on anything else
            //----------------------------------------------------------------------------------------------------

            //Attempt to do admin operations when user only has Read on a collection
            await AttemptAdminOperationsAsync(
                client.Endpoint.OriginalString,
                db.Id,
                container1.Id,
                permissionUser1Col1);

            //Attempt a write Document with read-only Collection permission
            await AttemptWriteWithReadPermissionAsync(
                client.Endpoint.OriginalString,
                db.Id,
                container1.Id,
                permissionUser1Col1);

            await db.DeleteAsync();
            await db2.DeleteAsync();
        }

        private static async Task AttemptWriteWithReadPermissionAsync(
            string endpoint,
            string databaseName,
            string containerName,
            PermissionProperties permission)
        {
            using (CosmosClient client = new CosmosClient(endpoint, permission.Token))
            {
                Container container = client.GetContainer(databaseName, containerName);
                //attempt to write a document > should fail
                try
                {
                    SalesOrder badSalesOrder = new SalesOrder()
                    {
                        Id = "Fail",
                        AccountNumber = "Fail"
                    };

                    await container.UpsertItemAsync<SalesOrder>(
                        badSalesOrder,
                        new PartitionKey(badSalesOrder.AccountNumber));

                    //should never get here, because we expect the create to fail
                    throw new ApplicationException("should never get here");
                }
                catch (CosmosException ce) when (ce.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    //expecting an Forbidden exception, anything else, rethrow
                    Console.WriteLine("Attempt to write a item failed as expected on read permission");
                }
            }
        }

        private static async Task AttemptAdminOperationsAsync(
            string endpoint,
            string databaseName,
            string containerName,
            PermissionProperties permission)
        {
            using (CosmosClient client = new CosmosClient(endpoint, permission.Token))
            {
                Container container = client.GetContainer(databaseName, containerName);
                //try read collection > should succeed because user1 was granted Read permission on col1
                FeedIterator<SalesOrder> feedIterator = container.GetItemQueryIterator<SalesOrder>();
                while (feedIterator.HasMoreResults)
                {
                    FeedResponse<SalesOrder> salesOrders = await feedIterator.ReadNextAsync();
                    foreach (SalesOrder salesOrder in salesOrders)
                    {
                        Console.WriteLine(salesOrder);
                    }
                }

                //try iterate databases > should fail because the user has no Admin rights 
                //but only read access to a single collection and therefore
                //cannot access anything outside of that collection.
                try
                {
                    FeedIterator<DatabaseProperties> databaseIterator = client.GetDatabaseQueryIterator<DatabaseProperties>("select T.* from T");
                    while (databaseIterator.HasMoreResults)
                    {
                        await databaseIterator.ReadNextAsync();
                        throw new ApplicationException("Should never get here");
                    }
                }
                catch (CosmosException ce) when (ce.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    Console.WriteLine("Database query failed because user has no admin rights");
                }
            }
        }
    }
}
