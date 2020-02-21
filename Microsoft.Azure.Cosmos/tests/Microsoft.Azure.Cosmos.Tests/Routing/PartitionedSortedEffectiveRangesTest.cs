//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Linq;
    using System.Runtime.ExceptionServices;
    using System.Text;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class PartitionedSortedEffectiveRangesTest
    {
        [TestMethod]
        public void TestNoPartitionKeyRanges()
        {
            VerifyCreate(PartitionedSortedEffectiveRanges.CreateOutcome.NoPartitionKeyRanges);
        }

        [TestMethod]
        public void TestEmptyPartitionKeyRange()
        {
            VerifyCreate(
                PartitionedSortedEffectiveRanges.CreateOutcome.EmptyPartitionKeyRange,
                CreateRange(0, 0));

            VerifyCreate(
                PartitionedSortedEffectiveRanges.CreateOutcome.EmptyPartitionKeyRange,
                CreateRange(0, 0), CreateRange(0, 1));

            VerifyCreate(
                PartitionedSortedEffectiveRanges.CreateOutcome.EmptyPartitionKeyRange,
                CreateRange(0, 1), CreateRange(1, 1));

            VerifyCreate(
                PartitionedSortedEffectiveRanges.CreateOutcome.EmptyPartitionKeyRange,
                CreateRange(0, 1), CreateRange(1, 1), CreateRange(1, 2));
        }

        [TestMethod]
        public void TestDuplicatePartitionKeyRange()
        {
            VerifyCreate(
                PartitionedSortedEffectiveRanges.CreateOutcome.DuplicatePartitionKeyRange,
                CreateRange(0, 1), CreateRange(0, 1));

            VerifyCreate(
                PartitionedSortedEffectiveRanges.CreateOutcome.DuplicatePartitionKeyRange,
                CreateRange(0, 1), CreateRange(1, 2), CreateRange(0, 1));
        }

        [TestMethod]
        public void TestRangeOverlap()
        {
            VerifyCreate(
                PartitionedSortedEffectiveRanges.CreateOutcome.RangesOverlap,
                CreateRange(0, 2), CreateRange(1, 3));

            VerifyCreate(
                PartitionedSortedEffectiveRanges.CreateOutcome.RangesOverlap,
                CreateRange(0, 2), CreateRange(0, 3));

            VerifyCreate(
                PartitionedSortedEffectiveRanges.CreateOutcome.RangesOverlap,
                CreateRange(0, 2), CreateRange(0, 1), CreateRange(0, 3));
        }

        [TestMethod]
        public void TestRangesOverlap()
        {
            VerifyCreate(
                PartitionedSortedEffectiveRanges.CreateOutcome.RangesAreNotContiguous,
                CreateRange(0, 1), CreateRange(2, 3));

            VerifyCreate(
                PartitionedSortedEffectiveRanges.CreateOutcome.RangesAreNotContiguous,
                CreateRange(0, 1), CreateRange(1, 2), CreateRange(3, 4));
        }

        [TestMethod]
        public void TestSuccess()
        {
            VerifyCreate(
                PartitionedSortedEffectiveRanges.CreateOutcome.Success,
                CreateRange(0, 1));

            VerifyCreate(
                PartitionedSortedEffectiveRanges.CreateOutcome.Success,
                CreateRange(0, 1), CreateRange(1, 2));

            VerifyCreate(
                PartitionedSortedEffectiveRanges.CreateOutcome.Success,
                CreateRange(0, 1234), CreateRange(1234, int.MaxValue));
        }

        private static void VerifyCreate(
            PartitionedSortedEffectiveRanges.CreateOutcome expectedCreateStatus,
            params EffectivePartitionKeyRange[] rangesToInsert)
        {
            PartitionedSortedEffectiveRanges.CreateOutcome actualCreateStatus = PartitionedSortedEffectiveRanges.TryCreate(
                rangesToInsert.OrderBy(x => Guid.NewGuid()),
                out PartitionedSortedEffectiveRanges insertedRanges);
            Assert.AreEqual(expectedCreateStatus, actualCreateStatus);
            if (expectedCreateStatus == PartitionedSortedEffectiveRanges.CreateOutcome.Success)
            {
                Assert.AreEqual(rangesToInsert.Count(), insertedRanges.Count());
                IEnumerable<(EffectivePartitionKeyRange, EffectivePartitionKeyRange)> pairs = insertedRanges.Zip(insertedRanges.Skip(1), (first, second) => (first, second));
                foreach ((EffectivePartitionKeyRange first, EffectivePartitionKeyRange second) in pairs)
                {
                    Assert.IsTrue(first.CompareTo(second) < 0, "Ranges are not sorted");
                }
            }
        }

        private static EffectivePartitionKeyRange CreateRange(UInt128 start, UInt128 end)
        {
            return new EffectivePartitionKeyRange(
                startInclusive: new EffectivePartitionKey(start),
                endExclusive: new EffectivePartitionKey(end));
        }
    }
}
