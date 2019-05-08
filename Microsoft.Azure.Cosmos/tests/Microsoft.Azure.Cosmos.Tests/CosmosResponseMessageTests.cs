//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CosmosResponseMessageTests
    {
        [TestMethod]
        public void IsFeedOperation_ForDocumentReads()
        {
            CosmosRequestMessage request = new CosmosRequestMessage();
            request.OperationType = OperationType.ReadFeed;
            request.ResourceType = ResourceType.Document;
            Assert.IsTrue(request.IsFeedOperation);
        }

        [TestMethod]
        public void IsFeedOperation_ForConflictReads()
        {
            CosmosRequestMessage request = new CosmosRequestMessage();
            request.OperationType = OperationType.ReadFeed;
            request.ResourceType = ResourceType.Conflict;
            Assert.IsTrue(request.IsFeedOperation);
        }

        [TestMethod]
        public void IsFeedOperation_ForChangeFeed()
        {
            CosmosRequestMessage request = new CosmosRequestMessage();
            request.OperationType = OperationType.ReadFeed;
            request.ResourceType = ResourceType.Document;
            request.PartitionKeyRangeId = "something";
            Assert.IsFalse(request.IsFeedOperation);
        }

        [TestMethod]
        public void IsFeedOperation_ForOtherOperations()
        {
            CosmosRequestMessage request = new CosmosRequestMessage();
            request.OperationType = OperationType.Upsert;
            request.ResourceType = ResourceType.Document;
            Assert.IsFalse(request.IsFeedOperation);

            CosmosRequestMessage request2 = new CosmosRequestMessage();
            request2.OperationType = OperationType.ReadFeed;
            request2.ResourceType = ResourceType.Database;
            Assert.IsFalse(request2.IsFeedOperation);
        }
    }
}
