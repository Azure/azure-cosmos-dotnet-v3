namespace CustomSerializer
{
    using Cosmos.Samples.Shared;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    class CustomSerializerProgram
    {
        static async Task Main(string[] args)
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

                CustomSerializerProgram customSerializerProgram = new CustomSerializerProgram();
                await customSerializerProgram.CustomSerializerSettings(endpoint, authKey);
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

        public async Task CustomSerializerSettings(string endpoint, string key)
        {
            JsonSerializerSettings settings = new JsonSerializerSettings()
            {
                DateFormatHandling = DateFormatHandling.MicrosoftDateFormat,
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                Converters = new List<JsonConverter>() { new ToUpperOnReadJsonConverter() },
                NullValueHandling = NullValueHandling.Ignore
            };

            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                Serializer = new CosmosJsonDotNetSerializer(settings)
            };

            CosmosClient clientWithCutomSerializer = null;
            CosmosClient clientWithDefaultSerializer = null;

            try
            {
                clientWithCutomSerializer = new CosmosClient(endpoint, key, clientOptions);
                clientWithDefaultSerializer = new CosmosClient(endpoint, key);

                string dbName = "CustomSerializerTest";
                string containerName = "ContainerTest";
                Database db = await clientWithCutomSerializer.CreateDatabaseIfNotExistsAsync(dbName);

                Container containerWithCustomSerializer = await db.CreateContainerIfNotExistsAsync(containerName, "/id");
                Container containerWithDefaultSerializer = clientWithDefaultSerializer.GetContainer(dbName, containerName);

                string accountNumber = "MyAccountNumberTest";
                DateTime orderDateTime = new DateTime(year: 2019, month: 2, day: 10, 5, 19, 42);
                SalesOrder salesOrder = new SalesOrder()
                {
                    Id = "SalesOrderId",
                    OrderDate = orderDateTime,
                    AccountNumber = accountNumber,
                    SubTotal = 42,
                    TaxAmount = 42.042m,
                    TotalDue = 9000,
                    Freight = 42000
                };

                SalesOrder salesOrder2 = new SalesOrder()
                {
                    Id = "SalesOrderId2",
                    OrderDate = orderDateTime,
                    AccountNumber = accountNumber,
                    SubTotal = 42,
                    TaxAmount = 42.042m,
                    TotalDue = 9000,
                    Freight = 42000
                };

                // Avoid conflicts if the item was left over from a previous run
                using (await containerWithCustomSerializer.DeleteItemStreamAsync(salesOrder.Id, new PartitionKey(salesOrder.Id)))
                { }
                using (await containerWithCustomSerializer.DeleteItemStreamAsync(salesOrder2.Id, new PartitionKey(salesOrder2.Id)))
                { }

                // Create item with custom serializer
                SalesOrder customResponse = await containerWithCustomSerializer.CreateItemAsync<SalesOrder>(salesOrder);

                // Create item with default serializer
                SalesOrder defaultResponse = await containerWithDefaultSerializer.CreateItemAsync<SalesOrder>(salesOrder2);

                Console.WriteLine($"AccountNumbers comparison");
                Console.WriteLine($"Default serializer: {defaultResponse.AccountNumber}");
                Console.WriteLine($"Custom serializer: {customResponse.AccountNumber}\n");

                // Validate the JSON converter is working and converting all strings to upper case
                if (!string.Equals(defaultResponse.AccountNumber.ToUpper(), customResponse.AccountNumber))
                {
                    throw new Exception("Customer serializer did not convert to upper case.");
                }

                // Get JSON string for the item created with custom serializer
                string jsonStringWithCustomSerialier = await this.GetJsonString(containerWithCustomSerializer, salesOrder);

                // Get JSON string for the item created with default serializer
                string jsonStringWithDefaultSerialier = await this.GetJsonString(containerWithDefaultSerializer, salesOrder2);

                // Compare the two JSON strings to show the difference between the two serializers
                Console.WriteLine($"JSON string with default serializer: \n{jsonStringWithDefaultSerialier}\n");
                Console.WriteLine($"JSON string with custom serializer (ignores null, uses camel casing, and uses MicrosoftDateFormat):\n {jsonStringWithCustomSerialier}\n");
                
                // Verify the JSON string are different
                if (string.Equals(jsonStringWithCustomSerialier, jsonStringWithDefaultSerialier))
                {
                    throw new Exception("Customer serializer was not used for the query.");
                }

                // Get items with queries using a custom serializer
                QueryDefinition queryDefinitionForCustomSerializer = new QueryDefinition("select * from T where T.accountNumber = @accountNumber and T.orderDate = @orderDate ")
                    .WithParameter("@accountNumber", salesOrder.AccountNumber)
                    .WithParameter("@orderDate", salesOrder.OrderDate);
                var queryCustomSalesOrderItem = await QueryItems(
                    containerWithCustomSerializer,
                    queryDefinitionForCustomSerializer);

                QueryDefinition queryDefinitionForDefaultSerializer = new QueryDefinition("select * from T where T.AccountNumber = @accountNumber and T.OrderDate = @orderDate ")
                    .WithParameter("@accountNumber", salesOrder2.AccountNumber)
                    .WithParameter("@orderDate", salesOrder2.OrderDate);
                var queryDefaultSalesOrderItem = await QueryItems(
                    containerWithDefaultSerializer,
                    queryDefinitionForDefaultSerializer);

                Console.WriteLine($"Query comparison");
                Console.WriteLine($"Default serializer Id: {queryDefaultSalesOrderItem.Id}, Account Number: {queryDefaultSalesOrderItem.AccountNumber}");
                Console.WriteLine($"Custom serializer Id: {queryCustomSalesOrderItem.Id}, Account Number: {queryCustomSalesOrderItem.AccountNumber}\n");

                // Validate the default query result matches the original account number
                if (!string.Equals(salesOrder2.AccountNumber, queryDefaultSalesOrderItem.AccountNumber))
                {
                    throw new Exception("Default serializer account numbers should match");
                }

                // Validate the JSON converter is working and converting all strings to upper case
                if (!string.Equals(salesOrder.AccountNumber.ToUpper(), queryCustomSalesOrderItem.AccountNumber))
                {
                    throw new Exception("Custom serializer should do ToUpper() to account numbers.");
                }

                using (await db.DeleteStreamAsync()) { }
            }
            finally
            {
                clientWithDefaultSerializer.Dispose();
                clientWithCutomSerializer.Dispose();
            }
        }

        public async Task<string> GetJsonString(Container container, SalesOrder salesOrder)
        {
            using (ResponseMessage responseMessage = await container.ReadItemStreamAsync(salesOrder.Id, new PartitionKey(salesOrder.Id)))
            {
                using (StreamReader sr = new StreamReader(responseMessage.Content))
                {
                    return await sr.ReadToEndAsync();
                }
            }
        }

        public async Task<SalesOrder> QueryItems(
            Container container,
            QueryDefinition queryDefinition)
        {
            FeedIterator<SalesOrder> iterator = container.GetItemQueryIterator<SalesOrder>(queryDefinition);
            List<SalesOrder> salesOrders = new List<SalesOrder>();
            while (iterator.HasMoreResults)
            {
                FeedResponse<SalesOrder> queryResponse = await iterator.ReadNextAsync();
                salesOrders.AddRange(queryResponse);
            }

            if (salesOrders.Count != 1)
            {
                throw new ArgumentException("Query should only have 1 result");
            }

            return salesOrders.First();
        }

        /// <summary>
        /// Convert all strings to upper case only on reads
        /// </summary>
        private class ToUpperOnReadJsonConverter : JsonConverter<string>
        {
            public override bool CanRead => true;
            public override bool CanWrite => false;

            public override string ReadJson(JsonReader reader, Type objectType, string existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                string s = (string)reader.Value;

                return s?.ToUpper();
            }

            public override void WriteJson(JsonWriter writer, string value, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }
        }
    }
}
