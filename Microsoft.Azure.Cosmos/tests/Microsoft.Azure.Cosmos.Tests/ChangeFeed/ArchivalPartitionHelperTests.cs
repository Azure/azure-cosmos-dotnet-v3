//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Text;
    using System.Threading;
    using Microsoft.Azure.Cosmos.ChangeFeed.Pagination;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;
    using PartitionKeyRange = Documents.PartitionKeyRange;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class ArchivalPartitionHelperTests
    {
        [TestMethod]
        public void GetArchivalRanges_EmptyOverlappingRanges()
        {
            var sut = new ArchivalPartitionHelper();
            Assert.IsNull(sut.GetArchivalRanges("0", null, new CancellationToken(), null));
            Assert.IsNull(sut.GetArchivalRanges("0", new List<PartitionKeyRange>(), new CancellationToken(), null));
        }

        /// <summary>
        /// 1 -> 2
        ///   -> 3
        /// </summary>
        [TestMethod]
        public void GetArchivalRanges_OneSplit()
        {
            var parentPKRange = new PartitionKeyRange { Id = "1" };
            var left = new PartitionKeyRange { Id = "2", Parents = new Collection<string> { parentPKRange.Id } };
            var right = new PartitionKeyRange { Id = "3", Parents = new Collection<string> { parentPKRange.Id } };
            var overlappingRanges = new List<PartitionKeyRange> { left, right };

            var sut = new ArchivalPartitionHelper();
            List<FeedRangeArchivalPartition> ranges = sut.GetArchivalRanges(parentPKRange.Id, overlappingRanges, new CancellationToken(), null);
            Assert.AreEqual(1, ranges.Count);

            var range = ranges[0];
            Assert.AreEqual("1", range.DataRangeId);

            var visitor = new DumpSplitGraphVisitor();
            range.Accept(visitor);
            Assert.AreEqual(
                "{\"PartitionKeyRangeId\":1,\"HasParent\":false,\"Children\":[{\"PartitionKeyRangeId\":2,\"HasParent\":true,\"Children\":[]},{\"PartitionKeyRangeId\":3,\"HasParent\":true,\"Children\":[]}]}",
                visitor.Json);
        }

        /// <summary>
        /// 1 ->      2
        ///   -> 3 -> 4
        ///        -> 5
        /// and 1 gets the split
        /// </summary>
        [TestMethod]
        public void GetArchivalRanges_TwoSplitsAtRoot()
        {
            var rootPKRange = new PartitionKeyRange { Id = "1" };
            var left1 = new PartitionKeyRange { Id = "2", Parents = new Collection<string> { rootPKRange.Id } };
            var right1 = new PartitionKeyRange { Id = "3", Parents = new Collection<string> { rootPKRange.Id } };
            var left3 = new PartitionKeyRange { Id = "4", Parents = new Collection<string> { rootPKRange.Id, right1.Id } };
            var right3 = new PartitionKeyRange { Id = "5", Parents = new Collection<string> { rootPKRange.Id, right1.Id } };

            var overlappingRanges = new List<PartitionKeyRange> { left1, left3, right3 };

            var sut = new ArchivalPartitionHelper();
            List<FeedRangeArchivalPartition> ranges = sut.GetArchivalRanges(rootPKRange.Id, overlappingRanges, new CancellationToken(), null);
            Assert.AreEqual(1, ranges.Count);

            var range = ranges[0];
            Assert.AreEqual("1", range.DataRangeId);

            var visitor = new DumpSplitGraphVisitor();
            range.Accept(visitor);
            Assert.AreEqual(
                "{\"PartitionKeyRangeId\":1,\"HasParent\":false,\"Children\":[{\"PartitionKeyRangeId\":2,\"HasParent\":true,\"Children\":[]},{\"PartitionKeyRangeId\":3,\"HasParent\":true,\"Children\":[{\"PartitionKeyRangeId\":4,\"HasParent\":true,\"Children\":[]},{\"PartitionKeyRangeId\":5,\"HasParent\":true,\"Children\":[]}]}]}",
                visitor.Json);
        }

        /// <summary>
        /// 1 ->      2
        ///   -> 3 -> 4
        ///        -> 5
        /// and 3 gets the split
        /// </summary>
        [TestMethod]
        public void GetArchivalRanges_TwoSplitsAtMiddle()
        {
            var rootPKRange = new PartitionKeyRange { Id = "1" };
            var left1 = new PartitionKeyRange { Id = "2", Parents = new Collection<string> { rootPKRange.Id } };
            var right1 = new PartitionKeyRange { Id = "3", Parents = new Collection<string> { rootPKRange.Id } };
            var left3 = new PartitionKeyRange { Id = "4", Parents = new Collection<string> { rootPKRange.Id, right1.Id } };
            var right3 = new PartitionKeyRange { Id = "5", Parents = new Collection<string> { rootPKRange.Id, right1.Id } };

            var overlappingRanges = new List<PartitionKeyRange> { left3, right3 };

            var sut = new ArchivalPartitionHelper();
            List<FeedRangeArchivalPartition> ranges = sut.GetArchivalRanges(right1.Id, overlappingRanges, new CancellationToken(), null);
            Assert.AreEqual(1, ranges.Count);

            var range = ranges[0];
            Assert.AreEqual("3", range.DataRangeId);

            var visitor = new DumpSplitGraphVisitor();
            range.Accept(visitor);
            Assert.AreEqual(
                "{\"PartitionKeyRangeId\":3,\"HasParent\":true,\"Children\":[{\"PartitionKeyRangeId\":4,\"HasParent\":true,\"Children\":[]},{\"PartitionKeyRangeId\":5,\"HasParent\":true,\"Children\":[]}]}",
                visitor.Json);
        }

        class DumpSplitGraphVisitor : IFeedRangeVisitor
        {
            public string Json { get; internal set; }

            public void Visit(FeedRangePartitionKey feedRange) => throw new System.NotImplementedException();

            public void Visit(FeedRangePartitionKeyRange feedRange) => throw new System.NotImplementedException();

            public void Visit(FeedRangeEpk feedRange) => throw new System.NotImplementedException();

            /// <summary>
            /// Result like this (will be without formatting):
            /// {
            ///     "id": 1,
            ///     "children": [{ "id": 2, "hasArchivalId": 1 }, ...]
            /// }
            /// </summary>
            public void Visit(FeedRangeArchivalPartition feedRange)
            {
                JObject obj = (JObject)JToken.FromObject(feedRange.SplitGraph);
                this.Json = obj.ToString(Newtonsoft.Json.Formatting.None);
            }
        }
    }
}
