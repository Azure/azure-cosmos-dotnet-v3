namespace Cosmos.Samples.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.ConstrainedExecution;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json;

    internal class PriorityBasedExecution
    {
        // Async main requires c# 7.1 which is set in the csproj with the LangVersion attribute
        // <Main>
        private class Document
        {
            [JsonProperty(PropertyName = "id")]
            public string Id { get; set; }
            [JsonProperty(PropertyName = "name")]
            public string Name { get; set; }
            [JsonProperty(PropertyName = "address")]
            public string Address { get; set; }
            [JsonProperty(PropertyName = "pkey")]
            public string Pkey { get; set; }
        }

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

                        Container container = await database.CreateContainerIfNotExistsAsync("mycoll", "/id");

                        Document doc = new Document()
                        {
                            Id = "id",
                            Address = "address1",
                            Pkey = "pkey1"
                        };

                        await container.CreateItemAsync<Document>(doc, new PartitionKey("pkey1"), new RequestOptions() { PriorityLevel = PriorityLevel.High });

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
    }
}
