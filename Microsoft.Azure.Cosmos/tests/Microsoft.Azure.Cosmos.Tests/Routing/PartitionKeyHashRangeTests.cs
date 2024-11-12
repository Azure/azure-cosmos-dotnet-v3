namespace Microsoft.Azure.Cosmos.Tests.Routing
{
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public sealed class PartitionKeyHashRangeTests
    {
        [TestMethod]
        public void Test_Contains_CompleteOverlap()
        {
            // Simple
            {
                PartitionKeyHashRange range1 = new PartitionKeyHashRange(startInclusive: new PartitionKeyHash(0), endExclusive: new PartitionKeyHash(10));
                PartitionKeyHashRange range2 = new PartitionKeyHashRange(startInclusive: new PartitionKeyHash(5), endExclusive: new PartitionKeyHash(7));
                Assert.IsTrue(range1.Contains(range2));
                Assert.IsFalse(range2.Contains(range1));
            }

            // null start
            {
                PartitionKeyHashRange range1 = new PartitionKeyHashRange(startInclusive: null, endExclusive: new PartitionKeyHash(10));
                PartitionKeyHashRange range2 = new PartitionKeyHashRange(startInclusive: new PartitionKeyHash(5), endExclusive: new PartitionKeyHash(7));
                Assert.IsTrue(range1.Contains(range2));
                Assert.IsFalse(range2.Contains(range1));
            }

            // null end
            {
                PartitionKeyHashRange range1 = new PartitionKeyHashRange(startInclusive: new PartitionKeyHash(0), endExclusive: null);
                PartitionKeyHashRange range2 = new PartitionKeyHashRange(startInclusive: new PartitionKeyHash(5), endExclusive: new PartitionKeyHash(7));
                Assert.IsTrue(range1.Contains(range2));
                Assert.IsFalse(range2.Contains(range1));
            }

            // Align on left
            {
                PartitionKeyHashRange range1 = new PartitionKeyHashRange(startInclusive: new PartitionKeyHash(0), endExclusive: new PartitionKeyHash(10));
                PartitionKeyHashRange range2 = new PartitionKeyHashRange(startInclusive: new PartitionKeyHash(0), endExclusive: new PartitionKeyHash(7));
                Assert.IsTrue(range1.Contains(range2));
                Assert.IsFalse(range2.Contains(range1));
            }

            // Align on right
            {
                PartitionKeyHashRange range1 = new PartitionKeyHashRange(startInclusive: new PartitionKeyHash(0), endExclusive: new PartitionKeyHash(10));
                PartitionKeyHashRange range2 = new PartitionKeyHashRange(startInclusive: new PartitionKeyHash(5), endExclusive: new PartitionKeyHash(10));
                Assert.IsTrue(range1.Contains(range2));
                Assert.IsFalse(range2.Contains(range1));
            }
        }

        [TestMethod]
        public void Test_Contains_PartialOverlap()
        {
            // Simple
            {
                PartitionKeyHashRange range1 = new PartitionKeyHashRange(startInclusive: new PartitionKeyHash(5), endExclusive: new PartitionKeyHash(10));
                PartitionKeyHashRange range2 = new PartitionKeyHashRange(startInclusive: new PartitionKeyHash(2), endExclusive: new PartitionKeyHash(7));
                Assert.IsFalse(range1.Contains(range2));
                Assert.IsFalse(range2.Contains(range1));
            }

            // null start
            {
                PartitionKeyHashRange range1 = new PartitionKeyHashRange(startInclusive: null, endExclusive: new PartitionKeyHash(10));
                PartitionKeyHashRange range2 = new PartitionKeyHashRange(startInclusive: new PartitionKeyHash(5), endExclusive: new PartitionKeyHash(15));
                Assert.IsFalse(range1.Contains(range2));
                Assert.IsFalse(range2.Contains(range1));
            }

            // null end
            {
                PartitionKeyHashRange range1 = new PartitionKeyHashRange(startInclusive: new PartitionKeyHash(5), endExclusive: null);
                PartitionKeyHashRange range2 = new PartitionKeyHashRange(startInclusive: new PartitionKeyHash(0), endExclusive: new PartitionKeyHash(7));
                Assert.IsFalse(range1.Contains(range2));
                Assert.IsFalse(range2.Contains(range1));
            }
        }

        [TestMethod]
        public void Test_Contains_Adjacent()
        {
            // Simple
            {
                PartitionKeyHashRange range1 = new PartitionKeyHashRange(startInclusive: new PartitionKeyHash(0), endExclusive: new PartitionKeyHash(5));
                PartitionKeyHashRange range2 = new PartitionKeyHashRange(startInclusive: new PartitionKeyHash(5), endExclusive: new PartitionKeyHash(10));
                Assert.IsFalse(range1.Contains(range2));
                Assert.IsFalse(range2.Contains(range1));
            }

            // null start
            {
                PartitionKeyHashRange range1 = new PartitionKeyHashRange(startInclusive: null, endExclusive: new PartitionKeyHash(10));
                PartitionKeyHashRange range2 = new PartitionKeyHashRange(startInclusive: new PartitionKeyHash(10), endExclusive: new PartitionKeyHash(15));
                Assert.IsFalse(range1.Contains(range2));
                Assert.IsFalse(range2.Contains(range1));
            }

            // null end
            {
                PartitionKeyHashRange range1 = new PartitionKeyHashRange(startInclusive: new PartitionKeyHash(5), endExclusive: null);
                PartitionKeyHashRange range2 = new PartitionKeyHashRange(startInclusive: new PartitionKeyHash(0), endExclusive: new PartitionKeyHash(5));
                Assert.IsFalse(range1.Contains(range2));
                Assert.IsFalse(range2.Contains(range1));
            }
        }

        [TestMethod]
        public void Test_TryGetOverlappingRange_CompleteOverlap()
        {
            // Simple
            {
                PartitionKeyHashRange range1 = new PartitionKeyHashRange(startInclusive: new PartitionKeyHash(0), endExclusive: new PartitionKeyHash(10));
                PartitionKeyHashRange range2 = new PartitionKeyHashRange(startInclusive: new PartitionKeyHash(5), endExclusive: new PartitionKeyHash(7));
                if (!range1.TryGetOverlappingRange(range2, out PartitionKeyHashRange overlappingRange))
                {
                    Assert.Fail("Failed to get overlapping range");
                }

                Assert.AreEqual(range2, overlappingRange);
            }

            // null start
            {
                PartitionKeyHashRange range1 = new PartitionKeyHashRange(startInclusive: null, endExclusive: new PartitionKeyHash(10));
                PartitionKeyHashRange range2 = new PartitionKeyHashRange(startInclusive: new PartitionKeyHash(5), endExclusive: new PartitionKeyHash(7));
                if (!range1.TryGetOverlappingRange(range2, out PartitionKeyHashRange overlappingRange))
                {
                    Assert.Fail("Failed to get overlapping range");
                }

                Assert.AreEqual(range2, overlappingRange);
            }

            // null end
            {
                PartitionKeyHashRange range1 = new PartitionKeyHashRange(startInclusive: new PartitionKeyHash(0), endExclusive: null);
                PartitionKeyHashRange range2 = new PartitionKeyHashRange(startInclusive: new PartitionKeyHash(5), endExclusive: new PartitionKeyHash(7));
                if (!range1.TryGetOverlappingRange(range2, out PartitionKeyHashRange overlappingRange))
                {
                    Assert.Fail("Failed to get overlapping range");
                }

                Assert.AreEqual(range2, overlappingRange);
            }

            // Align on left
            {
                PartitionKeyHashRange range1 = new PartitionKeyHashRange(startInclusive: new PartitionKeyHash(0), endExclusive: new PartitionKeyHash(10));
                PartitionKeyHashRange range2 = new PartitionKeyHashRange(startInclusive: new PartitionKeyHash(0), endExclusive: new PartitionKeyHash(7));
                if (!range1.TryGetOverlappingRange(range2, out PartitionKeyHashRange overlappingRange))
                {
                    Assert.Fail("Failed to get overlapping range");
                }

                Assert.AreEqual(range2, overlappingRange);
            }

            // Align on right
            {
                PartitionKeyHashRange range1 = new PartitionKeyHashRange(startInclusive: new PartitionKeyHash(0), endExclusive: new PartitionKeyHash(10));
                PartitionKeyHashRange range2 = new PartitionKeyHashRange(startInclusive: new PartitionKeyHash(5), endExclusive: new PartitionKeyHash(10));
                if (!range1.TryGetOverlappingRange(range2, out PartitionKeyHashRange overlappingRange))
                {
                    Assert.Fail("Failed to get overlapping range");
                }

                Assert.AreEqual(range2, overlappingRange);
            }
        }

        [TestMethod]
        public void Test_TryGetOverlappingRange_PartialOverlap()
        {
            // Simple
            {
                PartitionKeyHashRange range1 = new PartitionKeyHashRange(startInclusive: new PartitionKeyHash(7), endExclusive: new PartitionKeyHash(10));
                PartitionKeyHashRange range2 = new PartitionKeyHashRange(startInclusive: new PartitionKeyHash(5), endExclusive: new PartitionKeyHash(9));
                if (!range1.TryGetOverlappingRange(range2, out PartitionKeyHashRange overlappingRange))
                {
                    Assert.Fail("Failed to get overlapping range");
                }

                PartitionKeyHashRange expectedOverlappingRange = new PartitionKeyHashRange(startInclusive: new PartitionKeyHash(7), endExclusive: new PartitionKeyHash(9));

                Assert.AreEqual(expectedOverlappingRange, overlappingRange);
            }

            // null start
            {
                PartitionKeyHashRange range1 = new PartitionKeyHashRange(startInclusive: null, endExclusive: new PartitionKeyHash(10));
                PartitionKeyHashRange range2 = new PartitionKeyHashRange(startInclusive: new PartitionKeyHash(5), endExclusive: new PartitionKeyHash(15));
                if (!range1.TryGetOverlappingRange(range2, out PartitionKeyHashRange overlappingRange))
                {
                    Assert.Fail("Failed to get overlapping range");
                }

                PartitionKeyHashRange expectedOverlappingRange = new PartitionKeyHashRange(startInclusive: new PartitionKeyHash(5), endExclusive: new PartitionKeyHash(10));

                Assert.AreEqual(expectedOverlappingRange, overlappingRange);
            }

            // null end
            {
                PartitionKeyHashRange range1 = new PartitionKeyHashRange(startInclusive: new PartitionKeyHash(6), endExclusive: null);
                PartitionKeyHashRange range2 = new PartitionKeyHashRange(startInclusive: new PartitionKeyHash(5), endExclusive: new PartitionKeyHash(7));
                if (!range1.TryGetOverlappingRange(range2, out PartitionKeyHashRange overlappingRange))
                {
                    Assert.Fail("Failed to get overlapping range");
                }

                PartitionKeyHashRange expectedOverlappingRange = new PartitionKeyHashRange(startInclusive: new PartitionKeyHash(6), endExclusive: new PartitionKeyHash(7));

                Assert.AreEqual(expectedOverlappingRange, overlappingRange);
            }
        }

        [TestMethod]
        public void Test_TryGetOverlappingRange_NoOverlap()
        {
            // Simple
            {
                PartitionKeyHashRange range1 = new PartitionKeyHashRange(startInclusive: new PartitionKeyHash(7), endExclusive: new PartitionKeyHash(10));
                PartitionKeyHashRange range2 = new PartitionKeyHashRange(startInclusive: new PartitionKeyHash(5), endExclusive: new PartitionKeyHash(6));
                if (range1.TryGetOverlappingRange(range2, out PartitionKeyHashRange _))
                {
                    Assert.Fail("Expected no overlap");
                }
            }

            // null start
            {
                PartitionKeyHashRange range1 = new PartitionKeyHashRange(startInclusive: null, endExclusive: new PartitionKeyHash(10));
                PartitionKeyHashRange range2 = new PartitionKeyHashRange(startInclusive: new PartitionKeyHash(15), endExclusive: new PartitionKeyHash(20));
                if (range1.TryGetOverlappingRange(range2, out PartitionKeyHashRange _))
                {
                    Assert.Fail("Expected no overlap");
                }
            }

            // null end
            {
                PartitionKeyHashRange range1 = new PartitionKeyHashRange(startInclusive: new PartitionKeyHash(6), endExclusive: null);
                PartitionKeyHashRange range2 = new PartitionKeyHashRange(startInclusive: new PartitionKeyHash(1), endExclusive: new PartitionKeyHash(3));
                if (range1.TryGetOverlappingRange(range2, out PartitionKeyHashRange _))
                {
                    Assert.Fail("Expected no overlap");
                }
            }

            // Adjacent
            {
                PartitionKeyHashRange range1 = new PartitionKeyHashRange(startInclusive: new PartitionKeyHash(0), endExclusive: new PartitionKeyHash(5));
                PartitionKeyHashRange range2 = new PartitionKeyHashRange(startInclusive: new PartitionKeyHash(5), endExclusive: new PartitionKeyHash(10));
                if (range1.TryGetOverlappingRange(range2, out PartitionKeyHashRange _))
                {
                    Assert.Fail("Expected no overlap");
                }
            }
        }
    }
}