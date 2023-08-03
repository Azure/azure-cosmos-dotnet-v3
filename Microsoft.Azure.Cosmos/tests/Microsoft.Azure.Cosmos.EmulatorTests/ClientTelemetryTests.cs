//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System.Net.Http;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// In Emulator Mode, Run test against emulator and mock client telemetry service calls
    /// </summary>
    [TestClass]
    [TestCategory("ClientTelemetryEmulator")]
    public class ClientTelemetryTests : ClientTelemetryBaseTests
    {
        protected override CosmosClientBuilder GetBuilder()
        {
            return TestCommon.GetDefaultConfiguration();
        }

        protected override Task<HttpResponseMessage> HttpHandlerRequestCallbackChecks(HttpRequestMessage request)
        {
            if (request.RequestUri.AbsoluteUri.Equals(ClientTelemetryOptions.GetClientTelemetryEndpoint().AbsoluteUri))
            {
                HttpResponseMessage result = new HttpResponseMessage(HttpStatusCode.NoContent); // In Emulator test, send hardcoded response status code

                return Task.FromResult(result);
            }

            return null;
        }
    }
}
