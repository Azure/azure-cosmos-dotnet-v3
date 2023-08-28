//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;
    using OpenTelemetry.Trace;
    using OpenTelemetry;
    using AzureCore = global::Azure.Core;
    using Microsoft.Azure.Cosmos.Telemetry;
    using System.Diagnostics;
    using Microsoft.Azure.Cosmos.Tracing;
    using System.Net.Http;

    [VisualStudio.TestTools.UnitTesting.TestClass]
    public sealed class DistributedTracingOTelTests : BaseCosmosClientHelper
    {
        [TestInitialize]
        public void TestInitialize()
        {
            AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);
            AzureCore.ActivityExtensions.ResetFeatureSwitch();
        }

        [DataTestMethod]
        [DataRow($"{OpenTelemetryAttributeKeys.DiagnosticNamespace}.Operation", $"{OpenTelemetryAttributeKeys.DiagnosticNamespace}.Request", DisplayName = "DirectMode and DistributedFlag On: Asserts activity creation at operation and network level with Diagnostic TraceId being added to logs")]
        [DataRow($"{OpenTelemetryAttributeKeys.DiagnosticNamespace}.Operation", null, DisplayName = "DirectMode and DistributedFlag On: Asserts activity creation at operation level with Diagnostic TraceId being added to logs")]
        public async Task SourceEnabled_FlagOn_DirectMode_RecordsActivity_AssertLogTraceId_AssertTraceparent(string operationLevelSource, string networkLevelSource)
        {
            string[] sources = new string[] { operationLevelSource, networkLevelSource };
            sources = sources.Where(x => x != null).ToArray();

            using TracerProvider provider = Sdk.CreateTracerProviderBuilder()
                .AddCustomOtelExporter()
                .AddSource(sources)
                .Build();

            await base.TestInit(validateSinglePartitionKeyRangeCacheCall: false, 
                                customizeClientBuilder: (builder) => builder
                                                                        .WithClientTelemetryOptions(new CosmosClientTelemetryOptions()
                                                                         {
                                                                            DisableDistributedTracing = false
                                                                         })
                                                                        .WithConnectionModeDirect());

            Container containerResponse = await this.database.CreateContainerAsync(
                    id: Guid.NewGuid().ToString(),
                    partitionKeyPath: "/id",
                    throughput: 20000);

            CosmosObject cosmosObject = CosmosObject.Create(
                new Dictionary<string, CosmosElement>()
                {
                    { "id", CosmosString.Create("1") }
                });

            ItemResponse<JToken> createResponse = await containerResponse.CreateItemAsync(JToken.Parse(cosmosObject.ToString()));

            //Assert traceparent header in Direct mode request
            Assert.IsTrue(createResponse.RequestMessage.Headers.TryGetValue("traceparent", out string traceheader));
            Assert.IsNotNull(traceheader);
            string[] traceheaderParts = traceheader.Split('-');
            string traceheaderId = traceheaderParts[1];

            //Assert traceId in Diagnostics logs
            string diagnosticsCreateItem = createResponse.Diagnostics.ToString();
            JObject objDiagnosticsCreate = JObject.Parse(diagnosticsCreateItem);
            string distributedTraceId = (string)objDiagnosticsCreate["data"]["DistributedTraceId"];
            Assert.IsFalse(string.IsNullOrEmpty(distributedTraceId));

            //Assert diagnostics log trace id is same as parent trace id of the activity
            string operationName = (string)objDiagnosticsCreate["name"];
            string traceIdCreateItem = CustomOtelExporter.CollectedActivities
                                                            .Where(x => x.OperationName.Contains(operationName))
                                                            .FirstOrDefault()
                                                            .TraceId
                                                            .ToString();
            //Assert created activity traceId and diagnosticsLog traceId
            Assert.AreEqual(distributedTraceId, traceIdCreateItem);

            //Assert requestHeader trace id and and diagnosticsLog traceId
            Assert.AreEqual(distributedTraceId, traceheaderId);

            //Assert activity creation
            Assert.IsNotNull(CustomOtelExporter.CollectedActivities);

            if (networkLevelSource != null)
            {
                // Assert activity created at network level have an existing parent activity
                Activity networkLevelChildActivity = CustomOtelExporter.CollectedActivities
                                                                            .Where(x => x.OperationName.Contains("Request"))
                                                                            .FirstOrDefault();
                Assert.IsNotNull(CustomOtelExporter.CollectedActivities
                                                        .Where(x => x.Id == networkLevelChildActivity.ParentId));
            }
        }

        [DataTestMethod]
        [DataRow($"{OpenTelemetryAttributeKeys.DiagnosticNamespace}.Operation", $"{OpenTelemetryAttributeKeys.DiagnosticNamespace}.Request", DisplayName = "GatewayMode and DistributedFlag On: Asserts activity creation at operation and network level with Diagnostic TraceId being added to logs")]
        [DataRow($"{OpenTelemetryAttributeKeys.DiagnosticNamespace}.Operation", null, DisplayName = "GatewayMode and DistributedFlag On: Asserts activity creation at operation level with Diagnostic TraceId being added to logs")]
        public async Task SourceEnabled_FlagOn_GatewayMode_RecordsActivity_AssertLogTraceId_AssertTraceparent(string operationLevelSource, string networkLevelSource)
        {
            string[] sources = new string[] { operationLevelSource, networkLevelSource };
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

            using TracerProvider provider = Sdk.CreateTracerProviderBuilder()
                .AddCustomOtelExporter()
                .AddSource(sources)
                .Build();

            await base.TestInit(validateSinglePartitionKeyRangeCacheCall: false, 
                                customizeClientBuilder: (builder) => builder
                                                                        .WithClientTelemetryOptions(new CosmosClientTelemetryOptions()
                                                                         {
                                                                            DisableDistributedTracing = false
                                                                         })
                                                                        .WithHttpClientFactory(() => new HttpClient(httpClientHandlerHelper))
                                                                        .WithConnectionModeGateway());

            ContainerResponse containerResponse = await this.database.CreateContainerAsync(
                id: Guid.NewGuid().ToString(),
                partitionKeyPath: "/id",
                throughput: 20000);

            List<Activity> b = CustomOtelExporter.CollectedActivities.ToList();
            //Assert traceId in Diagnostics logs
            string diagnosticsCreateContainer = containerResponse.Diagnostics.ToString();
            JObject objDiagnosticsCreate = JObject.Parse(diagnosticsCreateContainer);
            string distributedTraceId = (string)objDiagnosticsCreate["data"]["DistributedTraceId"];
            Assert.IsFalse(string.IsNullOrEmpty(distributedTraceId));

            //Assert diagnostics log trace id is same as parent trace id of the activity
            string operationName = (string)objDiagnosticsCreate["name"];
            string traceIdCreateContainer = CustomOtelExporter.CollectedActivities
                                                                    .Where(x => x.OperationName.Contains(operationName))
                                                                    .FirstOrDefault()
                                                                    .TraceId
                                                                    .ToString();
            Assert.AreEqual(distributedTraceId, traceIdCreateContainer);

            //Assert activity creation
            Assert.IsNotNull(CustomOtelExporter.CollectedActivities);
        }

        [DataTestMethod]
        [DataRow(false, true, "random.source.name", DisplayName = "DirectMode, DistributedFlag On, Random/No Source:Asserts no activity creation")]
        [DataRow(true, true, "random.source.name", DisplayName = "GatewayMode, DistributedFlag On, Random/No Source:Asserts no activity creation")]
        [DataRow(false, false, "random.source.name", DisplayName = "DirectMode, DistributedFlag Off, Random/No Source:Asserts no activity creation")]
        [DataRow(true, false, "random.source.name", DisplayName = "GatewayMode, DistributedFlag Off, Random/No Source:Asserts no activity creation")]
        [DataRow(false, false, $"{OpenTelemetryAttributeKeys.DiagnosticNamespace}.Operation", DisplayName = "DirectMode, DistributedFlag Off, OperationLevel Source:Asserts no activity creation")]
        [DataRow(true, false, $"{OpenTelemetryAttributeKeys.DiagnosticNamespace}.Operation", DisplayName = "GatewayMode, DistributedFlag Off, OperationLevel Source:Asserts no activity creation")]
        public async Task NoSourceEnabled_ResultsInNoSourceParentActivityCreation_AssertLogTraceId(bool useGateway, bool enableDistributingTracing, string source)
        {
            using TracerProvider provider = Sdk.CreateTracerProviderBuilder()
                .AddCustomOtelExporter()
                .AddSource(source)
                .Build();

            if (useGateway)
            {
                await base.TestInit(validateSinglePartitionKeyRangeCacheCall: false, 
                                    customizeClientBuilder: (builder) => builder
                                                                            .WithClientTelemetryOptions(new CosmosClientTelemetryOptions()
                                                                             {
                                                                                DisableDistributedTracing = enableDistributingTracing
                                                                             })
                                                                            .WithConnectionModeGateway());
            }
            else
            {
                await base.TestInit(validateSinglePartitionKeyRangeCacheCall: false, 
                                    customizeClientBuilder: (builder) => builder
                                                                            .WithClientTelemetryOptions(new CosmosClientTelemetryOptions()
                                                                             {
                                                                                DisableDistributedTracing = enableDistributingTracing
                                                                             }));
            }

            ContainerResponse containerResponse = await this.database.CreateContainerAsync(
                id: Guid.NewGuid().ToString(),
                partitionKeyPath: "/id",
                throughput: 20000);

            //Assert traceId in Diagnostics logs
            string diagnosticsCreateContainer = containerResponse.Diagnostics.ToString();
            JObject objDiagnosticsCreate = JObject.Parse(diagnosticsCreateContainer);

            if (enableDistributingTracing)
            {
                //DistributedTraceId present in logs
                string distributedTraceId = (string)objDiagnosticsCreate["data"]["DistributedTraceId"];
                Assert.IsFalse(string.IsNullOrEmpty(distributedTraceId), "Distributed Trace Id is not there in diagnostics");
            }
            else
            {
                //DistributedTraceId field not present in logs
                Assert.IsNull(objDiagnosticsCreate["data"]["DistributedTraceId"], "Distributed Trace Id has value in diagnostics i.e. " + (string)objDiagnosticsCreate["data"]["DistributedTraceId"]);
            }

            //Assert no activity with attached source is created
            Assert.AreEqual(0, CustomOtelExporter.CollectedActivities.Count());
        }

        [TestCleanup]
        public async Task CleanUp()
        {
            await base.TestCleanup();

            AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", false);
            AzureCore.ActivityExtensions.ResetFeatureSwitch();
        }
    }
}