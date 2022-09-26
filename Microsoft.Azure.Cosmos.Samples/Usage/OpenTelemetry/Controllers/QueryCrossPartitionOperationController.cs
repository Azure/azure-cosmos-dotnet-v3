namespace OpenTelemetry.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Logging;
    using Models;
    using OpenTelemetry.Util;
    using WebApp.AspNetCore.Controllers;
    using WebApp.AspNetCore.Models;

    public class QueryCrossPartitionOperationController : Controller
    {
        private readonly ILogger<HomeController> logger;
        private readonly Container container;
        private readonly SuccessViewModel successModel = new SuccessViewModel();

        public QueryCrossPartitionOperationController(ILogger<HomeController> logger, CosmosDbSettings cosmosDbSettings)
        {
            this.logger = logger;
            this.container = CosmosClientInit.CreateClientAndContainer(
                connectionString: cosmosDbSettings.ConnectionString,
                mode: Enum.Parse<ConnectionMode>(cosmosDbSettings.ConnectionMode),
                dbAndContainerNameSuffix: "_large",
                isLargeContainer: true,
                isEnableOpenTelemetry: cosmosDbSettings.EnableOpenTelemetry).Result;
        }

        public IActionResult Index()
        {
            Task.Run(async () => await this.CrossPartitionQuery());

            this.successModel.CrossQueryOpsMessage = "Cross Partition Query Operation Triggered Successfully";

            return this.View(this.successModel);
        }

        private async Task CrossPartitionQuery()
        {
            await ToDoActivity.CreateRandomItems(
               container: this.container,
               pkCount: 2,
               perPKItemCount: 5);

            string sqlQueryText = "SELECT * FROM c";

            List<object> families = new List<object>();

            QueryDefinition queryDefinition = new QueryDefinition(sqlQueryText);
            using (FeedIterator<object> queryResultSetIterator = this.container.GetItemQueryIterator<object>(queryDefinition))
            {
                while (queryResultSetIterator.HasMoreResults)
                {
                    FeedResponse<object> currentResultSet = await queryResultSetIterator.ReadNextAsync();
                    foreach (object family in currentResultSet)
                    {
                        families.Add(family);
                    }
                }
            }

        }

        public IActionResult Privacy()
        {
            return this.View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return this.View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? this.HttpContext.TraceIdentifier });
        }
    }
}
