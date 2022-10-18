//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Globalization;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Cosmos.Linq;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.Utils;
    using Microsoft.Azure.Documents;
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
        public void RetryExceedingMaxTimeLimit()
        {
            Mock<IStoreModelExtension> mockStoreModel = new Mock<IStoreModelExtension>();
            mockStoreModel.Setup(model => model.ProcessMessageAsync(It.IsAny<DocumentServiceRequest>(), default(CancellationToken)))
                .Throws(this.CreateTooManyRequestException(100));

            ConnectionPolicy connectionPolicy = new ConnectionPolicy() 
            { 
                EnableEndpointDiscovery = false,
                RetryOptions = new RetryOptions { MaxRetryAttemptsOnThrottledRequests = 100, MaxRetryWaitTimeInSeconds = 1 }
            };

            DocumentClient client = new DocumentClient(
                new Uri(ConfigurationManager.AppSettings["GatewayEndpoint"]),
                ConfigurationManager.AppSettings["MasterKey"],
                (HttpMessageHandler)null,
                connectionPolicy);

            client.GetDatabaseAccountAsync().Wait();

            int expectedExecutionTimes = 11;

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
        }

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

            DocumentClient client = new (
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

            DocumentClient client = new (
                new Uri(ConfigurationManager.AppSettings["GatewayEndpoint"]),
                ConfigurationManager.AppSettings["MasterKey"],
                (HttpMessageHandler)null,
                connectionPolicy);

            client.GetDatabaseAccountAsync().Wait();
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
            DocumentClient client = new DocumentClient(
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