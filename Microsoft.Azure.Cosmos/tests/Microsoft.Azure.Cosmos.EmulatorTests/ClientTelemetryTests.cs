//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using Microsoft.Azure.Cosmos.Fluent;
    using System.Net.Http;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using System.Text;
    using Microsoft.Azure.Documents;
    using System;
    using Microsoft.Azure.Cosmos.Telemetry;

    /// <summary>
    /// In Emulator Mode, Run test against emulator and mock client telemetry service calls. 
    /// If you are making changes in this file please make sure you are adding similar test in <see cref="ClientTelemetryReleaseTests"/> also.
    /// </summary>
    [TestClass]
    [TestCategory("ClientTelemetryEmulator")]
    public class ClientTelemetryTests : ClientTelemetryTestsBase
    {
        public override Task<HttpResponseMessage> HttpHandlerRequestCallbackChecks(HttpRequestMessage request)
        {
            if (request.RequestUri.AbsoluteUri.Equals(telemetryServiceEndpoint.AbsoluteUri))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));  // In Emulator test, send hardcoded response status code as there is no real communication happens with client telemetry service
            }
            else if (request.RequestUri.AbsoluteUri.Contains(Paths.ClientConfigPathSegment))
            {
                HttpResponseMessage result = new HttpResponseMessage(HttpStatusCode.OK);
                AccountClientConfiguration clientConfigProperties = new AccountClientConfiguration
                {
                    ClientTelemetryConfiguration = new ClientTelemetryConfiguration
                    {
                        IsEnabled = true,
                        Endpoint = telemetryServiceEndpoint.AbsoluteUri
                    }
                };
                string payload = JsonConvert.SerializeObject(clientConfigProperties);
                result.Content = new StringContent(payload, Encoding.UTF8, "application/json");
                return Task.FromResult(result);
            }

            return null;
        }

        public override CosmosClientBuilder GetBuilder()
        {
            return TestCommon.GetDefaultConfiguration();
        }

        [ClassInitialize]
        public static void ClassInit(TestContext context)
        {
            ClientTelemetryTestsBase.ClassInitialize(context);
        }

        [ClassCleanup]
        public static void ClassCleanUp()
        {
            ClientTelemetryTestsBase.ClassCleanup();
        }

        [TestInitialize]
        public override void TestInitialize()
        {
            base.TestInitialize();
        }

        [TestCleanup]
        public override async Task Cleanup()
        {
            await base.Cleanup();
        }

        [TestMethod]
        [DataRow(ConnectionMode.Direct, true)]
        [DataRow(ConnectionMode.Gateway, true)]
        [DataRow(ConnectionMode.Direct, false)]
        [DataRow(ConnectionMode.Gateway, false)]
        public override async Task PointSuccessOperationsTest(ConnectionMode mode, bool isAzureInstance)
        {
            await base.PointSuccessOperationsTest(mode, isAzureInstance);
        }

        [TestMethod]
        [DataRow(ConnectionMode.Direct)]
        [DataRow(ConnectionMode.Gateway)]
        public override async Task PointReadFailureOperationsTest(ConnectionMode mode)
        {
            await base.PointReadFailureOperationsTest(mode);
        }

        [TestMethod]
        [DataRow(ConnectionMode.Direct)]
        [DataRow(ConnectionMode.Gateway)]
        public override async Task StreamReadFailureOperationsTest(ConnectionMode mode)
        {
            await base.StreamReadFailureOperationsTest(mode);
        }

        [TestMethod]
        [DataRow(ConnectionMode.Direct)]
        [DataRow(ConnectionMode.Gateway)]
        public override async Task StreamOperationsTest(ConnectionMode mode)
        {
            await base.StreamOperationsTest(mode);
        }

        [TestMethod]
        [DataRow(ConnectionMode.Direct)]
        [DataRow(ConnectionMode.Gateway)]
        public override async Task BatchOperationsTest(ConnectionMode mode)
        {
            await base.BatchOperationsTest(mode);
        }

        [TestMethod]
        [DataRow(ConnectionMode.Direct)]
        [DataRow(ConnectionMode.Gateway)]
        public override async Task SingleOperationMultipleTimesTest(ConnectionMode mode)
        {
            await base.SingleOperationMultipleTimesTest(mode);
        }

        [TestMethod]
        [DataRow(ConnectionMode.Direct)]
        [DataRow(ConnectionMode.Gateway)]
        public override async Task QueryOperationSinglePartitionTest(ConnectionMode mode)
        {
            await base.QueryOperationSinglePartitionTest(mode);
        }

        [TestMethod]
        [DataRow(ConnectionMode.Direct)]
        [DataRow(ConnectionMode.Gateway)]
        public override async Task QueryMultiPageSinglePartitionOperationTest(ConnectionMode mode)
        {
            await base.QueryMultiPageSinglePartitionOperationTest(mode);
        }

        [TestMethod]
        [DataRow(ConnectionMode.Direct)]
        [DataRow(ConnectionMode.Gateway)]
        public override async Task QueryOperationCrossPartitionTest(ConnectionMode mode)
        {
            await base.QueryOperationCrossPartitionTest(mode);
        }

        [TestMethod]
        [DataRow(ConnectionMode.Direct)]
        [DataRow(ConnectionMode.Gateway)]
        public override async Task QueryOperationMutiplePageCrossPartitionTest(ConnectionMode mode)
        {
            await base.QueryOperationMutiplePageCrossPartitionTest(mode);
        }

        [TestMethod]
        [DataRow(ConnectionMode.Direct)]
        [DataRow(ConnectionMode.Gateway)]
        public override async Task QueryOperationInvalidContinuationTokenTest(ConnectionMode mode)
        {
            await base.QueryOperationInvalidContinuationTokenTest(mode);
        }

        [TestMethod]
        [DataRow(ConnectionMode.Direct)]
        public override async Task CreateItemWithSubStatusCodeTest(ConnectionMode mode)
        {
            await base.CreateItemWithSubStatusCodeTest(mode);
        }


        [TestMethod]
        [DataRow("SystemUsageCollectionFailure")]
        [DataRow("RequestChargeCollectionFailure")]
        [DataRow("LatencyCollectionFailure")]
        [DataRow("TelemetryServiceApiCallFailure")]
        public async Task TelemetryFailuresInDiagnosticsTest(string failureType)
        {
            ClientTelemetryOptions.RequestChargeMax = TimeSpan.TicksPerHour;
            ClientTelemetryOptions.RequestLatencyMax = 9999900;
            ClientTelemetryOptions.CpuMax = 99999;

            if (failureType == "SystemUsageCollectionFailure")
            {
                ClientTelemetryOptions.CpuMax = 1; // It will fail system usage collection
            }
            else if (failureType == "RequestChargeCollectionFailure")
            {
                ClientTelemetryOptions.RequestChargeMax = 1; // It will fail operation level collection
            }
            else if (failureType == "LatencyCollectionFailure")
            {
                ClientTelemetryOptions.RequestLatencyMax = 1; // It will fail operation and network level collection
            }

            this.httpHandlerForNonAzureInstance = new HttpClientHandlerHelper
            {
                RequestCallBack = (request, cancellation) =>
                {
                    if (request.RequestUri.AbsoluteUri.Contains(Paths.ClientConfigPathSegment))
                    {
                        HttpResponseMessage result = new HttpResponseMessage(HttpStatusCode.OK);
                        AccountClientConfiguration clientConfigProperties = new AccountClientConfiguration
                        {
                            ClientTelemetryConfiguration = new ClientTelemetryConfiguration
                            {
                                IsEnabled = true,
                                Endpoint = telemetryServiceEndpoint.AbsoluteUri
                            }
                        };
                        string payload = JsonConvert.SerializeObject(clientConfigProperties);
                        result.Content = new StringContent(payload, Encoding.UTF8, "application/json");
                        return Task.FromResult(result);

                    }
                    else if (request.RequestUri.AbsoluteUri.Equals(telemetryServiceEndpoint.AbsoluteUri))
                    {
                        if (failureType == "TelemetryServiceApiCallFailure")
                        {
                            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway));
                        }
                        else
                        {
                            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
                        }
                    }
                    return null;
                }
            };

            Container container = await base.CreateClientAndContainer(
               mode: ConnectionMode.Direct,
               isAzureInstance: false);

            // Create an item
            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity("MyTestPkValue");

            ItemResponse<ToDoActivity> createResponse = await container.CreateItemAsync<ToDoActivity>(testItem);
            await Task.Delay(1500); // Wait for one telemetry service call

            // Read an Item
            ItemResponse<ToDoActivity> read1Response = await container.ReadItemAsync<ToDoActivity>(testItem.id, new Cosmos.PartitionKey(testItem.id));
            if (failureType == "TelemetryServiceApiCallFailure" || failureType == "SystemUsageCollectionFailure")
            {
                Assert.IsTrue(read1Response.Diagnostics.ToString().Contains(ClientTelemetryOptions.TelemetryToServiceJobException));
            }
            else
            {
                Assert.IsTrue(read1Response.Diagnostics.ToString().Contains(ClientTelemetryOptions.TelemetryCollectFailedKeyPrefix));
            }

            // Fix issues
            ClientTelemetryOptions.RequestChargeMax = TimeSpan.TicksPerHour;
            ClientTelemetryOptions.RequestLatencyMax = 9999900;
            ClientTelemetryOptions.CpuMax = 99999;

            await Task.Delay(1500); // Wait for one telemetry service call

            // Read an Item
            ItemResponse<ToDoActivity> read2Response = await container.ReadItemAsync<ToDoActivity>(testItem.id, new Cosmos.PartitionKey(testItem.id));
            if (failureType != "TelemetryServiceApiCallFailure")
            {
                Assert.IsFalse(read2Response.Diagnostics.ToString().Contains(ClientTelemetryOptions.TelemetryToServiceJobException));
                Assert.IsFalse(read2Response.Diagnostics.ToString().Contains(ClientTelemetryOptions.TelemetryCollectFailedKeyPrefix));
            }
        }
    }
}
