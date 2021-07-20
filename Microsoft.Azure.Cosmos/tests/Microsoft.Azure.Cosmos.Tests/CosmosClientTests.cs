//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class CosmosClientTests
    {
        public const string AccountEndpoint = "https://localhost:8081/";
        public const string ConnectionString = "AccountEndpoint=https://localtestcosmos.documents.azure.com:443/;AccountKey=425Mcv8CXQqzRNCgFNjIhT424GK99CKJvASowTnq15Vt8LeahXTcN5wt3342vQ==;";

        [TestMethod]
        public async Task TestDispose()
        {
            CosmosClient cosmosClient = new CosmosClient(ConnectionString);
            Database database = cosmosClient.GetDatabase("asdf");
            Container container = cosmosClient.GetContainer("asdf", "asdf");
            TransactionalBatch batch = container.CreateTransactionalBatch(new PartitionKey("asdf"));
            batch.ReadItem("Test");

            FeedIterator<dynamic> feedIterator1 = container.GetItemQueryIterator<dynamic>();
            FeedIterator<dynamic> feedIterator2 = container.GetItemQueryIterator<dynamic>(queryText: "select * from T");
            FeedIterator<dynamic> feedIterator3 = database.GetContainerQueryIterator<dynamic>(queryText: "select * from T");

            string userAgent = cosmosClient.ClientContext.UserAgent;
            // Dispose should be idempotent 
            cosmosClient.Dispose();
            cosmosClient.Dispose();

            List<Func<Task>> validateAsync = new List<Func<Task>>()
            {
                () => cosmosClient.ReadAccountAsync(),
                () => cosmosClient.CreateDatabaseAsync("asdf"),
                () => database.CreateContainerAsync("asdf", "/pkpathasdf", 200),
                () => container.ReadItemAsync<dynamic>("asdf", new PartitionKey("test")),
                () => container.Scripts.ReadStoredProcedureAsync("asdf"),
                () => container.Scripts.ReadTriggerAsync("asdf"),
                () => container.Scripts.ReadUserDefinedFunctionAsync("asdf"),
                () => batch.ExecuteAsync(),
                () => feedIterator1.ReadNextAsync(),
                () => feedIterator2.ReadNextAsync(),
                () => feedIterator3.ReadNextAsync(),
            };

            foreach (Func<Task> asyncFunc in validateAsync)
            {
                try
                {
                    await asyncFunc();
                    Assert.Fail("Should throw ObjectDisposedException");
                }
                catch (CosmosObjectDisposedException e) 
                { 
                    string expectedMessage = $"Cannot access a disposed 'CosmosClient'. Follow best practices and use the CosmosClient as a singleton." +
                        $" CosmosClient was disposed at: {cosmosClient.DisposedDateTimeUtc.Value.ToString("o", CultureInfo.InvariantCulture)}; CosmosClient Endpoint: https://localtestcosmos.documents.azure.com/; Created at: {cosmosClient.ClientConfigurationTraceDatum.ClientCreatedDateTimeUtc.ToString("o", CultureInfo.InvariantCulture)}; UserAgent: {userAgent};";
                    Assert.IsTrue(e.Message.Contains(expectedMessage));
                    string diagnostics = e.Diagnostics.ToString();
                    Assert.IsNotNull(diagnostics);
                    Assert.IsFalse(diagnostics.Contains("NoOp"));
                    Assert.IsTrue(diagnostics.Contains("Client Configuration"));
                    string exceptionString = e.ToString();
                    Assert.IsTrue(exceptionString.Contains(diagnostics));
                    Assert.IsTrue(exceptionString.Contains(e.Message));
                    Assert.IsTrue(exceptionString.Contains(e.StackTrace));
                }
            }
        }

        [DataTestMethod]
        [DataRow(null, "425Mcv8CXQqzRNCgFNjIhT424GK99CKJvASowTnq15Vt8LeahXTcN5wt3342vQ==")]
        [DataRow(AccountEndpoint, null)]
        [DataRow("", "425Mcv8CXQqzRNCgFNjIhT424GK99CKJvASowTnq15Vt8LeahXTcN5wt3342vQ==")]
        [DataRow(AccountEndpoint, "")]
        [ExpectedException(typeof(ArgumentNullException))]
        public void InvalidEndpointAndKey(string endpoint, string key)
        {
            new CosmosClient(endpoint, key);
        }

        [DataTestMethod]
        [DataRow(null, "425Mcv8CXQqzRNCgFNjIhT424GK99CKJvASowTnq15Vt8LeahXTcN5wt3342vQ==")]
        [DataRow(AccountEndpoint, null)]
        [DataRow("", "425Mcv8CXQqzRNCgFNjIhT424GK99CKJvASowTnq15Vt8LeahXTcN5wt3342vQ==")]
        [DataRow(AccountEndpoint, "")]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Builder_InvalidEndpointAndKey(string endpoint, string key)
        {
            new CosmosClientBuilder(endpoint, key);
        }

        [DataTestMethod]
        [DataRow(AccountEndpoint, "425Mcv8CXQqzRNCgFNjIhT424GK88ckJvASowTnq15Vt8LeahXTcN5wt3342vQ==")]
        public async Task Builder_InvalidKey(string endpoint, string key)
        {
            CosmosClient client = new CosmosClient(endpoint, key);

            string sqlQueryText = "SELECT * FROM c";

            QueryDefinition queryDefinition = new QueryDefinition(sqlQueryText);
            FeedIterator<object> queryResultSetIterator = client.GetContainer(new Guid().ToString(), new Guid().ToString()).GetItemQueryIterator<object>(queryDefinition);

            while (queryResultSetIterator.HasMoreResults)
            {
                await queryResultSetIterator.ReadNextAsync();
            }
        }

        [TestMethod]
        public void InvalidConnectionString()
        {
            Assert.ThrowsException<ArgumentException>(() => new CosmosClient(""));
            Assert.ThrowsException<ArgumentNullException>(() => new CosmosClient(null));
        }

        [TestMethod]
        public async Task ValidateAuthorizationTokenProviderTestAsync()
        {
            string authKeyValue = "MockAuthKey";
            Mock<AuthorizationTokenProvider> mockAuth = new Mock<AuthorizationTokenProvider>(MockBehavior.Strict);
            mockAuth.Setup(x => x.Dispose());
            mockAuth.Setup(x => x.AddAuthorizationHeaderAsync(
                It.IsAny<Documents.Collections.INameValueCollection>(),
                It.IsAny<Uri>(),
                It.IsAny<string>(),
                It.IsAny<Documents.AuthorizationTokenType>()))
                .Callback<Documents.Collections.INameValueCollection, Uri, string, Documents.AuthorizationTokenType>(
                (headers, uri, verb, tokenType) => headers.Add(Documents.HttpConstants.HttpHeaders.Authorization, authKeyValue))
                .Returns(() => new ValueTask());

            bool validAuth = false;
            Exception exceptionToThrow = new Exception("TestException");
            Mock<IHttpHandler> mockHttpHandler = new Mock<IHttpHandler>();
            mockHttpHandler.Setup(x => x.SendAsync(
                It.IsAny<HttpRequestMessage>(), 
                It.IsAny<CancellationToken>()))
                .Callback<HttpRequestMessage, CancellationToken>(
                (request, cancellationToken) =>
                {
                    Assert.IsTrue(request.Headers.TryGetValues(Documents.HttpConstants.HttpHeaders.Authorization, out IEnumerable<string> authValues));
                    Assert.AreEqual(authKeyValue, authValues.First());
                    validAuth = true;
                }).Throws(exceptionToThrow);

            using CosmosClient client = new CosmosClient(
                "https://localhost:8081",
                authorizationTokenProvider: mockAuth.Object,
                new CosmosClientOptions()
                {
                    HttpClientFactory = () => new HttpClient(new HttpHandlerHelper(mockHttpHandler.Object)),
                });

            Container container = client.GetContainer("Test", "MockTest");

            try
            {
                await container.ReadItemAsync<ToDoActivity>(Guid.NewGuid().ToString(), new PartitionKey(Guid.NewGuid().ToString()));
            }
            catch (Exception e) when (object.ReferenceEquals(e, exceptionToThrow))
            { 
            }
            
            Assert.IsTrue(validAuth);
        }

        [TestMethod]
        public void Builder_InvalidConnectionString()
        {
            Assert.ThrowsException<ArgumentException>(() => new CosmosClientBuilder(""));
            Assert.ThrowsException<ArgumentNullException>(() => new CosmosClientBuilder(null));
        }

        [TestMethod]
        public void Builder_ValidateHttpFactory()
        {
            _ = new CosmosClientBuilder("<<endpoint-here>>", "<<key-here>>")
                .WithHttpClientFactory(() => new HttpClient())
                .WithConnectionModeGateway();

            // Validate that setting it to null does not throw an argument exception
            _ = new CosmosClientOptions()
            {
                HttpClientFactory = null,
                WebProxy = new Mock<IWebProxy>().Object,
            };

            _ = new CosmosClientOptions()
            {
                WebProxy = new Mock<IWebProxy>().Object,
                HttpClientFactory = null,
            };

            _ = new CosmosClientOptions()
            {
                WebProxy = null,
                HttpClientFactory = () => new HttpClient(),
            };

            _ = new CosmosClientOptions()
            {
                HttpClientFactory = () => new HttpClient(),
                WebProxy = null,
            };
        }
    }
}
