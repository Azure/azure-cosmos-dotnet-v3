//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Threading.Tasks;
    using System.Xml.Serialization;
    using VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ActiveClientDiagnosticTest
    {
        private const string ConnectionString = "AccountEndpoint=https://localtestcosmos.documents.azure.com:443/;AccountKey=425Mcv8CXQqzRNCgFNjIhT424GK99CKJvASowTnq15Vt8LeahXTcN5wt3342vQ==;";

        private int prevActiveClients = 0;

        [TestInitialize]
        public void Initialize()
        {
            this.prevActiveClients = CosmosClient.NumberOfActiveClients;
        }

        [TestMethod]
        public void SingleClientTest()
        {
            CosmosClient cosmosClient = new CosmosClient(ConnectionString);
            Assert.AreEqual(1 + this.prevActiveClients, CosmosClient.NumberOfActiveClients);
            cosmosClient.Dispose();
        }

        [TestMethod]
        public void MultiClientTest()
        {
            CosmosClient cosmosClient1 = new CosmosClient(ConnectionString); // Initializing 1st time
            CosmosClient cosmosClient2 = new CosmosClient(ConnectionString); // Initializing 2nd time
            Assert.AreEqual(2 + this.prevActiveClients, CosmosClient.NumberOfActiveClients);
            cosmosClient1.Dispose();
            cosmosClient2.Dispose();
        }

        [TestMethod]
        public void MultiClientWithDisposeTest()
        {
            CosmosClient cosmosClient1 = new CosmosClient(ConnectionString); // Initializing 1st time
            CosmosClient cosmosClient2 = new CosmosClient(ConnectionString); // Initializing 2nd time
            CosmosClient cosmosClient3 = new CosmosClient(ConnectionString); // Initializing 3rd time
            cosmosClient2.Dispose(); // Destroying 1 instance
            Assert.AreEqual(2 + this.prevActiveClients, CosmosClient.NumberOfActiveClients);
            cosmosClient1.Dispose();
            cosmosClient3.Dispose();
        }
    }
}
