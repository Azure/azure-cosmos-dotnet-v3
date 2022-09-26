namespace OpenTelemetry.Controllers
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Logging;
    using Models;
    using OpenTelemetry.Util;
    using WebApp.AspNetCore.Controllers;
    using WebApp.AspNetCore.Models;

    public class MultiRegionPointOperationController : Controller
    {
        private readonly ILogger<HomeController> logger;
        private readonly Container container;
        private readonly SuccessViewModel successModel = new SuccessViewModel();

        public MultiRegionPointOperationController(ILogger<HomeController> logger)
        {
            this.logger = logger;
            this.container = CosmosClientInit.multiRegionAccount;
        }

        public IActionResult Index()
        {
            Task.Run(async () =>
            {
                // Create an item
                ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity("MyTestPkValue");
                ItemResponse<ToDoActivity> createResponse = await this.container.CreateItemAsync<ToDoActivity>(testItem);
                ToDoActivity testItemCreated = createResponse.Resource;

                // Read an Item
                await this.container.ReadItemAsync<ToDoActivity>(testItem.id, new Microsoft.Azure.Cosmos.PartitionKey(testItem.id));

                // Read failure scenario Item
                await this.container.ReadItemAsync<ToDoActivity>(new Guid().ToString(), new Microsoft.Azure.Cosmos.PartitionKey(testItem.id));

                // Upsert an Item
                await this.container.UpsertItemAsync<ToDoActivity>(testItem);

                // Replace an Item
                await this.container.ReplaceItemAsync<ToDoActivity>(testItemCreated, testItemCreated.id.ToString());

                // Delete an Item
                await this.container.DeleteItemAsync<ToDoActivity>(testItem.id, new Microsoft.Azure.Cosmos.PartitionKey(testItem.id));

            });

            this.successModel.MrPointOpsMessage = "Point Operation Triggered (Multi region) Successfully With one failure Scenario";

            return this.View(this.successModel);
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
