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
        private static readonly Uri DefaultEndpoint = new Uri("https://default.documents.azure.com");
        private static readonly Uri DefaultRegionalEndpoint = new Uri("https://location1.documents.azure.com");
        private static readonly Uri Location1Endpoint = new Uri("https://location1.documents.azure.com");
        private static readonly Uri Location2Endpoint = new Uri("https://location2.documents.azure.com");
        private static readonly Uri Location3Endpoint = new Uri("https://location3.documents.azure.com");
        private static readonly Uri Location4Endpoint = new Uri("https://location4.documents.azure.com");
        private static readonly Uri[] WriteEndpoints = new Uri[] { LocationCacheTests.Location1Endpoint, LocationCacheTests.Location2Endpoint, LocationCacheTests.Location3Endpoint };
        private static readonly Uri[] ReadEndpoints = new Uri[] { LocationCacheTests.Location1Endpoint, LocationCacheTests.Location2Endpoint, LocationCacheTests.Location4Endpoint };
        private static readonly Dictionary<string, Uri> EndpointByLocation = new Dictionary<string, Uri>()
        {
            { "location1", LocationCacheTests.Location1Endpoint },
            { "location2", LocationCacheTests.Location2Endpoint },
            { "location3", LocationCacheTests.Location3Endpoint },
            { "location4", LocationCacheTests.Location4Endpoint },
        };

        private ReadOnlyCollection<string> preferredLocations;
        private AccountProperties databaseAccount;
        private LocationCache cache;
        private GlobalPartitionEndpointManager partitionKeyRangeLocationCache;
        private Mock<IDocumentClientInternal> mockedClient;

        [TestMethod]
        [DataRow(true, false, DisplayName = "Validate write endpoint order with preferred locations as empty and multi-write usage disabled and default endpoint is global endpoint.")]
        [DataRow(false, false, DisplayName = "Validate write endpoint order with preferred locations as non-empty and multi-write usage disabled and default endpoint is global endpoint.")]
        [DataRow(true, true, DisplayName = "Validate write endpoint order with preferred locations as empty and multi-write usage disabled and default endpoint is regional endpoint.")]
        [DataRow(false, true, DisplayName = "Validate write endpoint order with preferred locations as non-empty and multi-write usage disabled and default endpoint is regional endpoint.")]
        [Owner("atulk")]
        public void ValidateWriteEndpointOrderWithClientSideDisableMultipleWriteLocation(bool isPreferredLocationListEmpty, bool isDefaultEndpointARegionalEndpoint)
        {
            using GlobalEndpointManager endpointManager = this.Initialize(
                useMultipleWriteLocations: false,
                enableEndpointDiscovery: true,
                isPreferredLocationsListEmpty: isPreferredLocationListEmpty,
                isDefaultEndpointARegionalEndpoint: isDefaultEndpointARegionalEndpoint);

            Assert.AreEqual(this.cache.WriteEndpoints[0], LocationCacheTests.Location1Endpoint);
            Assert.AreEqual(this.cache.WriteEndpoints[1], LocationCacheTests.Location2Endpoint);
            Assert.AreEqual(this.cache.WriteEndpoints[2], LocationCacheTests.Location3Endpoint);
        }

        [TestMethod]
        [DataRow(true, DisplayName = "Validate get location with preferred locations as non-empty.")]
        [DataRow(false, DisplayName = "Validate get location with preferred locations as empty.")]
        [Owner("atulk")]
        public void ValidateGetLocation(bool isPreferredLocationListEmpty)
        {
            using GlobalEndpointManager endpointManager = this.Initialize(
                useMultipleWriteLocations: false,
                enableEndpointDiscovery: true,
                isPreferredLocationsListEmpty: isPreferredLocationListEmpty);

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
        [Owner("sourabhjain")]
        public void ValidateTryGetLocationForGatewayDiagnostics()
        {
            using GlobalEndpointManager endpointManager = this.Initialize(
                useMultipleWriteLocations: false,
                enableEndpointDiscovery: true,
                isPreferredLocationsListEmpty: true);

            Assert.AreEqual(false, this.cache.TryGetLocationForGatewayDiagnostics(LocationCacheTests.DefaultEndpoint, out string regionName));
            Assert.IsNull(regionName);

            // Default Endpoint with path
            Assert.AreEqual(false, this.cache.TryGetLocationForGatewayDiagnostics(new Uri(LocationCacheTests.DefaultEndpoint, "random/path"), out regionName));
            Assert.IsNull(regionName);

            foreach (AccountRegion databaseAccountLocation in this.databaseAccount.WriteLocationsInternal)
            {
                Assert.AreEqual(true, this.cache.TryGetLocationForGatewayDiagnostics(new Uri(databaseAccountLocation.Endpoint), out regionName));
                Assert.AreEqual(databaseAccountLocation.Name, regionName);
            }

            foreach (AccountRegion databaseAccountLocation in this.databaseAccount.ReadLocationsInternal)
            {
                Assert.AreEqual(true, this.cache.TryGetLocationForGatewayDiagnostics(new Uri(databaseAccountLocation.Endpoint), out regionName));
                Assert.AreEqual(databaseAccountLocation.Name, regionName);
            }
        }

        [TestMethod]
        [Owner("atulk")]
        public async Task ValidateRetryOnSessionNotAvailableWithDisableMultipleWriteLocationsAndEndpointDiscoveryDisabled()
        {
            await this.ValidateRetryOnSessionNotAvailableWithEndpointDiscoveryDisabled(false, false, false);
            await this.ValidateRetryOnSessionNotAvailableWithEndpointDiscoveryDisabled(false, false, true);
            await this.ValidateRetryOnSessionNotAvailableWithEndpointDiscoveryDisabled(false, true, false);
            await this.ValidateRetryOnSessionNotAvailableWithEndpointDiscoveryDisabled(false, true, true);
            await this.ValidateRetryOnSessionNotAvailableWithEndpointDiscoveryDisabled(true, false, false);
            await this.ValidateRetryOnSessionNotAvailableWithEndpointDiscoveryDisabled(true, false, true);
            await this.ValidateRetryOnSessionNotAvailableWithEndpointDiscoveryDisabled(true, true, false);
            await this.ValidateRetryOnSessionNotAvailableWithEndpointDiscoveryDisabled(true, true, true);
        }

        private async Task ValidateRetryOnSessionNotAvailableWithEndpointDiscoveryDisabled(bool isPreferredLocationsListEmpty, bool useMultipleWriteLocations, bool isReadRequest)
        {
            const bool enableEndpointDiscovery = false;

            using GlobalEndpointManager endpointManager = this.Initialize(
                useMultipleWriteLocations: useMultipleWriteLocations,
                enableEndpointDiscovery: enableEndpointDiscovery,
                isPreferredLocationsListEmpty: isPreferredLocationsListEmpty);
            ClientRetryPolicy retryPolicy = this.CreateClientRetryPolicy(enableEndpointDiscovery, partitionLevelFailoverEnabled: false, endpointManager);

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
                                Assert.AreEqual(request.RequestContext.LocationEndpointToRoute, endpointManager.ReadEndpoints[0]);
                            }
                            else
                            {
                                Assert.Fail();
                            }

                            retryCount++;

                            StoreResponseNameValueCollection headers = new()
                            {
                                [WFConstants.BackendHeaders.SubStatus] = ((int)SubStatusCodes.ReadSessionNotAvailable).ToString()
                            };
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

        private ClientRetryPolicy CreateClientRetryPolicy(
            bool enableEndpointDiscovery,
            bool partitionLevelFailoverEnabled,
            GlobalEndpointManager endpointManager)
        {
            return new ClientRetryPolicy(
                endpointManager,
                this.partitionKeyRangeLocationCache,
                new RetryOptions(),
                enableEndpointDiscovery,
                isPertitionLevelFailoverEnabled: partitionLevelFailoverEnabled);
        }

        [TestMethod]
        [Owner("atulk")]
        public async Task ValidateRetryOnSessionNotAvailableWithDisableMultipleWriteLocationsAndEndpointDiscoveryEnabled()
        {
            await this.ValidateRetryOnSessionNotAvailableWithDisableMultipleWriteLocationsAndEndpointDiscoveryEnabledAsync(true);
            await this.ValidateRetryOnSessionNotAvailableWithDisableMultipleWriteLocationsAndEndpointDiscoveryEnabledAsync(false);
        }

        private async Task ValidateRetryOnSessionNotAvailableWithDisableMultipleWriteLocationsAndEndpointDiscoveryEnabledAsync(bool isPreferredLocationsListEmpty)
        {
            const bool useMultipleWriteLocations = false;
            bool enableEndpointDiscovery = true;

            using GlobalEndpointManager endpointManager = this.Initialize(
                useMultipleWriteLocations: useMultipleWriteLocations,
                enableEndpointDiscovery: enableEndpointDiscovery,
                isPreferredLocationsListEmpty: isPreferredLocationsListEmpty);

            endpointManager.InitializeAccountPropertiesAndStartBackgroundRefresh(this.databaseAccount);
            ClientRetryPolicy retryPolicy = this.CreateClientRetryPolicy(enableEndpointDiscovery, partitionLevelFailoverEnabled: false, endpointManager);

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

                            StoreResponseNameValueCollection headers = new()
                            {
                                [WFConstants.BackendHeaders.SubStatus] = ((int)SubStatusCodes.ReadSessionNotAvailable).ToString()
                            };
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
        [DataRow(false, false, DisplayName = "Validate (Read/Write)SessionNotAvailable cross-region retry w/o preferredLocations with global default endpoint.")]
        [DataRow(true, false, DisplayName = "Validate (Read/Write)SessionNotAvailable cross-region retry with preferredLocations with global default endpoint.")]
        [DataRow(false, true, DisplayName = "Validate (Read/Write)SessionNotAvailable cross-region retry w/o preferredLocations with regional default endpoint.")]
        [DataRow(true, true, DisplayName = "Validate (Read/Write)SessionNotAvailable cross-region retry with preferredLocations with regional default endpoint.")]
        [Owner("atulk")]
        public async Task ValidateRetryOnReadSessionNotAvailableWithEnableMultipleWriteLocationsAndEndpointDiscoveryEnabled(bool isPreferredLocationsEmpty, bool isDefaultEndpointARegionalEndpoint)
        {
            await this.ValidateRetryOnReadSessionNotAvailableWithEnableMultipleWriteLocationsAsync(isPreferredLocationsEmpty, isDefaultEndpointARegionalEndpoint);
            await this.ValidateRetryOnWriteSessionNotAvailableWithEnableMultipleWriteLocationsAsync(isPreferredLocationsEmpty, isDefaultEndpointARegionalEndpoint);
        }

        private async Task ValidateRetryOnReadSessionNotAvailableWithEnableMultipleWriteLocationsAsync(bool isPreferredLocationsEmpty, bool isDefaultEndpointARegionalEndpoint)
        {
            const bool useMultipleWriteLocations = true;
            bool enableEndpointDiscovery = true;

            ReadOnlyCollection<string> preferredList = isPreferredLocationsEmpty
                ? new List<string>().AsReadOnly()
                : new List<string>() { "location2", "location1" }.AsReadOnly();

            using GlobalEndpointManager endpointManager = this.Initialize(
                useMultipleWriteLocations: useMultipleWriteLocations,
                enableEndpointDiscovery: enableEndpointDiscovery,
                isPreferredLocationsListEmpty: false,
                preferedRegionListOverride: preferredList,
                isDefaultEndpointARegionalEndpoint: isDefaultEndpointARegionalEndpoint);

            endpointManager.InitializeAccountPropertiesAndStartBackgroundRefresh(this.databaseAccount);
            ClientRetryPolicy retryPolicy = this.CreateClientRetryPolicy(enableEndpointDiscovery, partitionLevelFailoverEnabled: false, endpointManager);

            if (!isPreferredLocationsEmpty)
            {
                using (DocumentServiceRequest request =
                       this.CreateRequest(isReadRequest: true, isMasterResourceType: false))
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

                                StoreResponseNameValueCollection headers = new()
                                {
                                    [WFConstants.BackendHeaders.SubStatus] =
                                    ((int)SubStatusCodes.ReadSessionNotAvailable).ToString()
                                };
                                DocumentClientException notFoundException =
                                    new NotFoundException(RMResources.NotFound, headers);


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
            else
            {
                if (!isDefaultEndpointARegionalEndpoint)
                {
                    ReadOnlyCollection<string> effectivePreferredLocations = this.cache.EffectivePreferredLocations;

                    // effective preferred locations are the account-level read locations
                    Assert.AreEqual(4, effectivePreferredLocations.Count);

                    using (DocumentServiceRequest request =
                           this.CreateRequest(isReadRequest: true, isMasterResourceType: false))
                    {
                        int retryCount = 0;

                        try
                        {
                            await BackoffRetryUtility<bool>.ExecuteAsync(() =>
                            {
                                retryPolicy.OnBeforeSendRequest(request);

                                if (retryCount == 0)
                                {
                                    // First request must go to the first effective preferred location
                                    Uri expectedEndpoint =
                                        LocationCacheTests.EndpointByLocation[effectivePreferredLocations[0]];

                                    Assert.AreEqual(expectedEndpoint, request.RequestContext.LocationEndpointToRoute);
                                }
                                else if (retryCount == 1)
                                {
                                    // Second request must go to the second effective preferred location
                                    Uri expectedEndpoint =
                                        LocationCacheTests.EndpointByLocation[effectivePreferredLocations[1]];

                                    Assert.AreEqual(expectedEndpoint, request.RequestContext.LocationEndpointToRoute);
                                }
                                else if (retryCount == 2)
                                {
                                    // Third request must go to third effective preferred location
                                    Uri expectedEndpoint =
                                        LocationCacheTests.EndpointByLocation[effectivePreferredLocations[2]];
                                    Assert.AreEqual(expectedEndpoint, request.RequestContext.LocationEndpointToRoute);
                                }
                                else if (retryCount == 3)
                                {
                                    // Third request must go to fourth effective preferred location
                                    Uri expectedEndpoint =
                                        LocationCacheTests.EndpointByLocation[effectivePreferredLocations[3]];
                                    Assert.AreEqual(expectedEndpoint, request.RequestContext.LocationEndpointToRoute);
                                }
                                else if (retryCount == 4)
                                {
                                    // Fourth request must go to first effective preferred location
                                    Uri expectedEndpoint =
                                        LocationCacheTests.EndpointByLocation[effectivePreferredLocations[0]];
                                    Assert.AreEqual(expectedEndpoint, request.RequestContext.LocationEndpointToRoute);
                                }
                                else
                                {
                                    Assert.Fail();
                                }

                                retryCount++;

                                StoreResponseNameValueCollection headers = new()
                                {
                                    [WFConstants.BackendHeaders.SubStatus] =
                                    ((int)SubStatusCodes.ReadSessionNotAvailable).ToString()
                                };
                                DocumentClientException notFoundException =
                                    new NotFoundException(RMResources.NotFound, headers);

                                throw notFoundException;
                            }, retryPolicy);

                            Assert.Fail();
                        }
                        catch (NotFoundException)
                        {
                            DefaultTrace.TraceInformation("Received expected notFoundException");
                            Assert.AreEqual(5, retryCount);
                        }
                    }
                }
                else
                {
                    ReadOnlyCollection<string> effectivePreferredLocations = this.cache.EffectivePreferredLocations;

                    // effective preferred locations is just the default regional endpoint
                    Assert.AreEqual(1, effectivePreferredLocations.Count);

                    using (DocumentServiceRequest request =
                           this.CreateRequest(isReadRequest: true, isMasterResourceType: false))
                    {
                        int retryCount = 0;

                        try
                        {
                            await BackoffRetryUtility<bool>.ExecuteAsync(() =>
                            {
                                retryPolicy.OnBeforeSendRequest(request);

                                if (retryCount == 0)
                                {
                                    // First request must go to the first effective preferred location
                                    Uri expectedEndpoint =
                                        LocationCacheTests.EndpointByLocation[effectivePreferredLocations[0]];

                                    Assert.AreEqual(expectedEndpoint, request.RequestContext.LocationEndpointToRoute);
                                }
                                else if (retryCount == 1)
                                {
                                    // Second request must go to the second effective preferred location
                                    Uri expectedEndpoint =
                                        LocationCacheTests.EndpointByLocation[effectivePreferredLocations[0]];

                                    Assert.AreEqual(expectedEndpoint, request.RequestContext.LocationEndpointToRoute);
                                }
                                else
                                {
                                    Assert.Fail();
                                }

                                retryCount++;

                                StoreResponseNameValueCollection headers = new()
                                {
                                    [WFConstants.BackendHeaders.SubStatus] =
                                    ((int)SubStatusCodes.ReadSessionNotAvailable).ToString()
                                };
                                DocumentClientException notFoundException =
                                    new NotFoundException(RMResources.NotFound, headers);

                                throw notFoundException;
                            }, retryPolicy);

                            Assert.Fail();
                        }
                        catch (NotFoundException)
                        {
                            DefaultTrace.TraceInformation("Received expected notFoundException");
                            Assert.AreEqual(2, retryCount);
                        }
                    }
                }
            }
        }

        private async Task ValidateRetryOnWriteSessionNotAvailableWithEnableMultipleWriteLocationsAsync(bool isPreferredLocationsEmpty, bool isDefaultEndpointARegionalEndpoint)
        {
            const bool useMultipleWriteLocations = true;
            bool enableEndpointDiscovery = true;

            ReadOnlyCollection<string> preferredList = isPreferredLocationsEmpty
                ? new List<string>().AsReadOnly()
                : new List<string>() { "location3", "location2", "location1" }.AsReadOnly();

            using GlobalEndpointManager endpointManager = this.Initialize(
                useMultipleWriteLocations: useMultipleWriteLocations,
                enableEndpointDiscovery: enableEndpointDiscovery,
                isPreferredLocationsListEmpty: false,
                preferedRegionListOverride: preferredList,
                isDefaultEndpointARegionalEndpoint: isDefaultEndpointARegionalEndpoint);

            endpointManager.InitializeAccountPropertiesAndStartBackgroundRefresh(this.databaseAccount);
            ClientRetryPolicy retryPolicy = this.CreateClientRetryPolicy(enableEndpointDiscovery, partitionLevelFailoverEnabled: false, endpointManager);

            if (!isPreferredLocationsEmpty)
            {
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

                                StoreResponseNameValueCollection headers = new()
                                {
                                    [WFConstants.BackendHeaders.SubStatus] = ((int)SubStatusCodes.ReadSessionNotAvailable).ToString()
                                };
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
            else
            {
                if (!isDefaultEndpointARegionalEndpoint)
                {
                    using (DocumentServiceRequest request =
                           this.CreateRequest(isReadRequest: false, isMasterResourceType: false))
                    {
                        int retryCount = 0;
                        ReadOnlyCollection<string> effectivePreferredLocations = this.cache.EffectivePreferredLocations;

                        // effective preferred locations are the account-level read locations
                        Assert.AreEqual(4, effectivePreferredLocations.Count);

                        // for regions touched for writes - it will be the first 3 effectivePreferredLocations (location1, location2, location3)
                        // which are the write regions for the account
                        try
                        {
                            await BackoffRetryUtility<bool>.ExecuteAsync(() =>
                            {
                                retryPolicy.OnBeforeSendRequest(request);

                                if (retryCount == 0)
                                {
                                    Uri expectedEndpoint =
                                        LocationCacheTests.EndpointByLocation[effectivePreferredLocations[0]];
                                    Assert.AreEqual(expectedEndpoint, request.RequestContext.LocationEndpointToRoute);
                                }
                                else if (retryCount == 1)
                                {
                                    // Second request must go to the next effective preferred location
                                    Uri expectedEndpoint =
                                        LocationCacheTests.EndpointByLocation[effectivePreferredLocations[1]];
                                    Assert.AreEqual(expectedEndpoint, request.RequestContext.LocationEndpointToRoute);
                                }
                                else if (retryCount == 2)
                                {
                                    // Third request must go to the next effective preferred location
                                    Uri expectedEndpoint =
                                        LocationCacheTests.EndpointByLocation[effectivePreferredLocations[2]];
                                    Assert.AreEqual(expectedEndpoint, request.RequestContext.LocationEndpointToRoute);
                                }
                                else if (retryCount == 3)
                                {
                                    // Fourth request must go to first effective preferred location
                                    Uri expectedEndpoint =
                                        LocationCacheTests.EndpointByLocation[effectivePreferredLocations[0]];
                                    Assert.AreEqual(expectedEndpoint, request.RequestContext.LocationEndpointToRoute);
                                }
                                else
                                {
                                    Assert.Fail();
                                }

                                retryCount++;

                                StoreResponseNameValueCollection headers = new()
                                {
                                    [WFConstants.BackendHeaders.SubStatus] =
                                    ((int)SubStatusCodes.ReadSessionNotAvailable).ToString()
                                };
                                DocumentClientException notFoundException =
                                    new NotFoundException(RMResources.NotFound, headers);

                                throw notFoundException;
                            }, retryPolicy);

                            Assert.Fail();
                        }
                        catch (NotFoundException)
                        {
                            DefaultTrace.TraceInformation("Received expected notFoundException");
                            Assert.AreEqual(4, retryCount);
                        }
                    }
                }
                else
                {
                    using (DocumentServiceRequest request =
                           this.CreateRequest(isReadRequest: false, isMasterResourceType: false))
                    {
                        int retryCount = 0;
                        ReadOnlyCollection<string> effectivePreferredLocations = this.cache.EffectivePreferredLocations;

                        // effective preferred locations is just the default regional endpoint
                        Assert.AreEqual(1, effectivePreferredLocations.Count);

                        // for regions touched for writes - it will be the first 3 effectivePreferredLocations (location1, location2, location3)
                        // which are the write regions for the account
                        try
                        {
                            await BackoffRetryUtility<bool>.ExecuteAsync(() =>
                            {
                                retryPolicy.OnBeforeSendRequest(request);

                                if (retryCount == 0)
                                {
                                    Uri expectedEndpoint =
                                        LocationCacheTests.EndpointByLocation[effectivePreferredLocations[0]];
                                    Assert.AreEqual(expectedEndpoint, request.RequestContext.LocationEndpointToRoute);
                                }
                                else if (retryCount == 1)
                                {
                                    // Second request must go to the first effective preferred location
                                    Uri expectedEndpoint =
                                        LocationCacheTests.EndpointByLocation[effectivePreferredLocations[0]];
                                    Assert.AreEqual(expectedEndpoint, request.RequestContext.LocationEndpointToRoute);
                                }
                                else
                                {
                                    Assert.Fail();
                                }

                                retryCount++;

                                StoreResponseNameValueCollection headers = new()
                                {
                                    [WFConstants.BackendHeaders.SubStatus] =
                                    ((int)SubStatusCodes.ReadSessionNotAvailable).ToString()
                                };
                                DocumentClientException notFoundException =
                                    new NotFoundException(RMResources.NotFound, headers);

                                throw notFoundException;
                            }, retryPolicy);

                            Assert.Fail();
                        }
                        catch (NotFoundException)
                        {
                            DefaultTrace.TraceInformation("Received expected notFoundException");
                            Assert.AreEqual(2, retryCount);
                        }
                    }
                }
            }
        }

        [TestMethod]
        [DataRow(false, false, DisplayName = "Validate WriteForbidden retries with preferredLocations with global default endpoint.")]
        [DataRow(true, false, DisplayName = "Validate WriteForbidden retries w/o preferredLocations with global default endpoint.")]
        [DataRow(false, true, DisplayName = "Validate WriteForbidden retries with preferredLocations with regional default endpoint.")]
        [DataRow(true, true, DisplayName = "Validate WriteForbidden retries w/o preferredLocations with regional default endpoint.")]
        [Owner("atulk")]
        public async Task ValidateRetryOnWriteForbiddenExceptionAsync(bool isPreferredLocationsEmpty, bool isDefaultEndpointARegionalEndpoint)
        {
            using GlobalEndpointManager endpointManager = this.Initialize(
                useMultipleWriteLocations: false,
                enableEndpointDiscovery: true,
                isPreferredLocationsListEmpty: isPreferredLocationsEmpty,
                isDefaultEndpointARegionalEndpoint: isDefaultEndpointARegionalEndpoint);

            endpointManager.InitializeAccountPropertiesAndStartBackgroundRefresh(this.databaseAccount);
            ClientRetryPolicy retryPolicy = this.CreateClientRetryPolicy(enableEndpointDiscovery: true, partitionLevelFailoverEnabled: false, endpointManager: endpointManager);

            if (isPreferredLocationsEmpty)
            {
                if (!isDefaultEndpointARegionalEndpoint)
                {
                    Assert.IsNotNull(this.cache.EffectivePreferredLocations);
                    Assert.AreEqual(4, this.cache.EffectivePreferredLocations.Count);
                }
                else
                {
                    Assert.IsNotNull(this.cache.EffectivePreferredLocations);
                    Assert.AreEqual(1, this.cache.EffectivePreferredLocations.Count);
                    Assert.AreEqual("location1", this.cache.EffectivePreferredLocations[0]);
                }
            }

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

                            Uri expectedEndpoint = isPreferredLocationsEmpty ?
                                LocationCacheTests.EndpointByLocation[this.cache.EffectivePreferredLocations[0]] :
                                LocationCacheTests.EndpointByLocation[this.preferredLocations[0]];

                            Assert.AreEqual(expectedEndpoint, request.RequestContext.LocationEndpointToRoute);

                            StoreResponseNameValueCollection headers = new()
                            {
                                [WFConstants.BackendHeaders.SubStatus] = ((int)SubStatusCodes.WriteForbidden).ToString()
                            };
                            DocumentClientException forbiddenException = new ForbiddenException(RMResources.Forbidden, headers);

                            throw forbiddenException;
                        }
                        else if (retryCount == 2)
                        {
                            this.mockedClient.Verify(client => client.GetDatabaseAccountInternalAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()), Times.Once);

                            // Next request must go to next available write endpoint
                            Uri expectedEndpoint;

                            if (isPreferredLocationsEmpty)
                            {
                                if (isDefaultEndpointARegionalEndpoint)
                                {
                                    ReadOnlyCollection<string> availableWriteLocations =
                                        this.cache.GetAvailableAccountLevelWriteLocations();

                                    Assert.IsNotNull(availableWriteLocations);
                                    Assert.AreEqual(3, availableWriteLocations.Count);

                                    Assert.IsNotNull(this.cache.EffectivePreferredLocations);
                                    Assert.AreEqual(this.cache.EffectivePreferredLocations.Count, 1);

                                    expectedEndpoint = LocationCacheTests.EndpointByLocation[availableWriteLocations[1]];
                                }
                                else
                                {
                                    expectedEndpoint = LocationCacheTests.EndpointByLocation[this.cache.EffectivePreferredLocations[1]];
                                }
                            }
                            else
                            {
                                expectedEndpoint = LocationCacheTests.EndpointByLocation[this.preferredLocations[1]];
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
        }

        [TestMethod]
        [DataRow(false, false, DisplayName = "Validate DatabaseAccountNotFound retries with preferredLocations with global default endpoint.")]
        [DataRow(true, false, DisplayName = "Validate DatabaseAccountNotFound retries w/o preferredLocations with global default endpoint.")]
        [DataRow(false, true, DisplayName = "Validate DatabaseAccountNotFound retries with preferredLocations with global default endpoint.")]
        [DataRow(true, true, DisplayName = "Validate DatabaseAccountNotFound retries w/o preferredLocations with global default endpoint.")]
        [Owner("atulk")]
        public async Task ValidateRetryOnDatabaseAccountNotFoundAsync(bool isPreferredLocationsEmpty, bool isDefaultEndpointARegionalEndpoint)
        {
            await this.ValidateRetryOnDatabaseAccountNotFoundAsync(enableMultipleWriteLocations: false, isReadRequest: false, isPreferredLocationsEmpty, isDefaultEndpointARegionalEndpoint);
            await this.ValidateRetryOnDatabaseAccountNotFoundAsync(enableMultipleWriteLocations: false, isReadRequest: true, isPreferredLocationsEmpty, isDefaultEndpointARegionalEndpoint);
            await this.ValidateRetryOnDatabaseAccountNotFoundAsync(enableMultipleWriteLocations: true, isReadRequest: false, isPreferredLocationsEmpty, isDefaultEndpointARegionalEndpoint);
            await this.ValidateRetryOnDatabaseAccountNotFoundAsync(enableMultipleWriteLocations: true, isReadRequest: true, isPreferredLocationsEmpty, isDefaultEndpointARegionalEndpoint);
        }

        private async Task ValidateRetryOnDatabaseAccountNotFoundAsync(bool enableMultipleWriteLocations, bool isReadRequest, bool isPreferredLocationsEmpty, bool isDefaultEndpointARegionalEndpoint)
        {
            using GlobalEndpointManager endpointManager = this.Initialize(
                useMultipleWriteLocations: enableMultipleWriteLocations,
                enableEndpointDiscovery: true,
                isPreferredLocationsListEmpty: isPreferredLocationsEmpty,
                isDefaultEndpointARegionalEndpoint: isDefaultEndpointARegionalEndpoint);

            if (isPreferredLocationsEmpty)
            {
                if (enableMultipleWriteLocations)
                {
                    if (isDefaultEndpointARegionalEndpoint)
                    {
                        Assert.IsNotNull(this.cache.EffectivePreferredLocations);
                        Assert.IsTrue(this.cache.EffectivePreferredLocations.Count == 1);
                        Assert.IsTrue(this.cache.EffectivePreferredLocations[0] == "location1");
                    }
                }
                else
                {
                    if (isDefaultEndpointARegionalEndpoint)
                    {
                        Assert.IsNotNull(this.cache.EffectivePreferredLocations);
                        Assert.IsTrue(this.cache.EffectivePreferredLocations.Count == 1);
                        Assert.IsTrue(this.cache.EffectivePreferredLocations[0] == "location1");
                    }
                }
            }

            endpointManager.InitializeAccountPropertiesAndStartBackgroundRefresh(this.databaseAccount);
            ClientRetryPolicy retryPolicy = this.CreateClientRetryPolicy(enableEndpointDiscovery: true, partitionLevelFailoverEnabled: false, endpointManager: endpointManager);

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

                            // both retries check for flip-flop behavior b/w first two available write regions
                            // in case of multi-write enabled end to end (client + account)
                            if (retryCount == 1)
                            {
                                Uri expectedEndpoint = isPreferredLocationsEmpty ?
                                    LocationCacheTests.EndpointByLocation[this.cache.EffectivePreferredLocations[0]] :
                                    LocationCacheTests.EndpointByLocation[this.preferredLocations[0]];

                                Assert.AreEqual(expectedEndpoint, request.RequestContext.LocationEndpointToRoute);

                                StoreResponseNameValueCollection headers = new()
                                {
                                    [WFConstants.BackendHeaders.SubStatus] = ((int)SubStatusCodes.DatabaseAccountNotFound).ToString()
                                };
                                DocumentClientException forbiddenException = new ForbiddenException(RMResources.NotFound, headers);

                                throw forbiddenException;
                            }
                            else if (retryCount == 2)
                            {
                                // Next request must go to next available write endpoint
                                Uri expectedEndpoint;

                                if (isPreferredLocationsEmpty)
                                {
                                    if (isDefaultEndpointARegionalEndpoint)
                                    {
                                        ReadOnlyCollection<string> availableWriteLocations =
                                            this.cache.GetAvailableAccountLevelWriteLocations();

                                        Assert.IsNotNull(availableWriteLocations);
                                        Assert.AreEqual(3, availableWriteLocations.Count);

                                        Assert.IsNotNull(this.cache.EffectivePreferredLocations);
                                        Assert.AreEqual(this.cache.EffectivePreferredLocations.Count, 1);

                                        expectedEndpoint = LocationCacheTests.EndpointByLocation[availableWriteLocations[1]];
                                    }
                                    else
                                    {
                                        expectedEndpoint = LocationCacheTests.EndpointByLocation[this.cache.EffectivePreferredLocations[1]];
                                    }
                                }
                                else
                                {
                                    expectedEndpoint = LocationCacheTests.EndpointByLocation[this.preferredLocations[1]];
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
        [DataRow(true, true, true, false, DisplayName = "MultipleWriteEndpointsEnabled | EndpointDiscoveryEnabled | PreferredLocationListEmpty | DefaultEndpointIsGlobalEndpoint")]
        [DataRow(true, false, false, false, DisplayName = "MultipleWriteEndpointsEnabled | EndpointDiscoveryNotEnabled | PreferredLocationListNotEmpty | DefaultEndpointIsGlobalEndpoint")]
        [DataRow(true, false, true, false, DisplayName = "MultipleWriteEndpointsEnabled | EndpointDiscoveryNotEnabled | PreferredLocationListEmpty | DefaultEndpointIsGlobalEndpoint")]
        [DataRow(true, true, false, false, DisplayName = "MultipleWriteEndpointsEnabled | EndpointDiscoveryEnabled | PreferredLocationListNotEmpty | DefaultEndpointIsGlobalEndpoint")]
        [DataRow(false, false, false, false, DisplayName = "MultipleWriteEndpointsNotEnabled | EndpointDiscoveryNotEnabled | PreferredLocationListNotEmpty | DefaultEndpointIsGlobalEndpoint")]
        [DataRow(false, true, true, false, DisplayName = "MultipleWriteEndpointsNotEnabled | EndpointDiscoveryEnabled | PreferredLocationListEmpty | DefaultEndpointIsGlobalEndpoint")]
        [DataRow(false, true, false, false, DisplayName = "MultipleWriteEndpointsNotEnabled | EndpointDiscoveryEnabled | PreferredLocationListNotEmpty | DefaultEndpointIsGlobalEndpoint")]
        [DataRow(false, true, true, false, DisplayName = "MultipleWriteEndpointsNotEnabled | EndpointDiscoveryEnabled | PreferredLocationListEmpty | DefaultEndpointIsGlobalEndpoint")]
        [DataRow(true, true, true, true, DisplayName = "MultipleWriteEndpointsEnabled | EndpointDiscoveryEnabled | PreferredLocationListEmpty | DefaultEndpointIsRegionalEndpoint")]
        [DataRow(true, false, false, true, DisplayName = "MultipleWriteEndpointsEnabled | EndpointDiscoveryNotEnabled | PreferredLocationListNotEmpty | DefaultEndpointIsRegionalEndpoint")]
        [DataRow(true, false, true, true, DisplayName = "MultipleWriteEndpointsEnabled | EndpointDiscoveryNotEnabled | PreferredLocationListEmpty | DefaultEndpointIsRegionalEndpoint")]
        [DataRow(true, true, false, true, DisplayName = "MultipleWriteEndpointsEnabled | EndpointDiscoveryEnabled | PreferredLocationListNotEmpty | DefaultEndpointIsRegionalEndpoint")]
        [DataRow(false, false, false, true, DisplayName = "MultipleWriteEndpointsNotEnabled | EndpointDiscoveryNotEnabled | PreferredLocationListNotEmpty | DefaultEndpointIsRegionalEndpoint")]
        [DataRow(false, true, true, true, DisplayName = "MultipleWriteEndpointsNotEnabled | EndpointDiscoveryEnabled | PreferredLocationListEmpty | DefaultEndpointIsRegionalEndpoint")]
        [DataRow(false, true, false, true, DisplayName = "MultipleWriteEndpointsNotEnabled | EndpointDiscoveryEnabled | PreferredLocationListNotEmpty | DefaultEndpointIsRegionalEndpoint")]
        [DataRow(false, true, true, true, DisplayName = "MultipleWriteEndpointsNotEnabled | EndpointDiscoveryEnabled | PreferredLocationListEmpty | DefaultEndpointIsRegionalEndpoint")]
        public async Task ValidateAsync(
            bool useMultipleWriteEndpoints,
            bool endpointDiscoveryEnabled,
            bool isPreferredListEmpty,
            bool isDefaultEndpointARegionalEndpoint)
        {
            await this.ValidateLocationCacheAsync(
                useMultipleWriteEndpoints,
                endpointDiscoveryEnabled,
                isPreferredListEmpty,
                isDefaultEndpointARegionalEndpoint);
        }

        [TestMethod]
        [DataRow(false, false, DisplayName = "Validate retry on HTTP exception retries with preferredLocations with global default endpoint.")]
        [DataRow(true, false, DisplayName = "Validate retry on HTTP exception retries w/o preferredLocations with global default endpoint.")]
        [DataRow(false, true, DisplayName = "Validate retry on HTTP exception retries with preferredLocations with regional default endpoint.")]
        [DataRow(true, true, DisplayName = "Validate retry on HTTP exception retries w/o preferredLocations with regional default endpoint.")]
        public async Task ValidateRetryOnHttpExceptionAsync(bool isPreferredLocationsEmpty, bool isDefaultEndpointARegionalEndpoint)
        {
            await this.ValidateRetryOnHttpExceptionAsync(enableMultipleWriteLocations: false, isReadRequest: false, isPreferredLocationsEmpty, isDefaultEndpointARegionalEndpoint);
            await this.ValidateRetryOnHttpExceptionAsync(enableMultipleWriteLocations: false, isReadRequest: true, isPreferredLocationsEmpty, isDefaultEndpointARegionalEndpoint);
            await this.ValidateRetryOnHttpExceptionAsync(enableMultipleWriteLocations: true, isReadRequest: false, isPreferredLocationsEmpty, isDefaultEndpointARegionalEndpoint);
            await this.ValidateRetryOnHttpExceptionAsync(enableMultipleWriteLocations: true, isReadRequest: true, isPreferredLocationsEmpty, isDefaultEndpointARegionalEndpoint);
        }

        private async Task ValidateRetryOnHttpExceptionAsync(bool enableMultipleWriteLocations, bool isReadRequest, bool isPreferredLocationsEmpty, bool isDefaultEndpointARegionalEndpoint)
        {
            ReadOnlyCollection<string> preferredList = new List<string>() {
                "location2",
                "location1"
            }.AsReadOnly();

            using GlobalEndpointManager endpointManager = this.Initialize(
                useMultipleWriteLocations: enableMultipleWriteLocations,
                enableEndpointDiscovery: true,
                isPreferredLocationsListEmpty: isPreferredLocationsEmpty,
                preferedRegionListOverride: preferredList,
                enforceSingleMasterSingleWriteLocation: true,
                isDefaultEndpointARegionalEndpoint: isDefaultEndpointARegionalEndpoint);

            endpointManager.InitializeAccountPropertiesAndStartBackgroundRefresh(this.databaseAccount);
            ClientRetryPolicy retryPolicy = this.CreateClientRetryPolicy(enableEndpointDiscovery: true, partitionLevelFailoverEnabled: false, endpointManager: endpointManager);

            if (isPreferredLocationsEmpty)
            {
                if (enableMultipleWriteLocations)
                {
                    if (isDefaultEndpointARegionalEndpoint)
                    {
                        Assert.IsNotNull(this.cache.EffectivePreferredLocations);
                        Assert.AreEqual(1, this.cache.EffectivePreferredLocations.Count);
                        Assert.AreEqual("location1", this.cache.EffectivePreferredLocations[0]);
                    }
                    else
                    {
                        Assert.IsNotNull(this.cache.EffectivePreferredLocations);
                        Assert.AreEqual(4, this.cache.EffectivePreferredLocations.Count);
                    }
                }
                else
                {
                    if (isDefaultEndpointARegionalEndpoint)
                    {
                        Assert.IsNotNull(this.cache.EffectivePreferredLocations);
                        Assert.AreEqual(1, this.cache.EffectivePreferredLocations.Count);
                        Assert.AreEqual("location1", this.cache.EffectivePreferredLocations[0]);
                    }
                    else
                    {
                        Assert.IsNotNull(this.cache.EffectivePreferredLocations);
                        Assert.AreEqual(4, this.cache.EffectivePreferredLocations.Count);
                    }
                }
            }

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
                                    expectedEndpoint = isPreferredLocationsEmpty ?
                                        LocationCacheTests.EndpointByLocation[this.cache.EffectivePreferredLocations[0]]
                                        : LocationCacheTests.EndpointByLocation[preferredList[0]];
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
                                    // [or] back to first effective preferred region in case empty preferred regions and regional default endpoint
                                    if (isPreferredLocationsEmpty)
                                    {
                                        expectedEndpoint = isDefaultEndpointARegionalEndpoint
                                            ? LocationCacheTests.EndpointByLocation[this.cache.EffectivePreferredLocations[0]]
                                            : LocationCacheTests.EndpointByLocation[this.cache.EffectivePreferredLocations[1]];
                                    }
                                    else
                                    {
                                        expectedEndpoint = LocationCacheTests.EndpointByLocation[preferredList[1]];
                                    }
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
        [DataRow(true, false, false, true, false, false, DisplayName = "Read request - Single master - no preferred locations - without partition level failover - should retry - global default endpoint")]
        [DataRow(false, false, false, false, false, false, DisplayName = "Write request - Single master - no preferred locations - without partition level failover - should NOT retry - global default endpoint")]
        [DataRow(true, true, false, true, false, false, DisplayName = "Read request - Multi master - no preferred locations - without partition level failover - should retry - global default endpoint")]
        [DataRow(false, true, false, true, false, false, DisplayName = "Write request - Multi master - no preferred locations - without partition level failover - should NOT retry - global default endpoint")]
        [DataRow(true, false, true, true, false, false, DisplayName = "Read request - Single master - with preferred locations - without partition level failover - should retry - global default endpoint")]
        [DataRow(false, false, true, false, false, false, DisplayName = "Write request - Single master - with preferred locations - without partition level failover - should NOT retry - global default endpoint")]
        [DataRow(true, true, true, true, false, false, DisplayName = "Read request - Multi master - with preferred locations - without partition level failover - should retry - global default endpoint")]
        [DataRow(false, true, true, true, false, false, DisplayName = "Write request - Multi master - with preferred locations - without partition level failover - should retry - global default endpoint")]
        [DataRow(true, false, false, true, true, false, DisplayName = "Read request - Single master - no preferred locations - with partition level failover - should retry - global default endpoint")]
        [DataRow(false, false, false, true, true, false, DisplayName = "Write request - Single master - no preferred locations - with partition level failover - should retry - global default endpoint")]
        [DataRow(true, true, false, true, true, false, DisplayName = "Read request - Multi master - no preferred locations - with partition level failover - should retry - global default endpoint")]
        [DataRow(false, true, false, true, true, false, DisplayName = "Write request - Multi master - no preferred locations - with partition level failover - should retry - global default endpoint")]
        [DataRow(true, false, true, true, true, false, DisplayName = "Read request - Single master - with preferred locations - with partition level failover - should NOT retry - global default endpoint")]
        [DataRow(false, false, true, true, true, false, DisplayName = "Write request - Single master - with preferred locations - with partition level failover - should retry - global default endpoint")]
        [DataRow(true, true, true, true, true, false, DisplayName = "Read request - Multi master - with preferred locations - with partition level failover - should retry - global default endpoint")]
        [DataRow(false, true, true, true, true, false, DisplayName = "Write request - Multi master - with preferred locations - with partition level failover - should retry - global default endpoint")]
        [DataRow(true, false, false, false, false, true, DisplayName = "Read request - Single master - no preferred locations - without partition level failover - should NOT retry - regional default endpoint")]
        [DataRow(false, false, false, false, false, true, DisplayName = "Write request - Single master - no preferred locations - without partition level failover - should NOT retry - regional default endpoint")]
        [DataRow(true, true, false, false, false, true, DisplayName = "Read request - Multi master - no preferred locations - without partition level failover - should NOT retry - regional default endpoint")]
        [DataRow(false, true, false, false, false, true, DisplayName = "Write request - Multi master - no preferred locations - without partition level failover - should NOT retry - regional default endpoint")]
        [DataRow(true, false, true, true, false, true, DisplayName = "Read request - Single master - with preferred locations - without partition level failover - should retry - regional default endpoint")]
        [DataRow(false, false, true, false, false, true, DisplayName = "Write request - Single master - with preferred locations - without partition level failover - should NOT retry - regional default endpoint")]
        [DataRow(true, true, true, true, false, true, DisplayName = "Read request - Multi master - with preferred locations - without partition level failover - should retry - regional default endpoint")]
        [DataRow(false, true, true, true, false, true, DisplayName = "Write request - Multi master - with preferred locations - without partition level failover - should retry - regional default endpoint")]
        [DataRow(true, false, false, false, true, true, DisplayName = "Read request - Single master - no preferred locations - with partition level failover - should NOT retry - regional default endpoint")]
        [DataRow(false, false, false, false, true, true, DisplayName = "Write request - Single master - no preferred locations - with partition level failover - should NOT retry - regional default endpoint")]
        [DataRow(true, true, false, false, true, true, DisplayName = "Read request - Multi master - no preferred locations - with partition level failover - should NOT retry - regional default endpoint")]
        [DataRow(false, true, false, false, true, true, DisplayName = "Write request - Multi master - no preferred locations - with partition level failover - should NOT retry - regional default endpoint")]
        [DataRow(true, false, true, true, true, true, DisplayName = "Read request - Single master - with preferred locations - with partition level failover - should NOT retry - regional default endpoint")]
        [DataRow(false, false, true, true, true, true, DisplayName = "Write request - Single master - with preferred locations - with partition level failover - should retry - regional default endpoint")]
        [DataRow(true, true, true, true, true, true, DisplayName = "Read request - Multi master - with preferred locations - with partition level failover - should retry - regional default endpoint")]
        [DataRow(false, true, true, true, true, true, DisplayName = "Write request - Multi master - with preferred locations - with partition level failover - should retry - regional default endpoint")]
        public async Task ClientRetryPolicy_ValidateRetryOnServiceUnavailable(
            bool isReadRequest,
            bool useMultipleWriteLocations,
            bool usesPreferredLocations,
            bool shouldHaveRetried,
            bool enablePartitionLevelFailover,
            bool isDefaultEndpointARegionalEndpoint)
        {
            const bool enableEndpointDiscovery = true;

            ReadOnlyCollection<string> preferredList = new List<string>() {
                "location2",
                "location1"
            }.AsReadOnly();

            using GlobalEndpointManager endpointManager = this.Initialize(
                useMultipleWriteLocations: useMultipleWriteLocations,
                enableEndpointDiscovery: enableEndpointDiscovery,
                isPreferredLocationsListEmpty: !usesPreferredLocations,
                enablePartitionLevelFailover: enablePartitionLevelFailover,
                preferedRegionListOverride: preferredList,
                enforceSingleMasterSingleWriteLocation: true,
                isDefaultEndpointARegionalEndpoint: isDefaultEndpointARegionalEndpoint);

            endpointManager.InitializeAccountPropertiesAndStartBackgroundRefresh(this.databaseAccount);

            ClientRetryPolicy retryPolicy = this.CreateClientRetryPolicy(enableEndpointDiscovery, partitionLevelFailoverEnabled: enablePartitionLevelFailover, endpointManager);

            if (!usesPreferredLocations)
            {
                if (isDefaultEndpointARegionalEndpoint)
                {
                    Assert.IsNotNull(this.cache.EffectivePreferredLocations);
                    Assert.AreEqual(1, this.cache.EffectivePreferredLocations.Count);
                    Assert.AreEqual("location1", this.cache.EffectivePreferredLocations[0]);
                }
                else
                {
                    Assert.IsNotNull(this.cache.EffectivePreferredLocations);
                    Assert.AreEqual(4, this.cache.EffectivePreferredLocations.Count);
                }
            }


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
                                Uri expectedEndpoint;

                                if (usesPreferredLocations)
                                {
                                    if (useMultipleWriteLocations)
                                    {
                                        expectedEndpoint = isReadRequest
                                            ? LocationCacheTests.EndpointByLocation[preferredList[1]]
                                            : LocationCacheTests.EndpointByLocation[preferredList[1]];
                                    }
                                    else
                                    {
                                        expectedEndpoint = isReadRequest
                                            ? LocationCacheTests.EndpointByLocation[preferredList[1]]
                                            : LocationCacheTests.EndpointByLocation[preferredList[1]];
                                    }
                                }
                                else
                                {
                                    if (useMultipleWriteLocations)
                                    {
                                        expectedEndpoint = isReadRequest
                                            ? LocationCacheTests.EndpointByLocation[this.cache.EffectivePreferredLocations[1]]
                                            : LocationCacheTests.EndpointByLocation[this.cache.EffectivePreferredLocations[1]];
                                    }
                                    else
                                    {
                                        expectedEndpoint = isReadRequest
                                            ? LocationCacheTests.EndpointByLocation[this.cache.EffectivePreferredLocations[1]]
                                            : LocationCacheTests.EndpointByLocation[this.cache.EffectivePreferredLocations[0]];
                                    }
                                }

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

        [TestMethod]
        [DataRow(true, true, true, false, DisplayName = "Read request - Multi master - with preferred locations - default endpoint is not regional endpoint")]
        [DataRow(true, true, false, false, DisplayName = "Read request - Multi master - no preferred locations - default endpoint is not regional endpoint")]
        [DataRow(true, false, true, false, DisplayName = "Read request - Single master - with preferred locations - default endpoint is not regional endpoint")]
        [DataRow(true, false, false, false, DisplayName = "Read request - Single master - no preferred locations - default endpoint is not regional endpoint")]
        [DataRow(false, true, true, false, DisplayName = "Write request - Multi master - with preferred locations - default endpoint is not regional endpoint")]
        [DataRow(false, true, false, false, DisplayName = "Write request - Multi master - no preferred locations - default endpoint is not regional endpoint")]
        [DataRow(false, false, true, false, DisplayName = "Write request - Single master - with preferred locations - default endpoint is not regional endpoint")]
        [DataRow(false, false, false, false, DisplayName = "Write request - Single master - no preferred locations - default endpoint is not regional endpoint")]
        [DataRow(true, true, true, true, DisplayName = "Read request - Multi master - with preferred locations - default endpoint is regional endpoint")]
        [DataRow(true, true, false, true, DisplayName = "Read request - Multi master - no preferred locations - default endpoint is regional endpoint")]
        [DataRow(true, false, true, true, DisplayName = "Read request - Single master - with preferred locations - default endpoint is regional endpoint")]
        [DataRow(true, false, false, true, DisplayName = "Read request - Single master - no preferred locations - default endpoint is regional endpoint")]
        [DataRow(false, true, true, true, DisplayName = "Write request - Multi master - with preferred locations - default endpoint is regional endpoint")]
        [DataRow(false, true, false, true, DisplayName = "Write request - Multi master - no preferred locations - default endpoint is regional endpoint")]
        [DataRow(false, false, true, true, DisplayName = "Write request - Single master - with preferred locations - default endpoint is regional endpoint")]
        [DataRow(false, false, false, true, DisplayName = "Write request - Single master - no preferred locations - default endpoint is regional endpoint")]
        public void VerifyRegionExcludedTest(
            bool isReadRequest,
            bool useMultipleWriteLocations,
            bool usesPreferredLocations,
            bool isDefaultEndpointAlsoRegionEndpoint)
        {
            bool enableEndpointDiscovery = true;

            ReadOnlyCollection<string> preferredList = usesPreferredLocations ?
                isReadRequest ?
                    new List<string> {
                        "location4",
                        "location2",
                        "location1"
                    }.AsReadOnly() :
                    new List<string> {
                        "location3",
                        "location2",
                        "location1"
                    }.AsReadOnly() :
                isReadRequest ?
                    new List<string>() {
                        "default",
                        "location1",
                        "location2",
                        "location4"
                    }.AsReadOnly() :
                    new List<string>() {
                        "default",
                        "location1",
                        "location2",
                        "location3"
                    }.AsReadOnly();

            List<List<string>> excludeRegionCases = isReadRequest ?
            new List<List<string>>()
            {
                new List<string> { "location1" },
                new List<string> { "location2" },
                new List<string> { "location4" },
                new List<string> { "location1", "location2" },
                new List<string> { "location1", "location4" },
                new List<string> { "location2", "location4" },
                new List<string> { "location1", "location2", "location4" },
                new List<string> { "location1", "location2", "location3", "location4" },
            } : new List<List<string>>()
            {
                new List<string> { "location1" },
                new List<string> { "location2" },
                new List<string> { "location3" },
                new List<string> { "location1", "location2" },
                new List<string> { "location1", "location3" },
                new List<string> { "location2", "location3" },
                new List<string> { "location1", "location2", "location3" }
            };

            foreach (List<string> excludeRegions in excludeRegionCases)
            {
                using GlobalEndpointManager endpointManager = this.Initialize(
                useMultipleWriteLocations: useMultipleWriteLocations,
                enableEndpointDiscovery: enableEndpointDiscovery,
                isPreferredLocationsListEmpty: !usesPreferredLocations,
                preferedRegionListOverride: preferredList,
                enforceSingleMasterSingleWriteLocation: true,
                isExcludeRegionsTest: true,
                isDefaultEndpointARegionalEndpoint: isDefaultEndpointAlsoRegionEndpoint);

                endpointManager.InitializeAccountPropertiesAndStartBackgroundRefresh(this.databaseAccount);
                ClientRetryPolicy retryPolicy = this.CreateClientRetryPolicy(enableEndpointDiscovery: true, partitionLevelFailoverEnabled: false, endpointManager: endpointManager);

                using (DocumentServiceRequest request = this.CreateRequest(isReadRequest: isReadRequest, isMasterResourceType: false))
                {
                    request.RequestContext.ExcludeRegions = excludeRegions;
                    ReadOnlyCollection<Uri> applicableEndpoints;

                    if (!isReadRequest && !useMultipleWriteLocations)
                    {
                        List<Uri> applicableEndpointsInner = new List<Uri>(1);

                        Assert.IsNotNull(this.cache.WriteEndpoints);
                        Assert.IsTrue(this.cache.WriteEndpoints.Count > 0);

                        applicableEndpointsInner.Add(this.cache.WriteEndpoints[0]);
                        applicableEndpoints = applicableEndpointsInner.AsReadOnly();
                    }
                    else
                    {
                        applicableEndpoints = this.cache.GetApplicableEndpoints(request, isReadRequest);
                    }

                    Uri endpoint = endpointManager.ResolveServiceEndpoint(request);
                    ReadOnlyCollection<Uri> applicableRegions = this.GetApplicableRegions(isReadRequest, useMultipleWriteLocations, usesPreferredLocations, excludeRegions, isDefaultEndpointAlsoRegionEndpoint);

                    Assert.AreEqual(applicableRegions.Count, applicableEndpoints.Count);
                    for (int i = 0; i < applicableRegions.Count; i++)
                    {
                        Assert.AreEqual(applicableRegions[i], applicableEndpoints[i]);
                    }

                    Assert.AreEqual(applicableRegions[0], endpoint);
                }
            }

        }

        private ReadOnlyCollection<Uri> GetApplicableRegions(bool isReadRequest, bool useMultipleWriteLocations, bool usesPreferredLocations, List<string> excludeRegions, bool isDefaultEndpointARegionalEndpoint)
        {
            // exclusion of write region for single-write maps to first available write region
            if (!isReadRequest && !useMultipleWriteLocations)
            {
                return new List<Uri>() { LocationCacheTests.Location1Endpoint }.AsReadOnly();
            }

            Dictionary<string, Uri> readWriteLocations = usesPreferredLocations ?
                isReadRequest ?
                    new Dictionary<string, Uri>()
                    {
                        {"location4", LocationCacheTests.Location4Endpoint },
                        {"location2", LocationCacheTests.Location2Endpoint },
                        {"location1", LocationCacheTests.Location1Endpoint },
                    } :
                    useMultipleWriteLocations ?
                        new Dictionary<string, Uri>()
                        {
                            {"location3", LocationCacheTests.Location3Endpoint },
                            {"location2", LocationCacheTests.Location2Endpoint },
                            {"location1", LocationCacheTests.Location1Endpoint },
                        } :
                        new Dictionary<string, Uri>()
                        {
                        } :
                isReadRequest ?
                    new Dictionary<string, Uri>()
                    {
                        {"location1", LocationCacheTests.Location1Endpoint },
                        {"location2", LocationCacheTests.Location2Endpoint },
                        {"location3", LocationCacheTests.Location3Endpoint },
                        {"location4", LocationCacheTests.Location4Endpoint },
                    } :
                    useMultipleWriteLocations ?
                        new Dictionary<string, Uri>()
                        {
                            {"location1", LocationCacheTests.Location1Endpoint },
                            {"location2", LocationCacheTests.Location2Endpoint },
                            {"location3", LocationCacheTests.Location3Endpoint },
                        } :
                        new Dictionary<string, Uri>()
                        {
                        };

            List<Uri> applicableRegions = new List<Uri>();

            // exclude regions applies when
            //  1. preferred regions are set
            //  2. preferred regions aren't set and default endpoint isn't a regional endpoint
            if (usesPreferredLocations || (!usesPreferredLocations && !isDefaultEndpointARegionalEndpoint))
            {
                foreach (string region in readWriteLocations.Keys)
                {
                    if (!excludeRegions.Contains(region))
                    {
                        applicableRegions.Add(readWriteLocations[region]);
                    }
                }
            }

            if (applicableRegions.Count == 0)
            {
                if (isDefaultEndpointARegionalEndpoint)
                {
                    applicableRegions.Add(LocationCacheTests.DefaultRegionalEndpoint);
                }
                else
                {
                    applicableRegions.Add(LocationCacheTests.DefaultEndpoint);
                }
            }

            return applicableRegions.AsReadOnly();
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
                writeLocations =
                    new Collection<AccountRegion>()
                    {
                        { new AccountRegion() { Name = "location1", Endpoint = LocationCacheTests.Location1Endpoint.ToString() } }
                    };
            }

            AccountProperties databaseAccount = new AccountProperties()
            {
                EnableMultipleWriteLocations = useMultipleWriteLocations,
                // ReadLocations should be a superset of WriteLocations
                ReadLocationsInternal = new Collection<AccountRegion>()
                    {
                        { new AccountRegion() { Name = "location1", Endpoint = LocationCacheTests.Location1Endpoint.ToString() } },
                        { new AccountRegion() { Name = "location2", Endpoint = LocationCacheTests.Location2Endpoint.ToString() } },
                        { new AccountRegion() { Name = "location3", Endpoint = LocationCacheTests.Location3Endpoint.ToString() } },
                        { new AccountRegion() { Name = "location4", Endpoint = LocationCacheTests.Location4Endpoint.ToString() } },
                    },
                WriteLocationsInternal = writeLocations
            };

            return databaseAccount;
        }

        private GlobalEndpointManager Initialize(
            bool useMultipleWriteLocations,
            bool enableEndpointDiscovery,
            bool isPreferredLocationsListEmpty,
            bool enforceSingleMasterSingleWriteLocation = false, // Some tests depend on the Initialize to create an account with multiple write locations, even when not multi master
            ReadOnlyCollection<string> preferedRegionListOverride = null,
            bool enablePartitionLevelFailover = false,
            bool isExcludeRegionsTest = false,
            bool isDefaultEndpointARegionalEndpoint = false)
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
                this.preferredLocations = preferedRegionListOverride ?? new List<string>()
                {
                    "location1",
                    "location2",
                    "location3"
                }.AsReadOnly();
            }

            this.cache = new LocationCache(
                this.preferredLocations,
                isDefaultEndpointARegionalEndpoint ? LocationCacheTests.DefaultRegionalEndpoint : LocationCacheTests.DefaultEndpoint,
                enableEndpointDiscovery,
                10,
                useMultipleWriteLocations);

            this.cache.OnDatabaseAccountRead(this.databaseAccount);

            this.mockedClient = new Mock<IDocumentClientInternal>();
            this.mockedClient.Setup(owner => owner.ServiceEndpoint).Returns(isDefaultEndpointARegionalEndpoint ? LocationCacheTests.DefaultRegionalEndpoint : LocationCacheTests.DefaultEndpoint);
            this.mockedClient.Setup(owner => owner.GetDatabaseAccountInternalAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>())).ReturnsAsync(this.databaseAccount);

            ConnectionPolicy connectionPolicy = new ConnectionPolicy()
            {
                EnableEndpointDiscovery = enableEndpointDiscovery,
                UseMultipleWriteLocations = useMultipleWriteLocations,
            };

            foreach (string preferredLocation in this.preferredLocations)
            {
                connectionPolicy.PreferredLocations.Add(preferredLocation);
            }

            GlobalEndpointManager endpointManager = new GlobalEndpointManager(this.mockedClient.Object, connectionPolicy);

            this.partitionKeyRangeLocationCache = enablePartitionLevelFailover
                ? new GlobalPartitionEndpointManagerCore(endpointManager)
                : GlobalPartitionEndpointManagerNoOp.Instance;

            return endpointManager;
        }

        private async Task ValidateLocationCacheAsync(
            bool useMultipleWriteLocations,
            bool endpointDiscoveryEnabled,
            bool isPreferredListEmpty,
            bool isDefaultEndpointARegionalEndpoint)
        {
            // hardcoded to represent - (location1, location2, location3) as the write regions (with and without preferred regions set)
            int maxWriteLocationIndex = 3;

            // hardcoded to represent - (location1, location2, location3, location4) as the account regions and (location1, location2, location3)
            // as the read regions (with preferred regions set)
            int maxReadLocationIndex = isPreferredListEmpty ? 4 : 3;

            if (isPreferredListEmpty && isDefaultEndpointARegionalEndpoint)
            {
                maxWriteLocationIndex = 1;
                maxReadLocationIndex = 1;
            }

            for (int writeLocationIndex = 0; writeLocationIndex < maxWriteLocationIndex; writeLocationIndex++)
            {
                for (int readLocationIndex = 0; readLocationIndex < maxReadLocationIndex; readLocationIndex++)
                {
                    using GlobalEndpointManager endpointManager = this.Initialize(
                        useMultipleWriteLocations: useMultipleWriteLocations,
                        enableEndpointDiscovery: endpointDiscoveryEnabled,
                        isPreferredLocationsListEmpty: isPreferredListEmpty,
                        isDefaultEndpointARegionalEndpoint: isDefaultEndpointARegionalEndpoint);

                    ReadOnlyCollection<Uri> currentWriteEndpoints = this.cache.WriteEndpoints;
                    ReadOnlyCollection<Uri> currentReadEndpoints = this.cache.ReadEndpoints;

                    for (int i = 0; i < readLocationIndex; i++)
                    {
                        this.cache.MarkEndpointUnavailableForRead(new Uri(this.databaseAccount.ReadLocationsInternal[i].Endpoint));
                        endpointManager.MarkEndpointUnavailableForRead(new Uri(this.databaseAccount.ReadLocationsInternal[i].Endpoint));
                    }

                    for (int i = 0; i < writeLocationIndex; i++)
                    {
                        this.cache.MarkEndpointUnavailableForWrite(new Uri(this.databaseAccount.WriteLocationsInternal[i].Endpoint));
                        endpointManager.MarkEndpointUnavailableForWrite(
                             new Uri(this.databaseAccount.WriteLocationsInternal[i].Endpoint));
                    }

                    Dictionary<string, Uri> writeEndpointByLocation = this.databaseAccount.WriteLocationsInternal.ToDictionary(
                        location => location.Name,
                        location => new Uri(location.Endpoint));

                    Dictionary<string, Uri> readEndpointByLocation = this.databaseAccount.ReadLocationsInternal.ToDictionary(
                        location => location.Name,
                        location => new Uri(location.Endpoint));

                    List<Uri> accountLevelReadEndpoints = this.databaseAccount.ReadLocationsInternal
                        .Where(accountRegion => readEndpointByLocation.ContainsKey(accountRegion.Name))
                        .Select(accountRegion => readEndpointByLocation[accountRegion.Name])
                        .ToList();

                    List<Uri> accountLevelWriteEndpoints = this.databaseAccount.WriteLocationsInternal
                        .Where(accountRegion => writeEndpointByLocation.ContainsKey(accountRegion.Name))
                        .Select(accountRegion => writeEndpointByLocation[accountRegion.Name])
                        .ToList();

                    ReadOnlyCollection<string> preferredLocationsWhenClientLevelPreferredLocationsIsEmpty = this.cache.EffectivePreferredLocations;

                    Uri[] preferredAvailableWriteEndpoints, preferredAvailableReadEndpoints;

                    if (isPreferredListEmpty)
                    {
                        preferredAvailableWriteEndpoints = preferredLocationsWhenClientLevelPreferredLocationsIsEmpty.Skip(writeLocationIndex)
                            .Where(location => writeEndpointByLocation.ContainsKey(location))
                            .Select(location => writeEndpointByLocation[location]).ToArray();

                        preferredAvailableReadEndpoints = preferredLocationsWhenClientLevelPreferredLocationsIsEmpty.Skip(readLocationIndex)
                            .Where(location => readEndpointByLocation.ContainsKey(location))
                            .Select(location => readEndpointByLocation[location]).ToArray();
                    }
                    else
                    {
                        preferredAvailableWriteEndpoints = this.preferredLocations.Skip(writeLocationIndex)
                            .Where(location => writeEndpointByLocation.ContainsKey(location))
                            .Select(location => writeEndpointByLocation[location]).ToArray();

                        preferredAvailableReadEndpoints = this.preferredLocations.Skip(readLocationIndex)
                            .Where(location => readEndpointByLocation.ContainsKey(location))
                            .Select(location => readEndpointByLocation[location]).ToArray();
                    }

                    this.ValidateEndpointRefresh(
                        useMultipleWriteLocations,
                        endpointDiscoveryEnabled,
                        isPreferredListEmpty,
                        preferredAvailableWriteEndpoints,
                        preferredAvailableReadEndpoints,
                        preferredLocationsWhenClientLevelPreferredLocationsIsEmpty,
                        preferredLocationsWhenClientLevelPreferredLocationsIsEmpty,
                        accountLevelWriteEndpoints,
                        accountLevelReadEndpoints,
                        writeLocationIndex > 0,
                        readLocationIndex > 0 &&
                        currentReadEndpoints[0] != LocationCacheTests.DefaultEndpoint,
                        currentWriteEndpoints.Count > 1,
                        currentReadEndpoints.Count > 1);

                    await this.ValidateGlobalEndpointLocationCacheRefreshAsync(endpointManager);

                    this.ValidateRequestEndpointResolution(
                        useMultipleWriteLocations,
                        endpointDiscoveryEnabled,
                        preferredAvailableWriteEndpoints,
                        preferredAvailableReadEndpoints,
                        isPreferredListEmpty,
                        isDefaultEndpointARegionalEndpoint);

                    // wait for TTL on unavailability info
                    string expirationTime = System.Configuration.ConfigurationManager.AppSettings["UnavailableLocationsExpirationTimeInSeconds"];
                    int delayInMilliSeconds = int.Parse(
                                                  expirationTime,
                                                  NumberStyles.Integer,
                                                  CultureInfo.InvariantCulture) * 1000 * 2;
                    await Task.Delay(delayInMilliSeconds);

                    string config = $"Delay{expirationTime};" +
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
            bool isPreferredListEmpty,
            Uri[] preferredAvailableWriteEndpoints,
            Uri[] preferredAvailableReadEndpoints,
            ReadOnlyCollection<string> preferredAvailableWriteRegions,
            ReadOnlyCollection<string> preferredAvailableReadRegions,
            List<Uri> accountLevelWriteEndpoints,
            List<Uri> accountLevelReadEndpoints,
            bool isFirstWriteEndpointUnavailable,
            bool isFirstReadEndpointUnavailable,
            bool hasMoreThanOneWriteEndpoints,
            bool hasMoreThanOneReadEndpoints)
        {
            bool shouldRefreshEndpoints = this.cache.ShouldRefreshEndpoints(out bool canRefreshInBackground);

            bool isMostPreferredLocationUnavailableForRead = isFirstReadEndpointUnavailable;
            bool isMostPreferredLocationUnavailableForWrite = !useMultipleWriteLocations && isFirstWriteEndpointUnavailable;

            if (this.preferredLocations.Count > 0 || (isPreferredListEmpty && endpointDiscoveryEnabled))
            {
                string mostPreferredReadLocationName = (isPreferredListEmpty && endpointDiscoveryEnabled) ? preferredAvailableReadRegions[0] : this.preferredLocations.FirstOrDefault(location => this.databaseAccount.ReadableRegions.Any(readLocation => readLocation.Name == location), "");
                Uri mostPreferredReadEndpoint = LocationCacheTests.EndpointByLocation[mostPreferredReadLocationName];
                isMostPreferredLocationUnavailableForRead = preferredAvailableReadEndpoints.Length == 0 || (preferredAvailableReadEndpoints[0] != mostPreferredReadEndpoint);

                if (isPreferredListEmpty && endpointDiscoveryEnabled)
                {
                    isMostPreferredLocationUnavailableForRead = preferredAvailableReadEndpoints[0] != accountLevelReadEndpoints[0];
                }

                string mostPreferredWriteLocationName = (isPreferredListEmpty && endpointDiscoveryEnabled) ? preferredAvailableWriteRegions[0] : this.preferredLocations.FirstOrDefault(location => this.databaseAccount.WritableRegions.Any(writeLocation => writeLocation.Name == location), "");
                Uri mostPreferredWriteEndpoint = LocationCacheTests.EndpointByLocation[mostPreferredWriteLocationName];

                if (useMultipleWriteLocations)
                {
                    isMostPreferredLocationUnavailableForWrite = preferredAvailableWriteEndpoints.Length == 0 || (preferredAvailableWriteEndpoints[0] != mostPreferredWriteEndpoint);
                }

                if (isPreferredListEmpty && endpointDiscoveryEnabled)
                {
                    isMostPreferredLocationUnavailableForWrite = preferredAvailableWriteEndpoints[0] != accountLevelWriteEndpoints[0];
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

        private async Task ValidateGlobalEndpointLocationCacheRefreshAsync(GlobalEndpointManager endpointManager)
        {
            IEnumerable<Task> refreshLocations = Enumerable.Range(0, 10).Select(index => Task.Factory.StartNew(() => endpointManager.RefreshLocationAsync(false)));

            await Task.WhenAll(refreshLocations);

            this.mockedClient.Verify(client => client.GetDatabaseAccountInternalAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()), Times.AtMostOnce);

            this.mockedClient.ResetCalls();

            foreach (Task task in Enumerable.Range(0, 10).Select(index => Task.Factory.StartNew(() => endpointManager.RefreshLocationAsync(false))))
            {
                await task;
            }

            this.mockedClient.Verify(client => client.GetDatabaseAccountInternalAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()), Times.AtMostOnce);
        }

        private void ValidateRequestEndpointResolution(
            bool useMultipleWriteLocations,
            bool endpointDiscoveryEnabled,
            Uri[] availableWriteEndpoints,
            Uri[] availableReadEndpoints,
            bool isPreferredLocationsListEmpty,
            bool isDefaultEndpointARegionalEndpoint)
        {
            Uri firstAvailableWriteEndpoint;
            Uri secondAvailableWriteEndpoint;

            if (!endpointDiscoveryEnabled)
            {
                firstAvailableWriteEndpoint = isDefaultEndpointARegionalEndpoint ? LocationCacheTests.DefaultRegionalEndpoint : LocationCacheTests.DefaultEndpoint;
                secondAvailableWriteEndpoint = isDefaultEndpointARegionalEndpoint ? LocationCacheTests.DefaultRegionalEndpoint : LocationCacheTests.DefaultEndpoint;
            }
            else if (!useMultipleWriteLocations)
            {
                firstAvailableWriteEndpoint = new Uri(this.databaseAccount.WriteLocationsInternal[0].Endpoint);
                secondAvailableWriteEndpoint = new Uri(this.databaseAccount.WriteLocationsInternal[1].Endpoint);
            }
            else if (availableWriteEndpoints.Length > 1)
            {

                if (isDefaultEndpointARegionalEndpoint && isPreferredLocationsListEmpty)
                {
                    firstAvailableWriteEndpoint = LocationCacheTests.DefaultRegionalEndpoint;
                    secondAvailableWriteEndpoint = LocationCacheTests.DefaultRegionalEndpoint;
                }
                else
                {
                    firstAvailableWriteEndpoint = availableWriteEndpoints[0];
                    secondAvailableWriteEndpoint = availableWriteEndpoints[1];
                }
            }
            else if (availableWriteEndpoints.Length > 0)
            {
                if (isDefaultEndpointARegionalEndpoint && isPreferredLocationsListEmpty)
                {
                    firstAvailableWriteEndpoint = LocationCacheTests.DefaultRegionalEndpoint;
                    secondAvailableWriteEndpoint = LocationCacheTests.DefaultRegionalEndpoint;
                }
                else
                {
                    firstAvailableWriteEndpoint = availableWriteEndpoints[0];
                    secondAvailableWriteEndpoint =
                        this.databaseAccount.WriteLocationsInternal[0].Endpoint != firstAvailableWriteEndpoint.ToString() ?
                        new Uri(this.databaseAccount.WriteLocationsInternal[0].Endpoint) :
                        new Uri(this.databaseAccount.WriteLocationsInternal[1].Endpoint);
                }
            }
            else
            {
                firstAvailableWriteEndpoint = isDefaultEndpointARegionalEndpoint ? LocationCacheTests.DefaultRegionalEndpoint : LocationCacheTests.DefaultEndpoint;
                secondAvailableWriteEndpoint = isDefaultEndpointARegionalEndpoint ? LocationCacheTests.DefaultRegionalEndpoint : LocationCacheTests.DefaultEndpoint;
            }

            Uri firstAvailableReadEndpoint;

            if (!endpointDiscoveryEnabled)
            {
                firstAvailableReadEndpoint = isDefaultEndpointARegionalEndpoint ? LocationCacheTests.DefaultRegionalEndpoint : LocationCacheTests.DefaultEndpoint;
            }
            else
            {
                firstAvailableReadEndpoint = availableReadEndpoints.Length > 0
                    ? availableReadEndpoints[0]
                    : LocationCacheTests.EndpointByLocation[this.preferredLocations[0]];
            }

            Uri firstWriteEndpoint = !endpointDiscoveryEnabled ?
                LocationCacheTests.DefaultEndpoint :
                new Uri(this.databaseAccount.WriteLocationsInternal[0].Endpoint);

            Uri secondWriteEndpoint = !endpointDiscoveryEnabled ?
                LocationCacheTests.DefaultEndpoint :
                new Uri(this.databaseAccount.WriteLocationsInternal[1].Endpoint);

            if (isDefaultEndpointARegionalEndpoint && !endpointDiscoveryEnabled)
            {
                firstWriteEndpoint = LocationCacheTests.DefaultRegionalEndpoint;
                secondWriteEndpoint = LocationCacheTests.DefaultRegionalEndpoint;
            }

            // If current write endpoint is unavailable, write endpoints order doesn't change
            // All write requests flip-flop between current write and alternate write endpoint
            ReadOnlyCollection<Uri> writeEndpoints = this.cache.WriteEndpoints;
            Assert.AreEqual(firstAvailableWriteEndpoint, writeEndpoints[0]);
            Assert.AreEqual(secondAvailableWriteEndpoint, this.ResolveEndpointForWriteRequest(ResourceType.Document, true));
            Assert.AreEqual(firstAvailableWriteEndpoint, this.ResolveEndpointForWriteRequest(ResourceType.Document, false));

            // Writes to other resource types should be directed to first/second write endpoint
            Assert.AreEqual(firstWriteEndpoint, this.ResolveEndpointForWriteRequest(ResourceType.Database, false));
            Assert.AreEqual(secondWriteEndpoint, this.ResolveEndpointForWriteRequest(ResourceType.Database, true));

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