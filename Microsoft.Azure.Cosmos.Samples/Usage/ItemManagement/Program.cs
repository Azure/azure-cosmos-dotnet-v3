namespace Cosmos.Samples.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json;

    // ----------------------------------------------------------------------------------------------------------
    // Prerequisites - 
    // 
    // 1. An Azure Cosmos account - 
    //    https://docs.microsoft.com/azure/cosmos-db/create-cosmosdb-resources-portal
    //
    // 2. Microsoft.Azure.Cosmos NuGet package - 
    //    http://www.nuget.org/packages/Microsoft.Azure.Cosmos/ 
    // ----------------------------------------------------------------------------------------------------------
    // Sample - demonstrates the basic CRUD operations on a Item resource for Azure Cosmos
    //
    // 1. Basic CRUD operations on a item using regular POCOs
    // 1.1 - Create a item
    // 1.2 - Read a item by its Id
    // 1.3 - Read all items in a container
    // 1.4 - Query for items by a property other than Id
    // 1.5 - Read Many items using Id and PartitionKey values.
    // 1.6 - Replace a item
    // 1.7 - Patch a item
    // 1.8 - Upsert a item
    // 1.9 - Delete a item
    //
    // 2. Work with dynamic objects
    //
    // 3. Using ETags to control execution
    // 3.1 - Use ETag with ReplaceItem for optimistic concurrency
    // 3.2 - Use ETag with ReadItem to only return a result if the ETag of the request does not match
    //
    // 4 - Access items system defined properties
    //-----------------------------------------------------------------------------------------------------------
    // See Also - 
    //
    // Cosmos.Samples.Queries -           We only included a VERY basic query here for completeness,
    //                                        For a detailed exploration of how to query for Items, 
    //                                        including how to paginate results of queries.
    //
    // Cosmos.Samples.ServerSideScripts - In these examples we do simple loops to create small numbers
    //                                        of items. For insert operations where you are creating many
    //                                        items we recommend using a Stored Procedure and pass batches
    //                                        of new items to this sproc. Consult this sample for an example
    //                                        of a BulkInsert stored procedure. 
    // ----------------------------------------------------------------------------------------------------------

    public class Program
    {
        private static readonly string databaseId = "samples";
        private static readonly string containerId = "item-samples";
        private static readonly JsonSerializer Serializer = new JsonSerializer();

        //Reusable instance of ItemClient which represents the connection to a Cosmos endpoint
        private static Database database = null;
        private static Container container = null;

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
                    await Program.Initialize(client);
                    await Program.RunItemsDemo();
                    await Program.Cleanup();
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
        /// Run basic item access methods as a console app demo
        /// </summary>
        /// <returns></returns>
        // <RunItemsDemo>
        private static async Task RunItemsDemo()
        {
            await Program.RunBasicOperationsOnStronglyTypedObjects();

            await Program.RunBasicOperationsOnDynamicObjects();

            await Program.UseETags();

            await Program.UseConsistencyLevels();

            await Program.AccessSystemDefinedProperties();
        }
        // </RunItemsDemo>

        /// <summary>
        /// 1. Basic CRUD operations on a item
        /// 1.1 - Create a item
        /// 1.2 - Read a item by its Id
        /// 1.3 - Read all items in a Collection
        /// 1.4 - Query for items by a property other than Id
        /// 1.5 - Read Many items using Id and PartitionKey values.
        /// 1.6 - Replace a item
        /// 1.7 - Patch a item
        /// 1.8 - Upsert a item
        /// 1.9 - Delete a item
        /// </summary>
        // <RunBasicOperationsOnStronglyTypedObjects>
        private static async Task RunBasicOperationsOnStronglyTypedObjects()
        {
            SalesOrder result = await Program.CreateItemsAsync();

            await Program.ReadItemAsync();

            await Program.ReadAllItems();

            await Program.QueryItems();

            await Program.ReadManyItems();

            await Program.ReplaceItemAsync(result);

            await Program.PatchItemAsync(result);

            await Program.UpsertItemAsync();

            await Program.DeleteItemAsync();
        }
        // </RunBasicOperationsOnStronglyTypedObjects>

        // <CreateItemsAsync>
        private static async Task<SalesOrder> CreateItemsAsync()
        {
            Console.WriteLine("\n1.1 - Creating items");

            // Create a SalesOrder object. This object has nested properties and various types including numbers, DateTimes and strings.
            // This can be saved as JSON as is without converting into rows/columns.
            SalesOrder salesOrder = GetSalesOrderSample("SalesOrder1");
            ItemResponse<SalesOrder> response = await container.CreateItemAsync(salesOrder, new PartitionKey(salesOrder.AccountNumber));
            SalesOrder salesOrder1 = response;
            Console.WriteLine($"\n1.1.1 - Item created {salesOrder1.Id}");

            // As your app evolves, let's say your object has a new schema. You can insert SalesOrderV2 objects without any 
            // changes to the database tier.
            SalesOrder2 salesOrder2 = GetSalesOrderV2Sample("SalesOrder2");
            ItemResponse<SalesOrder2> response2 = await container.CreateItemAsync(
                salesOrder2,
                new PartitionKey(salesOrder2.AccountNumber),
                new ItemRequestOptions()
                {
                    // The response will have a null resource. This avoids the overhead of 
                    // sending the item back over the network and serializing it.
                    EnableContentResponseOnWrite = false
                });

            if(response2.Resource != null)
            {
                throw new ArgumentException("Resource should be null");
            }

            Console.WriteLine($"\n1.1.2 - Item created {salesOrder2.Id}");

            // For better performance create a SalesOrder object from a stream. 
            SalesOrder salesOrderV3 = GetSalesOrderSample("SalesOrderV3");
            using (Stream stream = Program.ToStream<SalesOrder>(salesOrderV3))
            {
                using (ResponseMessage responseMessage = await container.CreateItemStreamAsync(stream, new PartitionKey(salesOrderV3.AccountNumber)))
                {
                    // Item stream operations do not throw exceptions for better performance
                    if (responseMessage.IsSuccessStatusCode)
                    {
                        SalesOrder streamResponse = FromStream<SalesOrder>(responseMessage.Content);
                        Console.WriteLine($"\n1.1.2 - Item created {streamResponse.Id}");
                    }
                    else
                    {
                        Console.WriteLine($"Create item from stream failed. Status code: {responseMessage.StatusCode} Message: {responseMessage.ErrorMessage}");
                    }
                }
            }

            return salesOrder;
        }
        // </CreateItemsAsync>

        // <ReadItemAsync>
        private static async Task ReadItemAsync()
        {
            Console.WriteLine("\n1.2 - Reading Item by Id");

            // Note that Reads require a partition key to be specified.
            ItemResponse<SalesOrder> response = await container.ReadItemAsync<SalesOrder>(
                partitionKey: new PartitionKey("Account1"),
                id: "SalesOrder1");

            // Log the diagnostics
            Console.WriteLine($"Diagnostics for ReadItemAsync: {response.Diagnostics.ToString()}");

            // You can measure the throughput consumed by any operation by inspecting the RequestCharge property
            Console.WriteLine("Item read by Id {0}", response.Resource);
            Console.WriteLine("Request Units Charge for reading a Item by Id {0}", response.RequestCharge);

            SalesOrder readOrder = (SalesOrder)response;

            // Read the same item but as a stream.
            using (ResponseMessage responseMessage = await container.ReadItemStreamAsync(
                partitionKey: new PartitionKey("Account1"),
                id: "SalesOrder1"))
            {
                // Item stream operations do not throw exceptions for better performance
                if (responseMessage.IsSuccessStatusCode)
                {
                    SalesOrder streamResponse = FromStream<SalesOrder>(responseMessage.Content);
                    Console.WriteLine($"\n1.2.2 - Item Read {streamResponse.Id}");

                    // Log the diagnostics
                    Console.WriteLine($"\n1.2.2 - Item Read Diagnostics: {responseMessage.Diagnostics.ToString()}");
                }
                else
                {
                    Console.WriteLine($"Read item from stream failed. Status code: {responseMessage.StatusCode} Message: {responseMessage.ErrorMessage}");
                }
            }
        }
        // </ReadItemAsync>

        // <ReadAllItems>
        private static async Task ReadAllItems()
        {
            //******************************************************************************************************************
            // 1.3 - Read all items in a container
            //
            // NOTE: Operations like AsEnumerable(), ToList(), ToArray() will make as many trips to the database
            //       as required to fetch the entire result-set. Even if you set MaxItemCount to a smaller number. 
            //       MaxItemCount just controls how many results to fetch each trip. 
            //******************************************************************************************************************
            Console.WriteLine("\n1.3 - Read all items with query using a specific partition key");

            List<SalesOrder> allSalesForAccount1 = new List<SalesOrder>();
            using (FeedIterator<SalesOrder> resultSet = container.GetItemQueryIterator<SalesOrder>(
                queryDefinition: null,
                requestOptions: new QueryRequestOptions()
                {
                    PartitionKey = new PartitionKey("Account1")
                }))
            {
                while (resultSet.HasMoreResults)
                {
                    FeedResponse<SalesOrder> response = await resultSet.ReadNextAsync();
                    SalesOrder sale = response.First();
                    Console.WriteLine($"\n1.3.1 Account Number: {sale.AccountNumber}; Id: {sale.Id};");
                    if (response.Diagnostics != null)
                    {
                        Console.WriteLine($" Diagnostics {response.Diagnostics.ToString()}");
                    }

                    allSalesForAccount1.AddRange(response);
                }
            }

              

            Console.WriteLine($"\n1.3.2 Read all items found {allSalesForAccount1.Count} items.");

            // Use the same query as before but get the cosmos response message to access the stream directly
            List<SalesOrder> allSalesForAccount1FromStream = new List<SalesOrder>();
            using (FeedIterator streamResultSet = container.GetItemQueryStreamIterator(
                queryDefinition: null,
                requestOptions: new QueryRequestOptions()
                {
                    PartitionKey = new PartitionKey("Account1")
                }))
            {
                while (streamResultSet.HasMoreResults)
                {
                    using (ResponseMessage responseMessage = await streamResultSet.ReadNextAsync())
                    {
                        // Item stream operations do not throw exceptions for better performance
                        if (responseMessage.IsSuccessStatusCode)
                        {
                            dynamic streamResponse = FromStream<dynamic>(responseMessage.Content);
                            List<SalesOrder> salesOrders = streamResponse.Documents.ToObject<List<SalesOrder>>();
                            Console.WriteLine($"\n1.3.3 - Read all items via stream {salesOrders.Count}");
                            allSalesForAccount1FromStream.AddRange(salesOrders);
                        }
                        else
                        {
                            Console.WriteLine($"Read all items from stream failed. Status code: {responseMessage.StatusCode} Message: {responseMessage.ErrorMessage}");
                        }
                    }
                }

                Console.WriteLine($"\n1.3.4 Read all items found {allSalesForAccount1FromStream.Count} items.");
            }

            if (allSalesForAccount1.Count != allSalesForAccount1FromStream.Count)
            {
                throw new InvalidDataException($"Both read all item operations should return the same list");
            }
        }
        // </QueryItems>

        // <QueryItems>
        private static async Task QueryItems()
        {
            //******************************************************************************************************************
            // 1.4 - Query for items by a property other than Id
            //
            // NOTE: Operations like AsEnumerable(), ToList(), ToArray() will make as many trips to the database
            //       as required to fetch the entire result-set. Even if you set MaxItemCount to a smaller number. 
            //       MaxItemCount just controls how many results to fetch each trip. 
            //******************************************************************************************************************
            Console.WriteLine("\n1.4 - Querying for a item using its AccountNumber property");

            QueryDefinition query = new QueryDefinition(
                "select * from sales s where s.AccountNumber = @AccountInput ")
                .WithParameter("@AccountInput", "Account1");

            List<SalesOrder> allSalesForAccount1 = new List<SalesOrder>();
            using (FeedIterator<SalesOrder> resultSet = container.GetItemQueryIterator<SalesOrder>(
                query,
                requestOptions: new QueryRequestOptions()
                {
                    PartitionKey = new PartitionKey("Account1"),
                    MaxItemCount = 1
                }))
            {
                while (resultSet.HasMoreResults)
                {
                    FeedResponse<SalesOrder> response = await resultSet.ReadNextAsync();
                    SalesOrder sale = response.First();
                    Console.WriteLine($"\n1.4.1 Account Number: {sale.AccountNumber}; Id: {sale.Id};");
                    if (response.Diagnostics != null)
                    {
                        Console.WriteLine($" Diagnostics {response.Diagnostics.ToString()}");
                    }

                    allSalesForAccount1.AddRange(response);
                }
            }

            Console.WriteLine($"\n1.4.2 Query found {allSalesForAccount1.Count} items.");

            // Use the same query as before but get the cosmos response message to access the stream directly
            List<SalesOrder> allSalesForAccount1FromStream = new List<SalesOrder>();
            using (FeedIterator streamResultSet = container.GetItemQueryStreamIterator(
                query,
                requestOptions: new QueryRequestOptions()
                {
                    PartitionKey = new PartitionKey("Account1"),
                    MaxItemCount = 10,
                    MaxConcurrency = 1
                }))
            {
                while (streamResultSet.HasMoreResults)
                {
                    using (ResponseMessage responseMessage = await streamResultSet.ReadNextAsync())
                    {
                        // Item stream operations do not throw exceptions for better performance
                        if (responseMessage.IsSuccessStatusCode)
                        {
                            dynamic streamResponse = FromStream<dynamic>(responseMessage.Content);
                            List<SalesOrder> salesOrders = streamResponse.Documents.ToObject<List<SalesOrder>>();
                            Console.WriteLine($"\n1.4.3 - Item Query via stream {salesOrders.Count}");
                            allSalesForAccount1FromStream.AddRange(salesOrders);
                        }
                        else
                        {
                            Console.WriteLine($"Query item from stream failed. Status code: {responseMessage.StatusCode} Message: {responseMessage.ErrorMessage}");
                        }
                    }
                }
            }

            Console.WriteLine($"\n1.4.4 Query found {allSalesForAccount1FromStream.Count} items.");

            if (allSalesForAccount1.Count != allSalesForAccount1FromStream.Count)
            {
                throw new InvalidDataException($"Both query operations should return the same list");
            }
        }
        // </QueryItems>

        // <ReadManyItems>
        private static async Task ReadManyItems()
        {
            //******************************************************************************************************************
            // 1.5 - Read Many Items in a container using id and partitionkey values
            //******************************************************************************************************************
            Console.WriteLine("\n1.5 - ReadManyItems with Id and PartitionKey values");

            // Create some items to be read back later
            for (int i = 0; i < 5; i++)
            {
                SalesOrder salesOrder = GetSalesOrderSample($"SalesOrderForReadMany{i}");
                await container.CreateItemAsync(salesOrder);
            }

            // Create item list with (id, pkvalue) tuples
            List<(string, PartitionKey)> itemList = new List<(string, PartitionKey)>
            {
                ("SalesOrderForReadMany1", new PartitionKey("Account1")),
                ("SalesOrderForReadMany2", new PartitionKey("Account1")),
                ("SalesOrderForReadMany4", new PartitionKey("Account1")),
            };

            Console.WriteLine("\nItems in read list:" + itemList.Count);
            FeedResponse<SalesOrder> feedResponse = await container.ReadManyItemsAsync<SalesOrder>(
                                                                        itemList);
            Console.WriteLine("\nItems returned by read many api:" + feedResponse.Count);
            foreach (SalesOrder salesOrder in feedResponse)
            {
                Console.WriteLine($"\nItem read: Id = {salesOrder.Id}, AccountNumber = {salesOrder.AccountNumber}");
            }
            Console.WriteLine("\nDiagnostics for ReadManyItems API: " + feedResponse.Diagnostics);

            // ReadManyStreamApi
            Console.WriteLine("\n1.5.2 - Using Stream API to get many items in container.");
            using (ResponseMessage responseMessage = await container.ReadManyItemsStreamAsync(itemList))
            {
                if (responseMessage.IsSuccessStatusCode)
                {
                    dynamic streamResponse = FromStream<dynamic>(responseMessage.Content);
                    List<SalesOrder> salesOrders = streamResponse.Documents.ToObject<List<SalesOrder>>();
                    Console.WriteLine($"\n Items returned from Stream API: {salesOrders.Count}");
                }
                else
                {
                    Console.WriteLine($"\nReadMany items from stream failed. Status code: {responseMessage.StatusCode} Message: {responseMessage.ErrorMessage}");
                }
                Console.WriteLine("\nDiagnostics for ReadManyItemsStream API: " + responseMessage.Diagnostics);
            }
        }
        // </ReadManyItems>

        // <ReplaceItemAsync>
        private static async Task ReplaceItemAsync(SalesOrder order)
        {
            //******************************************************************************************************************
            // 1.6 - Replace a item
            //
            // Just update a property on an existing item and issue a Replace command
            //******************************************************************************************************************
            Console.WriteLine("\n1.6 - Replacing a item using its Id");

            order.ShippedDate = DateTime.UtcNow;
            ItemResponse<SalesOrder> response = await container.ReplaceItemAsync(
                partitionKey: new PartitionKey(order.AccountNumber),
                id: order.Id,
                item: order);

            SalesOrder updated = response.Resource;
            Console.WriteLine($"Request charge of replace operation: {response.RequestCharge}");
            Console.WriteLine($"Shipped date of updated item: {updated.ShippedDate}");

            order.ShippedDate = DateTime.UtcNow;
            using (Stream stream = Program.ToStream<SalesOrder>(order))
            {
                using (ResponseMessage responseMessage = await container.ReplaceItemStreamAsync(
                    partitionKey: new PartitionKey(order.AccountNumber),
                    id: order.Id,
                    streamPayload: stream))
                {
                    // Item stream operations do not throw exceptions for better performance
                    if (responseMessage.IsSuccessStatusCode)
                    {
                        SalesOrder streamResponse = FromStream<SalesOrder>(responseMessage.Content);
                        Console.WriteLine($"\n1.6.2 - Item replace via stream {streamResponse.Id}");
                    }
                    else
                    {
                        Console.WriteLine($"Replace item from stream failed. Status code: {responseMessage.StatusCode} Message: {responseMessage.ErrorMessage}");
                    }
                }
            }
        }
        // </ReplaceItemAsync>

        // <PatchItemAsync>
        private static async Task PatchItemAsync(SalesOrder order)
        {
            //******************************************************************************************************************
            // 1.7 - Patch a item
            //
            // Just update a property of an existing item and issue a Patch command
            //******************************************************************************************************************
            Console.WriteLine("\n1.6 - Patching a item using its Id");

            ItemResponse<SalesOrder> response = await container.PatchItemAsync<SalesOrder>(
                id: order.Id,
                partitionKey: new PartitionKey(order.AccountNumber),
                patchOperations: new[] { PatchOperation.Replace("/TotalDue", 0) });

            SalesOrder updatedSalesOrder = response.Resource;
            Console.WriteLine($"TotalDue of updated item: {updatedSalesOrder.TotalDue}");

            PatchItemRequestOptions patchItemRequestOptions = new PatchItemRequestOptions
            {
                FilterPredicate = "from c where (c.TotalDue = 0 OR NOT IS_DEFINED(c.TotalDue))"
            };
            response = await container.PatchItemAsync<SalesOrder>(
                id: order.Id,
                partitionKey: new PartitionKey(order.AccountNumber),
                patchOperations: new[] { PatchOperation.Replace("/ShippedDate", DateTime.UtcNow) },
                patchItemRequestOptions);

            updatedSalesOrder = response.Resource;
            Console.WriteLine($"\n1.6.2 - Shipped date of updated item: {updatedSalesOrder.ShippedDate}");

            IReadOnlyList<PatchOperation> patchOperations = new[] { PatchOperation.Replace("/ShippedDate", DateTime.UtcNow) };
            using (Stream stream = Program.ToStream<IReadOnlyList<PatchOperation>>(patchOperations))
            {
                using (ResponseMessage responseMessage = await container.PatchItemStreamAsync(
                    id: order.Id,
                    partitionKey: new PartitionKey(order.AccountNumber),
                    patchOperations: patchOperations))
                {
                    // Item stream operations do not throw exceptions for better performance
                    if (responseMessage.IsSuccessStatusCode)
                    {
                        SalesOrder streamResponse = FromStream<SalesOrder>(responseMessage.Content);
                        Console.WriteLine($"\n1.6.3 - Item Patch via stream {streamResponse.Id}");
                    }
                    else
                    {
                        Console.WriteLine($"Patch item from stream failed. Status code: {responseMessage.StatusCode} Message: {responseMessage.ErrorMessage}");
                    }
                }
            }
        }
        // </PatchItemAsync>

        // <UpsertItemAsync>
        private static async Task UpsertItemAsync()
        {
            Console.WriteLine("\n1.8 - Upserting a item");

            SalesOrder upsertOrder = GetSalesOrderSample("SalesOrder3");
            
            //creates the initial SalesOrder document. 
            //notice the response.StatusCode returned indicates a Create operation was performed
            ItemResponse<SalesOrder> response = await container.UpsertItemAsync(
                partitionKey: new PartitionKey(upsertOrder.AccountNumber),
                item: upsertOrder);
            
            SalesOrder upserted = response.Resource;
            Console.WriteLine($"Request charge of upsert operation: {response.RequestCharge}");
            Console.WriteLine($"StatusCode of this operation: { response.StatusCode}");
            Console.WriteLine($"Id of upserted item: {upserted.Id}");
            Console.WriteLine($"Shipped Date of upserted item: {upserted.ShippedDate}");
            
            //update any field (other than the id, and the partitionKey, as these cannot be changed
            //notice the response.StatusCode from the Upsert operation now indicates that an Update was performed
            upserted.ShippedDate = DateTime.UtcNow;
            response = await container.UpsertItemAsync(partitionKey: new PartitionKey(upserted.AccountNumber), item: upserted);
            upserted = response.Resource;

            Console.WriteLine($"Request charge of upsert operation: {response.RequestCharge}");
            Console.WriteLine($"StatusCode of this operation: { response.StatusCode}");
            Console.WriteLine($"Id of upserted item: {upserted.Id}");
            Console.WriteLine($"ShippedDate of upserted item: {upserted.ShippedDate}");

            // For better performance upsert a SalesOrder object from a stream. 
            SalesOrder salesOrderV4 = GetSalesOrderSample("SalesOrder4");
            using (Stream stream = Program.ToStream<SalesOrder>(salesOrderV4))
            {
                using (ResponseMessage responseMessage = await container.UpsertItemStreamAsync(
                    partitionKey: new PartitionKey(salesOrderV4.AccountNumber),
                    streamPayload: stream))
                {
                    // Item stream operations do not throw exceptions for better performance
                    if (responseMessage.IsSuccessStatusCode)
                    {
                        SalesOrder streamResponse = FromStream<SalesOrder>(responseMessage.Content);
                        Console.WriteLine($"\n1.7.2 - Item upserted via stream {streamResponse.Id}");
                    }
                    else
                    {
                        Console.WriteLine($"Upsert item from stream failed. Status code: {responseMessage.StatusCode} Message: {responseMessage.ErrorMessage}");
                    }
                }
            }
        }
        // </UpsertItemAsync>

        // <DeleteItemAsync>
        private static async Task DeleteItemAsync()
        {
            Console.WriteLine("\n1.9 - Deleting a item");
            ItemResponse<SalesOrder> response = await container.DeleteItemAsync<SalesOrder>(
                partitionKey: new PartitionKey("Account1"),
                id: "SalesOrder3");

            Console.WriteLine("Request charge of delete operation: {0}", response.RequestCharge);
            Console.WriteLine("StatusCode of operation: {0}", response.StatusCode);
        }
        // </DeleteItemAsync>

        private static T FromStream<T>(Stream stream)
        {
            using (stream)
            {
                if (typeof(Stream).IsAssignableFrom(typeof(T)))
                {
                    return (T)(object)stream;
                }

                using (StreamReader sr = new StreamReader(stream))
                {
                    using (JsonTextReader jsonTextReader = new JsonTextReader(sr))
                    {
                        return Program.Serializer.Deserialize<T>(jsonTextReader);
                    }
                }
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

        private static SalesOrder GetSalesOrderSample(string itemId)
        {
            SalesOrder salesOrder = new SalesOrder
            {
                Id = itemId,
                AccountNumber = "Account1",
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

        private static SalesOrder2 GetSalesOrderV2Sample(string itemId)
        {
            return new SalesOrder2
            {
                Id = itemId,
                AccountNumber = "Account2",
                PurchaseOrderNumber = "PO15428132599",
                OrderDate = new DateTime(2005, 7, 1),
                DueDate = new DateTime(2005, 7, 13),
                ShippedDate = new DateTime(2005, 7, 8),
                SubTotal = 6107.0820m,
                TaxAmt = 586.1203m,
                Freight = 183.1626m,
                DiscountAmt = 1982.872m,            // new property added to SalesOrder2
                TotalDue = 4893.3929m,
                Items = new SalesOrderDetail2[]
                {
                    new SalesOrderDetail2
                    {
                        OrderQty = 3,
                        ProductCode = "A-123",      // notice how in SalesOrderDetail2 we no longer reference a ProductId
                        ProductName = "Product 1",  // instead we have decided to denormalize our schema and include 
                        CurrencySymbol = "$",       // the Product details relevant to the Order on to the Order directly
                        CurrencyCode = "USD",       // this is a typical refactor that happens in the course of an application
                        UnitPrice = 17.1m,          // that would have previously required schema changes and data migrations etc. 
                        LineTotal = 5.7m
                    }
                }
            };
        }

        /// <summary>
        /// 2. Basic CRUD operations using dynamics instead of strongly typed objects
        /// Cosmos does not require objects to be typed. Applications that merge data from different data sources, or 
        /// need to handle evolving schemas can write data directly as JSON or dynamic objects.
        /// </summary>
        // <RunBasicOperationsOnDynamicObjects>
        private static async Task RunBasicOperationsOnDynamicObjects()
        {
            Console.WriteLine("\n2. Use Dynamics");

            // Create a dynamic object
            dynamic salesOrder = new
            {
                id = "_SalesOrder5",
                AccountNumber = "NewUser01",
                PurchaseOrderNumber = "PO18009186470",
                OrderDate = DateTime.UtcNow,
                Total = 5.95,
            };

            Console.WriteLine("\nCreating item");

            ItemResponse<dynamic> response = await container.CreateItemAsync<dynamic>(salesOrder, new PartitionKey("NewUser01"));
            dynamic createdItem = response.Resource;

            Console.WriteLine("Item with id {0} created", createdItem.Id);
            Console.WriteLine("Request charge of operation: {0}", response.RequestCharge);

            response = await container.ReadItemAsync<dynamic>(partitionKey: new PartitionKey("NewUser01"), id: "_SalesOrder5");

            dynamic readItem = response.Resource;

            //update a dynamic object by just creating a new Property on the fly
            //Item is itself a dynamic object, so you can just use this directly too if you prefer
            readItem.Add("shippedDate", DateTime.UtcNow);

            //if you wish to work with a dynamic object so you don't need to use SetPropertyValue() or GetPropertyValue<T>()
            //then you can cast to a dynamic
            salesOrder = readItem;
            salesOrder.foo = "bar";

            //now do a replace using this dynamic item
            //everything that is needed is contained in the readDynOrder object 
            //it has a .self Property
            Console.WriteLine("\nReplacing item");

            response = await container.ReplaceItemAsync<dynamic>(partitionKey: new PartitionKey("NewUser01"), id: "_SalesOrder5", item: salesOrder);
            dynamic replaced = response.Resource;

            Console.WriteLine("Request charge of operation: {0}", response.RequestCharge);
            Console.WriteLine("shippedDate: {0} and foo: {1} of replaced item", replaced.shippedDate, replaced.foo);
        }
        // </RunBasicOperationsOnDynamicObjects>

        /// <summary>
        /// 3. Using ETags to control execution of operations
        /// 3.1 - Use ETag to control if a ReplaceItem operation should check if ETag of request matches Item
        /// 3.2 - Use ETag to control if ReadItem should only return a result if the ETag of the request does not match the Item
        /// </summary>
        /// <returns></returns>
        // <UseETags>
        private static async Task UseETags()
        {
            //******************************************************************************************************************
            // 3.1 - Use ETag to control if a replace should succeed, or not, based on whether the ETag on the request matches
            //       the current ETag value of the persisted Item
            //
            // All items in Cosmos have an _etag field. This gets set on the server every time a item is updated.
            // 
            // When doing a replace of a item you can opt-in to having the server only apply the Replace if the ETag 
            // on the request matches the ETag of the item on the server.
            // If someone did an update to the same item since you read it, then the ETag on the server will not match
            // and the Replace operation can be rejected. 
            //******************************************************************************************************************
            Console.WriteLine("\n3.1 - Using optimistic concurrency when doing a ReplaceItemAsync");

            //read a item
            ItemResponse<SalesOrder> itemResponse = await container.ReadItemAsync<SalesOrder>(
                partitionKey: new PartitionKey("Account1"),
                id: "SalesOrder1");

            Console.WriteLine("ETag of read item - {0}", itemResponse.ETag);

            SalesOrder item = itemResponse;
            //Update the total due
            itemResponse.Resource.TotalDue = 1000000;

            //persist the change back to the server
            ItemResponse<SalesOrder> updatedDoc = await container.ReplaceItemAsync<SalesOrder>(
                partitionKey: new PartitionKey(item.AccountNumber),
                id: item.Id,
                item: item);

            Console.WriteLine("ETag of item now that is has been updated - {0}", updatedDoc.ETag);

            //now, using the originally retrieved item do another update 
            //but set the AccessCondition class with the ETag of the originally read item and also set the AccessConditionType
            //this tells the service to only do this operation if ETag on the request matches the current ETag on the item
            //in our case it won't, because we updated the item and therefore gave it a new ETag
            try
            {
                itemResponse.Resource.TotalDue = 9999999;
                updatedDoc = await container.ReplaceItemAsync<SalesOrder>(itemResponse, item.Id, new PartitionKey(item.AccountNumber), new ItemRequestOptions { IfMatchEtag = itemResponse.ETag });
            }
            catch (CosmosException cre)
            {
                //   now notice the failure when attempting the update 
                //   this is because the ETag on the server no longer matches the ETag of doc (b/c it was changed in step 2)
                if (cre.StatusCode == HttpStatusCode.PreconditionFailed)
                {
                    Console.WriteLine("As expected, we have a pre-condition failure exception\n");
                }
            }

            //*******************************************************************************************************************
            // 3.2 - ETag on a ReadItemAsync request can be used to tell the server whether it should return a result, or not
            //
            // By setting the ETag on a ReadItemRequest along with an AccessCondition of IfNoneMatch instructs the server
            // to only return a result if the ETag of the request does not match that of the persisted Item
            //*******************************************************************************************************************

            Console.WriteLine("\n3.2 - Using ETag to do a conditional ReadItemAsync");

            // Get a item
            ItemResponse<SalesOrder> response = await container.ReadItemAsync<SalesOrder>(partitionKey: new PartitionKey("Account2"), id: "SalesOrder2");

            item = response;
            Console.WriteLine($"Read doc with StatusCode of {response.StatusCode}");

            // Get the item again with conditional access set, no item should be returned
            response = await container.ReadItemAsync<SalesOrder>(
                partitionKey: new PartitionKey("Account2"),
                id: "SalesOrder2",
                requestOptions: new ItemRequestOptions() { IfNoneMatchEtag = itemResponse.ETag });

            Console.WriteLine("Read doc with StatusCode of {0}", response.StatusCode);

            // Now change something on the item, then do another get and this time we should get the item back
            response.Resource.TotalDue = 42;

            response = await container.ReplaceItemAsync<SalesOrder>(item, item.Id, new PartitionKey(item.AccountNumber));

            response = await container.ReadItemAsync<SalesOrder>(
                partitionKey: new PartitionKey("Account2"),
                id: "SalesOrder2",
                requestOptions: new ItemRequestOptions() { IfNoneMatchEtag = itemResponse.ETag });


            Console.WriteLine("Read doc with StatusCode of {0}", response.StatusCode);
        }
        // </UseETags>

        /// <summary>
        /// 4. Access items system defined properties
        /// </summary>
        /// <returns></returns>
        // <AccessSystemDefinedProperties>
        private static async Task AccessSystemDefinedProperties()
        {
            //******************************************************************************************************************
            // Items contain attributes that are system defined:
            // Timestamp : Gets the last modified timestamp associated with the item from the Azure Cosmos DB service.
            // Etag : Gets the entity tag associated with the item from the Azure Cosmos DB service.
            // TimeToLive : Gets the time to live in seconds of the item in the Azure Cosmos DB service.
            // 
            // See also: https://docs.microsoft.com/azure/cosmos-db/databases-containers-items#azure-cosmos-containers
            //******************************************************************************************************************
            Console.WriteLine("\n4 - Accessing system defined properties");

            //read a item's metadata

            Metadata itemResponse = await container.ReadItemAsync<Metadata>(
                partitionKey: new PartitionKey("Account1"),
                id: "SalesOrder1");

            Console.WriteLine("ETag of read item - {0}", itemResponse.Etag);

            Console.WriteLine("TimeToLive of read item - {0}", itemResponse.TimeToLive);

            Console.WriteLine("Timestamp of read item - {0}", itemResponse.Timestamp.ToShortDateString());
        }
        // </AccessSystemDefinedProperties>

        // <UseConsistencyLevels>
        private static async Task UseConsistencyLevels()

        {
            // Override the consistency level for a read request
            ItemResponse<SalesOrder> response = await container.ReadItemAsync<SalesOrder>(
                partitionKey: new PartitionKey("Account2"),
                id: "SalesOrder2",
                requestOptions: new ItemRequestOptions() { ConsistencyLevel = ConsistencyLevel.Eventual });
        }
        // </UseConsistencyLevels>

        private static async Task Cleanup()
        {
            if (database != null)
            {
                await database.DeleteAsync();
            }
        }

        private static async Task Initialize(CosmosClient client)
        {
            database = await client.CreateDatabaseIfNotExistsAsync(databaseId);

            // Delete the existing container to prevent create item conflicts
            using (await database.GetContainer(containerId).DeleteContainerStreamAsync())
            { }

            // We create a partitioned collection here which needs a partition key. Partitioned collections
            // can be created with very high values of provisioned throughput (up to Throughput = 250,000)
            // and used to store up to 250 GB of data. You can also skip specifying a partition key to create
            // single partition collections that store up to 10 GB of data.
            // For this demo, we create a collection to store SalesOrders. We set the partition key to the account
            // number so that we can retrieve all sales orders for an account efficiently from a single partition,
            // and perform transactions across multiple sales order for a single account number. 
            ContainerProperties containerProperties = new ContainerProperties(containerId, partitionKeyPath: "/AccountNumber");

            // Create with a throughput of 1000 RU/s
            container = await database.CreateContainerIfNotExistsAsync(
                containerProperties,
                throughput: 1000);
        }
    }
}

