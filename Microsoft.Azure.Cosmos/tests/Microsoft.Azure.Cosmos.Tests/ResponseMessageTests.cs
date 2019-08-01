//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ResponseMessageTests
    {
        [TestMethod]
        public void IsFeedOperation_ForDocumentReads()
        {
            RequestMessage request = new RequestMessage();
            request.OperationType = OperationType.ReadFeed;
            request.ResourceType = ResourceType.Document;
            Assert.IsTrue(request.IsPartitionKeyRangeHandlerRequired);
        }

        [TestMethod]
        public void IsFeedOperation_ForConflictReads()
        {
            RequestMessage request = new RequestMessage();
            request.OperationType = OperationType.ReadFeed;
            request.ResourceType = ResourceType.Conflict;
            Assert.IsTrue(request.IsPartitionKeyRangeHandlerRequired);
        }

        [TestMethod]
        public void IsFeedOperation_ForChangeFeed()
        {
            RequestMessage request = new RequestMessage();
            request.OperationType = OperationType.ReadFeed;
            request.ResourceType = ResourceType.Document;
            request.PartitionKeyRangeId = new PartitionKeyRangeIdentity("something");
            Assert.IsFalse(request.IsPartitionKeyRangeHandlerRequired);
        }

        [TestMethod]
        public void IsFeedOperation_ForOtherOperations()
        {
            RequestMessage request = new RequestMessage();
            request.OperationType = OperationType.Upsert;
            request.ResourceType = ResourceType.Document;
            Assert.IsFalse(request.IsPartitionKeyRangeHandlerRequired);

            RequestMessage request2 = new RequestMessage();
            request2.OperationType = OperationType.ReadFeed;
            request2.ResourceType = ResourceType.Database;
            Assert.IsFalse(request2.IsPartitionKeyRangeHandlerRequired);
        }
    }
}
