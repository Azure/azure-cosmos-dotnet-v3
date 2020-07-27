//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.ObjectModel;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    /// <summary>
    /// Tests for <see cref="GlobalEndpointManager"/>
    /// </summary>
    [TestClass]
    public class GlobalEndpointManagerTest
    {
        /// <summary>
        /// Tests for <see cref="GlobalEndpointManager"/>
        /// </summary>
        [TestMethod]
        public async Task EndpointFailureMockTest()
        {
            // Setup dummpy read locations for the database account
            Collection<AccountRegion> readableLocations = new Collection<AccountRegion>();

            AccountRegion writeLocation = new AccountRegion();
            writeLocation.Name = "WriteLocation";
            writeLocation.Endpoint = "https://writeendpoint.net/";

            AccountRegion readLocation1 = new AccountRegion();
            readLocation1.Name = "ReadLocation1";
            readLocation1.Endpoint = "https://readendpoint1.net/";

            AccountRegion readLocation2 = new AccountRegion();
            readLocation2.Name = "ReadLocation2";
            readLocation2.Endpoint = "https://readendpoint2.net/";

            readableLocations.Add(writeLocation);
            readableLocations.Add(readLocation1);
            readableLocations.Add(readLocation2);

            AccountProperties databaseAccount = new AccountProperties();
            databaseAccount.ReadLocationsInternal = readableLocations;

            //Setup mock owner "document client"
            Mock<IDocumentClientInternal> mockOwner = new Mock<IDocumentClientInternal>();
            mockOwner.Setup(owner => owner.ServiceEndpoint).Returns(new Uri("https://defaultendpoint.net/"));
            mockOwner.Setup(owner => owner.GetDatabaseAccountInternalAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>())).ReturnsAsync(databaseAccount);

            //Create connection policy and populate preferred locations
            ConnectionPolicy connectionPolicy = new ConnectionPolicy();
            connectionPolicy.PreferredLocations.Add("ReadLocation1");
            connectionPolicy.PreferredLocations.Add("ReadLocation2");

            GlobalEndpointManager globalEndpointManager = new GlobalEndpointManager(mockOwner.Object, connectionPolicy);

            await globalEndpointManager.RefreshLocationAsync(databaseAccount);
            Assert.AreEqual(globalEndpointManager.ReadEndpoints[0], new Uri(readLocation1.Endpoint));

            //Mark each of the read locations as unavailable and validate that the read endpoint switches to the next preferred region / default endpoint.
            globalEndpointManager.MarkEndpointUnavailableForRead(globalEndpointManager.ReadEndpoints[0]);
            globalEndpointManager.RefreshLocationAsync(null).Wait();
            Assert.AreEqual(globalEndpointManager.ReadEndpoints[0], new Uri(readLocation2.Endpoint));

            globalEndpointManager.MarkEndpointUnavailableForRead(globalEndpointManager.ReadEndpoints[0]);
            await globalEndpointManager.RefreshLocationAsync(null);
            Assert.AreEqual(globalEndpointManager.ReadEndpoints[0], globalEndpointManager.WriteEndpoints[0]);

            //Sleep a second for the unavailable endpoint entry to expire and background refresh timer to kick in
            Thread.Sleep(2000);
            await globalEndpointManager.RefreshLocationAsync(null);
            Assert.AreEqual(globalEndpointManager.ReadEndpoints[0], new Uri(readLocation1.Endpoint));
        }

        /// <summary>
        /// Unit test for LocationHelper class
        /// </summary>
        [TestMethod]
        public void LocationHelperTest()
        {
            Uri globalEndpointUri = new Uri("https://contoso.documents.azure.com:443/");
            Uri regionalEndpointUri = LocationHelper.GetLocationEndpoint(globalEndpointUri, "West US");

            Assert.AreEqual("contoso-westus.documents.azure.com", regionalEndpointUri.Host);
            Assert.AreEqual(new Uri("https://contoso-westus.documents.azure.com:443/"), regionalEndpointUri);

            globalEndpointUri = new Uri("https://contoso:443/");
            regionalEndpointUri = LocationHelper.GetLocationEndpoint(globalEndpointUri, "West US");

            Assert.AreEqual("contoso-westus", regionalEndpointUri.Host);
            Assert.AreEqual(new Uri("https://contoso-westus:443/"), regionalEndpointUri);
        }

        /// <summary>
        /// Tests for <see cref="GlobalEndpointManager"/>
        /// </summary>
        [TestMethod]
        public void ReadLocationRemoveAndAddMockTest()
        {
            // Setup dummpy read locations for the database account
            Collection<AccountRegion> readableLocations = new Collection<AccountRegion>();

            AccountRegion writeLocation = new AccountRegion();
            writeLocation.Name = "WriteLocation";
            writeLocation.Endpoint = "https://writeendpoint.net/";

            AccountRegion readLocation1 = new AccountRegion();
            readLocation1.Name = "ReadLocation1";
            readLocation1.Endpoint = "https://readendpoint1.net/";

            AccountRegion readLocation2 = new AccountRegion();
            readLocation2.Name = "ReadLocation2";
            readLocation2.Endpoint = "https://readendpoint2.net/";

            readableLocations.Add(writeLocation);
            readableLocations.Add(readLocation1);
            readableLocations.Add(readLocation2);

            AccountProperties databaseAccount = new AccountProperties();
            databaseAccount.ReadLocationsInternal = readableLocations;

            //Setup mock owner "document client"
            Mock<IDocumentClientInternal> mockOwner = new Mock<IDocumentClientInternal>();
            mockOwner.Setup(owner => owner.ServiceEndpoint).Returns(new Uri("https://defaultendpoint.net/"));
            mockOwner.Setup(owner => owner.GetDatabaseAccountInternalAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>())).ReturnsAsync(databaseAccount);

            //Create connection policy and populate preferred locations
            ConnectionPolicy connectionPolicy = new ConnectionPolicy();
            connectionPolicy.PreferredLocations.Add("ReadLocation1");
            connectionPolicy.PreferredLocations.Add("ReadLocation2");

            GlobalEndpointManager globalEndpointManager = new GlobalEndpointManager(mockOwner.Object, connectionPolicy);

            globalEndpointManager.RefreshLocationAsync(databaseAccount).Wait();
            Assert.AreEqual(globalEndpointManager.ReadEndpoints[0], new Uri(readLocation1.Endpoint));

            //Remove location 1 from read locations and validate that the read endpoint switches to the next preferred location
            readableLocations.Remove(readLocation1);
            databaseAccount.ReadLocationsInternal = readableLocations;

            globalEndpointManager.RefreshLocationAsync(databaseAccount).Wait();
            Assert.AreEqual(globalEndpointManager.ReadEndpoints[0], new Uri(readLocation2.Endpoint));

            //Add location 1 back to read locations and validate that location 1 becomes the read endpoint again.
            readableLocations.Add(readLocation1);
            databaseAccount.ReadLocationsInternal = readableLocations;

            //Sleep a bit for the refresh timer to kick in and rediscover location 1
            Thread.Sleep(2000);
            Assert.AreEqual(globalEndpointManager.ReadEndpoints[0], new Uri(readLocation1.Endpoint));
        }
    }
}