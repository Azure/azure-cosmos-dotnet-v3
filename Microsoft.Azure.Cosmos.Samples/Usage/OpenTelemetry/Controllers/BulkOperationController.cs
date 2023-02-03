namespace OpenTelemetry.Controllers
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Logging;
    using Models;
    using WebApp.AspNetCore.Models;
    using OpenTelemetry.Util;

    public class BulkOperationController : Controller
    {
        private readonly ILogger<BulkOperationController> logger;
        private readonly Container container;
        private readonly SuccessViewModel successModel = new SuccessViewModel();

        public BulkOperationController(ILogger<BulkOperationController> logger)
        {
            this.logger = logger;
            this.container = CosmosClientInit.singleRegionAccountWithbulk;
        }

        public IActionResult Index()
        {
            Task.Run(async () =>
            {
                List<Task> concurrentTasks = new List<Task>();
                for (int i = 0; i < 10; i++)
                {
                    ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity("MyTestPkValue");
                    concurrentTasks.Add(this.container.CreateItemAsync<ToDoActivity>(testItem));
                }

                await Task.WhenAll(concurrentTasks);

            });

            this.successModel.BulkOpsMessage = "Bulk Operation Triggered Successfully";

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
