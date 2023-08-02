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

    [TestClass]
    [TestCategory("ClientTelemetryRelease")]
    public class ClientTelemetryReleaseTests : ClientTelemetryBaseTests
    {
        protected override CosmosClientBuilder GetBuilder()
        {
            string connectionString = Environment.GetEnvironmentVariable("COSMOS.DB_CONNECTION_STRING");
            return new CosmosClientBuilder(connectionString: connectionString);
        }

        protected override Task<HttpResponseMessage> HttpHandlerRequestCallbackChecks(HttpRequestMessage request)
        {
            return null;
        }
    }
}
