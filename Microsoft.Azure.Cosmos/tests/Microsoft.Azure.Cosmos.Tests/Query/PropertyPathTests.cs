//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.SecondaryIndexRouting;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class PropertyPathTests
    {
        [TestMethod]
        public void ConstructorRejectsNullSegments()
        {
            ArgumentNullException exception = Assert.ThrowsException<ArgumentNullException>(() => new PropertyPath(null));

            Assert.AreEqual("segments", exception.ParamName);
        }

        [TestMethod]
        public void ConstructorRejectsEmptyPath()
        {
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(() => new PropertyPath(Array.Empty<string>()));

            Assert.AreEqual("segments", exception.ParamName);
        }

        [TestMethod]
        public void ConstructorRejectsNullSegment()
        {
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(() => new PropertyPath(new[] { "address", null }));

            Assert.AreEqual("segments", exception.ParamName);
        }

        [TestMethod]
        public void ConstructorCopiesSegmentsAndExposesReadOnlyView()
        {
            string[] segments = { "address", "zip" };
            PropertyPath path = new PropertyPath(segments);
            Dictionary<PropertyPath, string> paths = new Dictionary<PropertyPath, string> { [path] = "value" };
            int hash = path.GetHashCode();

            segments[0] = "changed";

            CollectionAssert.AreEqual(new[] { "address", "zip" }, path.Segments.ToArray());
            IList<string> exposedSegments = (IList<string>)path.Segments;
            Assert.IsTrue(exposedSegments.IsReadOnly);
            Assert.ThrowsException<NotSupportedException>(() => exposedSegments[0] = "changed");
            Assert.ThrowsException<NotSupportedException>(() => exposedSegments.Add("changed"));
            Assert.AreEqual(hash, path.GetHashCode());
            Assert.AreEqual("value", paths[new PropertyPath(new[] { "address", "zip" })]);
        }

        [TestMethod]
        public void ConstructorMaterializesEnumerable()
        {
            int enumerationCount = 0;
            IEnumerable<string> segments = new[] { "address", "zip" }.Select(segment =>
            {
                enumerationCount++;
                return segment;
            });

            PropertyPath path = new PropertyPath(segments);
            CollectionAssert.AreEqual(new[] { "address", "zip" }, path.Segments.ToArray());
            path.GetHashCode();

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

            Assert.IsFalse(path.IsWildcard);
            Assert.AreEqual(1, path.Segments.Count);
            Assert.AreEqual(segment, path.Segments[0]);
        }

        [TestMethod]
        public void EqualPathsHaveEqualHashesAndSupportDictionaryLookup()
        {
            PropertyPath first = new PropertyPath(new[] { "address", "zip" });
            PropertyPath second = new PropertyPath(new[] { "address", "zip" });
            PropertyPath third = new PropertyPath(new[] { "address", "zip" });
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
            Assert.AreEqual(new PropertyPath(new[] { "zip" }), projections[second]);
        }

        [DataTestMethod]
        [DataRow(new[] { "address", "zip" }, new[] { "Address", "zip" })]
        [DataRow(new[] { "address", "zip" }, new[] { "zip", "address" })]
        [DataRow(new[] { "address", "zip" }, new[] { "address" })]
        [DataRow(new[] { "a/b" }, new[] { "a", "b" })]
        [DataRow(new[] { "a.b" }, new[] { "a", "b" })]
        [DataRow(new[] { "", "a" }, new[] { "a" })]
        [DataRow(new[] { "a" }, new[] { "\\u0061" })]
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

        [TestMethod]
        public void WildcardIsDistinctFromLiteralPaths()
        {
            PropertyPath wildcard = PropertyPath.Wildcard;
            PropertyPath literalStar = new PropertyPath(new[] { "*" });
            PropertyPath emptyProperty = new PropertyPath(new[] { "" });
            Dictionary<PropertyPath, int> paths = new Dictionary<PropertyPath, int>
            {
                [wildcard] = 1,
                [literalStar] = 2,
                [emptyProperty] = 3,
            };

            Assert.IsTrue(wildcard.IsWildcard);
            Assert.AreEqual(0, wildcard.Segments.Count);
            Assert.AreSame(wildcard, PropertyPath.Wildcard);
            Assert.IsTrue(wildcard.Equals(PropertyPath.Wildcard));
            Assert.IsTrue(wildcard.Equals((object)PropertyPath.Wildcard));
            Assert.AreEqual(wildcard.GetHashCode(), PropertyPath.Wildcard.GetHashCode());
            Assert.IsFalse(wildcard.Equals(literalStar));
            Assert.IsFalse(literalStar.Equals(wildcard));
            Assert.IsFalse(wildcard.Equals(emptyProperty));
            Assert.IsFalse(wildcard.Equals(new PropertyPath(new[] { "id" })));
            Assert.AreEqual(3, paths.Count);
            Assert.AreEqual(1, paths[PropertyPath.Wildcard]);
            Assert.AreEqual(2, paths[new PropertyPath(new[] { "*" })]);
            Assert.AreEqual(3, paths[new PropertyPath(new[] { "" })]);
        }
    }
}
