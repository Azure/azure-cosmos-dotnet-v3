//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Microsoft.Azure.Cosmos.Telemetry;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Telemetry.Models;

    /// <summary>
    /// Tests for <see cref="ClientTelemetry"/>.
    /// </summary>
    [TestClass]
    public class ClientTelemetryTests
    {
        [TestCleanup]
        public void Cleanup()
        {
            Environment.SetEnvironmentVariable(ClientTelemetryOptions.EnvPropsClientTelemetryEnabled, null);
        }

        [TestMethod]
        public void CheckJsonSerializerContract()
        {
            string json = JsonConvert.SerializeObject(new ClientTelemetryProperties(clientId: "clientId", 
                processId: "", 
                userAgent: null, 
                connectionMode: ConnectionMode.Direct, 
                preferredRegions: null, 
                aggregationIntervalInSec: 10), ClientTelemetryOptions.JsonSerializerSettings);
            Assert.AreEqual("{\"clientId\":\"clientId\",\"processId\":\"\",\"connectionMode\":\"DIRECT\",\"aggregationIntervalInSec\":10,\"systemInfo\":[]}", json);
        }
        
        [TestMethod]
        public void CheckJsonSerializerContractWithPreferredRegions()
        {
            List<string> preferredRegion = new List<string>
            {
                "region1"
            };
            string json = JsonConvert.SerializeObject(new ClientTelemetryProperties(clientId: "clientId", 
                processId: "", 
                userAgent: null, 
                connectionMode: ConnectionMode.Direct, 
                preferredRegions: preferredRegion,
                aggregationIntervalInSec: 1), ClientTelemetryOptions.JsonSerializerSettings);
            Assert.AreEqual("{\"clientId\":\"clientId\",\"processId\":\"\",\"connectionMode\":\"DIRECT\",\"preferredRegions\":[\"region1\"],\"aggregationIntervalInSec\":1,\"systemInfo\":[]}", json);
        }

        [TestMethod]
        [ExpectedException(typeof(System.FormatException))]
        public void CheckMisconfiguredTelemetry_should_fail()
        {
            Environment.SetEnvironmentVariable(ClientTelemetryOptions.EnvPropsClientTelemetryEnabled, "non-boolean");
            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();
        }
    }
}
