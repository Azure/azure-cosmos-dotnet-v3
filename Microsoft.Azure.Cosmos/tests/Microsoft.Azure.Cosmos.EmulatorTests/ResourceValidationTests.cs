//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ResourceValidationTests : BaseCosmosClientHelper
    {
        [TestInitialize]
        public async Task InitializeAsync()
        {
            await this.TestInit();
        }

        [TestCleanup]
        public async Task CleanupAsync()
        {
            await this.TestCleanup();
        }

        [TestMethod]
        [Timeout(60000)]
        public async Task CreateDatabaseRejectsEmptyId()
        {
            using CosmosClient client = TestCommon.CreateCosmosClient(useGateway: true);
            ArgumentNullException error = await Assert.ThrowsExceptionAsync<ArgumentNullException>(
                async () => await client.CreateDatabaseAsync(
                    string.Empty, cancellationToken: this.cancellationToken));
            Assert.AreEqual("id", error.ParamName);
        }

        [TestMethod]
        [Timeout(60000)]
        public async Task CreateContainerRejectsEmptyId()
        {
            ArgumentNullException error = await Assert.ThrowsExceptionAsync<ArgumentNullException>(
                async () => await this.database.CreateContainerAsync(
                    new ContainerProperties { Id = string.Empty, PartitionKeyPath = "/pk" },
                    cancellationToken: this.cancellationToken));
            Assert.AreEqual("id", error.ParamName);
        }
    }
}
