//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using Microsoft.Azure.Cosmos.Client.Core.Tests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System.Net;
    using System.Threading.Tasks;

    [TestClass]
    public class CosmosPermissionUnitTests
    {
        [TestMethod]
        public async Task ResourceTokenExpirySecondsHeaderIsAdded()
        {
            int testHandlerHitCount = 0;
            const int ResourceTokenExpirySeconds = 9000;

            TestHandler testHandler = new TestHandler((request, cancellationToken) =>
            {
                Assert.AreEqual(ResourceTokenExpirySeconds, int.Parse(request.Headers[Documents.HttpConstants.HttpHeaders.ResourceTokenExpiry]));
                testHandlerHitCount++;
                ResponseMessage response = new ResponseMessage(HttpStatusCode.OK, request, errorMessage: null);
                response.Content = request.Content;
                return Task.FromResult(response);
            });

            CosmosClient client = MockCosmosUtil.CreateMockCosmosClient(
                (builder) => builder.AddCustomHandlers(testHandler));

            Database database = client.GetDatabase("testdb");
            await database.GetUser("testUser").CreatePermissionAsync(
                new PermissionProperties("permissionId", PermissionMode.All, database.GetContainer("containerId")), 
                resourceTokenExpirySeconds: ResourceTokenExpirySeconds
            );

            await database.GetUser("testUser").GetPermission("permissionId").ReplaceAsync(
                new PermissionProperties("permissionId", PermissionMode.All, database.GetContainer("containerId")),
                resourceTokenExpirySeconds: ResourceTokenExpirySeconds
            );

            await database.GetUser("testUser").GetPermission("permissionId").ReadAsync(resourceTokenExpirySeconds: ResourceTokenExpirySeconds);

            //create,read, and replace
            Assert.AreEqual(3, testHandlerHitCount);
        }
    }
}
