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
        private static readonly Container container;
        private CosmosClientBuilder cosmosClientBuilder;
        private List<string> preferredRegionList;

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
        public async Task OperationOrRequestSourceEnabled_ResultsInActivityCreation2()
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
                
                Assert.IsTrue(createResponse.RequestMessage.Headers.TryGetValue("traceparent", out string traceheader));
                Assert.IsNotNull(traceheader);
                
                string diagnosticsCreateItem = createResponse.Diagnostics.ToString();
                JObject objDiagnosticsCreate = JObject.Parse(diagnosticsCreateItem);
                string distributedTraceId = (string)objDiagnosticsCreate["data"]["DistributedTraceId"];
                Assert.IsTrue(!string.IsNullOrEmpty(distributedTraceId));
                Assert.AreEqual(4, CustomOtelExporter.CollectedActivities.Count());
            }
        }

        [TestMethod]
        public async Task NoSourceEnabled_ResultsInNoActivity2()
        {
            AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);
            AzureCore.ActivityExtensions.ResetFeatureSwitch();

            HttpClientHandlerHelper httpHandler = new HttpClientHandlerHelper();
            HttpClient httpClient = new HttpClient(httpHandler);
            string traceparentHeader = null;

            httpHandler.RequestCallBack = (request, cancellation) =>
            {
                if (request.RequestUri.AbsoluteUri.Equals(ClientTelemetryOptions.GetClientTelemetryEndpoint().AbsoluteUri))
                {
                    traceparentHeader = request.Headers.GetValues("traceparent").FirstOrDefault();
                    HttpResponseMessage result = new HttpResponseMessage(HttpStatusCode.OK);

                    string jsonObject = request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    return Task.FromResult(result);
                }
                return null;
            };

            this.preferredRegionList = new List<string>
            {
                "region1",
                "region2"
            };
            this.cosmosClientBuilder = TestCommon.GetDefaultConfiguration()
                                        .WithApplicationPreferredRegions(this.preferredRegionList)
                                        .WithHttpClientFactory(() => new HttpClient(httpHandler));

            this.SetClient(mode == ConnectionMode.Gateway
                ? this.cosmosClientBuilder.WithConnectionModeGateway().Build()
                : this.cosmosClientBuilder.Build());

            this.database = await this.GetClient().CreateDatabaseAsync(Guid.NewGuid().ToString());

            using (TracerProvider provider = Sdk.CreateTracerProviderBuilder()
                .AddCustomOtelExporter()
                .AddSource($"{OpenTelemetryAttributeKeys.DiagnosticNamespace}.Operation")
                .AddSource($"{OpenTelemetryAttributeKeys.DiagnosticNamespace}.Request")
                .Build())
            {
                client = TestCommon.CreateCosmosClient(
                    useGateway: false,
                    enableDistributingTracing: true);

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
        private static void AssertAndResetActivityInformation()
        {
            AssertActivity.AreEqualAcrossListeners();
            CustomOtelExporter.CollectedActivities = new();
        }
    }
}