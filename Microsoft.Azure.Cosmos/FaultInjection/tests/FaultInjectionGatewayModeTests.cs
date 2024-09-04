//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.FaultInjection.Tests
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.FaultInjection.Tests.Utils;

    [TestClass]
    public class FaultInjectionGatewayModeTests
    {
        private const int Timeout = 60000;

        private string? connectionString;

        private CosmosClient? client;
        private Database? database;
        private Container? container;


        [TestInitialize]
        public async Task Initialize()
        {
            this.connectionString = ConfigurationManager.GetEnvironmentVariable<string>("COSMOSDB_MULTI_REGION", string.Empty);

            if (string.IsNullOrEmpty(this.connectionString))
            {
                Assert.Fail("Set environment variable COSMOSDB_MULTI_REGION to run the tests");
            }

            this.client = TestCommon.CreateCosmosClient(
                useGateway: true,
                multiRegion: true);

            (this.database, this.container) = await TestCommon.GetOrCreateMultiRegionFIDatabaseAndContainers(this.client);
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            if (this.database != null) { await this.database.DeleteAsync(); }
            this.client?.Dispose();
        }

    }
}
