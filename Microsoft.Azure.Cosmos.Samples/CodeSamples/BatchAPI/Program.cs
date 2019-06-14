namespace Cosmos.Samples.BatchAPI
{
    using Cosmos.Samples.Shared;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json;
    using System;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;

    class Program
    {
        private static readonly JsonSerializer Serializer = new JsonSerializer();

        //Reusable instance of ItemClient which represents the connection to a Cosmos endpoint
        private static CosmosDatabase database = null;
        private static Container container = null;

        public static async Task Main(string[] args)
        {
            try
            {
                // Read all configs
                string endpoint;
                string authKey;
                string databaseName;
                string containerId;
                Program.ReadConfigs(out endpoint, out authKey, out databaseName, out containerId);

                //Read the Cosmos endpointUrl and authorisationKeys from configuration
                //These values are available from the Azure Management Portal on the Cosmos Account Blade under "Keys"
                //NB > Keep these values in a safe & secure location. Together they provide Administrative access to your Cosmos account
                using (CosmosClient client = new CosmosClient(endpoint, authKey))
                {
                    await Program.SetupDatabaseAndContainer(client, databaseName, containerId);
                    Container container = client.GetContainer(databaseName, containerId);
                    ContainerResponse containerResponse = await container.ReadAsync();

                    await ExecuteCrudAsync(container);
                    await ExecuteCrudStreamAsync(container);
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

        private static async Task ExecuteCrudAsync(Container container)
        {
            string partitionKeyValue = Guid.NewGuid().ToString();
            {
                // Batch with cruds                      
                BatchResponse batchResponse = await container.CreateBatch(new PartitionKey(partitionKeyValue))
                    .CreateItem(Program.GetSalesOrderSample("SalesOrder100", partitionKeyValue))
                    .CreateItem(Program.GetSalesOrderSample("SalesOrder101", partitionKeyValue))
                    .ReadItem("SalesOrder100")
                    .ReadItem("SalesOrder101")
                    .DeleteItem("SalesOrder100")
                    .UpsertItem(Program.GetSalesOrderSample("SalesOrder102", partitionKeyValue))
                    .ExecuteAsync();

                Console.WriteLine(
                    "Batch Response for cruds, StatusCode=[" +
                    batchResponse.StatusCode + "] Count=[" +
                    batchResponse.Count + "]");
            }

            {
                // Batch with 4 creates                        
                Batch batch = container.CreateBatch(new PartitionKey(partitionKeyValue));
                BatchResponse batchResponse = await
                    batch.CreateItem(Program.GetSalesOrderSample("SalesOrder0", partitionKeyValue))
                    .CreateItem(Program.GetSalesOrderSample("SalesOrder1", partitionKeyValue))
                    .CreateItem(Program.GetSalesOrderSample("SalesOrder2", partitionKeyValue))
                    .CreateItem(Program.GetSalesOrderSample("SalesOrder3", partitionKeyValue))
                    .ExecuteAsync();

                Console.WriteLine(
                    "Batch Response for create, StatusCode=[" +
                    batchResponse.StatusCode + "] Count=[" +
                    batchResponse.Count + "]");
            }

            {
                // Batch with 4 reads
                int count = 4;
                Batch batch = container.CreateBatch(new PartitionKey(partitionKeyValue));
                for (int i = 0; i < count; i++)
                {
                    batch.ReadItem("SalesOrder" + i);
                }

                BatchResponse batchResponse = await batch.ExecuteAsync();
                Console.WriteLine(
                    "Batch Response for reads, StatusCode=[" + batchResponse.StatusCode + "] Count=[" +
                    batchResponse.Count + "]");

                // Read the resource
                SalesOrder salesOrderRead = batchResponse.GetOperationResultAtIndex<SalesOrder>(0).Resource;
                Console.WriteLine("Purchase Order Number for first order is " + salesOrderRead.PurchaseOrderNumber);
            }

            {
                // Batch with 4 replace
                int count = 4;
                Batch batch = container.CreateBatch(new PartitionKey(partitionKeyValue));
                for (int i = 0; i < count; i++)
                {
                    SalesOrder salesOrderUpdated = Program.GetSalesOrderSample("SalesOrder" + i, partitionKeyValue);
                    salesOrderUpdated.Freight++;
                    batch.ReplaceItem("SalesOrder" + i, salesOrderUpdated);
                }

                BatchResponse batchResponse = await batch.ExecuteAsync();
                Console.WriteLine(
                    "Batch Response for updates, StatusCode=[" + batchResponse.StatusCode + "] Count=[" +
                    batchResponse.Count + "]");
            }

            {
                // Batch with 4 deletes
                int count = 4;
                Batch batch = container.CreateBatch(new PartitionKey(partitionKeyValue));
                for (int i = 0; i < count; i++)
                {
                    batch.DeleteItem("SalesOrder" + i);
                }

                BatchResponse batchResponse = await batch.ExecuteAsync();
                Console.WriteLine(
                    "Batch Response for deletes, StatusCode=[" + batchResponse.StatusCode + "] Count=[" +
                    batchResponse.Count + "]");
            }

            {
                // Batch with 4 upsert
                int count = 4;
                Batch batch = container.CreateBatch(new PartitionKey(partitionKeyValue));
                for (int i = 0; i < count; i++)
                {
                    batch.UpsertItem(Program.GetSalesOrderSample("SalesOrder" + i, partitionKeyValue));
                }

                BatchResponse batchResponse = await batch.ExecuteAsync();
                Console.WriteLine(
                    "Batch Response for upsert, StatusCode=[" + batchResponse.StatusCode + "] Count=[" +
                    batchResponse.Count + "]");
            }

            {
                // Batch with mixed CRUDs
                Batch batch = container.CreateBatch(new PartitionKey(partitionKeyValue));

                // Two Creates
                batch.CreateItem(Program.GetSalesOrderSample("SO10", partitionKeyValue));
                batch.CreateItem(Program.GetSalesOrderSample("SO11", partitionKeyValue));

                // Two Reads
                batch.ReadItem("SO10");
                batch.ReadItem("SO11");

                // One Replace
                SalesOrder updated = Program.GetSalesOrderSample("SO10", partitionKeyValue);
                updated.PurchaseOrderNumber = "NewPO10";
                batch.ReplaceItem("SO10", updated);

                BatchResponse batchResponse = await batch.ExecuteAsync();
                Console.WriteLine(
                    "Batch Response for deletes, StatusCode=[" + batchResponse.StatusCode + "] Count=[" +
                    batchResponse.Count + "]");
            }
        }

        private static async Task ExecuteCrudStreamAsync(Container container)
        {
            string partitionKeyValue = Guid.NewGuid().ToString();
            {
                // Batch with cruds stream API 
                SalesOrder order1 = Program.GetSalesOrderSample("SalesOrder200", partitionKeyValue);
                SalesOrder order2 = Program.GetSalesOrderSample("SalesOrder201", partitionKeyValue);
                SalesOrder order3 = Program.GetSalesOrderSample("SalesOrder202", partitionKeyValue);
                SalesOrder order4 = Program.GetSalesOrderSample("SalesOrder203", partitionKeyValue);
                SalesOrder order1Updated = Program.GetSalesOrderSample("SalesOrder200", partitionKeyValue);
                order1Updated.ShippedDate = DateTime.Now;
                SalesOrder order2Updated = Program.GetSalesOrderSample("SalesOrder201", partitionKeyValue);
                order2Updated.TaxAmount = 1;
                BatchResponse batchResponse = await container.CreateBatch(new PartitionKey(partitionKeyValue))
                    .CreateItemStream(Program.ToStream<SalesOrder>(order1))
                    .CreateItemStream(Program.ToStream<SalesOrder>(order2))
                    .CreateItemStream(Program.ToStream<SalesOrder>(order3))
                    .ReplaceItemStream(order1.Id, Program.ToStream<SalesOrder>(order1Updated))
                    .ReplaceItemStream(order2.Id, Program.ToStream<SalesOrder>(order2Updated))
                    .UpsertItemStream(Program.ToStream<SalesOrder>(order4))
                    .DeleteItem("SalesOrder202")
                    .ExecuteAsync();

                Console.WriteLine(
                    "Batch Response for cruds, StatusCode=[" +
                    batchResponse.StatusCode + "] Count=[" +
                    batchResponse.Count + "]");
            }

            {
                // Batch with creates using stream API 
                SalesOrder order1 = Program.GetSalesOrderSample("SalesOrder300", partitionKeyValue);
                SalesOrder order2 = Program.GetSalesOrderSample("SalesOrder301", partitionKeyValue);
                SalesOrder order3 = Program.GetSalesOrderSample("SalesOrder302", partitionKeyValue);
                SalesOrder order4 = Program.GetSalesOrderSample("SalesOrder303", partitionKeyValue);

                // Create batch and add operations
                Batch batch = container.CreateBatch(new PartitionKey(partitionKeyValue));
                batch.CreateItemStream(Program.ToStream<SalesOrder>(order1));
                batch.CreateItemStream(Program.ToStream<SalesOrder>(order2));
                batch.CreateItemStream(Program.ToStream<SalesOrder>(order3));
                batch.CreateItemStream(Program.ToStream<SalesOrder>(order4));

                // Execute the batch
                BatchResponse batchResponse = await batch.ExecuteAsync();
                Console.WriteLine(
                    "Batch Response for create, StatusCode=[" +
                    batchResponse.StatusCode + "] Count=[" +
                    batchResponse.Count + "]");
            }

            {
                // Batch with replaces using stream API 
                SalesOrder order1Updated = Program.GetSalesOrderSample("SalesOrder300", partitionKeyValue);
                SalesOrder order2Updated = Program.GetSalesOrderSample("SalesOrder301", partitionKeyValue);
                SalesOrder order3Updated = Program.GetSalesOrderSample("SalesOrder302", partitionKeyValue);
                SalesOrder order4Updated = Program.GetSalesOrderSample("SalesOrder303", partitionKeyValue);
                order1Updated.PurchaseOrderNumber = "Order1";
                order2Updated.ShippedDate = DateTime.Now;
                order3Updated.SubTotal = 100;
                order4Updated.TotalDue = 99;

                // Create batch and add replace operations
                Batch batch = container.CreateBatch(new PartitionKey(partitionKeyValue));
                batch.ReplaceItemStream("SalesOrder300", Program.ToStream<SalesOrder>(order1Updated));
                batch.ReplaceItemStream("SalesOrder301", Program.ToStream<SalesOrder>(order2Updated));
                batch.ReplaceItemStream("SalesOrder302", Program.ToStream<SalesOrder>(order3Updated));
                batch.ReplaceItemStream("SalesOrder303", Program.ToStream<SalesOrder>(order4Updated));

                // Execute the batch
                BatchResponse batchResponse = await batch.ExecuteAsync();
                Console.WriteLine(
                    "Batch Response for create, StatusCode=[" +
                    batchResponse.StatusCode + "] Count=[" +
                    batchResponse.Count + "]");
            }

            {
                // Batch with creates using stream API 
                SalesOrder order1 = Program.GetSalesOrderSample("SalesOrder400", partitionKeyValue);
                SalesOrder order2 = Program.GetSalesOrderSample("SalesOrder401", partitionKeyValue);
                SalesOrder order3 = Program.GetSalesOrderSample("SalesOrder402", partitionKeyValue);
                SalesOrder order4 = Program.GetSalesOrderSample("SalesOrder403", partitionKeyValue);

                // Create batch and add operations
                Batch batch = container.CreateBatch(new PartitionKey(partitionKeyValue));
                batch.UpsertItemStream(Program.ToStream<SalesOrder>(order1));
                batch.UpsertItemStream(Program.ToStream<SalesOrder>(order2));
                batch.UpsertItemStream(Program.ToStream<SalesOrder>(order3));
                batch.UpsertItemStream(Program.ToStream<SalesOrder>(order4));

                // Execute the batch
                BatchResponse batchResponse = await batch.ExecuteAsync();
                Console.WriteLine(
                    "Batch Response for upserts, StatusCode=[" +
                    batchResponse.StatusCode + "] Count=[" +
                    batchResponse.Count + "]");
            }
        }

        private static Stream ToStream<T>(T input)
        {
            MemoryStream streamPayload = new MemoryStream();
            using (StreamWriter streamWriter = new StreamWriter(streamPayload, encoding: Encoding.Default, bufferSize: 1024, leaveOpen: true))
            {
                using (JsonWriter writer = new JsonTextWriter(streamWriter))
                {
                    writer.Formatting = Newtonsoft.Json.Formatting.None;
                    Program.Serializer.Serialize(writer, input);
                    writer.Flush();
                    streamWriter.Flush();
                }
            }

            streamPayload.Position = 0;
            return streamPayload;
        }

        private static void ReadConfigs(out string endpoint, out string authKey, out string databaseName, out string containerId)
        {
            IConfigurationRoot configuration = new ConfigurationBuilder()
                   .AddJsonFile("AppSettings.json")
                   .Build();

            endpoint = configuration["EndPointUrl"];
            if (string.IsNullOrEmpty(endpoint))
            {
                throw new ArgumentNullException("Please specify a valid endpoint in the appSettings.json");
            }

            authKey = configuration["AuthorizationKey"];
            if (string.IsNullOrEmpty(authKey) || string.Equals(authKey, "Super secret key"))
            {
                throw new ArgumentException("Please specify a valid AuthorizationKey in the appSettings.json");
            }

            databaseName = configuration["DatabaseName"];
            if (string.IsNullOrEmpty(databaseName) || string.Equals(databaseName, ""))
            {
                throw new ArgumentException("Please specify a valid DatabaseName in the appSettings.json");
            }

            containerId = configuration["CollectionName"];
            if (string.IsNullOrEmpty(databaseName) || string.Equals(databaseName, ""))
            {
                throw new ArgumentException("Please specify a valid CollectionName in the appSettings.json");
            }
        }

        private static async Task SetupDatabaseAndContainer(CosmosClient client, string databaseName, string containerId)
        {
            database = await client.CreateDatabaseIfNotExistsAsync(databaseName);

            // Delete the existing container to prevent create item conflicts
            await database.GetContainer(containerId).DeleteAsync();

            ContainerProperties containerSettings = new ContainerProperties(containerId, partitionKeyPath: "/AccountNumber");

            // Create with a throughput of 1000 RU/s
            container = await database.CreateContainerIfNotExistsAsync(containerSettings, throughput: 1000);
        }

        private static SalesOrder GetSalesOrderSample(string itemId, string accountNumber)
        {
            SalesOrder salesOrder = new SalesOrder
            {
                Id = itemId,
                AccountNumber = accountNumber,
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
    }
}
