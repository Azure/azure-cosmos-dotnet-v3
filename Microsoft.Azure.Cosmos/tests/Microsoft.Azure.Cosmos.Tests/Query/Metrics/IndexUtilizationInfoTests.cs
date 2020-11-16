//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query.Metrics
{
    using System;
    using VisualStudio.TestTools.UnitTesting;
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;
    using System.Collections.Generic;

    [TestClass]
    public class IndexUtilizationInfoTests
    {
        private static readonly SingleIndexUtilizationEntity singleUtilizationEntity = new SingleIndexUtilizationEntity(
                nameof(SingleIndexUtilizationEntity.FilterExpression),
                nameof(SingleIndexUtilizationEntity.IndexDocumentExpression),
                default,
                default,
                nameof(SingleIndexUtilizationEntity.IndexImpactScore));

        private static readonly CompositeIndexUtilizationEntity compositeUtilizationEntity = new CompositeIndexUtilizationEntity(
                new List<string>(),
                default,
                nameof(CompositeIndexUtilizationEntity.IndexImpactScore));

        internal static readonly IndexUtilizationInfo MockIndexUtilizationInfo = new IndexUtilizationInfo(
            new List<SingleIndexUtilizationEntity>() { singleUtilizationEntity },
            new List<SingleIndexUtilizationEntity>() { singleUtilizationEntity },
            new List<CompositeIndexUtilizationEntity>() { compositeUtilizationEntity },
            new List<CompositeIndexUtilizationEntity>() { compositeUtilizationEntity });

        [TestMethod]
        public void TestParse()
        {
            Assert.IsTrue(IndexUtilizationInfo.TryCreateFromDelimitedString("eyJVdGlsaXplZEluZGV4ZXMiOlt7IkZpbHRlckV4cHJlc3Npb24iOiIoUk9PVC5zdGF0ZSA9IFwiQVpcIikiLCJJbmRleFNwZWMiOiJcL3N0YXRlXC8/IiwiRmlsdGVyUHJlY2lzZVNldCI6dHJ1ZSwiSW5kZXhQcmVjaXNlU2V0Ijp0cnVlLCJJbmRleEltcGFjdFNjb3JlIjoiSGlnaCJ9LHsiRmlsdGVyRXhwcmVzc2lvbiI6IihST09ULm5hbWUgSU4geyA8U1RSSU5HPiB9KSIsIkluZGV4U3BlYyI6IlwvbmFtZVwvPyIsIkZpbHRlclByZWNpc2VTZXQiOnRydWUsIkluZGV4UHJlY2lzZVNldCI6dHJ1ZSwiSW5kZXhJbXBhY3RTY29yZSI6IkhpZ2gifV0sIlBvdGVudGlhbEluZGV4ZXMiOltdfQ==",
                out IndexUtilizationInfo parsedInfo));
            Assert.IsNotNull(parsedInfo);
        }

        [TestMethod]
        public void TestParseEmptyString()
        {
            Assert.IsTrue(IndexUtilizationInfo.TryCreateFromDelimitedString(string.Empty,
                out IndexUtilizationInfo parsedInfo));
            Assert.AreEqual(IndexUtilizationInfo.Empty, parsedInfo);
        }

        [TestMethod]
        public void TestAccumulator()
        {
            IndexUtilizationInfo.Accumulator accumulator = new IndexUtilizationInfo.Accumulator();
            accumulator = accumulator.Accumulate(MockIndexUtilizationInfo);
            accumulator = accumulator.Accumulate(MockIndexUtilizationInfo);

            IndexUtilizationInfo doubleInfo = IndexUtilizationInfo.Accumulator.ToIndexUtilizationInfo(accumulator);
            Assert.AreEqual(2 * MockIndexUtilizationInfo.PotentialSingleIndexes.Count, doubleInfo.PotentialSingleIndexes.Count);
            Assert.AreEqual(2 * MockIndexUtilizationInfo.UtilizedSingleIndexes.Count, doubleInfo.UtilizedSingleIndexes.Count);
            Assert.AreEqual(2 * MockIndexUtilizationInfo.PotentialCompositeIndexes.Count, doubleInfo.PotentialCompositeIndexes.Count);
            Assert.AreEqual(2 * MockIndexUtilizationInfo.UtilizedCompositeIndexes.Count, doubleInfo.UtilizedCompositeIndexes.Count);
        }
    }
}
