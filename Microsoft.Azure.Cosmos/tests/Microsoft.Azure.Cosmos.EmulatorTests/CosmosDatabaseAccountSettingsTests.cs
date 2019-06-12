//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class CosmosDatabaseAccountSettingsTests
    {
        private CosmosClient cosmosClient = null;

        [TestInitialize]
        public void TestInit()
        {
            this.cosmosClient = TestCommon.CreateCosmosClient();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            if (this.cosmosClient != null)
            {
                this.cosmosClient.Dispose();
            }
        }

        [TestMethod]
        public async Task GetCosmosDatabaseAccountSettings()
        {
            CosmosAccountProperties settings = await this.cosmosClient.GetAccountPropertiesAsync();
            Assert.IsNotNull(settings);
            Assert.IsNotNull(settings.Id);
            Assert.IsNotNull(settings.ReadableLocations);
            Assert.IsTrue(settings.ReadableLocations.Count() > 0);
            Assert.IsNotNull(settings.WritableLocations);
            Assert.IsTrue(settings.WritableLocations.Count() > 0);
        }
    }
}
