//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Net;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using static Microsoft.Azure.Cosmos.MetadataRequestThrottleRetryPolicy;

    /// <summary>
    /// Unit tests for <see cref="MetadataRequestThrottleRetryPolicy"/>.
    /// </summary>
    [TestClass]
    public class MetadataRequestThrottleRetryPolicyTests
    {
        [TestMethod]
        [Owner("dkunda")]
        [DataRow(true, DisplayName = "Test when a response message.")]
        [DataRow(false, DisplayName = "Test when an exception was thrown.")]
        public async Task ShouldRetryAsync_WithValidAndInvalidSubStatusCodes_ShouldIncrementLocationIndexOrSkip(
            bool useResponseMessage)
        {
            // Arrange.
            ShouldRetryResult retryResult;
            string collectionRid = "test-collection";
            Uri primaryServiceEndpoint = new("https://default-endpoint-region1.net/");
            Uri routedServiceEndpoint = new("https://default-endpoint-region2.net/");

            Documents.Collections.INameValueCollection headers = new Documents.Collections.RequestNameValueCollection();

            headers.Set(HttpConstants.HttpHeaders.PageSize, "10");
            headers.Set(HttpConstants.HttpHeaders.A_IM, HttpConstants.A_IMHeaderValues.IncrementalFeed);

            DocumentServiceRequest request = DocumentServiceRequest.Create(
                    OperationType.ReadFeed,
                    collectionRid,
                    Documents.ResourceType.PartitionKeyRange,
                    AuthorizationTokenType.PrimaryMasterKey,
                    headers);

            Mock<IGlobalEndpointManager> mockedGlobalEndpointManager = new();
            mockedGlobalEndpointManager
                .SetupSequence(gem => gem.ResolveServiceEndpoint(It.IsAny<DocumentServiceRequest>()))
                .Returns(primaryServiceEndpoint)
                .Returns(routedServiceEndpoint);

            MetadataRequestThrottleRetryPolicy policy = new(mockedGlobalEndpointManager.Object, 0);
            policy.OnBeforeSendRequest(request);

            Assert.AreEqual(primaryServiceEndpoint, request.RequestContext.LocationEndpointToRoute);

            // Act.
            if (useResponseMessage)
            {
                Headers responseHeaders = new()
                {
                    SubStatusCode = SubStatusCodes.TransportGenerated503
                };

                ResponseMessage responseMessage = new(
                    statusCode: HttpStatusCode.ServiceUnavailable,
                    requestMessage: null,
                    headers: responseHeaders,
                    cosmosException: null,
                    trace: NoOpTrace.Singleton);

                retryResult = await policy.ShouldRetryAsync(responseMessage, default);
            }
            else
            {
                CosmosException exception = CosmosExceptionFactory.CreateServiceUnavailableException(
                                                message: "Service Unavailable at the moment.",
                                                headers: new Headers()
                                                {
                                                    ActivityId = System.Diagnostics.Trace.CorrelationManager.ActivityId.ToString(),
                                                    SubStatusCode = SubStatusCodes.TransportGenerated503
                                                },
                                                trace: NoOpTrace.Singleton,
                                                innerException: null);


                retryResult = await policy.ShouldRetryAsync(exception, default);
            }

            policy.OnBeforeSendRequest(request);

            // Assert.
            FieldInfo fieldInfo = policy
                .GetType()
                .GetField(
                    name: "retryContext",
                    bindingAttr: BindingFlags.Instance | BindingFlags.NonPublic);

            MetadataRetryContext retryContext = (MetadataRetryContext)fieldInfo
                .GetValue(
                    obj: policy);

            Assert.IsNotNull(retryResult);
            Assert.AreEqual(true, retryResult.ShouldRetry, "MetadataRequestThrottleRetryPolicy should return true since the sub status code indicates to retry the request in the next preferred read region.");
            Assert.AreEqual(1, retryContext.RetryLocationIndex, "Indicates that the retry location index was incremented.");
            Assert.AreEqual(routedServiceEndpoint, request.RequestContext.LocationEndpointToRoute);
        }

        [TestMethod]
        [DataRow(1)]
        [DataRow(2)]
        public async Task ShouldRetryAsync_UsesReconciledRegionCountForUnavailableEndpointRetries(
            int accountRegionCount)
        {
            Uri eastUsEndpoint = new Uri("https://eastus.documents.azure.com/");
            Uri westUsEndpoint = new Uri("https://westus.documents.azure.com/");
            Collection<AccountRegion> readableRegions = new Collection<AccountRegion>
            {
                new AccountRegion { Name = Regions.EastUS, Endpoint = eastUsEndpoint.ToString() }
            };
            if (accountRegionCount > 1)
            {
                readableRegions.Add(
                    new AccountRegion { Name = Regions.WestUS, Endpoint = westUsEndpoint.ToString() });
            }

            AccountProperties databaseAccount = new AccountProperties
            {
                WriteLocationsInternal = new Collection<AccountRegion>
                {
                    new AccountRegion { Name = Regions.EastUS, Endpoint = eastUsEndpoint.ToString() }
                },
                ReadLocationsInternal = readableRegions
            };
            Mock<IDocumentClientInternal> owner = new Mock<IDocumentClientInternal>();
            owner.Setup(client => client.ServiceEndpoint).Returns(eastUsEndpoint);
            owner.Setup(client => client.GetDatabaseAccountInternalAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(databaseAccount);

            ConnectionPolicy connectionPolicy = new ConnectionPolicy();
            connectionPolicy.SetCurrentLocation(Regions.EastUS);

            using GlobalEndpointManager endpointManager = new GlobalEndpointManager(owner.Object, connectionPolicy);
            endpointManager.InitializeAccountPropertiesAndStartBackgroundRefresh(databaseAccount);
            Assert.AreEqual(accountRegionCount, endpointManager.PreferredLocationCount);

            MetadataRequestThrottleRetryPolicy policy = new MetadataRequestThrottleRetryPolicy(
                endpointManager,
                maxRetryAttemptsOnThrottledRequests: 0);
            using DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.ReadFeed,
                "test-collection",
                ResourceType.PartitionKeyRange,
                AuthorizationTokenType.PrimaryMasterKey,
                new Documents.Collections.RequestNameValueCollection());
            policy.OnBeforeSendRequest(request);

            CosmosException exception = CosmosExceptionFactory.CreateServiceUnavailableException(
                message: "Service unavailable",
                headers: new Headers
                {
                    ActivityId = Guid.NewGuid().ToString(),
                    SubStatusCode = SubStatusCodes.TransportGenerated503
                },
                trace: NoOpTrace.Singleton,
                innerException: null);

            for (int retryAttempt = 0; retryAttempt < accountRegionCount; retryAttempt++)
            {
                ShouldRetryResult retryResult = await policy.ShouldRetryAsync(exception, CancellationToken.None);
                Assert.IsTrue(retryResult.ShouldRetry);
                policy.OnBeforeSendRequest(request);
            }

            ShouldRetryResult exhaustedResult = await policy.ShouldRetryAsync(exception, CancellationToken.None);
            Assert.IsFalse(exhaustedResult.ShouldRetry);
        }

        [TestMethod]
        public async Task ShouldRetryAsync_ThrottledRetryAfterUnavailableEndpointStaysOnCurrentEndpoint()
        {
            Uri eastUsEndpoint = new Uri("https://eastus.documents.azure.com/");
            Uri westUsEndpoint = new Uri("https://westus.documents.azure.com/");
            Uri northEuropeEndpoint = new Uri("https://northeurope.documents.azure.com/");
            AccountProperties databaseAccount = new AccountProperties
            {
                WriteLocationsInternal = new Collection<AccountRegion>
                {
                    new AccountRegion { Name = Regions.EastUS, Endpoint = eastUsEndpoint.ToString() }
                },
                ReadLocationsInternal = new Collection<AccountRegion>
                {
                    new AccountRegion { Name = Regions.EastUS, Endpoint = eastUsEndpoint.ToString() },
                    new AccountRegion { Name = Regions.WestUS, Endpoint = westUsEndpoint.ToString() },
                    new AccountRegion { Name = Regions.NorthEurope, Endpoint = northEuropeEndpoint.ToString() }
                }
            };
            Mock<IDocumentClientInternal> owner = new Mock<IDocumentClientInternal>();
            owner.Setup(client => client.ServiceEndpoint).Returns(eastUsEndpoint);

            ConnectionPolicy connectionPolicy = new ConnectionPolicy();
            connectionPolicy.SetPreferredLocations(
                new[] { Regions.EastUS, Regions.WestUS, Regions.NorthEurope });

            using GlobalEndpointManager endpointManager = new GlobalEndpointManager(owner.Object, connectionPolicy);
            endpointManager.InitializeAccountPropertiesAndStartBackgroundRefresh(databaseAccount);

            MetadataRequestThrottleRetryPolicy policy = new MetadataRequestThrottleRetryPolicy(
                endpointManager,
                maxRetryAttemptsOnThrottledRequests: 1);
            using DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.ReadFeed,
                "test-collection",
                ResourceType.PartitionKeyRange,
                AuthorizationTokenType.PrimaryMasterKey,
                new Documents.Collections.RequestNameValueCollection());

            policy.OnBeforeSendRequest(request);
            Assert.AreEqual(eastUsEndpoint, request.RequestContext.LocationEndpointToRoute);

            using ResponseMessage unavailableResponse = new ResponseMessage(HttpStatusCode.ServiceUnavailable);
            unavailableResponse.Headers.SubStatusCode = SubStatusCodes.TransportGenerated503;
            ShouldRetryResult unavailableRetry = await policy.ShouldRetryAsync(
                unavailableResponse,
                CancellationToken.None);

            Assert.IsTrue(unavailableRetry.ShouldRetry);
            policy.OnBeforeSendRequest(request);
            Assert.AreEqual(westUsEndpoint, request.RequestContext.LocationEndpointToRoute);

            using ResponseMessage throttledResponse = new ResponseMessage(HttpStatusCode.TooManyRequests);
            throttledResponse.Headers.Set(
                HttpConstants.HttpHeaders.RetryAfterInMilliseconds,
                "0");
            ShouldRetryResult retryResult = await policy.ShouldRetryAsync(
                throttledResponse,
                CancellationToken.None);

            Assert.IsTrue(retryResult.ShouldRetry);
            policy.OnBeforeSendRequest(request);
            Assert.AreEqual(westUsEndpoint, request.RequestContext.LocationEndpointToRoute);
        }

        [TestMethod]
        public async Task ShouldRetryAsync_RankingRefreshRetriesUnattemptedRegion()
        {
            Uri eastUsEndpoint = new Uri("https://eastus.documents.azure.com/");
            Uri westUsEndpoint = new Uri("https://westus.documents.azure.com/");
            Uri northEuropeEndpoint = new Uri("https://northeurope.documents.azure.com/");
            Collection<AccountRegion> regions = new Collection<AccountRegion>
            {
                new AccountRegion { Name = Regions.EastUS, Endpoint = eastUsEndpoint.ToString() },
                new AccountRegion { Name = Regions.WestUS, Endpoint = westUsEndpoint.ToString() },
                new AccountRegion { Name = Regions.NorthEurope, Endpoint = northEuropeEndpoint.ToString() }
            };
            AccountProperties initialAccount = new AccountProperties
            {
                WriteLocationsInternal = new Collection<AccountRegion>
                {
                    new AccountRegion { Name = Regions.EastUS, Endpoint = eastUsEndpoint.ToString() }
                },
                ReadLocationsInternal = regions,
                AdditionalProperties = new Dictionary<string, Newtonsoft.Json.Linq.JToken>
                {
                    { Constants.Properties.RegionProximity, Newtonsoft.Json.Linq.JArray.FromObject(new[] { "eastus", "westus", "northeurope" }) }
                }
            };
            AccountProperties refreshedAccount = new AccountProperties
            {
                WriteLocationsInternal = initialAccount.WriteLocationsInternal,
                ReadLocationsInternal = regions,
                AdditionalProperties = new Dictionary<string, Newtonsoft.Json.Linq.JToken>
                {
                    { Constants.Properties.RegionProximity, Newtonsoft.Json.Linq.JArray.FromObject(new[] { "eastus", "northeurope", "westus" }) }
                }
            };
            Mock<IDocumentClientInternal> owner = new Mock<IDocumentClientInternal>();
            owner.Setup(client => client.ServiceEndpoint).Returns(eastUsEndpoint);
            owner.Setup(client => client.GetDatabaseAccountInternalAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(refreshedAccount);

            ConnectionPolicy connectionPolicy = new ConnectionPolicy();
            connectionPolicy.SetCurrentLocation(Regions.EastUS);

            using GlobalEndpointManager endpointManager = new GlobalEndpointManager(
                owner.Object,
                connectionPolicy);
            endpointManager.InitializeAccountPropertiesAndStartBackgroundRefresh(initialAccount);

            MetadataRequestThrottleRetryPolicy policy = new MetadataRequestThrottleRetryPolicy(
                endpointManager,
                maxRetryAttemptsOnThrottledRequests: 0);
            using DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.ReadFeed,
                "test-collection",
                ResourceType.PartitionKeyRange,
                AuthorizationTokenType.PrimaryMasterKey,
                new Documents.Collections.RequestNameValueCollection());

            CosmosException exception = CosmosExceptionFactory.CreateServiceUnavailableException(
                message: "Service unavailable",
                headers: new Headers
                {
                    ActivityId = Guid.NewGuid().ToString(),
                    SubStatusCode = SubStatusCodes.TransportGenerated503
                },
                trace: NoOpTrace.Singleton,
                innerException: null);

            policy.OnBeforeSendRequest(request);
            Assert.AreEqual(eastUsEndpoint, request.RequestContext.LocationEndpointToRoute);

            ShouldRetryResult retryResult = await policy.ShouldRetryAsync(exception, CancellationToken.None);
            Assert.IsTrue(retryResult.ShouldRetry);
            policy.OnBeforeSendRequest(request);
            Assert.AreEqual(westUsEndpoint, request.RequestContext.LocationEndpointToRoute);

            await endpointManager.RefreshLocationAsync(forceRefresh: true);

            retryResult = await policy.ShouldRetryAsync(exception, CancellationToken.None);
            Assert.IsTrue(retryResult.ShouldRetry);
            policy.OnBeforeSendRequest(request);
            Assert.AreEqual(northEuropeEndpoint, request.RequestContext.LocationEndpointToRoute);
        }
    }
}