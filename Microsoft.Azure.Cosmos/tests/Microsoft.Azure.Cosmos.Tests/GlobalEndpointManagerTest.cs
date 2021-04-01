//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Net.Http;
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
            Thread.Sleep(3000);
            await globalEndpointManager.RefreshLocationAsync(null);
            Assert.AreEqual(globalEndpointManager.ReadEndpoints[0], new Uri(readLocation1.Endpoint));
        }

        /// <summary>
        /// Tests for <see cref="GlobalEndpointManager"/>
        /// </summary>
        [TestMethod]
        public async Task GetDatabaseAccountFromAnyLocationsMockNegativeTestAsync()
        {
            Uri defaultEndpoint = new Uri("https://testfailover.documents-test.windows-int.net/");

            int count = 0;
            try
            {
                await GlobalEndpointManager.GetDatabaseAccountFromAnyLocationsAsync(
                    defaultEndpoint: defaultEndpoint,
                    locations: new List<string>(){
                       "westus",
                       "southeastasia",
                       "northcentralus"
                    },
                    getDatabaseAccountFn: (uri) =>
                    {
                        count++;
                        if (uri == defaultEndpoint)
                        {
                            throw new Microsoft.Azure.Documents.UnauthorizedException("Mock failed exception");
                        }

                        throw new Exception("This should never be hit since it should stop after the global endpoint hit the nonretriable exception");
                    });

                Assert.Fail("Should throw the UnauthorizedException");
            }
            catch (Microsoft.Azure.Documents.UnauthorizedException)
            {
                Assert.AreEqual(1, count, "Only request should be made");
            }

            int countDelayRequests = 0;
            count = 0;
            try
            {
                await GlobalEndpointManager.GetDatabaseAccountFromAnyLocationsAsync(
                    defaultEndpoint: defaultEndpoint,
                    locations: new List<string>(){
                       "westus",
                       "southeastasia",
                       "northcentralus"
                    },
                    getDatabaseAccountFn: async (uri) =>
                    {
                        count++;
                        if (uri == defaultEndpoint)
                        {
                            countDelayRequests++;
                            await Task.Delay(TimeSpan.FromMinutes(1));
                        }

                        throw new Microsoft.Azure.Documents.UnauthorizedException("Mock failed exception");
                    });

                Assert.Fail("Should throw the UnauthorizedException");
            }
            catch (Microsoft.Azure.Documents.UnauthorizedException)
            {
                Assert.IsTrue(count <= 3, "Global endpoint is 1, 2 tasks going to regions parallel");
                Assert.AreEqual(2, count, "Only request should be made");
            }
        }

        /// <summary>
        /// Tests for <see cref="GlobalEndpointManager"/>
        /// </summary>
        [TestMethod]
        public async Task GetDatabaseAccountFromAnyLocationsMockTestAsync()
        {
            AccountProperties databaseAccount = new AccountProperties
            {
                ReadLocationsInternal = new Collection<AccountRegion>()
                {
                    new AccountRegion
                    {
                        Name = "Location1",
                        Endpoint = "https://testfailover-westus.documents-test.windows-int.net/"
                    },
                     new AccountRegion
                    {
                        Name = "Location2",
                        Endpoint = "https://testfailover-southeastasia.documents-test.windows-int.net/"
                    },
                    new AccountRegion
                    {
                        Name = "Location3",
                        Endpoint = "https://testfailover-northcentralus.documents-test.windows-int.net/"
                    },
                }
            };

            Uri defaultEndpoint = new Uri("https://testfailover.documents-test.windows-int.net/");

            GetAccountRequestInjector slowPrimaryRegionHelper = new GetAccountRequestInjector()
            {
                ShouldFailRequest = (uri) => false,
                ShouldDelayRequest = (uri) => false,
                SuccessResponse = databaseAccount,
            };

            // Happy path where global succeeds
            AccountProperties globalEndpointResult = await GlobalEndpointManager.GetDatabaseAccountFromAnyLocationsAsync(
                defaultEndpoint: defaultEndpoint,
                locations: new List<string>(){
                   "westus",
                   "southeastasia",
                   "northcentralus"
                },
                getDatabaseAccountFn: (uri) => slowPrimaryRegionHelper.RequestHelper(uri));

            Assert.AreEqual(globalEndpointResult, databaseAccount);
            Assert.AreEqual(0, slowPrimaryRegionHelper.FailedEndpointCount);
            Assert.AreEqual(0, slowPrimaryRegionHelper.SlowEndpointCount);
            Assert.IsTrue(slowPrimaryRegionHelper.ReturnedSuccess);

            // global and primary slow and fail
            {
                slowPrimaryRegionHelper.Reset();
                slowPrimaryRegionHelper.ShouldDelayRequest = (uri) => uri == defaultEndpoint || uri == new Uri(databaseAccount.ReadLocationsInternal.First().Endpoint);
                slowPrimaryRegionHelper.ShouldFailRequest = slowPrimaryRegionHelper.ShouldDelayRequest;

                Stopwatch stopwatch = Stopwatch.StartNew();
                globalEndpointResult = await GlobalEndpointManager.GetDatabaseAccountFromAnyLocationsAsync(
                    defaultEndpoint: defaultEndpoint,
                    locations: new List<string>(){
                       "westus",
                       "southeastasia",
                       "northcentralus"
                    },
                    getDatabaseAccountFn: (uri) => slowPrimaryRegionHelper.RequestHelper(uri));
                stopwatch.Stop();

                Assert.AreEqual(globalEndpointResult, databaseAccount);
                Assert.AreEqual(2, slowPrimaryRegionHelper.SlowEndpointCount);
                Assert.IsTrue(slowPrimaryRegionHelper.ReturnedSuccess);
                Assert.IsTrue(stopwatch.Elapsed > TimeSpan.FromSeconds(5));
                Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(10));
            }

            // All but the last URI succeeds
            {
                slowPrimaryRegionHelper.Reset();
                slowPrimaryRegionHelper.ShouldDelayRequest = (uri) => false;
                slowPrimaryRegionHelper.ShouldFailRequest = (uri) => uri != new Uri(databaseAccount.ReadLocationsInternal.Last().Endpoint);

                globalEndpointResult = await GlobalEndpointManager.GetDatabaseAccountFromAnyLocationsAsync(
                    defaultEndpoint: defaultEndpoint,
                    locations: new List<string>(){
                       "westus",
                       "southeastasia",
                       "northcentralus"
                    },
                    getDatabaseAccountFn: (uri) => slowPrimaryRegionHelper.RequestHelper(uri));

                Assert.AreEqual(globalEndpointResult, databaseAccount);
                Assert.AreEqual(3, slowPrimaryRegionHelper.FailedEndpointCount);
                Assert.IsTrue(slowPrimaryRegionHelper.ReturnedSuccess);
            }

            // All request but middle is delayed
            {
                slowPrimaryRegionHelper.Reset();
                slowPrimaryRegionHelper.ShouldDelayRequest = (uri) => uri != new Uri(databaseAccount.ReadLocationsInternal[1].Endpoint);
                slowPrimaryRegionHelper.ShouldFailRequest = (uri) => false;

                globalEndpointResult = await GlobalEndpointManager.GetDatabaseAccountFromAnyLocationsAsync(
                    defaultEndpoint: defaultEndpoint,
                    locations: new List<string>(){
                       "westus",
                       "southeastasia",
                       "northcentralus"
                    },
                    getDatabaseAccountFn: (uri) => slowPrimaryRegionHelper.RequestHelper(uri));

                Assert.AreEqual(globalEndpointResult, databaseAccount);
                Assert.AreEqual(0, slowPrimaryRegionHelper.FailedEndpointCount);
                Assert.IsTrue(slowPrimaryRegionHelper.ReturnedSuccess);
            }

            // Delay global and primary region, then only last region should succeed.
            {
                slowPrimaryRegionHelper.Reset();
                slowPrimaryRegionHelper.ShouldFailRequest = (uri) => !uri.ToString().Contains("westus7");
                slowPrimaryRegionHelper.ShouldDelayRequest = (uri) => uri == defaultEndpoint || uri == new Uri(databaseAccount.ReadLocationsInternal.First().Endpoint);

                globalEndpointResult = await GlobalEndpointManager.GetDatabaseAccountFromAnyLocationsAsync(
                    defaultEndpoint: defaultEndpoint,
                    locations: new List<string>(){
                       "westus",
                       "westus2",
                       "westus3",
                       "westus4",
                       "westus5",
                       "westus6",
                       "westus7",
                    },
                    getDatabaseAccountFn: (uri) => slowPrimaryRegionHelper.RequestHelper(uri));

                Assert.AreEqual(globalEndpointResult, databaseAccount);
                Assert.AreEqual(5, slowPrimaryRegionHelper.FailedEndpointCount);
                Assert.IsTrue(slowPrimaryRegionHelper.ReturnedSuccess);
            }
        }

        private sealed class GetAccountRequestInjector
        {
            public Func<Uri, bool> ShouldFailRequest { get; set; }
            public Func<Uri, bool> ShouldDelayRequest { get; set; }
            public AccountProperties SuccessResponse { get; set; }

            public int SlowEndpointCount = 0;
            public int FailedEndpointCount = 0;
            public bool ReturnedSuccess { get; set; } = false;

            public Task<AccountProperties> SuccessHelper(
                Uri endpoint)
            {
                this.ReturnedSuccess = true;
                return Task.FromResult(this.SuccessResponse);
            }

            public async Task<AccountProperties> RequestHelper(
                Uri endpoint)
            {
                if (this.ShouldDelayRequest(endpoint))
                {
                    Interlocked.Increment(ref this.SlowEndpointCount);
                    await Task.Delay(TimeSpan.FromMinutes(1));
                }

                if (this.ShouldFailRequest(endpoint))
                {
                    Interlocked.Increment(ref this.FailedEndpointCount);
                    throw new HttpRequestException("Mocked failed request");
                }

                this.ReturnedSuccess = true;
                return this.SuccessResponse;
            }

            public void Reset()
            {
                this.ShouldDelayRequest = null;
                this.ShouldFailRequest = null;
                this.ReturnedSuccess = false;
                this.SlowEndpointCount = 0;
                this.FailedEndpointCount = 0;
            }
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