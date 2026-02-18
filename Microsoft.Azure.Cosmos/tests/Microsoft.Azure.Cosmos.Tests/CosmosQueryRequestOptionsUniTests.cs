//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class QueryRequestOptionsUniTests
    {
        [TestMethod]
        public void StatelessTest()
        {
            QueryRequestOptions requestOption = new QueryRequestOptions();

            RequestMessage testMessage = new RequestMessage();
            requestOption.PopulateRequestOptions(testMessage);

            Assert.IsNull(testMessage.Headers.ContinuationToken);
        }

        [TestMethod]
        public void PartitionKeyHeaderTest()
        {
            // Test with single partition key
            QueryRequestOptions requestOption = new QueryRequestOptions
            {
                PartitionKey = new PartitionKeyBuilder().Add("value1").Build()
            };

            RequestMessage testMessage = CreateRequestMessage();
            requestOption.PopulateRequestOptions(testMessage);

            Assert.IsNotNull(testMessage.Headers.PartitionKey);
            Assert.AreEqual("[\"value1\"]", testMessage.Headers.PartitionKey);

            testMessage = CreateRequestMessage(readFeed: true);
            requestOption.PopulateRequestOptions(testMessage);

            Assert.IsNull(testMessage.Headers.PartitionKey);
        }

        [TestMethod]
        public void PartialPartitionKeyHeaderTest()
        {
            // Test with partial partition key (1 out of 3 in hierarchical partition key)
            QueryRequestOptions requestOption = new QueryRequestOptions
            {
                PartitionKey = new PartitionKeyBuilder().Add("tenant1").Build()
            };

            RequestMessage testMessage = CreateRequestMessage();
            requestOption.PopulateRequestOptions(testMessage);

            Assert.IsNotNull(testMessage.Headers.PartitionKey);
            Assert.AreEqual("[\"tenant1\"]", testMessage.Headers.PartitionKey);
        }

        [TestMethod]
        public void PartialPartitionKeyHeaderTest_TwoComponents()
        {
            // Test with partial partition key (2 out of 3 in hierarchical partition key)
            QueryRequestOptions requestOption = new QueryRequestOptions
            {
                PartitionKey = new PartitionKeyBuilder().Add("tenant1").Add("user1").Build()
            };

            RequestMessage testMessage = CreateRequestMessage();
            requestOption.PopulateRequestOptions(testMessage);

            Assert.IsNotNull(testMessage.Headers.PartitionKey);
            Assert.AreEqual("[\"tenant1\",\"user1\"]", testMessage.Headers.PartitionKey);
        }

        [TestMethod]
        public void FullPartitionKeyHeaderTest()
        {
            // Test with full partition key (all 3 components in hierarchical partition key)
            QueryRequestOptions requestOption = new QueryRequestOptions
            {
                PartitionKey = new PartitionKeyBuilder().Add("tenant1").Add("user1").Add("session1").Build()
            };

            RequestMessage testMessage = CreateRequestMessage();
            requestOption.PopulateRequestOptions(testMessage);

            Assert.IsNotNull(testMessage.Headers.PartitionKey);
            Assert.AreEqual("[\"tenant1\",\"user1\",\"session1\"]", testMessage.Headers.PartitionKey);
        }

        [TestMethod]
        public void NoPartitionKeyHeaderTest()
        {
            // Test without partition key
            QueryRequestOptions requestOption = new QueryRequestOptions();

            RequestMessage testMessage = CreateRequestMessage();
            requestOption.PopulateRequestOptions(testMessage);

            Assert.IsNull(testMessage.Headers.PartitionKey);
        }

        [TestMethod]
        public void NonePartitionKeyHeaderTest()
        {
            // Test with PartitionKey.None
            QueryRequestOptions requestOption = new QueryRequestOptions
            {
                PartitionKey = PartitionKey.None
            };

            RequestMessage testMessage = CreateRequestMessage();
            requestOption.PopulateRequestOptions(testMessage);

            Assert.IsNull(testMessage.Headers.PartitionKey);
        }

        [TestMethod]
        public void PartitionKeyHeaderNotClobberedTest()
        {
            // Test that if the header is already present, PopulateRequestOptions doesn't clobber it
            string existingHeaderValue = "[\"existingValue\"]";
            
            RequestMessage testMessage = CreateRequestMessage();
            
            // Pre-populate the header
            testMessage.Headers.PartitionKey = existingHeaderValue;
            
            QueryRequestOptions requestOption = new QueryRequestOptions
            {
                PartitionKey = new PartitionKeyBuilder().Add("newValue").Build()
            };
            
            requestOption.PopulateRequestOptions(testMessage);

            // Verify that the existing header was NOT clobbered - it should remain unchanged
            Assert.IsNotNull(testMessage.Headers.PartitionKey);
            Assert.AreEqual(existingHeaderValue, testMessage.Headers.PartitionKey, 
                "PopulateRequestOptions should not clobber an existing PartitionKey header");
        }

        private static RequestMessage CreateRequestMessage(bool readFeed = false) => 
            new RequestMessage
            {
                ResourceType = ResourceType.Document,
                OperationType = readFeed? OperationType.ReadFeed : OperationType.Query
            };
    }
}