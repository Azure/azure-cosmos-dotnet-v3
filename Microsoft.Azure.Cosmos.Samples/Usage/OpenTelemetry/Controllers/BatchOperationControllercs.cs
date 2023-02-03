namespace AppInsightsIntegration.Controllers
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Logging;
    using OpenTelemetry.Models;
    using OpenTelemetry.Util;

    public class BatchOperationControllercs : Controller
    {
        private readonly ILogger<BatchOperationControllercs> logger;
        private readonly Container container;
        private readonly SuccessViewModel successModel = new SuccessViewModel();

        public BatchOperationControllercs(ILogger<BatchOperationControllercs> logger)
        {
            this.logger = logger;
            this.container = CosmosClientInit.singleRegionAccount;
        }

        public IActionResult Index()
        {
            Task.Run(async () =>
            {
                string pkValue = "TestPk";
                TransactionalBatch batch = this.container.CreateTransactionalBatch(new PartitionKey(pkValue));
                List<PatchOperation> patch = new List<PatchOperation>()
                {
                    PatchOperation.Remove("/cost")
                };

                List<ToDoActivity> createItems = new List<ToDoActivity>();
                for (int i = 0; i < 50; i++)
                {
                    ToDoActivity item = ToDoActivity.CreateRandomToDoActivity(pk: pkValue);
                    createItems.Add(item);
                    batch.CreateItem<ToDoActivity>(item);
                }

                for (int i = 0; i < 20; i++)
                {
                    batch.ReadItem(createItems[i].id);
                    batch.PatchItem(createItems[i].id, patch);
                }

                TransactionalBatchRequestOptions requestOptions = null;
                TransactionalBatchResponse response = await batch.ExecuteAsync(requestOptions);

            });

            this.successModel.BulkOpsMessage = "Batch Operation Triggered Successfully";

            return this.View(this.successModel);
        }

    }
}
