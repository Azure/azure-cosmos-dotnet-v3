using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Cosmos.Samples.AzureFunctions
{
    // ----------------------------------------------------------------------------------------------------------
    // Prerequisites - 
    // 
    // 1. An Azure Cosmos account - 
    //    https://docs.microsoft.com/azure/cosmos-db/create-cosmosdb-resources-portal
    //
    // 2. Microsoft.Azure.Cosmos NuGet package - 
    //    http://www.nuget.org/packages/Microsoft.Azure.Cosmos/ 
    // ----------------------------------------------------------------------------------------------------------
    // Sample - demonstrates how to maintain a Cosmos client and reuse the instance among Azure Function executions.
    //
    // More information: https://github.com/Azure/azure-functions-host/wiki/Managing-Connections

    public class AzureFunctionsCosmosClient
    {
        private CosmosClient cosmosClient;
        public AzureFunctionsCosmosClient(CosmosClient cosmosClient)
        {
            this.cosmosClient = cosmosClient;
        }


        [FunctionName("CosmosClient")]
        public async Task<IActionResult> Run(
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

            var container = this.cosmosClient.GetContainer("mydb", "mycoll");

            try
            {
                var result = await container.CreateItemAsync<Item>(data, new PartitionKey(data.Id));
                return new OkObjectResult(result.Resource.Id);
            }
            catch (CosmosException cosmosException)
            {
                log.LogError("Creating item failed with error {0}", cosmosException.ToString());
                return new BadRequestObjectResult($"Failed to create item. Cosmos Status Code {cosmosException.StatusCode}, Sub Status Code {cosmosException.SubStatusCode}: {cosmosException.Message}.");
            }
        }
    }
}
