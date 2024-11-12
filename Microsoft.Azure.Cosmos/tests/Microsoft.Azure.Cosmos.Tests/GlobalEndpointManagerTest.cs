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
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
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
        [TestCategory("Flaky")]
        public async Task EndpointFailureMockTest()
        {
            Environment.SetEnvironmentVariable("MinimumIntervalForNonForceRefreshLocationInMS", "100");
            try
            {
                // Setup dummpy read locations for the database account
                Collection<AccountRegion> readableLocations = new Collection<AccountRegion>();

                AccountRegion writeLocation = new AccountRegion
                {
                    Name = "WriteLocation",
                    Endpoint = "https://writeendpoint.net/"
                };

                AccountRegion readLocation1 = new AccountRegion
                {
                    Name = "ReadLocation1",
                    Endpoint = "https://readendpoint1.net/"
                };

                AccountRegion readLocation2 = new AccountRegion
                {
                    Name = "ReadLocation2",
                    Endpoint = "https://readendpoint2.net/"
                };

                readableLocations.Add(writeLocation);
                readableLocations.Add(readLocation1);
                readableLocations.Add(readLocation2);

                AccountProperties databaseAccount = new AccountProperties
                {
                    ReadLocationsInternal = readableLocations
                };

                //Setup mock owner "document client"
                Mock<IDocumentClientInternal> mockOwner = new Mock<IDocumentClientInternal>();
                mockOwner.Setup(owner => owner.ServiceEndpoint).Returns(new Uri("https://defaultendpoint.net/"));

                int getAccountInfoCount = 0;
                mockOwner.Setup(owner => owner.GetDatabaseAccountInternalAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
                    .Callback(() => getAccountInfoCount++)
                    .ReturnsAsync(databaseAccount);

                //Create connection policy and populate preferred locations
                ConnectionPolicy connectionPolicy = new ConnectionPolicy();
                connectionPolicy.PreferredLocations.Add("ReadLocation1");
                connectionPolicy.PreferredLocations.Add("ReadLocation2");

                using (GlobalEndpointManager globalEndpointManager = new GlobalEndpointManager(mockOwner.Object, connectionPolicy))
                {
                    globalEndpointManager.InitializeAccountPropertiesAndStartBackgroundRefresh(databaseAccount);
                    Assert.AreEqual(globalEndpointManager.ReadEndpoints[0], new Uri(readLocation1.Endpoint));

                    //Mark each of the read locations as unavailable and validate that the read endpoint switches to the next preferred region / default endpoint.
                    globalEndpointManager.MarkEndpointUnavailableForRead(globalEndpointManager.ReadEndpoints[0]);
                    await globalEndpointManager.RefreshLocationAsync();
                    Assert.AreEqual(globalEndpointManager.ReadEndpoints[0], new Uri(readLocation2.Endpoint));

                    globalEndpointManager.MarkEndpointUnavailableForRead(globalEndpointManager.ReadEndpoints[0]);
                    await globalEndpointManager.RefreshLocationAsync();
                    Assert.AreEqual(globalEndpointManager.ReadEndpoints[0], globalEndpointManager.WriteEndpoints[0]);

                    getAccountInfoCount = 0;
                    //Sleep a second for the unavailable endpoint entry to expire and background refresh timer to kick in
                    await Task.Delay(TimeSpan.FromSeconds(3));
                    Assert.IsTrue(getAccountInfoCount > 0, "Callback is not working. There should be at least one call in this time frame.");

                    await globalEndpointManager.RefreshLocationAsync();
                    Assert.AreEqual(globalEndpointManager.ReadEndpoints[0], new Uri(readLocation1.Endpoint));
                }

                Assert.IsTrue(getAccountInfoCount > 0, "Callback is not working. There should be at least one call in this time frame.");
                getAccountInfoCount = 0;
                await Task.Delay(TimeSpan.FromSeconds(5));
                Assert.IsTrue(getAccountInfoCount <= 1, "There should be at most 1 call to refresh tied to the background refresh happening while Dispose cancels the internal CancellationToken");
            }
            finally
            {
                Environment.SetEnvironmentVariable("MinimumIntervalForNonForceRefreshLocationInMS", null);
            }
        }

        [TestMethod]
        public async Task ValidateCancellationTokenLogicForGetDatabaseAccountFromAnyLocationAsync()
        {
            Uri defaultEndpoint = new Uri("https://testfailover.documents-test.windows-int.net/");
            using CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            try
            {
                await GlobalEndpointManager.GetDatabaseAccountFromAnyLocationsAsync(
                   defaultEndpoint,
                   locations: new List<string>(){
                           "westus",
                           "southeastasia",
                           "northcentralus"
                       },
                   accountInitializationCustomEndpoints: null,
                   getDatabaseAccountFn: (uri) => throw new Exception("The operation should be canceled and never make the network call."),
                   cancellationTokenSource.Token);

                Assert.Fail("Previous call should have failed");
            }
            catch (OperationCanceledException op)
            {
                Assert.IsTrue(op.Message.Contains("GlobalEndpointManager"));
            }
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
                    accountInitializationCustomEndpoints: null,
                    getDatabaseAccountFn: (uri) =>
                    {
                        count++;
                        if (uri == defaultEndpoint)
                        {
                            throw new Microsoft.Azure.Documents.UnauthorizedException("Mock failed exception");
                        }

                        throw new Exception("This should never be hit since it should stop after the global endpoint hit the nonretriable exception");
                    },
                    cancellationToken: default);

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
                    accountInitializationCustomEndpoints: null,
                    getDatabaseAccountFn: async (uri) =>
                    {
                        count++;
                        if (uri == defaultEndpoint)
                        {
                            countDelayRequests++;
                            await Task.Delay(TimeSpan.FromMinutes(1));
                        }

                        throw new Microsoft.Azure.Documents.UnauthorizedException("Mock failed exception");
                    },
                    cancellationToken: default);

                Assert.Fail("Should throw the UnauthorizedException");
            }
            catch (Microsoft.Azure.Documents.UnauthorizedException)
            {
                Assert.IsTrue(count <= 3, "Global endpoint is 1, 2 tasks going to regions parallel");
                Assert.AreEqual(2, count, "Only request should be made");
            }

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
                    accountInitializationCustomEndpoints: null,
                    getDatabaseAccountFn: (uri) =>
                    {
                        count++;
                        if (uri == defaultEndpoint)
                        {
                            throw new Microsoft.Azure.Documents.ForbiddenException("Mock ForbiddenException exception");
                        }

                        throw new Exception("This should never be hit since it should stop after the global endpoint hit the nonretriable exception");
                    },
                    cancellationToken: default);

                Assert.Fail("Should throw the ForbiddenException");
            }
            catch (Microsoft.Azure.Documents.ForbiddenException)
            {
                Assert.AreEqual(1, count, "Only request should be made");
            }

            // All endpoints failed. Validate aggregate exception
            count = 0;
            HashSet<Exception> exceptions = new HashSet<Exception>();
            try
            {
                await GlobalEndpointManager.GetDatabaseAccountFromAnyLocationsAsync(
                    defaultEndpoint: defaultEndpoint,
                    locations: new List<string>(){
                       "westus",
                       "southeastasia",
                       "northcentralus"
                    },
                    accountInitializationCustomEndpoints: null,
                    getDatabaseAccountFn: (uri) =>
                    {
                        count++;
                        Exception exception = new HttpRequestException("Mock HttpRequestException exception:" + count);
                        exceptions.Add(exception);
                        throw exception;
                    },
                    cancellationToken: default);

                Assert.Fail("Should throw the AggregateException");
            }
            catch (AggregateException aggregateException)
            {
                Assert.AreEqual(4, count, "All endpoints should have been tried. 1 global, 3 regional endpoints");
                Assert.AreEqual(4, exceptions.Count, "Some exceptions were not logged");
                Assert.AreEqual(4, aggregateException.InnerExceptions.Count, "aggregateException should have 4 inner exceptions");
                foreach (Exception exception in aggregateException.InnerExceptions)
                {
                    Assert.IsTrue(exceptions.Contains(exception));
                }
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
                accountInitializationCustomEndpoints: null,
                getDatabaseAccountFn: (uri) => slowPrimaryRegionHelper.RequestHelper(uri),
                cancellationToken: default);

            Assert.AreEqual(globalEndpointResult, databaseAccount);
            Assert.AreEqual(0, slowPrimaryRegionHelper.FailedEndpointCount);
            Assert.AreEqual(0, slowPrimaryRegionHelper.SlowEndpointCount);
            Assert.IsTrue(slowPrimaryRegionHelper.ReturnedSuccess);

            // global and primary slow and fail
            {
                slowPrimaryRegionHelper.Reset();
                slowPrimaryRegionHelper.ShouldDelayRequest = (uri) => uri == defaultEndpoint || uri == new Uri(databaseAccount.ReadLocationsInternal.First().Endpoint);
                slowPrimaryRegionHelper.ShouldFailRequest = slowPrimaryRegionHelper.ShouldDelayRequest;

                ValueStopwatch stopwatch = ValueStopwatch.StartNew();
                globalEndpointResult = await GlobalEndpointManager.GetDatabaseAccountFromAnyLocationsAsync(
                    defaultEndpoint: defaultEndpoint,
                    locations: new List<string>(){
                       "westus",
                       "southeastasia",
                       "northcentralus"
                    },
                    accountInitializationCustomEndpoints: null,
                    getDatabaseAccountFn: (uri) => slowPrimaryRegionHelper.RequestHelper(uri),
                    cancellationToken: default);
                stopwatch.Stop();

                Assert.AreEqual(globalEndpointResult, databaseAccount);
                Assert.AreEqual(2, slowPrimaryRegionHelper.SlowEndpointCount);
                Assert.IsTrue(slowPrimaryRegionHelper.ReturnedSuccess);
                Assert.IsTrue(stopwatch.Elapsed > TimeSpan.FromSeconds(1));
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
                    accountInitializationCustomEndpoints: null,
                    getDatabaseAccountFn: (uri) => slowPrimaryRegionHelper.RequestHelper(uri),
                    cancellationToken: default);

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
                    accountInitializationCustomEndpoints: null,
                    getDatabaseAccountFn: (uri) => slowPrimaryRegionHelper.RequestHelper(uri),
                    cancellationToken: default);

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
                    accountInitializationCustomEndpoints: null,
                    getDatabaseAccountFn: (uri) => slowPrimaryRegionHelper.RequestHelper(uri),
                    cancellationToken: default);

                Assert.AreEqual(globalEndpointResult, databaseAccount);
                Assert.AreEqual(5, slowPrimaryRegionHelper.FailedEndpointCount);
                Assert.IsTrue(slowPrimaryRegionHelper.ReturnedSuccess);
            }
        }

        /// <summary>
        /// Test to validate for a client that has been warmed up with account-level regions, any subsequent
        /// DatabaseAccount refresh calls should go through the effective preferred regions / account-level read regions
        /// if the DatabaseAccount refresh call to the global / default endpoint failed with HttpRequestException (timeout also but not possible to inject
        /// w/o adding a refresh method just for this test)
        /// </summary>
        [TestMethod]
        public async Task GetDatabaseAccountFromEffectiveRegionalEndpointTestAsync()
        {
            AccountProperties databaseAccount = new AccountProperties
            {
                ReadLocationsInternal = new Collection<AccountRegion>()
                {
                    new AccountRegion
                    {
                        Name = "Location1",
                        Endpoint = "https://testfailover-location1.documents-test.windows-int.net/"
                    },
                    new AccountRegion
                    {
                        Name = "Location2",
                        Endpoint = "https://testfailover-location2.documents-test.windows-int.net/"
                    },
                    new AccountRegion
                    {
                        Name = "Location3",
                        Endpoint = "https://testfailover-location3.documents-test.windows-int.net/"
                    },
                }
            };

            Uri defaultEndpoint = new Uri("https://testfailover.documents-test.windows-int.net/");
            Uri effectivePreferredRegion1SuffixedUri = new Uri("https://testfailover-location1.documents-test.windows-int.net/");

            //Setup mock owner "document client"
            Mock<IDocumentClientInternal> mockOwner = new Mock<IDocumentClientInternal>();

            mockOwner.Setup(owner => owner.ServiceEndpoint).Returns(defaultEndpoint);
            mockOwner.SetupSequence(owner =>
                    owner.GetDatabaseAccountInternalAsync(defaultEndpoint, It.IsAny<CancellationToken>()))
                .ReturnsAsync(databaseAccount)
                .ThrowsAsync(new HttpRequestException());
            mockOwner.Setup(owner =>
                    owner.GetDatabaseAccountInternalAsync(effectivePreferredRegion1SuffixedUri, It.IsAny<CancellationToken>()))
                .ReturnsAsync(databaseAccount);

            // Create connection policy with no preferred locations
            ConnectionPolicy connectionPolicy = new ConnectionPolicy();

            using GlobalEndpointManager globalEndpointManager =
                new GlobalEndpointManager(mockOwner.Object, connectionPolicy);
            globalEndpointManager.InitializeAccountPropertiesAndStartBackgroundRefresh(databaseAccount);

            await Task.Delay(TimeSpan.FromSeconds(5));
            await globalEndpointManager.RefreshLocationAsync(forceRefresh: true);

            mockOwner.Verify(
                owner => owner.GetDatabaseAccountInternalAsync(defaultEndpoint, It.IsAny<CancellationToken>()),
                Times.Exactly(2));
            mockOwner.Verify(
                owner => owner.GetDatabaseAccountInternalAsync(effectivePreferredRegion1SuffixedUri, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        /// <summary>
        /// Test to validate that when an exception is thrown during a RefreshLocationAsync call
        /// the exception should not be bubbled up and remain unobserved. The exception should be
        /// handled gracefully and logged as a warning trace event.
        /// </summary>
        [TestMethod]
        public async Task RefreshLocationAsync_WhenGetDatabaseThrowsException_ShouldNotBubbleUpAsUnobservedException()
        {
            // Arrange.
            Mock<IDocumentClientInternal> mockOwner = new Mock<IDocumentClientInternal>();
            mockOwner.Setup(owner => owner.ServiceEndpoint).Returns(new Uri("https://defaultendpoint.net/"));
            mockOwner.Setup(owner => owner.GetDatabaseAccountInternalAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>())).ThrowsAsync(new TaskCanceledException());

            //Create connection policy and populate preferred locations
            ConnectionPolicy connectionPolicy = new ConnectionPolicy();
            connectionPolicy.PreferredLocations.Add("ReadLocation1");
            connectionPolicy.PreferredLocations.Add("ReadLocation2");

            bool isExceptionLogged = false;
            void TraceHandler(string message)
            {
                if (message.Contains("Failed to refresh database account with exception:"))
                {
                    isExceptionLogged = true;
                }
            }

            DefaultTrace.TraceSource.Listeners.Add(new TestTraceListener { Callback = TraceHandler });
            DefaultTrace.InitEventListener();

            using GlobalEndpointManager globalEndpointManager = new(mockOwner.Object, connectionPolicy);

            // Act.
            await globalEndpointManager.RefreshLocationAsync(forceRefresh: false);

            // Assert.
            Assert.IsTrue(isExceptionLogged, "The exception was logged as a warning trace event.");
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
        public async Task ReadLocationRemoveAndAddMockTest()
        {
            string originalConfigValue = Environment.GetEnvironmentVariable("MinimumIntervalForNonForceRefreshLocationInMS");
            Environment.SetEnvironmentVariable("MinimumIntervalForNonForceRefreshLocationInMS", "1000");

            // Setup dummpy read locations for the database account
            Collection<AccountRegion> readableLocations = new Collection<AccountRegion>();

            AccountRegion writeLocation = new AccountRegion
            {
                Name = "WriteLocation",
                Endpoint = "https://writeendpoint.net/"
            };

            AccountRegion readLocation1 = new AccountRegion
            {
                Name = "ReadLocation1",
                Endpoint = "https://readendpoint1.net/"
            };

            AccountRegion readLocation2 = new AccountRegion
            {
                Name = "ReadLocation2",
                Endpoint = "https://readendpoint2.net/"
            };

            readableLocations.Add(writeLocation);
            readableLocations.Add(readLocation1);
            readableLocations.Add(readLocation2);

            AccountProperties databaseAccount = new AccountProperties
            {
                ReadLocationsInternal = readableLocations
            };

            //Setup mock owner "document client"
            Mock<IDocumentClientInternal> mockOwner = new Mock<IDocumentClientInternal>();
            mockOwner.Setup(owner => owner.ServiceEndpoint).Returns(new Uri("https://defaultendpoint.net/"));
            mockOwner.Setup(owner => owner.GetDatabaseAccountInternalAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>())).ReturnsAsync(databaseAccount);

            //Create connection policy and populate preferred locations
            ConnectionPolicy connectionPolicy = new ConnectionPolicy();
            connectionPolicy.PreferredLocations.Add("ReadLocation1");
            connectionPolicy.PreferredLocations.Add("ReadLocation2");

            using GlobalEndpointManager globalEndpointManager = new GlobalEndpointManager(mockOwner.Object, connectionPolicy);

            globalEndpointManager.InitializeAccountPropertiesAndStartBackgroundRefresh(databaseAccount);
            Assert.AreEqual(globalEndpointManager.ReadEndpoints[0], new Uri(readLocation1.Endpoint));

            //Remove location 1 from read locations and validate that the read endpoint switches to the next preferred location
            readableLocations.Remove(readLocation1);
            databaseAccount.ReadLocationsInternal = readableLocations;

            globalEndpointManager.InitializeAccountPropertiesAndStartBackgroundRefresh(databaseAccount);
            Assert.AreEqual(globalEndpointManager.ReadEndpoints[0], new Uri(readLocation2.Endpoint));

            //Add location 1 back to read locations and validate that location 1 becomes the read endpoint again.
            readableLocations.Add(readLocation1);
            databaseAccount.ReadLocationsInternal = readableLocations;

            bool isGlobalEndpointRefreshStarted = false;
            void TraceHandler(string message)
            {
                if (message.Contains("GlobalEndpointManager: StartLocationBackgroundRefreshWithTimer() - Invoking refresh"))
                {
                    isGlobalEndpointRefreshStarted = true;
                }
            }

            DefaultTrace.TraceSource.Listeners.Add(new TestTraceListener { Callback = TraceHandler });
            DefaultTrace.InitEventListener();

            ValueStopwatch stopwatch = ValueStopwatch.StartNew();
            // Wait for the trace message saying the background refresh occurred
            while (!isGlobalEndpointRefreshStarted)
            {
                Assert.IsTrue(stopwatch.Elapsed.TotalSeconds < 15, "Background task did not start within 15 seconds.");
                await Task.Delay(500);
            }

            Assert.AreEqual(globalEndpointManager.ReadEndpoints[0], new Uri(readLocation1.Endpoint));

            Environment.SetEnvironmentVariable("MinimumIntervalForNonForceRefreshLocationInMS", originalConfigValue);
        }

        private class TestTraceListener : TraceListener
        {
            public Action<string> Callback { get; set; }
            public override bool IsThreadSafe => true;
            public override void Write(string message)
            {
                this.Callback(message);
            }

            public override void WriteLine(string message)
            {
                this.Callback(message);
            }
        }
    }
}