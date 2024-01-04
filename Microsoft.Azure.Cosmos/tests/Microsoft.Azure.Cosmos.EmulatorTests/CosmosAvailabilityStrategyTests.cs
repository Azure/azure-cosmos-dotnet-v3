namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure.Core;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using PartitionKey = PartitionKey;

    [TestClass]
    public class CosmosAvailabilityStrategyTests
    {

        private CosmosClient client = null;

        [TestInitialize]
        public void TestInitialize()
        {
           
            (string endpoint, string authKey) = TestCommon.GetAccountInfo();
            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                ConnectionMode = ConnectionMode.Direct,
                ApplicationPreferredRegions = new List<string>() { "East US", "West US" },
                AvailabilityStrategyOptions = new AvailabilityStrategyOptions(
                    new ParallelHedging(
                        threshold: TimeSpan.FromMilliseconds(100), 
                        step: TimeSpan.FromMilliseconds(50)))
            };

            this.client = new CosmosClient(
                accountEndpoint: endpoint,
                authKeyOrResourceToken: authKey,
                clientOptions: clientOptions);
        }

        [TestCleanup]
        public void Cleanup()
        {
            this.client.Dispose();
        }

        [TestMethod]
        public async Task AvailabilityStrategyNoTriggerTest()
        {
            AccountProperties accountProperties = new AccountProperties()
            {
                ReadLocationsInternal = new Collection<AccountRegion>()
                    {
                        new AccountRegion()
                        {
                            Name = "East US",
                            Endpoint = "https://eastus.documents.azure.com:443/"
                        },
                        new AccountRegion()
                        {
                            Name = "West US",
                            Endpoint = "https://westus.documents.azure.com:443/"
                        }
                    },
                WriteLocationsInternal = new Collection<AccountRegion>()
                    {
                        new AccountRegion()
                        {
                            Name = "South Central US",
                            Endpoint = "https://127.0.0.1:8081/"
                        },
                    },
                EnableMultipleWriteLocations = false
            };

            Mock<IDocumentClientInternal> mockedClient = new Mock<IDocumentClientInternal>();
            mockedClient.Setup(owner => owner.ServiceEndpoint).Returns(new Uri("https://default.documents.azure.com:443/"));
            mockedClient.Setup(owner => owner.GetDatabaseAccountInternalAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>())).ReturnsAsync(accountProperties);

            ConnectionPolicy connectionPolicy = new ConnectionPolicy()
            {
                EnableEndpointDiscovery = false,
                UseMultipleWriteLocations = false,
            };
            connectionPolicy.PreferredLocations.Add("East US");
            connectionPolicy.PreferredLocations.Add("West US");
            
            ResponseMessage responseMessageOK = new ResponseMessage(HttpStatusCode.OK);
            ResponseMessage responseMessageServiceUnavailable = new ResponseMessage(HttpStatusCode.ServiceUnavailable);

            this.client.DocumentClient.GlobalEndpointManager.UpdateLocationCache(accountProperties);

            Mock<RequestInvokerHandler> mockRequestInvokerHandler = new Mock<RequestInvokerHandler>(
                this.client,
                null);
            mockRequestInvokerHandler.SetupSequence(x =>
                x.BaseSendAsync(
                    It.IsAny<RequestMessage>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.Delay(TimeSpan.FromMilliseconds(50)).ContinueWith(_ => responseMessageOK))
                .Returns(Task.FromResult(responseMessageServiceUnavailable));

            RequestMessage requestMessage = new RequestMessage(
                HttpMethod.Get,
                new Uri("https://eastus.documents.azure.com:443/testDb/test/1"))
            {
                ResourceType = ResourceType.Document,
                OperationType = OperationType.Read,
                DatabaseId = "testDb",
                ContainerId = "test"
            };

            requestMessage.Headers.Add(HttpConstants.HttpHeaders.PartitionKey, "\"1\"");

            CancellationToken cancellationToken = new CancellationToken();
            ResponseMessage rm = await this.client.ClientOptions.AvailabilityStrategyOptions.AvailabilityStrategy.ExecuteAvailablityStrategyAsync(mockRequestInvokerHandler.Object,
                this.client,
                requestMessage,
                cancellationToken);
            
            Assert.AreEqual(HttpStatusCode.OK, rm.StatusCode);
        }

        [TestMethod]
        public async Task AvailabilityStrategyTriggerTest()
        {
            AccountProperties accountProperties = new AccountProperties()
            {
                ReadLocationsInternal = new Collection<AccountRegion>()
                    {
                        new AccountRegion()
                        {
                            Name = "East US",
                            Endpoint = "https://eastus.documents.azure.com:443/"
                        },
                        new AccountRegion()
                        {
                            Name = "West US",
                            Endpoint = "https://westus.documents.azure.com:443/"
                        }
                    },
                WriteLocationsInternal = new Collection<AccountRegion>()
                    {
                        new AccountRegion()
                        {
                            Name = "South Central US",
                            Endpoint = "https://127.0.0.1:8081/"
                        },
                    },
                EnableMultipleWriteLocations = false
            };

            Mock<IDocumentClientInternal> mockedClient = new Mock<IDocumentClientInternal>();
            mockedClient.Setup(owner => owner.ServiceEndpoint).Returns(new Uri("https://default.documents.azure.com:443/"));
            mockedClient.Setup(owner => owner.GetDatabaseAccountInternalAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>())).ReturnsAsync(accountProperties);

            ConnectionPolicy connectionPolicy = new ConnectionPolicy()
            {
                EnableEndpointDiscovery = false,
                UseMultipleWriteLocations = false,
            };
            connectionPolicy.PreferredLocations.Add("East US");
            connectionPolicy.PreferredLocations.Add("West US");

            ResponseMessage responseMessageOK = new ResponseMessage(HttpStatusCode.OK);
            ResponseMessage responseMessageServiceUnavailable = new ResponseMessage(HttpStatusCode.ServiceUnavailable);

            this.client.DocumentClient.GlobalEndpointManager.UpdateLocationCache(accountProperties);

            Mock<RequestInvokerHandler> mockRequestInvokerHandler = new Mock<RequestInvokerHandler>(
                this.client,
                null);
            mockRequestInvokerHandler.SetupSequence(x =>
                x.BaseSendAsync(
                    It.IsAny<RequestMessage>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.Delay(TimeSpan.FromMilliseconds(150)).ContinueWith(_ => responseMessageServiceUnavailable))
                .Returns(Task.FromResult(responseMessageOK));

            RequestMessage requestMessage = new RequestMessage(
                HttpMethod.Get,
                new Uri("https://eastus.documents.azure.com:443/testDb/test/1"))
            {
                ResourceType = ResourceType.Document,
                OperationType = OperationType.Read,
                DatabaseId = "testDb",
                ContainerId = "test"
            };

            requestMessage.Headers.Add(HttpConstants.HttpHeaders.PartitionKey, "\"1\"");

            CancellationToken cancellationToken = new CancellationToken();
            ResponseMessage rm = await this.client.ClientOptions.AvailabilityStrategyOptions.AvailabilityStrategy.ExecuteAvailablityStrategyAsync(mockRequestInvokerHandler.Object,
                this.client,
                requestMessage,
                cancellationToken);

            Assert.AreEqual(HttpStatusCode.OK, rm.StatusCode);
        }

        [TestMethod]
        public async Task AvailabilityStrategyTriggerPrimaryReturnTest()
        {
            AccountProperties accountProperties = new AccountProperties()
            {
                ReadLocationsInternal = new Collection<AccountRegion>()
                    {
                        new AccountRegion()
                        {
                            Name = "East US",
                            Endpoint = "https://eastus.documents.azure.com:443/"
                        },
                        new AccountRegion()
                        {
                            Name = "West US",
                            Endpoint = "https://westus.documents.azure.com:443/"
                        }
                    },
                WriteLocationsInternal = new Collection<AccountRegion>()
                    {
                        new AccountRegion()
                        {
                            Name = "South Central US",
                            Endpoint = "https://127.0.0.1:8081/"
                        },
                    },
                EnableMultipleWriteLocations = false
            };

            Mock<IDocumentClientInternal> mockedClient = new Mock<IDocumentClientInternal>();
            mockedClient.Setup(owner => owner.ServiceEndpoint).Returns(new Uri("https://default.documents.azure.com:443/"));
            mockedClient.Setup(owner => owner.GetDatabaseAccountInternalAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>())).ReturnsAsync(accountProperties);

            ConnectionPolicy connectionPolicy = new ConnectionPolicy()
            {
                EnableEndpointDiscovery = false,
                UseMultipleWriteLocations = false,
            };
            connectionPolicy.PreferredLocations.Add("East US");
            connectionPolicy.PreferredLocations.Add("West US");

            ResponseMessage responseMessageOK = new ResponseMessage(HttpStatusCode.OK);
            ResponseMessage responseMessageServiceUnavailable = new ResponseMessage(HttpStatusCode.ServiceUnavailable);

            this.client.DocumentClient.GlobalEndpointManager.UpdateLocationCache(accountProperties);

            Mock<RequestInvokerHandler> mockRequestInvokerHandler = new Mock<RequestInvokerHandler>(
                this.client,
                null);
            mockRequestInvokerHandler.SetupSequence(x =>
                x.BaseSendAsync(
                    It.IsAny<RequestMessage>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.Delay(TimeSpan.FromMilliseconds(115))
                    .ContinueWith(_ => responseMessageOK))
                .Returns(Task.Delay(TimeSpan.FromMilliseconds(125))
                .ContinueWith( _ => responseMessageServiceUnavailable));

            RequestMessage requestMessage = new RequestMessage(
                HttpMethod.Get,
                new Uri("https://eastus.documents.azure.com:443/testDb/test/1"))
            {
                ResourceType = ResourceType.Document,
                OperationType = OperationType.Read,
                DatabaseId = "testDb",
                ContainerId = "test"
            };

            requestMessage.Headers.Add(HttpConstants.HttpHeaders.PartitionKey, "\"1\"");

            CancellationToken cancellationToken = new CancellationToken();
            ResponseMessage rm = await this.client.ClientOptions.AvailabilityStrategyOptions.AvailabilityStrategy.ExecuteAvailablityStrategyAsync(mockRequestInvokerHandler.Object,
                this.client,
                requestMessage,
                cancellationToken);

            Assert.AreEqual(HttpStatusCode.OK, rm.StatusCode);
        }

        [TestMethod]
        public async Task AvailabilityStrategyStepTest()
        {
            AccountProperties accountProperties = new AccountProperties()
            {
                ReadLocationsInternal = new Collection<AccountRegion>()
                    {
                        new AccountRegion()
                        {
                            Name = "East US",
                            Endpoint = "https://eastus.documents.azure.com:443/"
                        },
                        new AccountRegion()
                        {
                            Name = "West US",
                            Endpoint = "https://westus.documents.azure.com:443/"
                        },
                        new AccountRegion()
                        {
                            Name = "South Central US",
                            Endpoint = "https://southcentralus.documents.azure.com:443/"
                        }
                    },
                WriteLocationsInternal = new Collection<AccountRegion>()
                    {
                        new AccountRegion()
                        {
                            Name = "South Central US",
                            Endpoint = "https://127.0.0.1:8081/"
                        },
                    },
                EnableMultipleWriteLocations = false
            };

            Mock<IDocumentClientInternal> mockedClient = new Mock<IDocumentClientInternal>();
            mockedClient.Setup(owner => owner.ServiceEndpoint).Returns(new Uri("https://default.documents.azure.com:443/"));
            mockedClient.Setup(owner => owner.GetDatabaseAccountInternalAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>())).ReturnsAsync(accountProperties);

            ConnectionPolicy connectionPolicy = new ConnectionPolicy()
            {
                EnableEndpointDiscovery = false,
                UseMultipleWriteLocations = false,
            };
            connectionPolicy.PreferredLocations.Add("East US");
            connectionPolicy.PreferredLocations.Add("West US");
            connectionPolicy.PreferredLocations.Add("South Central US");

            ResponseMessage responseMessageOK = new ResponseMessage(HttpStatusCode.OK);
            ResponseMessage responseMessageServiceUnavailable = new ResponseMessage(HttpStatusCode.ServiceUnavailable);
            ResponseMessage responseMessageBadRequest = new ResponseMessage(HttpStatusCode.BadRequest);

            this.client.DocumentClient.GlobalEndpointManager.UpdateLocationCache(accountProperties);

            Mock<RequestInvokerHandler> mockRequestInvokerHandler = new Mock<RequestInvokerHandler>(
                this.client,
                null);
            mockRequestInvokerHandler.SetupSequence(x =>
                x.BaseSendAsync(
                    It.IsAny<RequestMessage>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.Delay(TimeSpan.FromMilliseconds(200))
                    .ContinueWith(_ => responseMessageServiceUnavailable))
                .Returns(Task.Delay(TimeSpan.FromMilliseconds(200))
                    .ContinueWith(_ => responseMessageBadRequest))
                .Returns(Task.FromResult(responseMessageOK));

            RequestMessage requestMessage = new RequestMessage(
                HttpMethod.Get,
                new Uri("https://eastus.documents.azure.com:443/testDb/test/1"))
            {
                ResourceType = ResourceType.Document,
                OperationType = OperationType.Read,
                DatabaseId = "testDb",
                ContainerId = "test"
            };

            requestMessage.Headers.Add(HttpConstants.HttpHeaders.PartitionKey, "\"1\"");

            CancellationToken cancellationToken = new CancellationToken();
            ResponseMessage rm = await this.client.ClientOptions.AvailabilityStrategyOptions.AvailabilityStrategy.ExecuteAvailablityStrategyAsync(
                mockRequestInvokerHandler.Object,
                this.client,
                requestMessage,
                cancellationToken);

            Assert.AreEqual(HttpStatusCode.OK, rm.StatusCode);
        }

        [TestMethod]
        public void AvailabilityStrategyDisableOverideTest()
        {
            RequestInvokerHandler requestInvokerHandler = new RequestInvokerHandler(
                this.client,
                null);

            RequestOptions requestOptions = new RequestOptions()
            {
                AvailabilityStrategyOptions = new AvailabilityStrategyOptions(
                    new DisabledStrategy(),
                    enabled: false)
            };

            RequestMessage requestMessage = new RequestMessage(
                HttpMethod.Get,
                new Uri("https://eastus.documents.azure.com:443/testDb/test/1"))
            {
                ResourceType = ResourceType.Document,
                OperationType = OperationType.Read,
                DatabaseId = "testDb",
                ContainerId = "test",
                RequestOptions = requestOptions
            };

            requestMessage.Headers.Add(HttpConstants.HttpHeaders.PartitionKey, "\"1\"");

            bool shouldHedge = requestInvokerHandler.ShouldHedge(requestMessage);

            Assert.IsFalse(shouldHedge);
        }

        [TestMethod]
        public void RequestMessageCloneTests()
        {
            RequestMessage httpRequest = new RequestMessage(
                HttpMethod.Get,
                new Uri("/dbs/testdb/colls/testcontainer/docs/testId", UriKind.Relative));

            string key = Guid.NewGuid().ToString();
            Dictionary<string, object> properties = new Dictionary<string, object>()
            {
                { key, Guid.NewGuid() }
            };

            RequestOptions requestOptions = new RequestOptions()
            {
                Properties = properties
            };

            httpRequest.RequestOptions = requestOptions;
            httpRequest.ResourceType = ResourceType.Document;
            httpRequest.OperationType = OperationType.Read;
            httpRequest.Headers.CorrelatedActivityId = Guid.NewGuid().ToString();
            httpRequest.PartitionKeyRangeId = new PartitionKeyRangeIdentity("0", "1");
            httpRequest.UseGatewayMode = true;
            httpRequest.OnBeforeSendRequestActions = (request) => { };
            httpRequest.ContainerId = "testcontainer";
            httpRequest.DatabaseId = "testdb";

            RequestMessage clone = httpRequest.Clone();

            Assert.AreEqual(httpRequest.RequestOptions.Properties, clone.RequestOptions.Properties);
            Assert.AreEqual(httpRequest.ResourceType, clone.ResourceType);
            Assert.AreEqual(httpRequest.OperationType, clone.OperationType);
            Assert.AreEqual(httpRequest.Headers.CorrelatedActivityId, clone.Headers.CorrelatedActivityId);
            Assert.AreEqual(httpRequest.PartitionKeyRangeId, clone.PartitionKeyRangeId);
            Assert.AreEqual(httpRequest.UseGatewayMode, clone.UseGatewayMode);
            Assert.AreEqual(httpRequest.OnBeforeSendRequestActions, clone.OnBeforeSendRequestActions);
            Assert.AreEqual(httpRequest.ContainerId, clone.ContainerId);
            Assert.AreEqual(httpRequest.DatabaseId, clone.DatabaseId);
        }
    }
}
