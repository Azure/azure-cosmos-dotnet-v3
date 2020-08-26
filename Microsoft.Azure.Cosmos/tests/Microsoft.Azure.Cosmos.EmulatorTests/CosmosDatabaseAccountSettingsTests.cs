//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

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
            AccountProperties accountProperties = await this.cosmosClient.ReadAccountAsync();
            Assert.IsNotNull(accountProperties);
            Assert.IsNotNull(accountProperties.Id);
            Assert.IsNotNull(accountProperties.ReadableRegions);
            Assert.IsTrue(accountProperties.ReadableRegions.Count() > 0);
            Assert.IsNotNull(accountProperties.WritableRegions);
            Assert.IsTrue(accountProperties.WritableRegions.Count() > 0);
        }
    }
}
