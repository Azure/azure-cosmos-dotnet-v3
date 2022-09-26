namespace OpenTelemetry.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Net.Http;
    using System.Runtime.Serialization.Formatters.Binary;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Logging;
    using Models;
    using Newtonsoft.Json.Converters;
    using Newtonsoft.Json;
    using WebApp.AspNetCore.Controllers;
    using WebApp.AspNetCore.Models;
    using System.Text;
    using OpenTelemetry.Util;

    public class StreamOperationController : Controller
    {
        private readonly ILogger<HomeController> logger;
        private readonly Container container;
        private readonly SuccessViewModel successModel = new SuccessViewModel();

        public StreamOperationController(ILogger<HomeController> logger)
        {
            this.logger = logger;
            this.container = CosmosClientInit.singleRegionAccount;
        }

        public IActionResult Index()
        {
            Task.Run(async () =>
            {
                // Create an item
                var testItem = new { id = "MyTestItemId", partitionKeyPath = "MyTestPkValue", details = "it's working", status = "done" };

                var ms = this.ToStream(testItem);

                await this.container
                    .CreateItemStreamAsync(ms,
                    new PartitionKey(testItem.id));
           
                //Upsert an Item
                await this.container.UpsertItemStreamAsync(ms, new PartitionKey(testItem.id));

                //Read an Item
                await this.container.ReadItemStreamAsync(testItem.id, new PartitionKey(testItem.id));

                //Replace an Item
                await this.container.ReplaceItemStreamAsync(ms, testItem.id, new PartitionKey(testItem.id));

                // Patch an Item
                List<PatchOperation> patch = new List<PatchOperation>()
            {
                PatchOperation.Add("/new", "patched")
            };
                await this.container.PatchItemStreamAsync(
                    partitionKey: new PartitionKey(testItem.id),
                    id: testItem.id,
                    patchOperations: patch);

                //Delete an Item
                await this.container.DeleteItemStreamAsync(testItem.id, new PartitionKey(testItem.id));


            });

            this.successModel.StreamOpsMessage = "Stream Operation Triggered Successfully";

            return this.View(this.successModel);
        }


        public Stream ToStream<T>(T input)
        {
            MemoryStream streamPayload = new MemoryStream();
            using (StreamWriter streamWriter = new StreamWriter(streamPayload, encoding: new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true), bufferSize: 1024, leaveOpen: true))
            {
                using (JsonWriter writer = new JsonTextWriter(streamWriter))
                {
                    writer.Formatting = Newtonsoft.Json.Formatting.None;
                    JsonSerializer jsonSerializer = new JsonSerializer();
                    jsonSerializer.Serialize(writer, input);
                    writer.Flush();
                    streamWriter.Flush();
                }
            }
            streamPayload.Position = 0;
            return streamPayload;
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
