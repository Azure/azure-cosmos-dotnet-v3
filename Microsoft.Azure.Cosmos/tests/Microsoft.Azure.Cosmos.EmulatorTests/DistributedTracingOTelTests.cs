namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.EmulatorTests.Tracing;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests;
    using Microsoft.Azure.Cosmos.Tests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;
    using OpenTelemetry.Trace;
    using OpenTelemetry;
    using AzureCore = global::Azure.Core;
    using Microsoft.Azure.Cosmos.Telemetry;
    using System.Diagnostics;
    using Microsoft.Azure.Cosmos.Tracing;
    using System.Runtime.InteropServices;
    using System.Globalization;
    using System.Threading;
    using Newtonsoft.Json;
    using Microsoft.Azure.Cosmos.Telemetry.Models;
    using System.Net.Http;
    using System.Net;
    using Microsoft.Azure.Cosmos.Fluent;

    [VisualStudio.TestTools.UnitTesting.TestClass]
    public sealed class DistributedTracingOTelTests
    {
        private static CosmosClient client;
        private static Database database;

        [TestInitialize]
        public void TestInitialize()
        {
            CustomOtelExporter.ResetData();
        }

        [DataRow(true, $"{OpenTelemetryAttributeKeys.DiagnosticNamespace}.Operation")]
        [DataRow(true,  $"{OpenTelemetryAttributeKeys.DiagnosticNamespace}.Request")]
        [DataRow(false, $"{OpenTelemetryAttributeKeys.DiagnosticNamespace}.Operation")]
        [DataRow(false, $"{OpenTelemetryAttributeKeys.DiagnosticNamespace}.Request")]
        [TestMethod]
        public async Task OperationOrRequestSourceEnabled_ResultsInActivityCreation(bool useGateway, string source)
        {
            AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);
            AzureCore.ActivityExtensions.ResetFeatureSwitch();
            using (TracerProvider provider = Sdk.CreateTracerProviderBuilder()
                .AddCustomOtelExporter()
                .AddSource(source)
                .Build())
            {
                client = TestCommon.CreateCosmosClient(
                    useGateway: useGateway,
                    enableDistributingTracing: true);

                database = await client.CreateDatabaseAsync(
                        Guid.NewGuid().ToString(),
                        cancellationToken: default);
                Assert.AreEqual(1, CustomOtelExporter.CollectedActivities.Count());
                Assert.AreEqual(source, CustomOtelExporter.CollectedActivities.FirstOrDefault().Source.Name);
            }
        }

        [TestMethod]
        public async Task OperationOrRequestSourceEnabled_DirectMode_RecordsActivity()
        {
            AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);
            AzureCore.ActivityExtensions.ResetFeatureSwitch();
            using (TracerProvider provider = Sdk.CreateTracerProviderBuilder()
                .AddCustomOtelExporter()
                .AddSource($"{OpenTelemetryAttributeKeys.DiagnosticNamespace}.Operation")
                .AddSource($"{OpenTelemetryAttributeKeys.DiagnosticNamespace}.Request")
                .Build())
            {
                client = TestCommon.CreateCosmosClient(
                    useGateway: false,
                    enableDistributingTracing: true);

                DatabaseResponse dbResposne = await client.CreateDatabaseAsync(
                        Guid.NewGuid().ToString(),
                        cancellationToken: default);

                ContainerResponse containerResponse = await dbResposne.Database.CreateContainerAsync(
                    id: Guid.NewGuid().ToString(),
                    partitionKeyPath: "/id",
                    throughput: 20000);

                CosmosObject cosmosObject = CosmosObject.Create(
                    new Dictionary<string, CosmosElement>()
                    {
                    { "id", CosmosString.Create("1") }
                    });

                ItemResponse<JToken> createResponse = await containerResponse.Container.CreateItemAsync(JToken.Parse(cosmosObject.ToString()));

                //Asserts traceparent header in Direct mode request
                Assert.IsTrue(createResponse.RequestMessage.Headers.TryGetValue("traceparent", out string traceheader));
                Assert.IsNotNull(traceheader);

                //Asserts traceId in Diagnostics logs
                string diagnosticsCreateItem = createResponse.Diagnostics.ToString();
                JObject objDiagnosticsCreate = JObject.Parse(diagnosticsCreateItem);
                string distributedTraceId = (string)objDiagnosticsCreate["data"]["DistributedTraceId"];
                Assert.IsTrue(!string.IsNullOrEmpty(distributedTraceId));

                Assert.AreEqual(4, CustomOtelExporter.CollectedActivities.Count());
            }
        }

        [TestMethod]
        public async Task OperationOrRequestSourceEnabled_GatewayMode_RecordsActivity()
        {
            AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);
            AzureCore.ActivityExtensions.ResetFeatureSwitch();

            HttpClientHandlerHelper httpClientHandlerHelper = new HttpClientHandlerHelper
            {
                RequestCallBack = (request, cancellation) =>
                {
                    if (request.Headers.TryGetValues("traceparent", out IEnumerable<string> traceparentHeaderValues))
                    {
                        Assert.IsNotNull(traceparentHeaderValues);
                    }
                    return null;
                }
            };

            using (TracerProvider provider = Sdk.CreateTracerProviderBuilder()
                .AddCustomOtelExporter()
                .AddSource($"{OpenTelemetryAttributeKeys.DiagnosticNamespace}.Operation")
                .AddSource($"{OpenTelemetryAttributeKeys.DiagnosticNamespace}.Request")
                .Build())
            {
                client = TestCommon.CreateCosmosClient(
                          useGateway: false,
                          enableDistributingTracing: true,
                          httpClientFactory: () => new HttpClient(httpClientHandlerHelper));

                database = await client.CreateDatabaseAsync(
                        Guid.NewGuid().ToString(),
                        cancellationToken: default);

                Assert.AreEqual(1, CustomOtelExporter.CollectedActivities.Count());
            }
        }

        [DataRow(true)]
        [DataRow(false)]
        [TestMethod]
        public async Task NoSourceEnabled_ResultsInNoActivity(bool enableDistributingTracing)
        {
            AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", false);
            AzureCore.ActivityExtensions.ResetFeatureSwitch();
            using (TracerProvider provider = Sdk.CreateTracerProviderBuilder()
                .AddCustomOtelExporter()
                .Build())
            {
                client = TestCommon.CreateCosmosClient(
                    useGateway: false,
                    enableDistributingTracing: enableDistributingTracing);

                database = await client.CreateDatabaseAsync(
                        Guid.NewGuid().ToString(),
                        cancellationToken: default);
                Assert.AreEqual(0, CustomOtelExporter.CollectedActivities.Count());
            }
        }

        [TestCleanup]
        public async Task CleanUp()
        {
            if (database != null)
            {
                await database.DeleteStreamAsync();
            }

            client?.Dispose();
            AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", false);
            AzureCore.ActivityExtensions.ResetFeatureSwitch();
        }
    }
}