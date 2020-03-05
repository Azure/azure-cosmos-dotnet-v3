//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class PartitionKeyHashRangesTests
    {
        [TestMethod]
        public void TestNoPartitionKeyRanges()
        {
            VerifyCreate(PartitionKeyHashRanges.CreateOutcome.NoPartitionKeyRanges);
        }

        [TestMethod]
        public void TestEmptyPartitionKeyRange()
        {
            VerifyCreate(
                PartitionKeyHashRanges.CreateOutcome.EmptyPartitionKeyRange,
                CreateRange(0, 0));

            VerifyCreate(
                PartitionKeyHashRanges.CreateOutcome.EmptyPartitionKeyRange,
                CreateRange(0, 0), CreateRange(0, 1));

            VerifyCreate(
                PartitionKeyHashRanges.CreateOutcome.EmptyPartitionKeyRange,
                CreateRange(0, 1), CreateRange(1, 1));

            VerifyCreate(
                PartitionKeyHashRanges.CreateOutcome.EmptyPartitionKeyRange,
                CreateRange(0, 1), CreateRange(1, 1), CreateRange(1, 2));
        }

        [TestMethod]
        public void TestDuplicatePartitionKeyRange()
        {
            VerifyCreate(
                PartitionKeyHashRanges.CreateOutcome.DuplicatePartitionKeyRange,
                CreateRange(0, 1), CreateRange(0, 1));

            VerifyCreate(
                PartitionKeyHashRanges.CreateOutcome.DuplicatePartitionKeyRange,
                CreateRange(0, 1), CreateRange(1, 2), CreateRange(0, 1));
        }

        [TestMethod]
        public void TestRangeOverlap()
        {
            VerifyCreate(
                PartitionKeyHashRanges.CreateOutcome.RangesOverlap,
                CreateRange(0, 2), CreateRange(1, 3));

            VerifyCreate(
                PartitionKeyHashRanges.CreateOutcome.RangesOverlap,
                CreateRange(0, 2), CreateRange(0, 3));

            VerifyCreate(
                PartitionKeyHashRanges.CreateOutcome.RangesOverlap,
                CreateRange(0, 2), CreateRange(0, 1), CreateRange(0, 3));
        }

        [TestMethod]
        public void TestRangesOverlap()
        {
            VerifyCreate(
                PartitionKeyHashRanges.CreateOutcome.RangesAreNotContiguous,
                CreateRange(0, 1), CreateRange(2, 3));

            VerifyCreate(
                PartitionKeyHashRanges.CreateOutcome.RangesAreNotContiguous,
                CreateRange(0, 1), CreateRange(1, 2), CreateRange(3, 4));
        }

        [TestMethod]
        public void TestSuccess()
        {
            VerifyCreate(
                PartitionKeyHashRanges.CreateOutcome.Success,
                CreateRange(0, 1));

            VerifyCreate(
                PartitionKeyHashRanges.CreateOutcome.Success,
                CreateRange(0, 1), CreateRange(1, 2));

            VerifyCreate(
                PartitionKeyHashRanges.CreateOutcome.Success,
                CreateRange(0, 1234), CreateRange(1234, int.MaxValue));
        }

        private static void VerifyCreate(
            PartitionKeyHashRanges.CreateOutcome expectedCreateStatus,
            params PartitionKeyHashRange[] rangesToInsert)
        {
            PartitionKeyHashRanges.CreateOutcome actualCreateStatus = PartitionKeyHashRanges.TryCreate(
                rangesToInsert.OrderBy(x => Guid.NewGuid()),
                out PartitionKeyHashRanges insertedRanges);
            Assert.AreEqual(expectedCreateStatus, actualCreateStatus);
            if (expectedCreateStatus == PartitionKeyHashRanges.CreateOutcome.Success)
            {
                Assert.AreEqual(rangesToInsert.Count(), insertedRanges.Count());
                IEnumerable<(PartitionKeyHashRange, PartitionKeyHashRange)> pairs = insertedRanges.Zip(insertedRanges.Skip(1), (first, second) => (first, second));
                foreach ((PartitionKeyHashRange first, PartitionKeyHashRange second) in pairs)
                {
                    Assert.IsTrue(first.CompareTo(second) < 0, "Ranges are not sorted");
                }
            }
        }

        private static PartitionKeyHashRange CreateRange(UInt128 start, UInt128 end)
        {
            return new PartitionKeyHashRange(
                startInclusive: new PartitionKeyHash(start),
                endExclusive: new PartitionKeyHash(end));
        }
    }
}
