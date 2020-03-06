//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CosmosClientTests
    {
        public const string AccountEndpoint = "https://localhost:8081/";
        public const string ConnectionString = "AccountEndpoint=https://localtestcosmos.documents.azure.com:443/;AccountKey=425Mcv8CXQqzRNCgFNjIhT424GK99CKJvASowTnq15Vt8LeahXTcN5wt3342vQ==;";

        [TestMethod]
        [ExpectedException(typeof(ObjectDisposedException))]
        public void TestDispose()
        {
            CosmosClient cosmosClient = new CosmosClient(ConnectionString);
            // Dispose should be idempotent 
            cosmosClient.Dispose();
            cosmosClient.Dispose();
            cosmosClient.GetContainer("asdf", "asdf");
        }
    }
}
