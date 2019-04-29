//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.ObjectModel;
    using System.Threading;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Internal;
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
        public void EndpointFailureMockTest()
        {
            // Setup dummpy read locations for the database account
            Collection<CosmosAccountLocation> readableLocations = new Collection<CosmosAccountLocation>();

            CosmosAccountLocation writeLocation = new CosmosAccountLocation();
            writeLocation.Name = "WriteLocation";
            writeLocation.DatabaseAccountEndpoint = "https://writeendpoint.net/";

            CosmosAccountLocation readLocation1 = new CosmosAccountLocation();
            readLocation1.Name = "ReadLocation1";
            readLocation1.DatabaseAccountEndpoint = "https://readendpoint1.net/";

            CosmosAccountLocation readLocation2 = new CosmosAccountLocation();
            readLocation2.Name = "ReadLocation2";
            readLocation2.DatabaseAccountEndpoint = "https://readendpoint2.net/";

            readableLocations.Add(writeLocation);
            readableLocations.Add(readLocation1);
            readableLocations.Add(readLocation2);

            CosmosAccountSettings databaseAccount = new CosmosAccountSettings();
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
            Assert.IsTrue(globalEndpointManager.ReadEndpoints[0] == new Uri(readLocation1.DatabaseAccountEndpoint));

            //Mark each of the read locations as unavailable and validate that the read endpoint switches to the next preferred region / default endpoint.
            globalEndpointManager.MarkEndpointUnavailableForRead(globalEndpointManager.ReadEndpoints[0]);
            globalEndpointManager.RefreshLocationAsync(null).Wait();
            Assert.IsTrue(globalEndpointManager.ReadEndpoints[0] == new Uri(readLocation2.DatabaseAccountEndpoint));

            globalEndpointManager.MarkEndpointUnavailableForRead(globalEndpointManager.ReadEndpoints[0]);
            globalEndpointManager.RefreshLocationAsync(null).Wait();
            Assert.IsTrue(globalEndpointManager.ReadEndpoints[0] == globalEndpointManager.WriteEndpoints[0]);

            //Sleep a second for the unavailable endpoint entry to expire and background refresh timer to kick in
            Thread.Sleep(1000);
            globalEndpointManager.RefreshLocationAsync(null).Wait();
            Assert.IsTrue(globalEndpointManager.ReadEndpoints[0] == new Uri(readLocation1.DatabaseAccountEndpoint));
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
            Collection<CosmosAccountLocation> readableLocations = new Collection<CosmosAccountLocation>();

            CosmosAccountLocation writeLocation = new CosmosAccountLocation();
            writeLocation.Name = "WriteLocation";
            writeLocation.DatabaseAccountEndpoint = "https://writeendpoint.net/";

            CosmosAccountLocation readLocation1 = new CosmosAccountLocation();
            readLocation1.Name = "ReadLocation1";
            readLocation1.DatabaseAccountEndpoint = "https://readendpoint1.net/";

            CosmosAccountLocation readLocation2 = new CosmosAccountLocation();
            readLocation2.Name = "ReadLocation2";
            readLocation2.DatabaseAccountEndpoint = "https://readendpoint2.net/";

            readableLocations.Add(writeLocation);
            readableLocations.Add(readLocation1);
            readableLocations.Add(readLocation2);

            CosmosAccountSettings databaseAccount = new CosmosAccountSettings();
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
            Assert.IsTrue(globalEndpointManager.ReadEndpoints[0] == new Uri(readLocation1.DatabaseAccountEndpoint));

            //Remove location 1 from read locations and validate that the read endpoint switches to the next preferred location
            readableLocations.Remove(readLocation1);
            databaseAccount.ReadLocationsInternal = readableLocations;

            globalEndpointManager.RefreshLocationAsync(databaseAccount).Wait();
            Assert.IsTrue(globalEndpointManager.ReadEndpoints[0] == new Uri(readLocation2.DatabaseAccountEndpoint));

            //Add location 1 back to read locations and validate that location 1 becomes the read endpoint again.
            readableLocations.Add(readLocation1);
            databaseAccount.ReadLocationsInternal = readableLocations;

            //Sleep a bit for the refresh timer to kick in and rediscover location 1
            Thread.Sleep(2000);
            Assert.IsTrue(globalEndpointManager.ReadEndpoints[0] == new Uri(readLocation1.DatabaseAccountEndpoint));
        }
    }
}