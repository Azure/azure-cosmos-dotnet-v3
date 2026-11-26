//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query.Pipeline
{
    using System;
    using System.Collections.Generic;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Key = Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.HybridSearch.HybridSearchStatisticsCacheKey;

    [TestClass]
    public class HybridSearchStatisticsCacheKeyTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void KindsIdentifyDistinctScalarStatistics()
        {
            Key documentCount = Key.CreateTotalDocumentCount("rid", "etag");
            Key wordCount = Key.CreateDocumentPathWordCount("rid", "etag", new[] { "text" });
            Key hitCount = Key.CreateDocumentPathTermHitCount("rid", "etag", new[] { "text" }, "run", null);
            Assert.AreNotEqual(documentCount, wordCount);
            Assert.AreNotEqual(documentCount, hitCount);
            Assert.AreNotEqual(wordCount, hitCount);
        }

        [TestMethod]
        public void EveryKindIncludesOrdinalContainerIdentity()
        {
            List<KeyKindTestCase> testCases = new List<KeyKindTestCase>
            {
                new KeyKindTestCase("Document count", Key.CreateTotalDocumentCount),
                new KeyKindTestCase("Path word count", (rid, etag) => Key.CreateDocumentPathWordCount(rid, etag, new[] { "text" })),
                new KeyKindTestCase("Term hit count", (rid, etag) => Key.CreateDocumentPathTermHitCount(rid, etag, new[] { "text" }, "run", null)),
            };

            for (int index = 0; index < testCases.Count; index++)
            {
                KeyKindTestCase testCase = testCases[index];
                this.TestContext.WriteLine($"Case {index}: {testCase.Name}, RID=rid, ETag=etag");
                Func<string, string, Key> create = testCase.Create;
                Key key = create("rid", "etag");
                Key equal = create("rid", "etag");
                Assert.AreEqual(key, equal);
                Assert.AreEqual(key.GetHashCode(), equal.GetHashCode());
                Assert.IsTrue(key.Equals((object)equal));
                Assert.AreNotEqual(key, create("other", "etag"));
                Assert.AreNotEqual(key, create("RID", "etag"));
                Assert.AreNotEqual(key, create("rid", "other"));
                Assert.AreNotEqual(key, create("rid", "ETAG"));
                Assert.IsFalse(key.Equals(null));
                Assert.IsFalse(key.Equals(new object()));
            }
        }

        [TestMethod]
        public void PathsUseStructuralEqualityAndPreserveSegmentBoundaries()
        {
            Key key = Key.CreateDocumentPathWordCount("rid", "etag", new[] { "author", "bio" });
            Key equal = Key.CreateDocumentPathWordCount("rid", "etag", new List<string> { "author", "bio" });
            Assert.AreEqual(key, equal);
            Assert.AreEqual(key.GetHashCode(), equal.GetHashCode());
            Assert.AreNotEqual(key, Key.CreateDocumentPathWordCount("rid", "etag", new[] { "author/bio" }));
            Assert.AreNotEqual(key, Key.CreateDocumentPathWordCount("rid", "etag", new[] { "bio", "author" }));
            Assert.AreNotEqual(key, Key.CreateDocumentPathWordCount("rid", "etag", new[] { "Author", "bio" }));
            Assert.AreNotEqual(key, Key.CreateDocumentPathWordCount("rid", "etag", new[] { "author", "Bio" }));
            Assert.AreNotEqual(key, Key.CreateDocumentPathWordCount("rid", "etag", new[] { "author" }));
            Assert.AreNotEqual(key, Key.CreateDocumentPathWordCount("rid", "etag", new[] { "author", "bio", "" }));
        }

        [TestMethod]
        public void HitCountsIncludeExactTermAndDistance()
        {
            string[] path = { "text" };
            Key key = Key.CreateDocumentPathTermHitCount("rid", "etag", path, "United States", null);
            Assert.AreEqual(key, Key.CreateDocumentPathTermHitCount("rid", "etag", path, "United States", null));
            Assert.AreNotEqual(key, Key.CreateDocumentPathTermHitCount("rid", "etag", path, "united states", null));
            Assert.AreNotEqual(key, Key.CreateDocumentPathTermHitCount("rid", "etag", path, "United", null));
            Assert.AreNotEqual(key, Key.CreateDocumentPathTermHitCount("rid", "etag", path, "United States", 0));
            Assert.AreNotEqual(
                Key.CreateDocumentPathTermHitCount("rid", "etag", path, "run", 1),
                Key.CreateDocumentPathTermHitCount("rid", "etag", path, "run", 2));
            Assert.AreEqual(
                Key.CreateDocumentPathTermHitCount("rid", "etag", path, "", int.MaxValue),
                Key.CreateDocumentPathTermHitCount("rid", "etag", path, "", int.MaxValue));
        }

        [TestMethod]
        public void KeysDoNotExposeIdentityInToString()
        {
            Key hitCount = Key.CreateDocumentPathTermHitCount("private-rid", "etag", new[] { "text" }, "private-search-text", 2);
            Assert.IsFalse(hitCount.ToString().Contains("private-search-text"));
            Assert.IsFalse(hitCount.ToString().Contains("private-rid"));
        }

        [TestMethod]
        public void HashBucketCollisionsCompareFullIdentity()
        {
            Dictionary<Key, long> values = new Dictionary<Key, long>(new CollidingComparer())
            {
                [Key.CreateTotalDocumentCount("rid", "etag")] = 10,
                [Key.CreateDocumentPathWordCount("rid", "etag", new[] { "text" })] = 100,
                [Key.CreateDocumentPathTermHitCount("rid", "etag", new[] { "text" }, "run", null)] = 2,
            };
            Assert.AreEqual(3, values.Count);
            Assert.AreEqual(10L, values[Key.CreateTotalDocumentCount("rid", "etag")]);
            Assert.AreEqual(100L, values[Key.CreateDocumentPathWordCount("rid", "etag", new[] { "text" })]);
            Assert.AreEqual(2L, values[Key.CreateDocumentPathTermHitCount("rid", "etag", new[] { "text" }, "run", null)]);
        }

        [TestMethod]
        public void FactoriesRejectNullRequiredArguments()
        {
            string[] path = { "text" };
            List<NullArgumentTestCase> testCases = new List<NullArgumentTestCase>
            {
                new NullArgumentTestCase("containerResourceId", () => Key.CreateTotalDocumentCount(null, "etag")),
                new NullArgumentTestCase("containerETag", () => Key.CreateTotalDocumentCount("rid", null)),
                new NullArgumentTestCase("containerResourceId", () => Key.CreateDocumentPathWordCount(null, "etag", path)),
                new NullArgumentTestCase("containerETag", () => Key.CreateDocumentPathWordCount("rid", null, path)),
                new NullArgumentTestCase("path", () => Key.CreateDocumentPathWordCount("rid", "etag", null)),
                new NullArgumentTestCase("containerResourceId", () => Key.CreateDocumentPathTermHitCount(null, "etag", path, "run", null)),
                new NullArgumentTestCase("containerETag", () => Key.CreateDocumentPathTermHitCount("rid", null, path, "run", null)),
                new NullArgumentTestCase("path", () => Key.CreateDocumentPathTermHitCount("rid", "etag", null, "run", null)),
                new NullArgumentTestCase("term", () => Key.CreateDocumentPathTermHitCount("rid", "etag", path, null, null)),
            };

            for (int index = 0; index < testCases.Count; index++)
            {
                NullArgumentTestCase testCase = testCases[index];
                this.TestContext.WriteLine($"Case {index}: null {testCase.ParameterName}");
                ArgumentNullException exception = Assert.ThrowsException<ArgumentNullException>(() => testCase.Create(), $"Case {index}");
                Assert.AreEqual(testCase.ParameterName, exception.ParamName, $"Case {index}");
            }

            Assert.AreEqual(
                Key.CreateDocumentPathTermHitCount("rid", "etag", path, "run", null),
                Key.CreateDocumentPathTermHitCount("rid", "etag", path, "run", null),
                "A null distance is optional.");
        }

        private readonly struct NullArgumentTestCase
        {
            public NullArgumentTestCase(string parameterName, Func<Key> create)
            {
                this.ParameterName = parameterName;
                this.Create = create;
            }

            public string ParameterName { get; }

            public Func<Key> Create { get; }
        }

        private readonly struct KeyKindTestCase
        {
            public KeyKindTestCase(string name, Func<string, string, Key> create)
            {
                this.Name = name;
                this.Create = create;
            }

            public string Name { get; }

            public Func<string, string, Key> Create { get; }
        }

        private sealed class CollidingComparer : IEqualityComparer<Key>
        {
            public bool Equals(Key left, Key right) => left.Equals(right);

            public int GetHashCode(Key key) => 0;
        }
    }
}
