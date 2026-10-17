//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query
{
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.SecondaryIndexRouting;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class PropertyPathTests
    {
        [TestMethod]
        public void ConstructorRetainsOriginalSegments()
        {
            string[] segments = { "address", "zip" };
            PropertyPath path = new PropertyPath(segments);

            segments[0] = "changed";

            Assert.AreSame(segments, path.Segments);
            CollectionAssert.AreEqual(new[] { "changed", "zip" }, path.Segments.ToArray());
        }

        [TestMethod]
        public void ConstructorRetainsEnumerableWithoutEnumerating()
        {
            int enumerationCount = 0;
            IEnumerable<string> segments = new[] { "address", "zip" }.Select(segment =>
            {
                enumerationCount++;
                return segment;
            });

            PropertyPath path = new PropertyPath(segments);

            Assert.AreSame(segments, path.Segments);
            Assert.AreEqual(0, enumerationCount);
            CollectionAssert.AreEqual(new[] { "address", "zip" }, path.Segments.ToArray());
            Assert.AreEqual(2, enumerationCount);
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow(" ")]
        [DataRow("a/b")]
        [DataRow("a.b")]
        [DataRow("a~1b")]
        [DataRow("a\"b")]
        [DataRow("a\\b")]
        [DataRow("\u0061")]
        [DataRow("\\u0061")]
        [DataRow("\n")]
        [DataRow("*")]
        public void ConstructorPreservesLiteralSegment(string segment)
        {
            PropertyPath path = new PropertyPath(new[] { segment });

            Assert.AreEqual(segment, path.Segments.Single());
        }

        [TestMethod]
        public void EqualPathsHaveEqualHashesAndSupportDictionaryLookup()
        {
            PropertyPath first = new PropertyPath(new[] { "address", "zip" });
            PropertyPath second = new PropertyPath(new List<string> { "address", "zip" });
            PropertyPath third = new PropertyPath(new[] { "address", "zip" }.Select(segment => segment));
            Dictionary<PropertyPath, PropertyPath> projections = new Dictionary<PropertyPath, PropertyPath>
            {
                [first] = new PropertyPath(new[] { "zip" }),
            };

            Assert.IsTrue(first.Equals(first));
            Assert.IsTrue(first.Equals(second));
            Assert.IsTrue(second.Equals(first));
            Assert.IsTrue(second.Equals(third));
            Assert.IsTrue(first.Equals(third));
            Assert.IsTrue(first.Equals((object)second));
            Assert.AreEqual(first.GetHashCode(), second.GetHashCode());
            Assert.AreEqual(first.GetHashCode(), third.GetHashCode());
            Assert.AreEqual(new PropertyPath(new[] { "zip" }), projections[second]);
            Assert.AreEqual(new PropertyPath(new[] { "zip" }), projections[third]);
        }

        [DataTestMethod]
        [DataRow(new[] { "address", "zip" }, new[] { "Address", "zip" })]
        [DataRow(new[] { "address", "zip" }, new[] { "zip", "address" })]
        [DataRow(new[] { "address", "zip" }, new[] { "address" })]
        [DataRow(new[] { "a/b" }, new[] { "a", "b" })]
        [DataRow(new[] { "a.b" }, new[] { "a", "b" })]
        [DataRow(new[] { "", "a" }, new[] { "a" })]
        [DataRow(new[] { "a" }, new[] { "\\u0061" })]
        [DataRow(new[] { "*" }, new[] { "id" })]
        [DataRow(new[] { "*" }, new[] { "" })]
        public void EqualityPreservesCaseOrderAndSegmentBoundaries(string[] firstSegments, string[] secondSegments)
        {
            PropertyPath first = new PropertyPath(firstSegments);
            PropertyPath second = new PropertyPath(secondSegments);
            Dictionary<PropertyPath, int> paths = new Dictionary<PropertyPath, int> { [first] = 1, [second] = 2 };

            Assert.IsFalse(first.Equals(second));
            Assert.IsFalse(second.Equals(first));
            Assert.AreEqual(2, paths.Count);
            Assert.AreEqual(1, paths[new PropertyPath(firstSegments)]);
            Assert.AreEqual(2, paths[new PropertyPath(secondSegments)]);
        }

        [TestMethod]
        public void EqualityRejectsNullAndOtherTypes()
        {
            PropertyPath path = new PropertyPath(new[] { "id" });

            Assert.IsFalse(path.Equals(null));
            Assert.IsFalse(path.Equals((object)null));
            Assert.IsFalse(path.Equals("id"));
            Assert.IsFalse(path.Equals(new[] { "id" }));
        }
    }
}
