//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// In Release pipeline, no need to mock Client Telemetry Service Call and Test will talk to the real database account.
    /// If you are making changes in this file please make sure you are adding similar test in <see cref="ClientTelemetryTests"/> also.
    /// </summary>
    [TestClass]
    [TestCategory("Quarantine") /* Release pipelines failing to connect to telemetry service */]
    [TestCategory("ClientTelemetryRelease")]
    public class ClientTelemetryReleaseTests : ClientTelemetryTestsBase
    {
        public override CosmosClientBuilder GetBuilder()
        {
            string connectionString = ConfigurationManager.GetEnvironmentVariable<string>("COSMOSDB_ACCOUNT_CONNECTION_STRING", null);
            return new CosmosClientBuilder(connectionString: connectionString);
        }

        /// <summary>
        /// Returing null means do not return any hard codd response for any HTTP call.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public override Task<HttpResponseMessage> HttpHandlerRequestCallbackChecks(HttpRequestMessage request)
        {
            return null;
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
