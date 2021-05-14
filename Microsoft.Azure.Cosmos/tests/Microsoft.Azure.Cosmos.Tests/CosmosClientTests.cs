//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net;
    using System.Net.Http;
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
                    Assert.IsTrue(e.Message.Contains($"CosmosClient Endpoint: https://localtestcosmos.documents.azure.com/; Created at: {cosmosClient.ClientConfigurationTraceDatum.ClientCreatedDateTimeUtc.ToString("o", CultureInfo.InvariantCulture)};  Disposed at: {cosmosClient.DisposedDateTimeUtc.Value.ToString("o", CultureInfo.InvariantCulture)}; UserAgent: {userAgent};"));
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

        [TestMethod]
        public void InvalidConnectionString()
        {
            Assert.ThrowsException<ArgumentException>(() => new CosmosClient(""));
            Assert.ThrowsException<ArgumentNullException>(() => new CosmosClient(null));
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
