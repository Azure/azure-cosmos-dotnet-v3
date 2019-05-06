using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using AzureFunctions.Models;

namespace AzureFunctions
{
    // ----------------------------------------------------------------------------------------------------------
    // Prerequisites - 
    // 
    // 1. An Azure Cosmos account - 
    //    https://azure.microsoft.com/en-us/itemation/articles/itemdb-create-account/
    //
    // 2. Microsoft.Azure.Cosmos NuGet package - 
    //    http://www.nuget.org/packages/Microsoft.Azure.Cosmos/ 
    // ----------------------------------------------------------------------------------------------------------
    // Sample - demonstrates how to maintain a Cosmos client and reuse the instance among Azure Function executions.
    //
    // More information: https://github.com/Azure/azure-functions-host/wiki/Managing-Connections

    public static class AzureFunctionsCosmosClient
    {
        private static Lazy<CosmosClient> lazyClient = new Lazy<CosmosClient>(InitializeCosmosClient);
        private static CosmosClient cosmosClient => lazyClient.Value;
        private static IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(Environment.CurrentDirectory)
                .AddJsonFile("AppSettings.json", optional: true, reloadOnChange: true)
                .Build();

        /// <summary>
        /// Initialize a static instance of the <see cref="CosmosClient"/>.
        /// </summary>
        /// <returns></returns>
        private static CosmosClient InitializeCosmosClient()
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

            CosmosClientBuilder cosmosClientBuilder = new CosmosClientBuilder(endpoint, authKey);
            var cosmosClient = cosmosClientBuilder.Build();

            // Optional. Initialize container
            CosmosDatabaseResponse databaseResponse = cosmosClient.Databases.CreateDatabaseIfNotExistsAsync("mydb").GetAwaiter().GetResult();
            CosmosDatabase database = databaseResponse.Database;

            var containerResponse = database.Containers.CreateContainerIfNotExistsAsync("mycoll", "/id").GetAwaiter().GetResult();

            return cosmosClient;
        }

        //private static CosmosContainer Container 

        [FunctionName("CosmosClient")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            Item data = JsonConvert.DeserializeObject<Item>(requestBody);
            if (data == null)
            {
                return new BadRequestObjectResult($"Cannot parse body.");
            }

            if (string.IsNullOrEmpty(data.Id))
            {
                data.Id = Guid.NewGuid().ToString();
            }

            var container = cosmosClient.Databases["mydb"].Containers["mycoll"];

            try
            {
                var result = await container.Items.CreateItemAsync<Item>(data.Id, data);
                return new OkObjectResult(result.Resource.Id);
            }
            catch (CosmosException cosmosException)
            {
                return new BadRequestObjectResult($"Failed to create item. Cosmos Status Code {cosmosException.StatusCode}, Sub Status Code {cosmosException.SubStatusCode}: {cosmosException.Message}.");
            }
        }
    }
}
