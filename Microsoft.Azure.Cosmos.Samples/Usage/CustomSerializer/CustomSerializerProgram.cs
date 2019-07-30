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

            using (CosmosClient client = new CosmosClient(endpoint, key, clientOptions))
            {
                Database db = await client.CreateDatabaseIfNotExistsAsync("CustomSerializerTest");
                Container container = await db.CreateContainerIfNotExistsAsync("ContainerTest", "/id");

                SalesOrder salesOrder = new SalesOrder()
                {
                    Id = "SalesOrderId",
                    OrderDate = new DateTime(year: 2019, month: 2, day: 10, 5, 19, 42),
                    AccountNumber = "MyAccountNumberTest"
                };

                // Avoid conflicts if the item was left over from a previous run
                using (await container.DeleteItemStreamAsync(salesOrder.Id, new PartitionKey(salesOrder.Id)))
                { }

                ItemResponse<SalesOrder> itemResponse = await container.CreateItemAsync<SalesOrder>(salesOrder);

                SalesOrder createdItem = itemResponse;

                // Validate the JSON converter is working and converting all strings to upper case
                if (!string.Equals(salesOrder.AccountNumber.ToUpper(), createdItem.AccountNumber))
                {
                    throw new Exception("Customer serializer did not convert to upper case.");
                }

                // Verify that the JSON stored in Cosmos is valid
                using (ResponseMessage responseMessage = await container.ReadItemStreamAsync(salesOrder.Id, new PartitionKey(salesOrder.Id)))
                {
                    using (StreamReader sr = new StreamReader(responseMessage.Content))
                    {
                        string jsonString = await sr.ReadToEndAsync();

                        // Notice all fields are CamelCase, 
                        // DateTime is in MicrosoftDateFormat, 
                        // id value is lower case because it is only converted to upper case on read
                        string expectedStartString = "{\"id\":\"SalesOrderId\",\"orderDate\":\"\\/Date(1549804782000-0800)\\/\",\"shippedDate\":\"\\/Date(-62135596800000)\\/\",\"accountNumber\":\"MyAccountNumberTest\",\"subTotal\":0,\"taxAmount\":0,\"freight\":0,\"totalDue\":0";
                        if (!jsonString.StartsWith(expectedStartString))
                        {
                            throw new Exception("Customer serializer did not convert to the correct object");
                        }

                        Console.WriteLine(jsonString);
                    }
                }

                // Validate query uses the custom serializer
                QueryDefinition queryDefinition = new QueryDefinition("select * from T where T.accountNumber = @accountNumber and T.orderDate = @orderDate ")
                    .WithParameter("@accountNumber", salesOrder.AccountNumber)
                    .WithParameter("@orderDate", salesOrder.OrderDate);

                FeedIterator<SalesOrder> iterator = container.GetItemQueryIterator<SalesOrder>( queryDefinition);
                List<SalesOrder> salesOrders = new List<SalesOrder>();
                while (iterator.HasMoreResults)
                {
                    FeedResponse<SalesOrder> queryResponse = await iterator.ReadNextAsync();
                    salesOrders.AddRange(queryResponse);
                }

                if(salesOrders.Count != 1)
                {
                    throw new ArgumentException("Query should only have 1 result");
                }

                SalesOrder querySalesOrderItem = salesOrders.First();

                // Validate the JSON converter is working and converting all strings to upper case
                if (!string.Equals(salesOrder.AccountNumber.ToUpper(), querySalesOrderItem.AccountNumber))
                {
                    throw new Exception("Customer serializer did not convert to upper case for query.");
                }

                using(await db.DeleteStreamAsync()) { }
            }
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
