namespace Cosmos.Samples.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json.Linq;

    internal class Program
    {
        private static string databaseName = "";
        private static string containerName = "";

        public static async Task Main(string[] _)
        {
            try
            {
                IConfigurationRoot configuration = new ConfigurationBuilder()
                    .AddJsonFile("appSettings.json")
                    .Build();
                
                databaseName = configuration["databaseName"];
                containerName = configuration["containerName"];


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

        private static async Task RunDemoAsync(CosmosClient client)
        {
            Container container = client.GetDatabase(databaseName).GetContainer(containerName);

            QueryDefinition query = new QueryDefinition("SELECT * FROM c ORDER by ky asc");
            string prevky = null;
            HashSet<string> ids = new HashSet<string>();
            
            using (FeedIterator<JObject> resultSetIterator = container.GetItemQueryIterator<JObject>(query))
            {
                while (resultSetIterator.HasMoreResults)
                {
                    FeedResponse<JObject> response = await resultSetIterator.ReadNextAsync();
                    foreach (JObject result in response)
                    {
                        string currentky = result["ky"].Value<string>();
                        string currentid = result["id"].Value<string>();

                        if(prevky != currentky)
                        {
                            ids.Clear();
                        }

                        if (ids.Contains(currentid))
                        {
                            Console.WriteLine("ky: " + currentky + " id: " + currentid);
                        }

                        ids.Add(currentid);
                        prevky = currentky;
                    }
                }
            }
        }
    }
}

