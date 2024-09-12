//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.Diagnostics;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure.Core;
    using Microsoft.Azure.Cosmos.Client.Tests;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class ResourceThrottleRetryPolicyTests
    {
        private static readonly Uri DefaultEndpoint = new ("https://default.documents.azure.com");
        private readonly List<TraceListener> existingListener = new List<TraceListener>();
        private SourceSwitch existingSourceSwitch;

        [TestInitialize]
        public void CaptureCurrentTraceConfiguration()
        {
            foreach (TraceListener listener in DefaultTrace.TraceSource.Listeners)
            {
                this.existingListener.Add(listener);
            }

            DefaultTrace.TraceSource.Listeners.Clear();
            this.existingSourceSwitch = DefaultTrace.TraceSource.Switch;
        }

        [TestCleanup]
        public void ResetTraceConfiguration()
        {
            DefaultTrace.TraceSource.Listeners.Clear();
            foreach (TraceListener listener in this.existingListener)
            {
                DefaultTrace.TraceSource.Listeners.Add(listener);
            }

            DefaultTrace.TraceSource.Switch = this.existingSourceSwitch;
        }

        [TestMethod]
        public async Task DoesNotSerializeExceptionOnTracingDisabled()
        {
            Mock<IDocumentClientInternal> mockedClient = new();
            GlobalEndpointManager endpointManager = new(mockedClient.Object, new ConnectionPolicy());

            // No listeners
            ResourceThrottleRetryPolicy policy = new ResourceThrottleRetryPolicy(0, endpointManager);
            CustomException exception = new CustomException();
            await policy.ShouldRetryAsync(exception, default);
            Assert.AreEqual(0, exception.ToStringCount, "Exception was serialized");
        }

        [TestMethod]
        public async Task DoesSerializeExceptionOnTracingEnabled()
        {
            Mock<IDocumentClientInternal> mockedClient = new();
            GlobalEndpointManager endpointManager = new(mockedClient.Object, new ConnectionPolicy());

            // Let the default trace listener
            DefaultTrace.TraceSource.Switch = new SourceSwitch("ClientSwitch", "Error");
            DefaultTrace.TraceSource.Listeners.Add(new DefaultTraceListener());
            ResourceThrottleRetryPolicy policy = new ResourceThrottleRetryPolicy(0, endpointManager);
            CustomException exception = new CustomException();
            await policy.ShouldRetryAsync(exception, default);
            Assert.AreEqual(1, exception.ToStringCount, "Exception was not serialized");
        }

        [TestMethod]
        [DataRow(true, DisplayName = "Validate retry policy with multi master write account.")]
        [DataRow(false, DisplayName = "Validate retry policy with single master write account.")]
        public async Task ShouldRetryAsync_WhenResourceNotAvailableThrown_ShouldThrow503OnMultiMasterWrite(
            bool isMultiMasterAccount)
        {
            Documents.Collections.INameValueCollection requestHeaders = new Documents.Collections.DictionaryNameValueCollection();

            GlobalEndpointManager endpointManager = await this.InitializeEndpointManager(
                useMultipleWriteLocations: isMultiMasterAccount,
                enableEndpointDiscovery: true,
                isPreferredLocationsListEmpty: false,
                enforceSingleMasterSingleWriteLocation: !isMultiMasterAccount);

            ResourceThrottleRetryPolicy policy = new (0, endpointManager);

            DocumentServiceRequest request = new(
                OperationType.Create,
                ResourceType.Document,
                "dbs/db/colls/coll1/docs/doc1",
                null,
                AuthorizationTokenType.PrimaryMasterKey,
                requestHeaders);

            policy.OnBeforeSendRequest(request);

            DocumentClientException dce = new (
                "SystemResourceUnavailable: 429 with 3092 occurred.",
                HttpStatusCode.TooManyRequests,
                SubStatusCodes.SystemResourceUnavailable);

            ShouldRetryResult shouldRetryResult = await policy.ShouldRetryAsync(dce, default);

            if (isMultiMasterAccount)
            {
                Assert.IsFalse(shouldRetryResult.ShouldRetry);
                Assert.IsNotNull(shouldRetryResult.ExceptionToThrow);
                Assert.AreEqual(typeof(ServiceUnavailableException), shouldRetryResult.ExceptionToThrow.GetType());
            }
            else
            {
                Assert.IsFalse(shouldRetryResult.ShouldRetry);
                Assert.IsNull(shouldRetryResult.ExceptionToThrow);
            }
        }

        private async Task<GlobalEndpointManager> InitializeEndpointManager(
            bool useMultipleWriteLocations,
            bool enableEndpointDiscovery,
            bool isPreferredLocationsListEmpty,
            bool enforceSingleMasterSingleWriteLocation = false, // Some tests depend on the Initialize to create an account with multiple write locations, even when not multi master
            ReadOnlyCollection<string> preferedRegionListOverride = null,
            bool isExcludeRegionsTest = false)
        {
            ReadOnlyCollection<string> preferredLocations;
            AccountProperties databaseAccount = ResourceThrottleRetryPolicyTests.CreateDatabaseAccount(
                useMultipleWriteLocations,
                enforceSingleMasterSingleWriteLocation,
                isExcludeRegionsTest);

            if (isPreferredLocationsListEmpty)
            {
                preferredLocations = new List<string>().AsReadOnly();
            }
            else
            {
                // Allow for override at the test method level if needed
                preferredLocations = preferedRegionListOverride ?? new List<string>()
                {
                    "location1",
                    "location2",
                    "location3"
                }.AsReadOnly();
            }

            Mock<IDocumentClientInternal> mockedClient = new Mock<IDocumentClientInternal>();
            mockedClient.Setup(owner => owner.ServiceEndpoint).Returns(ResourceThrottleRetryPolicyTests.DefaultEndpoint);
            mockedClient.Setup(owner => owner.GetDatabaseAccountInternalAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>())).ReturnsAsync(databaseAccount);

            ConnectionPolicy connectionPolicy = new ConnectionPolicy()
            {
                EnableEndpointDiscovery = enableEndpointDiscovery,
                UseMultipleWriteLocations = useMultipleWriteLocations,
            };

            foreach (string preferredLocation in preferredLocations)
            {
                connectionPolicy.PreferredLocations.Add(preferredLocation);
            }

            GlobalEndpointManager endpointManager = new GlobalEndpointManager(mockedClient.Object, connectionPolicy);
            await endpointManager.RefreshLocationAsync(false);

            return endpointManager;
        }

        private static AccountProperties CreateDatabaseAccount(
            bool useMultipleWriteLocations,
            bool enforceSingleMasterSingleWriteLocation,
            bool isExcludeRegionsTest = false)
        {
            Uri Location1Endpoint = new ("https://location1.documents.azure.com");
            Uri Location2Endpoint = new ("https://location2.documents.azure.com");
            Uri Location3Endpoint = new ("https://location3.documents.azure.com");
            Uri Location4Endpoint = new ("https://location4.documents.azure.com");

            Collection<AccountRegion> writeLocations = isExcludeRegionsTest ?

                new Collection<AccountRegion>()
                {
                    { new AccountRegion() { Name = "default", Endpoint = ResourceThrottleRetryPolicyTests.DefaultEndpoint.ToString() } },
                    { new AccountRegion() { Name = "location1", Endpoint = Location1Endpoint.ToString() } },
                    { new AccountRegion() { Name = "location2", Endpoint = Location2Endpoint.ToString() } },
                    { new AccountRegion() { Name = "location3", Endpoint = Location3Endpoint.ToString() } },
                } :
                new Collection<AccountRegion>()
                {
                    { new AccountRegion() { Name = "location1", Endpoint = Location1Endpoint.ToString() } },
                    { new AccountRegion() { Name = "location2", Endpoint = Location2Endpoint.ToString() } },
                    { new AccountRegion() { Name = "location3", Endpoint = Location3Endpoint.ToString() } },
                };

            if (!useMultipleWriteLocations
                && enforceSingleMasterSingleWriteLocation)
            {
                // Some pre-existing tests depend on the account having multiple write locations even on single master setup
                // Newer tests can correctly define a single master account (single write region) without breaking existing tests
                writeLocations = isExcludeRegionsTest ?
                    new Collection<AccountRegion>()
                    {
                        { new AccountRegion() { Name = "default", Endpoint = ResourceThrottleRetryPolicyTests.DefaultEndpoint.ToString() } }
                    } :
                    new Collection<AccountRegion>()
                    {
                        { new AccountRegion() { Name = "location1", Endpoint = Location1Endpoint.ToString() } }
                    };
            }

            AccountProperties databaseAccount = new ()
            {
                EnableMultipleWriteLocations = useMultipleWriteLocations,
                ReadLocationsInternal = isExcludeRegionsTest ?
                    new Collection<AccountRegion>()
                    {
                        { new AccountRegion() { Name = "default", Endpoint = ResourceThrottleRetryPolicyTests.DefaultEndpoint.ToString() } },
                        { new AccountRegion() { Name = "location1", Endpoint = Location1Endpoint.ToString() } },
                        { new AccountRegion() { Name = "location2", Endpoint = Location2Endpoint.ToString() } },
                        { new AccountRegion() { Name = "location4", Endpoint = Location4Endpoint.ToString() } },
                    } :
                    new Collection<AccountRegion>()
                    {
                        { new AccountRegion() { Name = "location1", Endpoint = Location1Endpoint.ToString() } },
                        { new AccountRegion() { Name = "location2", Endpoint = Location2Endpoint.ToString() } },
                        { new AccountRegion() { Name = "location4", Endpoint = Location4Endpoint.ToString() } },
                    },
                WriteLocationsInternal = writeLocations
            };

            return databaseAccount;
        }

        private class CustomException : Exception
        {
            public int ToStringCount { get; private set; } = 0;

            public override string ToString()
            {
                ++this.ToStringCount;
                return string.Empty;
            }
        }
    }
}
