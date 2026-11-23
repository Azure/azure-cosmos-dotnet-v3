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

        [DataTestMethod]
        [DataRow("[\"tenant-a\"]")]
        [DataRow("[\"tenant-a\",null,42,true]")]
        public void PartialPartitionKeySurvivesBothSerializers(string partialKeyJson)
        {
            Assert.IsTrue(Cosmos.PartitionKey.TryParseJsonString(partialKeyJson, out Cosmos.PartitionKey partialKey));
            FeedRangeEpk feedRange = new FeedRangeEpk(
                new Documents.Routing.Range<string>("AA", "BB", true, false),
                partialKey);
            TryCatch<FeedRangeInternal> parsed = FeedRangeCosmosElementSerializer.MonadicCreateFromCosmosElement(
                FeedRangeCosmosElementSerializer.ToCosmosElement(feedRange));
            Assert.IsTrue(parsed.Succeeded);
            Assert.AreEqual(partialKey, ((FeedRangeEpk)parsed.Result).PartialPartitionKey);
            Assert.IsTrue(FeedRangeInternal.TryParse(feedRange.ToJsonString(), out FeedRangeInternal legacyParsed));
            Assert.AreEqual(partialKey, ((FeedRangeEpk)legacyParsed).PartialPartitionKey);
            Assert.AreNotEqual(new FeedRangeEpk(feedRange.Range), feedRange);
        }

        [DataTestMethod]
        [DataRow("null")]
        [DataRow("42")]
        [DataRow("\"not a partition key\"")]
        public void InvalidPartialPartitionKeyIsRejected(string value)
        {
            CosmosElement cosmosElement = CosmosElement.Parse(
                "{\"type\":\"Effective Partition Key Range\",\"value\":{\"min\":\"AA\",\"max\":\"BB\",\"partialPartitionKey\":" + value + "}}");
            Assert.IsTrue(FeedRangeCosmosElementSerializer.MonadicCreateFromCosmosElement(cosmosElement).Failed);
            Assert.IsFalse(FeedRangeInternal.TryParse(
                "{\"Range\":{\"min\":\"AA\",\"max\":\"BB\"},\"PartialPK\":" + value + "}",
                out _));
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