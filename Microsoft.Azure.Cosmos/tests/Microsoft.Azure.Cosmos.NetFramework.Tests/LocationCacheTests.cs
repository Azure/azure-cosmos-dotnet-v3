//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Client.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    /// <summary>
    /// Tests for <see cref="LocationCache"/>
    /// </summary>
    [TestClass]
    public sealed class LocationCacheTests
    {
        private static Uri DefaultEndpoint = new Uri("https://default.documents.azure.com");
        private static Uri Location1Endpoint = new Uri("https://location1.documents.azure.com");
        private static Uri Location2Endpoint = new Uri("https://location2.documents.azure.com");
        private static Uri Location3Endpoint = new Uri("https://location3.documents.azure.com");
        private static Uri Location4Endpoint = new Uri("https://location4.documents.azure.com");
        private static Uri[] WriteEndpoints = new Uri[] { LocationCacheTests.Location1Endpoint, LocationCacheTests.Location2Endpoint, LocationCacheTests.Location3Endpoint };
        private static Uri[] ReadEndpoints = new Uri[] { LocationCacheTests.Location1Endpoint, LocationCacheTests.Location2Endpoint, LocationCacheTests.Location4Endpoint };
        private static Dictionary<string, Uri> EndpointByLocation = new Dictionary<string, Uri>()
        {
            { "location1", LocationCacheTests.Location1Endpoint },
            { "location2", LocationCacheTests.Location2Endpoint },
            { "location3", LocationCacheTests.Location3Endpoint },
            { "location4", LocationCacheTests.Location4Endpoint },
        };

        private ReadOnlyCollection<string> preferredLocations;
        private CosmosAccountSettings databaseAccount;
        private LocationCache cache;
        private GlobalEndpointManager endpointManager;
        private Mock<IDocumentClientInternal> mockedClient;

        [TestMethod]
        [Owner("atulk")]
        public void ValidateWriteEndpointOrderWithClientSideDisableMultipleWriteLocation()
        {
            this.Initialize(false, true, false);
            Assert.AreEqual(this.cache.WriteEndpoints[0], LocationCacheTests.Location1Endpoint);
            Assert.AreEqual(this.cache.WriteEndpoints[1], LocationCacheTests.Location2Endpoint);
            Assert.AreEqual(this.cache.WriteEndpoints[2], LocationCacheTests.Location3Endpoint);
        }

        [TestMethod]
        [Owner("atulk")]
        public void ValidateGetLocation()
        {
            this.Initialize(
                useMultipleWriteLocations: false,
                enableEndpointDiscovery: true,
                isPreferredLocationsListEmpty: true);

            Assert.AreEqual(this.databaseAccount.WriteLocationsInternal.First().Name, this.cache.GetLocation(LocationCacheTests.DefaultEndpoint));

            foreach (CosmosAccountLocation databaseAccountLocation in this.databaseAccount.WriteLocationsInternal)
            {
                Assert.AreEqual(databaseAccountLocation.Name, this.cache.GetLocation(new Uri(databaseAccountLocation.DatabaseAccountEndpoint)));
            }

            foreach (CosmosAccountLocation databaseAccountLocation in this.databaseAccount.ReadLocationsInternal)
            {
                Assert.AreEqual(databaseAccountLocation.Name, this.cache.GetLocation(new Uri(databaseAccountLocation.DatabaseAccountEndpoint)));
            }
        }

        [TestMethod]
        [Owner("atulk")]
        public async Task ValidateRetryOnSessionNotAvailabeWithDisableMultipleWriteLocationsAndEndpointDiscoveryDisabled()
        {
            await this.ValidateRetryOnSessionNotAvailabeWithEndpointDiscoveryDisabled(false, false, false);
            await this.ValidateRetryOnSessionNotAvailabeWithEndpointDiscoveryDisabled(false, false, true);
            await this.ValidateRetryOnSessionNotAvailabeWithEndpointDiscoveryDisabled(false, true, false);
            await this.ValidateRetryOnSessionNotAvailabeWithEndpointDiscoveryDisabled(false, true, true);
            await this.ValidateRetryOnSessionNotAvailabeWithEndpointDiscoveryDisabled(true, false, false);
            await this.ValidateRetryOnSessionNotAvailabeWithEndpointDiscoveryDisabled(true, false, true);
            await this.ValidateRetryOnSessionNotAvailabeWithEndpointDiscoveryDisabled(true, true, false);
            await this.ValidateRetryOnSessionNotAvailabeWithEndpointDiscoveryDisabled(true, true, true);
        }

        private async Task ValidateRetryOnSessionNotAvailabeWithEndpointDiscoveryDisabled(bool isPreferredLocationsListEmpty, bool useMultipleWriteLocations, bool isReadRequest)
        {
            const bool enableEndpointDiscovery = false;

            this.Initialize(
                useMultipleWriteLocations: useMultipleWriteLocations,
                enableEndpointDiscovery: enableEndpointDiscovery,
                isPreferredLocationsListEmpty: isPreferredLocationsListEmpty);

            ClientRetryPolicy retryPolicy = new ClientRetryPolicy(this.endpointManager, enableEndpointDiscovery, new RetryOptions());

            using (DocumentServiceRequest request = this.CreateRequest(isReadRequest: isReadRequest, isMasterResourceType: false))
            {
                int retryCount = 0;

                try
                {
                    await BackoffRetryUtility<bool>.ExecuteAsync(
                        () =>
                        {
                            retryPolicy.OnBeforeSendRequest(request);

                            if (retryCount == 0)
                            {
                                Assert.AreEqual(request.RequestContext.LocationEndpointToRoute, this.endpointManager.ReadEndpoints[0]);
                            }
                            else
                            {
                                Assert.Fail();
                            }

                            retryCount++;

                            StringKeyValueCollection headers = new StringKeyValueCollection();
                            headers[WFConstants.BackendHeaders.SubStatus] = ((int)SubStatusCodes.ReadSessionNotAvailable).ToString();
                            DocumentClientException notFoundException = new NotFoundException(RMResources.NotFound, headers);

                            throw notFoundException;
                        },
                        retryPolicy);

                    Assert.Fail();
                }
                catch (NotFoundException)
                {
                    DefaultTrace.TraceInformation("Received expected notFoundException");
                    Assert.AreEqual(1, retryCount);
                }
            }
        }

        [TestMethod]
        [Owner("atulk")]
        public async Task ValidateRetryOnSessionNotAvailabeWithDisableMultipleWriteLocationsAndEndpointDiscoveryEnabled()
        {
            await this.ValidateRetryOnSessionNotAvailabeWithDisableMultipleWriteLocationsAndEndpointDiscoveryEnabledAsync(true);
            await this.ValidateRetryOnSessionNotAvailabeWithDisableMultipleWriteLocationsAndEndpointDiscoveryEnabledAsync(false);
        }

        private async Task ValidateRetryOnSessionNotAvailabeWithDisableMultipleWriteLocationsAndEndpointDiscoveryEnabledAsync(bool isPreferredLocationsListEmpty)
        {
            const bool useMultipleWriteLocations = false;
            bool enableEndpointDiscovery = true;

            this.Initialize(
                useMultipleWriteLocations: useMultipleWriteLocations,
                enableEndpointDiscovery: enableEndpointDiscovery,
                isPreferredLocationsListEmpty: isPreferredLocationsListEmpty);

            await this.endpointManager.RefreshLocationAsync(this.databaseAccount);
            ClientRetryPolicy retryPolicy = new ClientRetryPolicy(this.endpointManager, enableEndpointDiscovery, new RetryOptions());

            using (DocumentServiceRequest request = this.CreateRequest(isReadRequest: true, isMasterResourceType: false))
            {
                int retryCount = 0;

                try
                {
                    await BackoffRetryUtility<bool>.ExecuteAsync(
                        () =>
                        {
                            retryPolicy.OnBeforeSendRequest(request);

                            if (retryCount == 0)
                            {
                                Uri expectedEndpoint = isPreferredLocationsListEmpty ?
                                    new Uri(this.databaseAccount.WriteLocationsInternal[0].DatabaseAccountEndpoint) : // All requests go to write endpoint
                                    LocationCacheTests.EndpointByLocation[this.preferredLocations[0]];

                                Assert.AreEqual(expectedEndpoint, request.RequestContext.LocationEndpointToRoute);
                            }
                            else if (retryCount == 1)
                            {
                                // Second request must go to write endpoint
                                Uri expectedEndpoint = new Uri(this.databaseAccount.WriteLocationsInternal[0].DatabaseAccountEndpoint);
                                Assert.AreEqual(expectedEndpoint, request.RequestContext.LocationEndpointToRoute);
                            }
                            else
                            {
                                Assert.Fail();
                            }

                            retryCount++;

                            StringKeyValueCollection headers = new StringKeyValueCollection();
                            headers[WFConstants.BackendHeaders.SubStatus] = ((int)SubStatusCodes.ReadSessionNotAvailable).ToString();
                            DocumentClientException notFoundException = new NotFoundException(RMResources.NotFound, headers);


                            throw notFoundException;
                        },
                        retryPolicy);

                    Assert.Fail();
                }
                catch (NotFoundException)
                {
                    DefaultTrace.TraceInformation("Received expected notFoundException");
                    Assert.AreEqual(2, retryCount);
                }
            }
        }

        [TestMethod]
        [Owner("atulk")]
        public async Task ValidateRetryOnReadSessionNotAvailabeWithEnableMultipleWriteLocationsAndEndpointDiscoveryEnabled()
        {
            await this.ValidateRetryOnReadSessionNotAvailabeWithEnableMultipleWriteLocationsAsync();
            await this.ValidateRetryOnWriteSessionNotAvailabeWithEnableMultipleWriteLocationsAsync();
        }

        private async Task ValidateRetryOnReadSessionNotAvailabeWithEnableMultipleWriteLocationsAsync()
        {
            const bool useMultipleWriteLocations = true;
            bool enableEndpointDiscovery = true;

            this.Initialize(
                useMultipleWriteLocations: useMultipleWriteLocations,
                enableEndpointDiscovery: enableEndpointDiscovery,
                isPreferredLocationsListEmpty: false);

            await this.endpointManager.RefreshLocationAsync(this.databaseAccount);
            ClientRetryPolicy retryPolicy = new ClientRetryPolicy(this.endpointManager, enableEndpointDiscovery, new RetryOptions());

            using (DocumentServiceRequest request = this.CreateRequest(isReadRequest: true, isMasterResourceType: false))
            {
                int retryCount = 0;

                try
                {
                    await BackoffRetryUtility<bool>.ExecuteAsync(
                        () =>
                        {
                            retryPolicy.OnBeforeSendRequest(request);

                            if (retryCount == 0)
                            {
                                Uri expectedEndpoint = LocationCacheTests.EndpointByLocation[this.preferredLocations[0]];

                                Assert.AreEqual(expectedEndpoint, request.RequestContext.LocationEndpointToRoute);
                            }
                            else if (retryCount == 1)
                            {
                                // Second request must go to first write endpoint
                                Uri expectedEndpoint = new Uri(this.databaseAccount.WriteLocationsInternal[0].DatabaseAccountEndpoint);

                                Assert.AreEqual(expectedEndpoint, request.RequestContext.LocationEndpointToRoute);
                            }
                            else if (retryCount == 2)
                            {
                                // Second request must go to first write endpoint
                                Uri expectedEndpoint = LocationCacheTests.EndpointByLocation[this.preferredLocations[1]];
                                Assert.AreEqual(expectedEndpoint, request.RequestContext.LocationEndpointToRoute);
                            }
                            else
                            {
                                Assert.Fail();
                            }

                            retryCount++;

                            StringKeyValueCollection headers = new StringKeyValueCollection();
                            headers[WFConstants.BackendHeaders.SubStatus] = ((int)SubStatusCodes.ReadSessionNotAvailable).ToString();
                            DocumentClientException notFoundException = new NotFoundException(RMResources.NotFound, headers);


                            throw notFoundException;
                        },
                        retryPolicy);

                    Assert.Fail();
                }
                catch (NotFoundException)
                {
                    DefaultTrace.TraceInformation("Received expected notFoundException");
                    Assert.AreEqual(3, retryCount);
                }
            }
        }

        private async Task ValidateRetryOnWriteSessionNotAvailabeWithEnableMultipleWriteLocationsAsync()
        {
            const bool useMultipleWriteLocations = true;
            bool enableEndpointDiscovery = true;

            this.Initialize(
                useMultipleWriteLocations: useMultipleWriteLocations,
                enableEndpointDiscovery: enableEndpointDiscovery,
                isPreferredLocationsListEmpty: false);

            await this.endpointManager.RefreshLocationAsync(this.databaseAccount);
            ClientRetryPolicy retryPolicy = new ClientRetryPolicy(this.endpointManager, enableEndpointDiscovery, new RetryOptions());

            using (DocumentServiceRequest request = this.CreateRequest(isReadRequest: false, isMasterResourceType: false))
            {
                int retryCount = 0;

                try
                {
                    await BackoffRetryUtility<bool>.ExecuteAsync(
                        () =>
                        {
                            retryPolicy.OnBeforeSendRequest(request);

                            if (retryCount == 0)
                            {
                                Uri expectedEndpoint = LocationCacheTests.EndpointByLocation[this.preferredLocations[0]];

                                Assert.AreEqual(expectedEndpoint, request.RequestContext.LocationEndpointToRoute);
                            }
                            else if (retryCount == 1)
                            {
                                // Second request must go to first write endpoint
                                Uri expectedEndpoint = new Uri(this.databaseAccount.WriteLocationsInternal[0].DatabaseAccountEndpoint);

                                Assert.AreEqual(expectedEndpoint, request.RequestContext.LocationEndpointToRoute);
                            }
                            else if (retryCount == 2)
                            {
                                // Second request must go to first write endpoint
                                Uri expectedEndpoint = LocationCacheTests.EndpointByLocation[this.preferredLocations[1]];
                                Assert.AreEqual(expectedEndpoint, request.RequestContext.LocationEndpointToRoute);
                            }
                            else if (retryCount == 3)
                            {
                                // Second request must go to first write endpoint
                                Uri expectedEndpoint = LocationCacheTests.EndpointByLocation[this.preferredLocations[2]];
                                Assert.AreEqual(expectedEndpoint, request.RequestContext.LocationEndpointToRoute);
                            }
                            else
                            {
                                Assert.Fail();
                            }

                            retryCount++;

                            StringKeyValueCollection headers = new StringKeyValueCollection();
                            headers[WFConstants.BackendHeaders.SubStatus] = ((int)SubStatusCodes.ReadSessionNotAvailable).ToString();
                            DocumentClientException notFoundException = new NotFoundException(RMResources.NotFound, headers);


                            throw notFoundException;
                        },
                        retryPolicy);

                    Assert.Fail();
                }
                catch (NotFoundException)
                {
                    DefaultTrace.TraceInformation("Received expected notFoundException");
                    Assert.AreEqual(4, retryCount);
                }
            }
        }

        [TestMethod]
        [Owner("atulk")]
        public async Task ValidateRetryOnWriteForbiddenExceptionAsync()
        {
            this.Initialize(
                useMultipleWriteLocations: false,
                enableEndpointDiscovery: true,
                isPreferredLocationsListEmpty: false);

            await this.endpointManager.RefreshLocationAsync(this.databaseAccount);
            ClientRetryPolicy retryPolicy = new ClientRetryPolicy(this.endpointManager, true, new RetryOptions());

            using (DocumentServiceRequest request = this.CreateRequest(isReadRequest: false, isMasterResourceType: false))
            {
                int retryCount = 0;

                await BackoffRetryUtility<bool>.ExecuteAsync(
                    () =>
                    {
                        retryCount++;
                        retryPolicy.OnBeforeSendRequest(request);

                        if (retryCount == 1)
                        {
                            this.mockedClient.ResetCalls();

                            Uri expectedEndpoint = LocationCacheTests.EndpointByLocation[this.preferredLocations[0]];

                            Assert.AreEqual(expectedEndpoint, request.RequestContext.LocationEndpointToRoute);

                            StringKeyValueCollection headers = new StringKeyValueCollection();
                            headers[WFConstants.BackendHeaders.SubStatus] = ((int)SubStatusCodes.WriteForbidden).ToString();
                            DocumentClientException forbiddenException = new ForbiddenException(RMResources.Forbidden, headers);

                            throw forbiddenException;
                        }
                        else if (retryCount == 2)
                        {
                            this.mockedClient.Verify(client => client.GetDatabaseAccountInternalAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()), Times.Once);

                            // Next request must go to next preferred endpoint
                            Uri expectedEndpoint = LocationCacheTests.EndpointByLocation[this.preferredLocations[1]];
                            Assert.AreEqual(expectedEndpoint, request.RequestContext.LocationEndpointToRoute);

                            return Task.FromResult(true);
                        }
                        else
                        {
                            Assert.Fail();
                        }

                        return Task.FromResult(true);
                    },
                    retryPolicy);
            }
        }

        [TestMethod]
        [Owner("atulk")]
        public async Task ValidateRetryOnDatabaseAccountNotFoundAsync()
        {
            await this.ValidateRetryOnDatabaseAccountNotFoundAsync(enableMultipleWriteLocations: false, isReadRequest: false);
            await this.ValidateRetryOnDatabaseAccountNotFoundAsync(enableMultipleWriteLocations: false, isReadRequest: true);
            await this.ValidateRetryOnDatabaseAccountNotFoundAsync(enableMultipleWriteLocations: true, isReadRequest: false);
            await this.ValidateRetryOnDatabaseAccountNotFoundAsync(enableMultipleWriteLocations: true, isReadRequest: true);
        }

        private async Task ValidateRetryOnDatabaseAccountNotFoundAsync(bool enableMultipleWriteLocations, bool isReadRequest)
        {
            this.Initialize(
                useMultipleWriteLocations: enableMultipleWriteLocations,
                enableEndpointDiscovery: true,
                isPreferredLocationsListEmpty: false);

            await this.endpointManager.RefreshLocationAsync(this.databaseAccount);
            ClientRetryPolicy retryPolicy = new ClientRetryPolicy(this.endpointManager, true, new RetryOptions());

            int expectedRetryCount = isReadRequest || enableMultipleWriteLocations ? 2 : 1;

            using (DocumentServiceRequest request = this.CreateRequest(isReadRequest: isReadRequest, isMasterResourceType: false))
            {
                int retryCount = 0;

                try
                {
                    await BackoffRetryUtility<bool>.ExecuteAsync(
                        () =>
                        {
                            retryCount++;
                            retryPolicy.OnBeforeSendRequest(request);

                            if (retryCount == 1)
                            {
                                Uri expectedEndpoint = LocationCacheTests.EndpointByLocation[this.preferredLocations[0]];

                                Assert.AreEqual(expectedEndpoint, request.RequestContext.LocationEndpointToRoute);

                                StringKeyValueCollection headers = new StringKeyValueCollection();
                                headers[WFConstants.BackendHeaders.SubStatus] = ((int)SubStatusCodes.DatabaseAccountNotFound).ToString();
                                DocumentClientException forbiddenException = new ForbiddenException(RMResources.NotFound, headers);

                                throw forbiddenException;
                            }
                            else if (retryCount == 2)
                            {
                                // Next request must go to next preferred endpoint
                                Uri expectedEndpoint = LocationCacheTests.EndpointByLocation[this.preferredLocations[1]];
                                Assert.AreEqual(expectedEndpoint, request.RequestContext.LocationEndpointToRoute);

                                return Task.FromResult(true);
                            }
                            else
                            {
                                Assert.Fail();
                            }

                            return Task.FromResult(true);
                        },
                        retryPolicy);
                }
                catch (ForbiddenException)
                {
                    if (expectedRetryCount == 1)
                    {
                        DefaultTrace.TraceInformation("Received expected ForbiddenException");
                    }
                    else
                    {
                        Assert.Fail();
                    }
                }

                Assert.AreEqual(expectedRetryCount, retryCount);
            }
        }

        [TestMethod]
        [Owner("atulk")]
        public async Task ValidateAsync()
        {
            for (int i = 0; i < 8; i++)
            {
                bool useMultipleWriteEndpoints = (i & 1) > 0;
                bool endpointDiscoveryEnabled = (i & 2) > 0;
                bool isPreferredListEmpty = (i & 4) > 0;
                await this.ValidateLocationCacheAsync(useMultipleWriteEndpoints, endpointDiscoveryEnabled, isPreferredListEmpty);
            }
        }

        private static CosmosAccountSettings CreateDatabaseAccount(bool useMultipleWriteLocations)
        {
            CosmosAccountSettings databaseAccount = new CosmosAccountSettings()
            {
                EnableMultipleWriteLocations = useMultipleWriteLocations,
                ReadLocationsInternal = new Collection<CosmosAccountLocation>()
                {
                    { new CosmosAccountLocation() { Name = "location1", DatabaseAccountEndpoint = LocationCacheTests.Location1Endpoint.ToString() } },
                    { new CosmosAccountLocation() { Name = "location2", DatabaseAccountEndpoint = LocationCacheTests.Location2Endpoint.ToString() } },
                    { new CosmosAccountLocation() { Name = "location4", DatabaseAccountEndpoint = LocationCacheTests.Location4Endpoint.ToString() } },
                },
                WriteLocationsInternal = new Collection<CosmosAccountLocation>()
                {
                    { new CosmosAccountLocation() { Name = "location1", DatabaseAccountEndpoint = LocationCacheTests.Location1Endpoint.ToString() } },
                    { new CosmosAccountLocation() { Name = "location2", DatabaseAccountEndpoint = LocationCacheTests.Location2Endpoint.ToString() } },
                    { new CosmosAccountLocation() { Name = "location3", DatabaseAccountEndpoint = LocationCacheTests.Location3Endpoint.ToString() } },
                }
            };

            return databaseAccount;
        }

        private void Initialize(
            bool useMultipleWriteLocations,
            bool enableEndpointDiscovery,
            bool isPreferredLocationsListEmpty)
        {
            this.databaseAccount = LocationCacheTests.CreateDatabaseAccount(useMultipleWriteLocations);

            this.preferredLocations = isPreferredLocationsListEmpty ? new List<string>().AsReadOnly() : new List<string>()
            {
                "location1",
                "location2",
                "location3"
            }.AsReadOnly();

            this.cache = new LocationCache(
                this.preferredLocations,
                LocationCacheTests.DefaultEndpoint,
                enableEndpointDiscovery,
                10,
                useMultipleWriteLocations);

            this.cache.OnDatabaseAccountRead(this.databaseAccount);

            this.mockedClient = new Mock<IDocumentClientInternal>();
            mockedClient.Setup(owner => owner.ServiceEndpoint).Returns(LocationCacheTests.DefaultEndpoint);
            mockedClient.Setup(owner => owner.GetDatabaseAccountInternalAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>())).ReturnsAsync(this.databaseAccount);

            ConnectionPolicy connectionPolicy = new ConnectionPolicy()
            {
                EnableEndpointDiscovery = enableEndpointDiscovery,
                UseMultipleWriteLocations = useMultipleWriteLocations,
            };

            foreach (string preferredLocation in this.preferredLocations)
            {
                connectionPolicy.PreferredLocations.Add(preferredLocation);
            }

            this.endpointManager = new GlobalEndpointManager(mockedClient.Object, connectionPolicy);
        }

        private async Task ValidateLocationCacheAsync(
            bool useMultipleWriteLocations,
            bool endpointDiscoveryEnabled,
            bool isPreferredListEmpty)
        {
            for (int writeLocationIndex = 0; writeLocationIndex < 3; writeLocationIndex++)
            {
                for (int readLocationIndex = 0; readLocationIndex < 2; readLocationIndex++)
                {
                    this.Initialize(
                        useMultipleWriteLocations,
                        endpointDiscoveryEnabled,
                        isPreferredListEmpty);

                    ReadOnlyCollection<Uri> currentWriteEndpoints = this.cache.WriteEndpoints;
                    ReadOnlyCollection<Uri> currentReadEndpoints = this.cache.ReadEndpoints;

                    for (int i = 0; i < readLocationIndex; i++)
                    {
                        this.cache.MarkEndpointUnavailableForRead(new Uri(this.databaseAccount.ReadLocationsInternal[i].DatabaseAccountEndpoint));
                        this.endpointManager.MarkEndpointUnavailableForRead(new Uri(this.databaseAccount.ReadLocationsInternal[i].DatabaseAccountEndpoint));
                    }

                    for (int i = 0; i < writeLocationIndex; i++)
                    {
                        this.cache.MarkEndpointUnavailableForWrite(new Uri(this.databaseAccount.WriteLocationsInternal[i].DatabaseAccountEndpoint));
                        this.endpointManager.MarkEndpointUnavailableForWrite(new Uri(this.databaseAccount.WriteLocationsInternal[i].DatabaseAccountEndpoint));
                    }

                    Dictionary<string, Uri> writeEndpointByLocation = this.databaseAccount.WriteLocationsInternal.ToDictionary(
                        location => location.Name,
                        location => new Uri(location.DatabaseAccountEndpoint));

                    Dictionary<string, Uri> readEndpointByLocation = this.databaseAccount.ReadableLocations.ToDictionary(
                        location => location.Name,
                        location => new Uri(location.DatabaseAccountEndpoint));

                    Uri[] preferredAvailableWriteEndpoints = this.preferredLocations.Skip(writeLocationIndex)
                        .Where(location => writeEndpointByLocation.ContainsKey(location))
                        .Select(location => writeEndpointByLocation[location]).ToArray();

                    Uri[] preferredAvailableReadEndpoints = this.preferredLocations.Skip(readLocationIndex)
                        .Where(location => readEndpointByLocation.ContainsKey(location))
                        .Select(location => readEndpointByLocation[location]).ToArray();

                    this.ValidateEndpointRefresh(
                        useMultipleWriteLocations,
                        endpointDiscoveryEnabled,
                        preferredAvailableWriteEndpoints,
                        preferredAvailableReadEndpoints,
                        writeLocationIndex > 0,
                        readLocationIndex > 0 &&
                        currentReadEndpoints[0] != LocationCacheTests.DefaultEndpoint,
                        currentWriteEndpoints.Count > 1,
                        currentReadEndpoints.Count > 1);

                    await this.ValidateGlobalEndpointLocationCacheRefreshAsync();

                    this.ValidateRequestEndpointResolution(
                        useMultipleWriteLocations,
                        endpointDiscoveryEnabled,
                        preferredAvailableWriteEndpoints,
                        preferredAvailableReadEndpoints);

                    // wait for TTL on unavailablity info
                    await Task.Delay(
                        int.Parse(
                            System.Configuration.ConfigurationManager.AppSettings["UnavailableLocationsExpirationTimeInSeconds"],
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture) * 1000);

                    Assert.IsTrue(Enumerable.SequenceEqual(currentWriteEndpoints, this.cache.WriteEndpoints));
                    Assert.IsTrue(Enumerable.SequenceEqual(currentReadEndpoints, this.cache.ReadEndpoints));
                }
            }
        }

        private void ValidateEndpointRefresh(
            bool useMultipleWriteLocations,
            bool endpointDiscoveryEnabled,
            Uri[] preferredAvailableWriteEndpoints,
            Uri[] preferredAvailableReadEndpoints,
            bool isFirstWriteEndpointUnavailable,
            bool isFirstReadEndpointUnavailable,
            bool hasMoreThanOneWriteEndpoints,
            bool hasMoreThanOneReadEndpoints)
        {
            bool canRefreshInBackground = false;
            bool shouldRefreshEndpoints = this.cache.ShouldRefreshEndpoints(out canRefreshInBackground);

            bool isMostPreferredLocationUnavailableForRead = isFirstReadEndpointUnavailable;
            bool isMostPreferredLocationUnavailableForWrite = useMultipleWriteLocations ? false : isFirstWriteEndpointUnavailable;
            if (this.preferredLocations.Count > 0)
            {
                string mostPreferredReadLocationName = this.preferredLocations.First(location => databaseAccount.ReadableLocations.Any(readLocation => readLocation.Name == location));
                Uri mostPreferredReadEndpoint = LocationCacheTests.EndpointByLocation[mostPreferredReadLocationName];
                isMostPreferredLocationUnavailableForRead = preferredAvailableReadEndpoints.Length == 0 ? true : (preferredAvailableReadEndpoints[0] != mostPreferredReadEndpoint);

                string mostPreferredWriteLocationName = this.preferredLocations.First(location => databaseAccount.WritableLocations.Any(writeLocation => writeLocation.Name == location));
                Uri mostPreferredWriteEndpoint = LocationCacheTests.EndpointByLocation[mostPreferredWriteLocationName];

                if (useMultipleWriteLocations)
                {
                    isMostPreferredLocationUnavailableForWrite = preferredAvailableWriteEndpoints.Length == 0 ? true : (preferredAvailableWriteEndpoints[0] != mostPreferredWriteEndpoint);
                }
            }

            if (!endpointDiscoveryEnabled)
            {
                Assert.AreEqual(false, shouldRefreshEndpoints);
            }
            else
            {
                Assert.AreEqual(isMostPreferredLocationUnavailableForRead || isMostPreferredLocationUnavailableForWrite, shouldRefreshEndpoints);
            }

            if (shouldRefreshEndpoints)
            {
                if (isMostPreferredLocationUnavailableForRead)
                {
                    Assert.AreEqual(hasMoreThanOneReadEndpoints, canRefreshInBackground);
                }
                else if (isMostPreferredLocationUnavailableForWrite)
                {
                    Assert.AreEqual(hasMoreThanOneWriteEndpoints, canRefreshInBackground);
                }
            }
        }

        private async Task ValidateGlobalEndpointLocationCacheRefreshAsync()
        {
            IEnumerable<Task> refreshLocations = Enumerable.Range(0, 10).Select(index => Task.Factory.StartNew(() => this.endpointManager.RefreshLocationAsync(null)));

            await Task.WhenAll(refreshLocations);

            this.mockedClient.Verify(client => client.GetDatabaseAccountInternalAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()), Times.AtMostOnce);

            this.mockedClient.ResetCalls();

            foreach (Task task in Enumerable.Range(0, 10).Select(index => Task.Factory.StartNew(() => this.endpointManager.RefreshLocationAsync(null))))
            {
                await task;
            }

            this.mockedClient.Verify(client => client.GetDatabaseAccountInternalAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()), Times.AtMostOnce);
        }

        private void ValidateRequestEndpointResolution(
            bool useMultipleWriteLocations,
            bool endpointDiscoveryEnabled,
            Uri[] availableWriteEndpoints,
            Uri[] availableReadEndpoints)
        {
            Uri firstAvailableWriteEndpoint;
            Uri secondAvailableWriteEndpoint;

            if (!endpointDiscoveryEnabled)
            {
                firstAvailableWriteEndpoint = LocationCacheTests.DefaultEndpoint;
                secondAvailableWriteEndpoint = LocationCacheTests.DefaultEndpoint;
            }
            else if (!useMultipleWriteLocations)
            {
                firstAvailableWriteEndpoint = new Uri(this.databaseAccount.WriteLocationsInternal[0].DatabaseAccountEndpoint);
                secondAvailableWriteEndpoint = new Uri(this.databaseAccount.WriteLocationsInternal[1].DatabaseAccountEndpoint);
            }
            else if (availableWriteEndpoints.Length > 1)
            {
                firstAvailableWriteEndpoint = availableWriteEndpoints[0];
                secondAvailableWriteEndpoint = availableWriteEndpoints[1];
            }
            else if (availableWriteEndpoints.Length > 0)
            {
                firstAvailableWriteEndpoint = availableWriteEndpoints[0];
                secondAvailableWriteEndpoint =
                    this.databaseAccount.WriteLocationsInternal[0].DatabaseAccountEndpoint != firstAvailableWriteEndpoint.ToString() ?
                    new Uri(this.databaseAccount.WriteLocationsInternal[0].DatabaseAccountEndpoint) :
                    new Uri(this.databaseAccount.WriteLocationsInternal[1].DatabaseAccountEndpoint);
            }
            else
            {
                firstAvailableWriteEndpoint = LocationCacheTests.DefaultEndpoint;
                secondAvailableWriteEndpoint = LocationCacheTests.DefaultEndpoint;
            }

            Uri firstAvailableReadEndpoint;

            if (!endpointDiscoveryEnabled)
            {
                firstAvailableReadEndpoint = LocationCacheTests.DefaultEndpoint;
            }
            else if (this.preferredLocations.Count == 0)
            {
                firstAvailableReadEndpoint = firstAvailableWriteEndpoint;
            }
            else if (availableReadEndpoints.Length > 0)
            {
                firstAvailableReadEndpoint = availableReadEndpoints[0];
            }
            else
            {
                firstAvailableReadEndpoint = LocationCacheTests.EndpointByLocation[this.preferredLocations[0]];
            }

            Uri firstWriteEnpoint = !endpointDiscoveryEnabled ?
                LocationCacheTests.DefaultEndpoint :
                new Uri(this.databaseAccount.WriteLocationsInternal[0].DatabaseAccountEndpoint);

            Uri secondWriteEnpoint = !endpointDiscoveryEnabled ?
                LocationCacheTests.DefaultEndpoint :
                new Uri(this.databaseAccount.WriteLocationsInternal[1].DatabaseAccountEndpoint);

            // If current write endpoint is unavailable, write endpoints order doesn't change
            // All write requests flip-flop between current write and alternate write endpoint
            ReadOnlyCollection<Uri> writeEndpoints = this.cache.WriteEndpoints;
            Assert.AreEqual(firstAvailableWriteEndpoint, writeEndpoints[0]);
            Assert.AreEqual(secondAvailableWriteEndpoint, this.ResolveEndpointForWriteRequest(ResourceType.Document, true));
            Assert.AreEqual(firstAvailableWriteEndpoint, this.ResolveEndpointForWriteRequest(ResourceType.Document, false));

            // Writes to other resource types should be directed to first/second write endpoint
            Assert.AreEqual(firstWriteEnpoint, this.ResolveEndpointForWriteRequest(ResourceType.Database, false));
            Assert.AreEqual(secondWriteEnpoint, this.ResolveEndpointForWriteRequest(ResourceType.Database, true));

            // Reads should be directed to available read endpoints regardless of resource type
            Assert.AreEqual(firstAvailableReadEndpoint, this.ResolveEndpointForReadRequest(true));
            Assert.AreEqual(firstAvailableReadEndpoint, this.ResolveEndpointForReadRequest(false));
        }

        private Uri ResolveEndpointForReadRequest(bool masterResourceType)
        {
            using (DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.Read, masterResourceType ? ResourceType.Database : ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey))
            {
                return this.cache.ResolveServiceEndpoint(request);
            }
        }

        private Uri ResolveEndpointForWriteRequest(ResourceType resourceType, bool useAlternateWriteEndpoint)
        {
            using (DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.Create, resourceType, AuthorizationTokenType.PrimaryMasterKey))
            {
                request.RequestContext.RouteToLocation(useAlternateWriteEndpoint ? 1 : 0, resourceType.IsCollectionChild());
                return this.cache.ResolveServiceEndpoint(request);
            }
        }

        private DocumentServiceRequest CreateRequest(bool isReadRequest, bool isMasterResourceType)
        {
            if (isReadRequest)
            {
                return DocumentServiceRequest.Create(OperationType.Read, isMasterResourceType ? ResourceType.Database : ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);
            }
            else
            {
                return DocumentServiceRequest.Create(OperationType.Create, isMasterResourceType ? ResourceType.Database : ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);
            }
        }
    }
}
