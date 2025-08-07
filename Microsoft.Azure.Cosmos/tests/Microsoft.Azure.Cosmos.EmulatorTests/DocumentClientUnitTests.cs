//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Globalization;
    using System.Net;
    using System.Net.Http;
    using System.Net.Security;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Cosmos.Linq;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.Tests;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Utils;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public sealed class DocumentClientUnitTests
    {
        [TestMethod]
        public void DefaultRetryOnThrottled()
        {
            this.TestRetryOnThrottled(null);
        }

        [TestMethod]
        public void RetryOnThrottledOverride()
        {
            this.TestRetryOnThrottled(2);
        }

        [TestMethod]
        public void NoRetryOnThrottledOverride()
        {
            this.TestRetryOnThrottled(0);
        }

        [TestMethod]
        public async Task RetryExceedingMaxTimeLimit()
        {
            Mock<IStoreModelExtension> mockStoreModel = new Mock<IStoreModelExtension>();
            mockStoreModel.Setup(model => model.ProcessMessageAsync(It.IsAny<DocumentServiceRequest>(), default(CancellationToken)))
                .Throws(this.CreateTooManyRequestException(100));

            ConnectionPolicy connectionPolicy = new ConnectionPolicy() 
            { 
                EnableEndpointDiscovery = false,
                RetryOptions = new RetryOptions { MaxRetryAttemptsOnThrottledRequests = 100, MaxRetryWaitTimeInSeconds = 1 }
            };

            using DocumentClient client = new DocumentClient(
                new Uri(ConfigurationManager.AppSettings["GatewayEndpoint"]),
                ConfigurationManager.AppSettings["MasterKey"],
                (HttpMessageHandler)null,
                connectionPolicy);

            await client.EnsureValidClientAsync(NoOpTrace.Singleton);
            await client.GetDatabaseAccountAsync();

            int expectedExecutionTimes = 11;

            client.StoreModel = mockStoreModel.Object;
            client.GatewayStoreModel = mockStoreModel.Object;
            bool throttled = false;
            try
            {
                Database db = new Database { Id = "test db 1" };
                await client.CreateDatabaseAsync(db);
            }
            catch (DocumentClientException docExp)
            {
                Assert.AreEqual((HttpStatusCode)429, docExp.StatusCode);
                throttled = true;
            }

            mockStoreModel.Verify(model => model.ProcessMessageAsync(It.IsAny<DocumentServiceRequest>(), default(CancellationToken)), Times.Exactly(expectedExecutionTimes));
            Assert.IsTrue(throttled);
        }

        /// <summary>
        /// Test to validate that when <see cref="DocumentClient.OpenConnectionsToAllReplicasAsync()"/> invoked with
        /// an empty database/ container name, a <see cref="ArgumentNullException"/> is thrown during cosmos client
        /// initialization.
        /// </summary>
        [TestMethod]
        [Owner("dkunda")]
        public async Task OpenConnectionsToAllReplicasAsync_WithEmptyDatabaseName_ShouldThrowExceptionDuringInitialization()
        {
            // Arrange.
            ConnectionPolicy connectionPolicy = new ()
            {
                EnableEndpointDiscovery = false,
                RetryOptions = new RetryOptions { MaxRetryAttemptsOnThrottledRequests = 100, MaxRetryWaitTimeInSeconds = 1 }
            };

            using DocumentClient client = new (
                new Uri(ConfigurationManager.AppSettings["GatewayEndpoint"]),
                ConfigurationManager.AppSettings["MasterKey"],
                (HttpMessageHandler)null,
                connectionPolicy);

            // Act.
            ArgumentNullException ane = await Assert.ThrowsExceptionAsync<ArgumentNullException>(() => client.OpenConnectionsToAllReplicasAsync(
                databaseName: string.Empty,
                containerLinkUri: "https://replica.cosmos.com/test",
                cancellationToken: default));

            // Assert.
            Assert.IsNotNull(ane);
            Assert.AreEqual("Value cannot be null. (Parameter 'databaseName')", ane.Message);
        }

        /// <summary>
        /// Test to validate that when <see cref="DocumentClient.OpenConnectionsToAllReplicasAsync()"/> invoked and
        /// the store model throws some internal exception, the exception is indeed bubbled up and thrown during
        /// cosmos client initialization.
        /// </summary>
        [TestMethod]
        [Owner("dkunda")]
        public async Task OpenConnectionsToAllReplicasAsync_WhenStoreModelThrowsInternalException_ShouldThrowExceptionDuringInitialization()
        {
            // Arrange.
            string exceptionMessage = "Internal Server Error";
            Mock<IStoreModelExtension> mockStoreModel = new ();
            mockStoreModel
                .Setup(model => model.OpenConnectionsToAllReplicasAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Throws(new Exception(exceptionMessage));

            ConnectionPolicy connectionPolicy = new ()
            {
                EnableEndpointDiscovery = false,
                RetryOptions = new RetryOptions { MaxRetryAttemptsOnThrottledRequests = 100, MaxRetryWaitTimeInSeconds = 1 }
            };

            using DocumentClient client = new (
                new Uri(ConfigurationManager.AppSettings["GatewayEndpoint"]),
                ConfigurationManager.AppSettings["MasterKey"],
                (HttpMessageHandler)null,
                connectionPolicy);

            await client.EnsureValidClientAsync(NoOpTrace.Singleton);
            await client.GetDatabaseAccountAsync();
            client.StoreModel = mockStoreModel.Object;
            client.GatewayStoreModel = mockStoreModel.Object;

            // Act.
            Exception ex = await Assert.ThrowsExceptionAsync<Exception>(() => client.OpenConnectionsToAllReplicasAsync(
                databaseName: "some-valid-database",
                containerLinkUri: "https://replica.cosmos.com/test",
                cancellationToken: default));

            // Assert.
            Assert.IsNotNull(ex);
            Assert.AreEqual(exceptionMessage, ex.Message);
            mockStoreModel.Verify(x => x.OpenConnectionsToAllReplicasAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task QueryPartitionProviderSingletonTestAsync()
        {
            using DocumentClient client = new DocumentClient(
                new Uri(ConfigurationManager.AppSettings["GatewayEndpoint"]),
                ConfigurationManager.AppSettings["MasterKey"],
                (HttpMessageHandler)null,
                new ConnectionPolicy());

            Task<QueryPartitionProvider> queryPartitionProviderTaskOne = client.QueryPartitionProvider;
            Task<QueryPartitionProvider> queryPartitionProviderTaskTwo = client.QueryPartitionProvider;
            Assert.AreSame(queryPartitionProviderTaskOne, queryPartitionProviderTaskTwo, "QueryPartitionProvider property is not a singleton");
            Assert.AreSame(await queryPartitionProviderTaskOne, await queryPartitionProviderTaskTwo, "QueryPartitionProvider property is not a singleton");
        }

        private void TestRetryOnThrottled(int? numberOfRetries)
        {
            Mock<IStoreModelExtension> mockStoreModel = new Mock<IStoreModelExtension>();
            mockStoreModel.Setup(model => model.ProcessMessageAsync(It.IsAny<DocumentServiceRequest>(), default(CancellationToken)))
                .Throws(this.CreateTooManyRequestException(100));

            ConnectionPolicy connectionPolicy = new ConnectionPolicy() 
            { 
                EnableEndpointDiscovery = false,
            };

            if (numberOfRetries != null)
            {
                connectionPolicy.RetryOptions = new RetryOptions { MaxRetryAttemptsOnThrottledRequests = numberOfRetries.Value };
            }

            DocumentClient client = new DocumentClient(
                new Uri(ConfigurationManager.AppSettings["GatewayEndpoint"]),
                ConfigurationManager.AppSettings["MasterKey"],
                (HttpMessageHandler)null,
                connectionPolicy);

            client.EnsureValidClientAsync(NoOpTrace.Singleton).Wait();
            client.GetDatabaseAccountAsync().Wait();

            int expectedExecutionTimes = numberOfRetries + 1 ?? 10;

            client.StoreModel = mockStoreModel.Object;
            client.GatewayStoreModel = mockStoreModel.Object;
            bool throttled = false;
            try
            {
                Database db = new Database { Id = "test db 1" };
                client.CreateDatabaseAsync(db).Wait();
            }
            catch (Exception exp)
            {
                DocumentClientException docExp = exp.InnerException as DocumentClientException;
                Assert.AreEqual((HttpStatusCode)429, docExp.StatusCode);
                throttled = true;
            }

            mockStoreModel.Verify(model => model.ProcessMessageAsync(It.IsAny<DocumentServiceRequest>(), default(CancellationToken)), Times.Exactly(expectedExecutionTimes));
            Assert.IsTrue(throttled);

            throttled = false;
            try
            {
                client.ReadDatabaseAsync("/dbs/id1").Wait();
            }
            catch (Exception exp)
            {
                DocumentClientException docExp = exp.InnerException as DocumentClientException;
                Assert.AreEqual((HttpStatusCode)429, docExp.StatusCode);
                throttled = true;
            }

            mockStoreModel.Verify(model => model.ProcessMessageAsync(It.IsAny<DocumentServiceRequest>(), default(CancellationToken)), Times.Exactly(2 * expectedExecutionTimes));
            Assert.IsTrue(throttled);

            throttled = false;
            try
            {
                client.DeleteDocumentCollectionAsync("dbs/db_rid/colls/col_rid/").Wait();
            }
            catch (Exception exp)
            {
                DocumentClientException docExp = exp.InnerException as DocumentClientException;
                Assert.AreEqual((HttpStatusCode)429, docExp.StatusCode);
                throttled = true;
            }

            mockStoreModel.Verify(model => model.ProcessMessageAsync(It.IsAny<DocumentServiceRequest>(), default(CancellationToken)), Times.Exactly(3 * expectedExecutionTimes));
            Assert.IsTrue(throttled);

            throttled = false;
            try
            {
                client.CreateDatabaseQuery("SELECT * FROM r").AsDocumentQuery().ExecuteNextAsync().Wait();
            }
            catch (Exception exp)
            {
                DocumentClientException docExp = exp.InnerException as DocumentClientException;
                Assert.AreEqual((HttpStatusCode)429, docExp.StatusCode);
                throttled = true;
            }

            mockStoreModel.Verify(model => model.ProcessMessageAsync(It.IsAny<DocumentServiceRequest>(), default(CancellationToken)), Times.Exactly(4 * expectedExecutionTimes));
            Assert.IsTrue(throttled);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void EnableNRegionSynchronousCommit_PassedToStoreClient(bool nRegionCommitEnabled)
        {

            StoreClient storeClient = new StoreClient(
                        new Mock<IAddressResolver>().Object,
                        new SessionContainer(string.Empty),
                        new Mock<IServiceConfigurationReader>().Object,
                        new Mock<IAuthorizationTokenProvider>().Object,
                        Protocol.Tcp,
                        new Mock<TransportClient>().Object);
            // Arrange
            Mock<IStoreClientFactory> mockStoreClientFactory = new Mock<IStoreClientFactory>();
            mockStoreClientFactory.Setup(f => f.CreateStoreClient(
                It.IsAny<IAddressResolver>(),
                It.IsAny<ISessionContainer>(),
                It.IsAny<IServiceConfigurationReader>(),
                It.IsAny<IAuthorizationTokenProvider>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<AccountConfigurationProperties>(),
                It.IsAny<ISessionRetryOptions>()
            )).Returns(storeClient);

            DocumentClient documentClient = new DocumentClient(
                new Uri("https://localhost:8081"),
                new Mock<AuthorizationTokenProvider>().Object,
                new EventHandler<SendingRequestEventArgs>((s, e) => { }),
                new ConnectionPolicy(),
                null, // desiredConsistencyLevel
                null, // serializerSettings
                ApiType.None,
                new EventHandler<ReceivedResponseEventArgs>((s, e) => { }),
                null, // handler
                new Mock<ISessionContainer>().Object,
                null, // enableCpuMonitor
                new Func<TransportClient, TransportClient>(tc => tc),
                mockStoreClientFactory.Object,
                false, // isLocalQuorumConsistency
                "testClientId",
                new RemoteCertificateValidationCallback((sender, certificate, chain, sslPolicyErrors) => true),
                new Mock<CosmosClientTelemetryOptions>().Object,
                new Mock<IChaosInterceptorFactory>().Object,
                true // enableAsyncCacheExceptionNoSharing
            );

            AccountProperties accountProperties = new AccountProperties
            {
                // Set the property to true for test
                EnableNRegionSynchronousCommit = nRegionCommitEnabled,
            };

            AccountConsistency ac = new AccountConsistency();
            ac.DefaultConsistencyLevel = (Cosmos.ConsistencyLevel) ConsistencyLevel.Session;
            accountProperties.Consistency = ac;

            Func<Task<AccountProperties>> getDatabaseAccountFn = () =>
                // When called with any Uri, return the expected AccountProperties
                Task.FromResult(accountProperties);

            CosmosAccountServiceConfiguration accountServiceConfiguration = new CosmosAccountServiceConfiguration(
                getDatabaseAccountFn);

            typeof(CosmosAccountServiceConfiguration)
                .GetProperty("AccountProperties", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(accountServiceConfiguration, accountProperties);

            //Inject the accountServiceConfiguration into the DocumentClient via reflection.
            typeof(DocumentClient)
                .GetProperty("accountServiceConfiguration", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(documentClient, accountServiceConfiguration);


            typeof(DocumentClient)
                .GetField("storeClientFactory", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(documentClient, mockStoreClientFactory.Object);

            // Act: Call the private method via reflection
            typeof(DocumentClient)
                .GetMethod("CreateStoreModel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(documentClient, new object[] { true });

            // Assert: Verify the correct value was passed
            mockStoreClientFactory.Verify(f =>
                f.CreateStoreClient(
                    It.IsAny<IAddressResolver>(),
                    It.IsAny<ISessionContainer>(),
                    It.IsAny<IServiceConfigurationReader>(),
                    It.IsAny<IAuthorizationTokenProvider>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.Is<AccountConfigurationProperties>(config => config.EnableNRegionSynchronousCommit == accountProperties.EnableNRegionSynchronousCommit),
                    It.IsAny<ISessionRetryOptions>()),
                Times.Once,
                "EnableNRegionSynchronousCommit was not passed correctly to AccountConfigurationProperties and StoreClient.");
        }

        private DocumentClientException CreateTooManyRequestException(int retryAfterInMilliseconds)
        {
            HttpResponseMessage responseMessage = new HttpResponseMessage();
            responseMessage.Headers.Add(HttpConstants.HttpHeaders.RetryAfterInMilliseconds, retryAfterInMilliseconds.ToString(CultureInfo.InvariantCulture));
            responseMessage.Headers.Add(HttpConstants.HttpHeaders.ActivityId, Guid.NewGuid().ToString());

            Error error = new Error() { Code = "429", Message = "Message: {'Errors':['Request rate is large']}" };

            return new DocumentClientException(error, responseMessage.Headers, (HttpStatusCode)429);
        }
    }
}