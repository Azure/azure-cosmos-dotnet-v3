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

        [DataRow($"{OpenTelemetryAttributeKeys.DiagnosticNamespace}.Operation", $"{OpenTelemetryAttributeKeys.DiagnosticNamespace}.Request")]
        [DataRow($"{OpenTelemetryAttributeKeys.DiagnosticNamespace}.Request", null)]
        [DataRow($"{OpenTelemetryAttributeKeys.DiagnosticNamespace}.Operation", null)]
        [TestMethod]
        public async Task SourceEnabled_FlagOn_DirectMode_RecordsActivity_AssertLogTraceId_AssertTraceparent(string firstSource, string secondSource)
        {
            AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);
            AzureCore.ActivityExtensions.ResetFeatureSwitch();
            
            string[] sources = new string[] {firstSource, secondSource};
            sources = sources.Where(x => x != null).ToArray();

            using (TracerProvider provider = Sdk.CreateTracerProviderBuilder()
                .AddCustomOtelExporter()
                .AddSource(sources)
                .Build())
            {
                client = TestCommon.CreateCosmosClient(
                useGateway: false,
                enableDistributingTracing: true);

                DatabaseResponse dbResponse = await client.CreateDatabaseAsync(
                        Guid.NewGuid().ToString(),
                        cancellationToken: default);
                database = dbResponse.Database;
                ContainerResponse containerResponse = await database.CreateContainerAsync(
                        id: Guid.NewGuid().ToString(),
                        partitionKeyPath: "/id",
                        throughput: 20000);

                CosmosObject cosmosObject = CosmosObject.Create(
                    new Dictionary<string, CosmosElement>()
                    {
                        { "id", CosmosString.Create("1") }
                    });

                ItemResponse<JToken> createResponse = await containerResponse.Container.CreateItemAsync(JToken.Parse(cosmosObject.ToString()));

                //Assert traceparent header in Direct mode request
                Assert.IsTrue(createResponse.RequestMessage.Headers.TryGetValue("traceparent", out string traceheader));
                Assert.IsNotNull(traceheader);

                //Assert traceId in Diagnostics logs
                string diagnosticsCreateItem = createResponse.Diagnostics.ToString();
                JObject objDiagnosticsCreate = JObject.Parse(diagnosticsCreateItem);
                string distributedTraceId = (string)objDiagnosticsCreate["data"]["DistributedTraceId"];
                Assert.IsFalse(string.IsNullOrEmpty(distributedTraceId));

                //Assert diagnostics log trace id is same as parent trace id of the activity
                string operationName = (string)objDiagnosticsCreate["name"];
                string traceIdCreateItem = CustomOtelExporter.CollectedActivities.Where(x => x.OperationName.Contains(operationName)).FirstOrDefault().TraceId.ToString();
                Assert.AreEqual(distributedTraceId, traceIdCreateItem);

                //Assert activity creation
                Assert.IsNotNull(CustomOtelExporter.CollectedActivities);

                // Assert activity created at network level have an existing parent activity
                Activity networkLevelChildActivity = CustomOtelExporter.CollectedActivities.Where(x => x.OperationName.Contains("Request")).FirstOrDefault();
                Assert.IsNotNull(CustomOtelExporter.CollectedActivities.Where(x => x.Id == networkLevelChildActivity.ParentId));
            }
        }

        [DataRow($"{OpenTelemetryAttributeKeys.DiagnosticNamespace}.Operation", $"{OpenTelemetryAttributeKeys.DiagnosticNamespace}.Request")]
        [DataRow($"{OpenTelemetryAttributeKeys.DiagnosticNamespace}.Request", null)]
        [DataRow($"{OpenTelemetryAttributeKeys.DiagnosticNamespace}.Operation", null)]
        [TestMethod]
        public async Task SourceEnabled_FlagOn_GatewayMode_RecordsActivity_AssertLogTraceId_AssertTraceparent(string firstSource, string secondSource)
        {
            AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);
            AzureCore.ActivityExtensions.ResetFeatureSwitch();

            string[] sources = new string[] { firstSource, secondSource };
            sources = sources.Where(x => x != null).ToArray();

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
                .AddSource(sources)
                .Build())
            {
                client = TestCommon.CreateCosmosClient(
                        useGateway: true,
                        customizeClientBuilder: builder => builder.WithHttpClientFactory(() => new HttpClient(httpClientHandlerHelper)),
                        enableDistributingTracing: true);

                DatabaseResponse dbResponse = await client.CreateDatabaseAsync(
                        Guid.NewGuid().ToString(),
                        cancellationToken: default);
                database = dbResponse.Database;

                //Assert traceId in Diagnostics logs
                string diagnosticsCreateDB = dbResponse.Diagnostics.ToString();
                JObject objDiagnosticsCreate = JObject.Parse(diagnosticsCreateDB);
                string distributedTraceId = (string)objDiagnosticsCreate["data"]["DistributedTraceId"];
                Assert.IsFalse(string.IsNullOrEmpty(distributedTraceId));

                //Assert diagnostics log trace id is same as parent trace id of the activity
                string operationName = (string)objDiagnosticsCreate["name"];
                string traceIdCreateDB = CustomOtelExporter.CollectedActivities.Where(x => x.OperationName.Contains(operationName)).FirstOrDefault().TraceId.ToString();
                Assert.AreEqual(distributedTraceId, traceIdCreateDB);

                //Assert activity creation
                Assert.IsNotNull(CustomOtelExporter.CollectedActivities);
            }
        }

        [DataRow(false, true)]
        [DataRow(true, true)]
        [DataRow(false, false)]
        [DataRow(true, false)]
        [TestMethod]
        public async Task NoSourceEnabled_ResultsInNoSourceParentActivityCreation_AssertLogTraceId(bool useGateway, bool enableDistributingTracing)
        {
            AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", false);
            AzureCore.ActivityExtensions.ResetFeatureSwitch();
            using (TracerProvider provider = Sdk.CreateTracerProviderBuilder()
                .AddCustomOtelExporter()
                .Build())
            {
                client = TestCommon.CreateCosmosClient(
                useGateway: useGateway,
                enableDistributingTracing: enableDistributingTracing);

                DatabaseResponse dbResponse = await client.CreateDatabaseAsync(
                       Guid.NewGuid().ToString(),
                       cancellationToken: default);
                database = dbResponse.Database;

                //Assert traceId in Diagnostics logs
                string diagnosticsCreateDB = dbResponse.Diagnostics.ToString();
                JObject objDiagnosticsCreate = JObject.Parse(diagnosticsCreateDB);

                if (enableDistributingTracing)
                {
                    //DistributedTraceId present in logs
                    string distributedTraceId = (string)objDiagnosticsCreate["data"]["DistributedTraceId"];
                    Assert.IsFalse(string.IsNullOrEmpty(distributedTraceId));
                }
                else
                {
                    //DistributedTraceId field not present in logs
                    Assert.IsNull((string)objDiagnosticsCreate["data"]["DistributedTraceId"]);
                }

                //Assert no activity with attached source is created
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