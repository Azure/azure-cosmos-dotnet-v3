//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
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

        [TestMethod]
        public void IsFeedOperation_ForFeedTokenEPKRange()
        {
            RequestMessage request = new RequestMessage();
            request.OperationType = OperationType.ReadFeed;
            request.ResourceType = ResourceType.Document;
            FeedTokenInternal feedTokenEPKRange = new FeedTokenEPKRange(Guid.NewGuid().ToString(), new PartitionKeyRange() { MinInclusive = "AA", MaxExclusive = "BB", Id = "0" });
            feedTokenEPKRange.EnrichRequest(request);
            Assert.IsTrue(request.IsPartitionKeyRangeHandlerRequired);
        }

        [TestMethod]
        public void IsFeedOperation_ForFeedTokenPartitionKeyRange()
        {
            RequestMessage request = new RequestMessage();
            request.OperationType = OperationType.ReadFeed;
            request.ResourceType = ResourceType.Document;
            FeedTokenInternal feedTokenEPKRange = new FeedTokenPartitionKeyRange("0");
            feedTokenEPKRange.EnrichRequest(request);
            Assert.IsFalse(request.IsPartitionKeyRangeHandlerRequired);
        }

        [TestMethod]
        public void IsFeedOperation_ForFeedTokenPartitionKey()
        {
            RequestMessage request = new RequestMessage();
            request.OperationType = OperationType.ReadFeed;
            request.ResourceType = ResourceType.Document;
            FeedTokenInternal feedTokenEPKRange = new FeedTokenPartitionKey(new Cosmos.PartitionKey("0"));
            feedTokenEPKRange.EnrichRequest(request);
            Assert.IsFalse(request.IsPartitionKeyRangeHandlerRequired);
        }
    }
}
