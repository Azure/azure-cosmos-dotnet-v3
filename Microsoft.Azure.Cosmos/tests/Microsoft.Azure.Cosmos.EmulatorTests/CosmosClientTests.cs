//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CosmosClientTests
    {
        [TestMethod]
        public async Task ClientTest()
        {
            var clientBuilder = TestCommon.GetDefaultConfiguration();
            using (CosmosClient client = clientBuilder.Build())
            {
                await client.GetAccountSettingsAsync();
            }
        }

        [TestMethod]
        public async Task ClientSecureStringTest()
        {
            var clientBuilder = TestCommon.GetDefaultSecureStringConfiguration();
            using (CosmosClient client = clientBuilder.Build())
            {
                await client.GetAccountSettingsAsync();
            }
        }
    }
}
