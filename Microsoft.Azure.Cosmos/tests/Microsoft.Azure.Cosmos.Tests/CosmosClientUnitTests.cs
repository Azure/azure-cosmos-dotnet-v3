//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class CosmosClientUnitTests
    {
        [TestMethod]
        public async Task VerifyDatabasesTestability()
        {
            Mock<CosmosDatabaseResponse> dbResponse = new Mock<CosmosDatabaseResponse>();
            dbResponse.Setup(x => x.StatusCode).Returns(HttpStatusCode.Created);
            Mock<CosmosDatabases> mockDatabases = new Mock<CosmosDatabases>();
            mockDatabases.Setup(x => x.CreateDatabaseAsync(
                "testid",
                null,
                null,
                It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(dbResponse.Object));

            CosmosDatabases databases = mockDatabases.Object;
            CosmosDatabaseResponse response = await databases.CreateDatabaseAsync("testid");
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        }

        [TestMethod]
        public async Task VerifyContainerTestability()
        {
            Mock<CosmosContainerResponse> containerResponse = new Mock<CosmosContainerResponse>();
            containerResponse.Setup(x => x.StatusCode).Returns(HttpStatusCode.Created);
            Mock<CosmosContainers> mockContainers = new Mock<CosmosContainers>();
            mockContainers.Setup(x => x.CreateContainerAsync(
                "containerId",
                "/pk",
                null,
                null,
                It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(containerResponse.Object));

            CosmosContainers containers = mockContainers.Object;
            CosmosContainerResponse response = await containers.CreateContainerAsync("containerId", "/pk");
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        }

        [TestMethod]
        public async Task VerifyItemsTestability()
        {
            Mock<CosmosItemResponse<dynamic>> itemResponse = new Mock<CosmosItemResponse<dynamic>>();
            itemResponse.Setup(x => x.StatusCode).Returns(HttpStatusCode.Created);
            Mock<CosmosItems> mockItems = new Mock<CosmosItems>();
            mockItems.Setup(x => x.CreateItemAsync<object>(
                "pkValue",
                It.IsAny<object>(),
                null,
                It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(itemResponse.Object));

            CosmosItems items = mockItems.Object;
            CosmosItemResponse<dynamic> response = await items.CreateItemAsync<dynamic>("pkValue", new { Id = "TestId", pk = "pkValue" });
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        }
    }
}
