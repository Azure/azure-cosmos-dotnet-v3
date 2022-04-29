namespace WebApp.AspNetCore.Controllers
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests;
    using Microsoft.Extensions.Logging;

    using WebApp.AspNetCore.Models;

    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        private CosmosClientBuilder cosmosClientBuilder;
        private CosmosClient cosmosClient;
        private Database database;
            
        public HomeController(ILogger<HomeController> logger)
        {
            this.cosmosClientBuilder = new CosmosClientBuilder(accountEndpoint: "https://localhost:8081", authKeyOrResourceToken: "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==");

            this._logger = logger;

        }

        public IActionResult Index()
        {
            Task.Run(async () =>
            {
                Container container = await this.CreateClientAndContainer(ConnectionMode.Direct);
                // Create an item
                ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity("MyTestPkValue");
                ItemResponse<ToDoActivity> createResponse = await container.CreateItemAsync<ToDoActivity>(testItem);
                ToDoActivity testItemCreated = createResponse.Resource;

                this._logger.LogInformation("Item created");

                // Read an Item
                await container.ReadItemAsync<ToDoActivity>(testItem.id, new Microsoft.Azure.Cosmos.PartitionKey(testItem.id));

                // Upsert an Item
                await container.UpsertItemAsync<ToDoActivity>(testItem);

                // Replace an Item
                await container.ReplaceItemAsync<ToDoActivity>(testItemCreated, testItemCreated.id.ToString());

                // Delete an Item
                await container.DeleteItemAsync<ToDoActivity>(testItem.id, new Microsoft.Azure.Cosmos.PartitionKey(testItem.id));

            });

            return this.View();
        }

        private async Task<Container> CreateClientAndContainer(ConnectionMode mode,
           Microsoft.Azure.Cosmos.ConsistencyLevel? consistency = null,
           bool isLargeContainer = false)
        {
            if (consistency.HasValue)
            {
                this.cosmosClientBuilder = this.cosmosClientBuilder.WithConsistencyLevel(consistency.Value);
            }

            this.cosmosClient = mode == ConnectionMode.Gateway
                ? this.cosmosClientBuilder.WithConnectionModeGateway().Build()
                : this.cosmosClientBuilder.Build();

            this.database = await this.cosmosClient.CreateDatabaseAsync(Guid.NewGuid().ToString());

            return await this.database.CreateContainerAsync(
                id: Guid.NewGuid().ToString(),
                partitionKeyPath: "/id",
                throughput: isLargeContainer ? 15000 : 400);

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
