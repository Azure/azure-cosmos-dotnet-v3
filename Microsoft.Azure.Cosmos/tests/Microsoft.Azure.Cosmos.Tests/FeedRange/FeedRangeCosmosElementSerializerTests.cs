//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.FeedRange
{
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public sealed class FeedRangeCosmosElementSerializerTests
    {
        [TestMethod]
        public void LogicalPartitionKey()
        {
            Cosmos.PartitionKey somePartitionKey = new Cosmos.PartitionKey(42);
            FeedRangeInternal feedRange = new FeedRangePartitionKey(somePartitionKey);
            AssertRoundTrip(feedRange);
        }

        [TestMethod]
        public void PhysicalPartitionKeyRangeId()
        {
            int physicalPkRangeId = 0;
            FeedRangeInternal feedRange = new FeedRangePartitionKeyRange(physicalPkRangeId.ToString());
            AssertRoundTrip(feedRange);
        }

        [TestMethod]
        public void EffectivePartitionKeyRange()
        {
            Microsoft.Azure.Documents.Routing.Range<string> range = new Microsoft.Azure.Documents.Routing.Range<string>(
                min: "A",
                max: "B",
                isMinInclusive: true,
                isMaxInclusive: false);

            FeedRangeInternal feedRange = new FeedRangeEpk(range);
            AssertRoundTrip(feedRange);
        }

        private static void AssertRoundTrip(FeedRangeInternal feedRange)
        {
            CosmosElement cosmosElement = FeedRangeCosmosElementSerializer.ToCosmosElement(feedRange);
            TryCatch<FeedRangeInternal> monad = FeedRangeCosmosElementSerializer.MonadicCreateFromCosmosElement(cosmosElement);
            Assert.IsTrue(monad.Succeeded);
            Assert.AreEqual(feedRange.ToJsonString(), monad.Result.ToJsonString());
        }
    }
}