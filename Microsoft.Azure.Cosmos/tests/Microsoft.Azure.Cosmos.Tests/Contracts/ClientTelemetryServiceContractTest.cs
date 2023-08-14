//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Contracts
{
    using System.IO;
    using Microsoft.Azure.Cosmos.Telemetry.Models;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using NJsonSchema;
    using NJsonSchema.Generation;

    [TestCategory("Windows")]
    [TestCategory("UpdateContract")]
    [TestClass]
    public class ClientTelemetryServiceContractTest
    {
        private const string ContractPath = "ClientTelemetryServiceAPI.json";
        private const string ContractChangesPath = "ClientTelemetryServiceChangesAPI.json";

        [TestMethod]
        public void TraceApiContractTestAsync()
        {
            JsonSchema schema = JsonSchema.FromType<ClientTelemetryProperties>(new JsonSchemaGeneratorSettings()
            {
                UseXmlDocumentation = false,
                DefaultDictionaryValueReferenceTypeNullHandling = ReferenceTypeNullHandling.Null

            });
            string localJson = schema.ToJson();
            File.WriteAllText($"Contracts/{ContractChangesPath}", localJson);

            string baselineJson = File.ReadAllText($"Contracts/{ContractPath}");
            Assert.AreEqual(localJson, baselineJson);
        }
    }
}
