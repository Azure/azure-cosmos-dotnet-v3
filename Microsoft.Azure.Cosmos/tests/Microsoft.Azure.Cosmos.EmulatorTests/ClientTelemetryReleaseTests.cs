//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// In Release pipeline, no need to mock Client Telemetry Service Call and Test will talk to the real database account.
    /// </summary>
    [TestClass]
    [TestCategory("ClientTelemetryRelease")]
    public class ClientTelemetryReleaseTests : ClientTelemetryTests
    {
        [ClassInitialize]
        public static new void ClassInitialize(TestContext context)
        {
            // It will go away in next PR
            Environment.SetEnvironmentVariable(ClientTelemetryOptions.EnvPropsClientTelemetryEnabled, "true");
            Environment.SetEnvironmentVariable(ClientTelemetryOptions.EnvPropsClientTelemetrySchedulingInSeconds, "1");
            Environment.SetEnvironmentVariable(ClientTelemetryOptions.EnvPropsClientTelemetryEndpoint, "https://tools.cosmos.azure.com/api/clienttelemetry/trace");

            ClientTelemetryTests.ClassInitialize(context);
        }

        [ClassCleanup]
        public static new void FinalCleanup()
        {
            ClientTelemetryTests.FinalCleanup();
        }

        public override CosmosClientBuilder GetBuilder()
        {
            string connectionString = ConfigurationManager.GetEnvironmentVariable<string>("COSMOSDB_ACCOUNT_CONNECTION_STRING", null);
            return new CosmosClientBuilder(connectionString: connectionString);
        }

        public override Task<HttpResponseMessage> HttpHandlerRequestCallbackChecks(HttpRequestMessage request)
        {
            return null;
        }
    }
}
