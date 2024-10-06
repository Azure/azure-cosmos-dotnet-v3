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

    /// <summary>
    /// In Emulator Mode, Run test against emulator and mock client telemetry service calls. 
    /// If you are making changes in this file please make sure you are adding similar test in <see cref="ClientTelemetryReleaseTests"/> also.
    /// </summary>
    [TestClass]
    [TestCategory("Flaky")]
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
    }
}
