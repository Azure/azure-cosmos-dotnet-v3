//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// In Release pipeline, no need to mock Client Telemetry Service Call and Test will talk to the real database account.
    /// </summary>
    [TestClass]
    [TestCategory("ClientTelemetryRelease")]
    public class ClientTelemetryReleaseTests : ClientTelemetryTests
    {
        public override CosmosClientBuilder GetBuilder()
        {
            return new CosmosClientBuilder(connectionString: Environment.GetEnvironmentVariable("COSMOS.DB_CONNECTION_STRING"));
        }

        public override Task<HttpResponseMessage> HttpHandlerRequestCallbackChecks(HttpRequestMessage request)
        {
            return null;
        }
    }
}
