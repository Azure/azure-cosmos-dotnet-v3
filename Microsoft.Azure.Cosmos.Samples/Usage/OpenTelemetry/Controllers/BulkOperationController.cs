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

    public class BulkOperationController : Controller
    {
        private readonly ILogger<HomeController> logger;
        private readonly Container container;
        private readonly SuccessViewModel successModel = new SuccessViewModel();

        public BulkOperationController(ILogger<HomeController> logger, Container container)
        {
            this.logger = logger;
            this.container = container;
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
