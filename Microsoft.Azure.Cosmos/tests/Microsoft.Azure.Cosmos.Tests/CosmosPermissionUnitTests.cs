//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class CosmosPermissionUnitTests
    {
        [TestMethod]
        public async Task tokenExpiryInSecondsHeaderIsAdded()
        {
            int testHandlerHitCount = 0;
            const int tokenExpiryInSeconds = 9000;

            TestHandler testHandler = new TestHandler((request, cancellationToken) =>
            {
                Assert.AreEqual(tokenExpiryInSeconds, int.Parse(request.Headers[Documents.HttpConstants.HttpHeaders.ResourceTokenExpiry]));
                testHandlerHitCount++;
                ResponseMessage response = new ResponseMessage(HttpStatusCode.OK, request, errorMessage: null)
                {
                    Content = request.Content
                };
                return Task.FromResult(response);
            });

            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient(
                (builder) => builder.AddCustomHandlers(testHandler));

            Database database = client.GetDatabase("testdb");
            await database.GetUser("testUser").CreatePermissionAsync(
                new PermissionProperties("permissionId", PermissionMode.All, database.GetContainer("containerId")),
                tokenExpiryInSeconds: tokenExpiryInSeconds
            );

            await database.GetUser("testUser").GetPermission("permissionId").ReplaceAsync(
                new PermissionProperties("permissionId", PermissionMode.All, database.GetContainer("containerId")),
                tokenExpiryInSeconds: tokenExpiryInSeconds
            );

            await database.GetUser("testUser").GetPermission("permissionId").ReadAsync(tokenExpiryInSeconds: tokenExpiryInSeconds);

            //create,read, and replace
            Assert.AreEqual(3, testHandlerHitCount);
        }

        [TestMethod]
        public void MockContainerPermissionTest()
        {
            Mock<Database> database = new Mock<Database>();
            database.Setup(d => d.Id).Returns("MockDatabase");
            Mock<Container> container = new Mock<Container>();
            container.Setup(c => c.Id).Returns("MockContainer");
            container.Setup(c => c.Database).Returns(database.Object);
            PermissionProperties properties = new PermissionProperties("permissionId", PermissionMode.All, container.Object);
            Assert.AreEqual(properties.ResourceUri, $"dbs/{database.Object.Id}/colls/{container.Object.Id}");
        }
    }
}