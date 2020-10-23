//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
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
                () => container.GetItemQueryIterator<dynamic>(queryText: "select * from T").ReadNextAsync(),
                () => container.GetItemQueryIterator<dynamic>().ReadNextAsync(),
            };

            foreach (Func<Task> asyncFunc in validateAsync)
            {
                try
                {
                   await asyncFunc();
                    Assert.Fail("Should throw ObjectDisposedException");
                }
                catch (ObjectDisposedException) { }
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

            // Validate that setting it to null does throw an argument exception
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
