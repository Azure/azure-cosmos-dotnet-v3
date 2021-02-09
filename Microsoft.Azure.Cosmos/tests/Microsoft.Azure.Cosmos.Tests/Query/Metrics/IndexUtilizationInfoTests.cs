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
            Assert.IsTrue(IndexUtilizationInfo.TryCreateFromDelimitedString("ewogICAgIlV0aWxpemVkU2luZ2xlSW5kZXhlcyI6IFsKICAgICAgICB7CiAgICAgICAgICAgICJGaWx0ZXJFeHByZXNzaW9uIjogIihST09ULm5hbWUgPSBcIkp1bGllblwiKSIsCiAgICAgICAgICAgICJJbmRleFNwZWMiOiAiXC9uYW1lXC8 / IiwKICAgICAgICAgICAgIkZpbHRlclByZWNpc2VTZXQiOiB0cnVlLAogICAgICAgICAgICAiSW5kZXhQcmVjaXNlU2V0IjogdHJ1ZSwKICAgICAgICAgICAgIkluZGV4SW1wYWN0U2NvcmUiOiAiSGlnaCIKICAgICAgICB9LAogICAgICAgIHsKICAgICAgICAgICAgIkZpbHRlckV4cHJlc3Npb24iOiAiKFJPT1QuYWdlID4gMTIpIiwKICAgICAgICAgICAgIkluZGV4U3BlYyI6ICJcL2FnZVwvPyIsCiAgICAgICAgICAgICJGaWx0ZXJQcmVjaXNlU2V0IjogdHJ1ZSwKICAgICAgICAgICAgIkluZGV4UHJlY2lzZVNldCI6IHRydWUsCiAgICAgICAgICAgICJJbmRleEltcGFjdFNjb3JlIjogIkhpZ2giCiAgICAgICAgfQogICAgXSwKICAgICJQb3RlbnRpYWxTaW5nbGVJbmRleGVzIjogW10sCiAgICAiVXRpbGl6ZWRDb21wb3NpdGVJbmRleGVzIjogW10sCiAgICAiUG90ZW50aWFsQ29tcG9zaXRlSW5kZXhlcyI6IFsKICAgICAgICB7CiAgICAgICAgICAgICJJbmRleFNwZWNzIjogWwogICAgICAgICAgICAgICAgIlwvbmFtZSBBU0MiLAogICAgICAgICAgICAgICAgIlwvYWdlIEFTQyIKICAgICAgICAgICAgXSwKICAgICAgICAgICAgIkluZGV4UHJlY2lzZVNldCI6IGZhbHNlLAogICAgICAgICAgICAiSW5kZXhJbXBhY3RTY29yZSI6ICJIaWdoIgogICAgICAgIH0KICAgIF0KfQ == ",
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
