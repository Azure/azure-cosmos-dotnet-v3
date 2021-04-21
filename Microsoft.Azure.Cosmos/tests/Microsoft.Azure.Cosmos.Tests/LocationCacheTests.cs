//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Client.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Linq;
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
        private AccountProperties databaseAccount;
        private LocationCache cache;
        private GlobalEndpointManager endpointManager;
        private GlobalPartitionEndpointManager partitionKeyRangeLocationCache;
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

            foreach (AccountRegion databaseAccountLocation in this.databaseAccount.WriteLocationsInternal)
            {
                Assert.AreEqual(databaseAccountLocation.Name, this.cache.GetLocation(new Uri(databaseAccountLocation.Endpoint)));
            }

            foreach (AccountRegion databaseAccountLocation in this.databaseAccount.ReadLocationsInternal)
            {
                Assert.AreEqual(databaseAccountLocation.Name, this.cache.GetLocation(new Uri(databaseAccountLocation.Endpoint)));
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
            ClientRetryPolicy retryPolicy = this.CreateClientRetryPolicy(enableEndpointDiscovery);

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

                            StoreRequestNameValueCollection headers = new StoreRequestNameValueCollection();
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

        private ClientRetryPolicy CreateClientRetryPolicy(bool enableEndpointDiscovery)
        {
            return new ClientRetryPolicy(this.endpointManager, this.partitionKeyRangeLocationCache, enableEndpointDiscovery, new RetryOptions());
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
            ClientRetryPolicy retryPolicy = this.CreateClientRetryPolicy(enableEndpointDiscovery);

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
                                    new Uri(this.databaseAccount.WriteLocationsInternal[0].Endpoint) : // All requests go to write endpoint
                                    LocationCacheTests.EndpointByLocation[this.preferredLocations[0]];

                                Assert.AreEqual(expectedEndpoint, request.RequestContext.LocationEndpointToRoute);
                            }
                            else if (retryCount == 1)
                            {
                                // Second request must go to write endpoint
                                Uri expectedEndpoint = new Uri(this.databaseAccount.WriteLocationsInternal[0].Endpoint);
                                Assert.AreEqual(expectedEndpoint, request.RequestContext.LocationEndpointToRoute);
                            }
                            else
                            {
                                Assert.Fail();
                            }

                            retryCount++;

                            StoreRequestNameValueCollection headers = new StoreRequestNameValueCollection();
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

            ReadOnlyCollection<string> preferredList = new List<string>() {
                "location2",
                "location1"
            }.AsReadOnly();

            this.Initialize(
                useMultipleWriteLocations: useMultipleWriteLocations,
                enableEndpointDiscovery: enableEndpointDiscovery,
                isPreferredLocationsListEmpty: false,
                preferedRegionListOverride: preferredList);

            await this.endpointManager.RefreshLocationAsync(this.databaseAccount);
            ClientRetryPolicy retryPolicy = this.CreateClientRetryPolicy(enableEndpointDiscovery);

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
                                Uri expectedEndpoint = LocationCacheTests.EndpointByLocation[preferredList[0]];

                                Assert.AreEqual(expectedEndpoint, request.RequestContext.LocationEndpointToRoute);
                            }
                            else if (retryCount == 1)
                            {
                                // Second request must go to the next preferred location
                                Uri expectedEndpoint = LocationCacheTests.EndpointByLocation[preferredList[1]];

                                Assert.AreEqual(expectedEndpoint, request.RequestContext.LocationEndpointToRoute);
                            }
                            else if (retryCount == 2)
                            {
                                // Third request must go to first preferred location
                                Uri expectedEndpoint = LocationCacheTests.EndpointByLocation[preferredList[0]];
                                Assert.AreEqual(expectedEndpoint, request.RequestContext.LocationEndpointToRoute);
                            }
                            else
                            {
                                Assert.Fail();
                            }

                            retryCount++;

                            StoreRequestNameValueCollection headers = new StoreRequestNameValueCollection();
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

            ReadOnlyCollection<string> preferredList = new List<string>() {
                "location3",
                "location2",
                "location1"
            }.AsReadOnly();

            this.Initialize(
                useMultipleWriteLocations: useMultipleWriteLocations,
                enableEndpointDiscovery: enableEndpointDiscovery,
                isPreferredLocationsListEmpty: false,
                preferedRegionListOverride: preferredList);

            await this.endpointManager.RefreshLocationAsync(this.databaseAccount);
            ClientRetryPolicy retryPolicy = this.CreateClientRetryPolicy(enableEndpointDiscovery);

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
                                Uri expectedEndpoint = LocationCacheTests.EndpointByLocation[preferredList[0]];
                                Assert.AreEqual(expectedEndpoint, request.RequestContext.LocationEndpointToRoute);
                            }
                            else if (retryCount == 1)
                            {
                                // Second request must go to the next preferred location
                                Uri expectedEndpoint = LocationCacheTests.EndpointByLocation[preferredList[1]];
                                Assert.AreEqual(expectedEndpoint, request.RequestContext.LocationEndpointToRoute);
                            }
                            else if (retryCount == 2)
                            {
                                // Third request must go to the next preferred location
                                Uri expectedEndpoint = LocationCacheTests.EndpointByLocation[preferredList[2]];
                                Assert.AreEqual(expectedEndpoint, request.RequestContext.LocationEndpointToRoute);
                            }
                            else if (retryCount == 3)
                            {
                                // Fourth request must go to first preferred location
                                Uri expectedEndpoint = LocationCacheTests.EndpointByLocation[preferredList[0]];
                                Assert.AreEqual(expectedEndpoint, request.RequestContext.LocationEndpointToRoute);
                            }
                            else
                            {
                                Assert.Fail();
                            }

                            retryCount++;

                            StoreRequestNameValueCollection headers = new StoreRequestNameValueCollection();
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
            ClientRetryPolicy retryPolicy = this.CreateClientRetryPolicy(true);

            using (DocumentServiceRequest request = this.CreateRequest(isReadRequest: false, isMasterResourceType: false))
            {
                request.RequestContext.ResolvedPartitionKeyRange = new PartitionKeyRange()
                {
                    Id = "0",
                    MinInclusive = "",
                    MaxExclusive = "FF"
                };

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

                            StoreRequestNameValueCollection headers = new StoreRequestNameValueCollection();
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
            ClientRetryPolicy retryPolicy = this.CreateClientRetryPolicy(true);

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

                                StoreRequestNameValueCollection headers = new StoreRequestNameValueCollection();
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
            bool[] boolValues = new bool[] {true, false};

            foreach (bool useMultipleWriteEndpoints in boolValues)
            {
                foreach (bool endpointDiscoveryEnabled in boolValues)
                {
                    foreach (bool isPreferredListEmpty in boolValues)
                    {
                        await this.ValidateLocationCacheAsync(
                            useMultipleWriteEndpoints,
                            endpointDiscoveryEnabled,
                            isPreferredListEmpty);
                    }
                }
            }
        }

        [TestMethod]
        public async Task ValidateRetryOnHttpExceptionAsync()
        {
            await this.ValidateRetryOnHttpExceptionAsync(enableMultipleWriteLocations: false, isReadRequest: false);
            await this.ValidateRetryOnHttpExceptionAsync(enableMultipleWriteLocations: false, isReadRequest: true);
            await this.ValidateRetryOnHttpExceptionAsync(enableMultipleWriteLocations: true, isReadRequest: false);
            await this.ValidateRetryOnHttpExceptionAsync(enableMultipleWriteLocations: true, isReadRequest: true);
        }

        private async Task ValidateRetryOnHttpExceptionAsync(bool enableMultipleWriteLocations, bool isReadRequest)
        {
            ReadOnlyCollection<string> preferredList = new List<string>() {
                "location2",
                "location1"
            }.AsReadOnly();

            this.Initialize(
                useMultipleWriteLocations: enableMultipleWriteLocations,
                enableEndpointDiscovery: true,
                isPreferredLocationsListEmpty: false,
                preferedRegionListOverride: preferredList,
                enforceSingleMasterSingleWriteLocation: true);

            await this.endpointManager.RefreshLocationAsync(this.databaseAccount);
            ClientRetryPolicy retryPolicy = this.CreateClientRetryPolicy(true);

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
                                Uri expectedEndpoint = null;
                                if (enableMultipleWriteLocations
                                    || isReadRequest)
                                {
                                    // MultiMaster or Single Master Read can use preferred locations for first request
                                    expectedEndpoint = LocationCacheTests.EndpointByLocation[preferredList[0]];
                                }
                                else
                                {
                                    // Single Master Write always goes to the only write region
                                    expectedEndpoint = new Uri(this.databaseAccount.WriteLocationsInternal[0].Endpoint);
                                }

                                Assert.AreEqual(expectedEndpoint, request.RequestContext.LocationEndpointToRoute);

                                HttpRequestException httpException = new HttpRequestException();
                                throw httpException;
                            }
                            else if (retryCount == 2)
                            {
                                Uri expectedEndpoint = null;
                                if (enableMultipleWriteLocations
                                    || isReadRequest)
                                {
                                    // Next request must go to next preferred endpoint
                                    expectedEndpoint = LocationCacheTests.EndpointByLocation[preferredList[1]];
                                }
                                else
                                {
                                    // Single Master Write does not have anywhere else to go
                                    expectedEndpoint = new Uri(this.databaseAccount.WriteLocationsInternal[0].Endpoint);
                                }

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
                    Assert.Fail();
                }
            }
        }

        [DataTestMethod]
        [DataRow(true, false, false, false, DisplayName = "Read request - Single master - no preferred locations - should NOT retry")]
        [DataRow(false, false, false, false, DisplayName = "Write request - Single master - no preferred locations - should NOT retry")]
        [DataRow(true, true, false, false, DisplayName = "Read request - Multi master - no preferred locations - should NOT retry")]
        [DataRow(false, true, false, false, DisplayName = "Write request - Multi master - no preferred locations - should NOT retry")]
        [DataRow(true, false, true, true, DisplayName = "Read request - Single master - with preferred locations - should retry")]
        [DataRow(false, false, true, false, DisplayName = "Write request - Single master - with preferred locations - should NOT retry")]
        [DataRow(true, true, true, true, DisplayName = "Read request - Multi master - with preferred locations - should retry")]
        [DataRow(false, true, true, true, DisplayName = "Write request - Multi master - with preferred locations - should retry")]
        public async Task ClientRetryPolicy_ValidateRetryOnServiceUnavailable(
            bool isReadRequest,
            bool useMultipleWriteLocations,
            bool usesPreferredLocations,
            bool shouldHaveRetried)
        {
            const bool enableEndpointDiscovery = true;

            ReadOnlyCollection<string> preferredList = new List<string>() {
                "location2",
                "location1"
            }.AsReadOnly();

            this.Initialize(
                useMultipleWriteLocations: useMultipleWriteLocations,
                enableEndpointDiscovery: enableEndpointDiscovery,
                isPreferredLocationsListEmpty: !usesPreferredLocations,
                preferedRegionListOverride: preferredList,
                enforceSingleMasterSingleWriteLocation: true);

            await this.endpointManager.RefreshLocationAsync(this.databaseAccount);
            ClientRetryPolicy retryPolicy = this.CreateClientRetryPolicy(enableEndpointDiscovery);

            using (DocumentServiceRequest request = this.CreateRequest(isReadRequest: isReadRequest, isMasterResourceType: false))
            {
                int retryCount = 0;

                try
                {
                    await BackoffRetryUtility<bool>.ExecuteAsync(
                        () =>
                        {
                            retryPolicy.OnBeforeSendRequest(request);

                            if (retryCount == 1)
                            {
                                if (!usesPreferredLocations)
                                {
                                    Assert.Fail("Should not be retrying if preferredlocations is not being used");
                                }

                                Uri expectedEndpoint = LocationCacheTests.EndpointByLocation[preferredList[1]];

                                Assert.AreEqual(expectedEndpoint, request.RequestContext.LocationEndpointToRoute);
                            }
                            else if (retryCount > 1)
                            {
                                Assert.Fail("Should retry once");
                            }

                            retryCount++;

                            throw new ServiceUnavailableException();
                        },
                        retryPolicy);

                    Assert.Fail();
                }
                catch (ServiceUnavailableException)
                {
                    DefaultTrace.TraceInformation("Received expected ServiceUnavailableException");
                    if (shouldHaveRetried)
                    {
                        Assert.AreEqual(2, retryCount, $"Retry count {retryCount}, shouldHaveRetried {shouldHaveRetried} isReadRequest {isReadRequest} useMultipleWriteLocations {useMultipleWriteLocations} usesPreferredLocations {usesPreferredLocations}");
                    }
                    else
                    {
                        Assert.AreEqual(1, retryCount, $"Retry count {retryCount}, shouldHaveRetried {shouldHaveRetried} isReadRequest {isReadRequest} useMultipleWriteLocations {useMultipleWriteLocations} usesPreferredLocations {usesPreferredLocations}");
                    }
                }
            }
        }

        private static AccountProperties CreateDatabaseAccount(
            bool useMultipleWriteLocations,
            bool enforceSingleMasterSingleWriteLocation)
        {
            Collection<AccountRegion> writeLocations = new Collection<AccountRegion>()
                {
                    { new AccountRegion() { Name = "location1", Endpoint = LocationCacheTests.Location1Endpoint.ToString() } },
                    { new AccountRegion() { Name = "location2", Endpoint = LocationCacheTests.Location2Endpoint.ToString() } },
                    { new AccountRegion() { Name = "location3", Endpoint = LocationCacheTests.Location3Endpoint.ToString() } },
                };

            if (!useMultipleWriteLocations
                && enforceSingleMasterSingleWriteLocation)
            {
                // Some pre-existing tests depend on the account having multiple write locations even on single master setup
                // Newer tests can correctly define a single master account (single write region) without breaking existing tests
                writeLocations = new Collection<AccountRegion>()
                {
                    { new AccountRegion() { Name = "location1", Endpoint = LocationCacheTests.Location1Endpoint.ToString() } }
                };
            }

            AccountProperties databaseAccount = new AccountProperties()
            {
                EnableMultipleWriteLocations = useMultipleWriteLocations,
                ReadLocationsInternal = new Collection<AccountRegion>()
                {
                    { new AccountRegion() { Name = "location1", Endpoint = LocationCacheTests.Location1Endpoint.ToString() } },
                    { new AccountRegion() { Name = "location2", Endpoint = LocationCacheTests.Location2Endpoint.ToString() } },
                    { new AccountRegion() { Name = "location4", Endpoint = LocationCacheTests.Location4Endpoint.ToString() } },
                },
                WriteLocationsInternal = writeLocations
            };

            return databaseAccount;
        }

        private void Initialize(
            bool useMultipleWriteLocations,
            bool enableEndpointDiscovery,
            bool isPreferredLocationsListEmpty,
            bool enforceSingleMasterSingleWriteLocation = false, // Some tests depend on the Initialize to create an account with multiple write locations, even when not multi master
            ReadOnlyCollection<string> preferedRegionListOverride = null)
        {
            this.databaseAccount = LocationCacheTests.CreateDatabaseAccount(
                useMultipleWriteLocations,
                enforceSingleMasterSingleWriteLocation);

            if (isPreferredLocationsListEmpty)
            {
                this.preferredLocations = new List<string>().AsReadOnly();
            }
            else
            {
                // Allow for override at the test method level if needed
                this.preferredLocations = preferedRegionListOverride != null ? preferedRegionListOverride : new List<string>()
                {
                    "location1",
                    "location2",
                    "location3"
                }.AsReadOnly();
            }

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
            this.partitionKeyRangeLocationCache = GlobalPartitionEndpointManagerNoOp.Instance;
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
                        this.cache.MarkEndpointUnavailableForRead(new Uri(this.databaseAccount.ReadLocationsInternal[i].Endpoint));
                        this.endpointManager.MarkEndpointUnavailableForRead(new Uri(this.databaseAccount.ReadLocationsInternal[i].Endpoint));
                    }

                    for (int i = 0; i < writeLocationIndex; i++)
                    {
                        this.cache.MarkEndpointUnavailableForWrite(new Uri(this.databaseAccount.WriteLocationsInternal[i].Endpoint));
                        this.endpointManager.MarkEndpointUnavailableForWrite(
                             new Uri(this.databaseAccount.WriteLocationsInternal[i].Endpoint));
                    }

                    Dictionary<string, Uri> writeEndpointByLocation = this.databaseAccount.WriteLocationsInternal.ToDictionary(
                        location => location.Name,
                        location => new Uri(location.Endpoint));

                    Dictionary<string, Uri> readEndpointByLocation = this.databaseAccount.ReadableRegions.ToDictionary(
                        location => location.Name,
                        location => new Uri(location.Endpoint));

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

                    // wait for TTL on unavailability info
                    string expirationTime = System.Configuration.ConfigurationManager.AppSettings["UnavailableLocationsExpirationTimeInSeconds"];
                    int delayInMilliSeconds = int.Parse(
                                                  expirationTime,
                                                  NumberStyles.Integer,
                                                  CultureInfo.InvariantCulture) * 1000 * 2;
                    await Task.Delay(delayInMilliSeconds);

                    string config =  $"Delay{expirationTime};" + 
                                     $"useMultipleWriteLocations:{useMultipleWriteLocations};" +
                                     $"endpointDiscoveryEnabled:{endpointDiscoveryEnabled};" +
                                     $"isPreferredListEmpty:{isPreferredListEmpty}";

                    CollectionAssert.AreEqual(
                        currentWriteEndpoints, 
                        this.cache.WriteEndpoints, 
                        "Write Endpoints failed;" +
                            $"config:{config};" +
                            $"Current:{string.Join(",", currentWriteEndpoints)};" +
                            $"Cache:{string.Join(",", this.cache.WriteEndpoints)};");

                    CollectionAssert.AreEqual(
                        currentReadEndpoints, 
                        this.cache.ReadEndpoints,
                        "Read Endpoints failed;" +
                            $"config:{config};" +
                            $"Current:{string.Join(",", currentReadEndpoints)};" +
                            $"Cache:{string.Join(",", this.cache.ReadEndpoints)};");
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
                string mostPreferredReadLocationName = this.preferredLocations.First(location => databaseAccount.ReadableRegions.Any(readLocation => readLocation.Name == location));
                Uri mostPreferredReadEndpoint = LocationCacheTests.EndpointByLocation[mostPreferredReadLocationName];
                isMostPreferredLocationUnavailableForRead = preferredAvailableReadEndpoints.Length == 0 ? true : (preferredAvailableReadEndpoints[0] != mostPreferredReadEndpoint);

                string mostPreferredWriteLocationName = this.preferredLocations.First(location => databaseAccount.WritableRegions.Any(writeLocation => writeLocation.Name == location));
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
                firstAvailableWriteEndpoint = new Uri(this.databaseAccount.WriteLocationsInternal[0].Endpoint);
                secondAvailableWriteEndpoint = new Uri(this.databaseAccount.WriteLocationsInternal[1].Endpoint);
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
                    this.databaseAccount.WriteLocationsInternal[0].Endpoint != firstAvailableWriteEndpoint.ToString() ?
                    new Uri(this.databaseAccount.WriteLocationsInternal[0].Endpoint) :
                    new Uri(this.databaseAccount.WriteLocationsInternal[1].Endpoint);
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
                new Uri(this.databaseAccount.WriteLocationsInternal[0].Endpoint);

            Uri secondWriteEnpoint = !endpointDiscoveryEnabled ?
                LocationCacheTests.DefaultEndpoint :
                new Uri(this.databaseAccount.WriteLocationsInternal[1].Endpoint);

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
