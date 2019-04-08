//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Linq;
    using System.Reflection;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CosmosResponseMessageTests
    {
        [TestMethod]
        public void IsDocumentFeed_ForDocumentReads()
        {
            CosmosRequestMessage request = new CosmosRequestMessage();
            request.OperationType = OperationType.ReadFeed;
            request.ResourceType = ResourceType.Document;
            Assert.IsTrue(request.IsDocumentFeedOperation);
        }

        [TestMethod]
        public void IsDocumentFeed_ForChangeFeed()
        {
            CosmosRequestMessage request = new CosmosRequestMessage();
            request.OperationType = OperationType.ReadFeed;
            request.ResourceType = ResourceType.Document;
            request.PartitionKeyRangeId = "something";
            Assert.IsFalse(request.IsDocumentFeedOperation);
        }

        [TestMethod]
        public void IsDocumentFeed_ForOtherOperations()
        {
            CosmosRequestMessage request = new CosmosRequestMessage();
            request.OperationType = OperationType.Upsert;
            request.ResourceType = ResourceType.Document;
            Assert.IsFalse(request.IsDocumentFeedOperation);

            CosmosRequestMessage request2 = new CosmosRequestMessage();
            request2.OperationType = OperationType.ReadFeed;
            request2.ResourceType = ResourceType.Database;
            Assert.IsFalse(request2.IsDocumentFeedOperation);
        }
    }
}
