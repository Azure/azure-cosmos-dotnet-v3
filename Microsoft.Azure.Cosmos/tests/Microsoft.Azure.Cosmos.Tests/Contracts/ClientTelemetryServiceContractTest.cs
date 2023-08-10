//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Contracts
{
    using System;
    using Microsoft.Azure.Cosmos.Telemetry.Models;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using NJsonSchema;
    using static Microsoft.Azure.Cosmos.PatchConstants;

    [TestClass]
    public class ClientTelemetryServiceContractTest
    {
        [TestMethod]
        public void TraceApiContractTest()
        {
            JsonSchema schema = JsonSchema.FromType(typeof(Schemas));
            Console.WriteLine(schema.ToJson());
        }
    }

    [Serializable]
    internal class Schemas
    {
        [JsonProperty(PropertyName = "clientTelemetryProperties")]
        internal ClientTelemetryProperties ClientTelemetryProperties { get; set; }
    }
}
