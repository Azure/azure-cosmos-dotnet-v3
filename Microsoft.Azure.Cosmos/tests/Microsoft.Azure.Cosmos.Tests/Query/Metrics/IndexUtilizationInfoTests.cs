//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query.Metrics
{
    using System;
    using VisualStudio.TestTools.UnitTesting;
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;
    using System.Collections.Generic;
    using System.Text;

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

        [TestMethod]
        public void TestBase64Parse()
        {
            Assert.IsTrue(IndexUtilizationInfo.TryCreateFromDelimitedBase64String("ewogICAgIlV0aWxpemVkU2luZ2xlSW5kZXhlcyI6IFsKICAgICAgICB7CiAgICAgICAgICAgICJGaWx0ZXJFeHByZXNzaW9uIjogIihST09ULm5hbWUgPSBcIkp1bGllblwiKSIsCiAgICAgICAgICAgICJJbmRleFNwZWMiOiAiXC9uYW1lXC8 / IiwKICAgICAgICAgICAgIkZpbHRlclByZWNpc2VTZXQiOiB0cnVlLAogICAgICAgICAgICAiSW5kZXhQcmVjaXNlU2V0IjogdHJ1ZSwKICAgICAgICAgICAgIkluZGV4SW1wYWN0U2NvcmUiOiAiSGlnaCIKICAgICAgICB9LAogICAgICAgIHsKICAgICAgICAgICAgIkZpbHRlckV4cHJlc3Npb24iOiAiKFJPT1QuYWdlID4gMTIpIiwKICAgICAgICAgICAgIkluZGV4U3BlYyI6ICJcL2FnZVwvPyIsCiAgICAgICAgICAgICJGaWx0ZXJQcmVjaXNlU2V0IjogdHJ1ZSwKICAgICAgICAgICAgIkluZGV4UHJlY2lzZVNldCI6IHRydWUsCiAgICAgICAgICAgICJJbmRleEltcGFjdFNjb3JlIjogIkhpZ2giCiAgICAgICAgfQogICAgXSwKICAgICJQb3RlbnRpYWxTaW5nbGVJbmRleGVzIjogW10sCiAgICAiVXRpbGl6ZWRDb21wb3NpdGVJbmRleGVzIjogW10sCiAgICAiUG90ZW50aWFsQ29tcG9zaXRlSW5kZXhlcyI6IFsKICAgICAgICB7CiAgICAgICAgICAgICJJbmRleFNwZWNzIjogWwogICAgICAgICAgICAgICAgIlwvbmFtZSBBU0MiLAogICAgICAgICAgICAgICAgIlwvYWdlIEFTQyIKICAgICAgICAgICAgXSwKICAgICAgICAgICAgIkluZGV4UHJlY2lzZVNldCI6IGZhbHNlLAogICAgICAgICAgICAiSW5kZXhJbXBhY3RTY29yZSI6ICJIaWdoIgogICAgICAgIH0KICAgIF0KfQ == ",
                out IndexUtilizationInfo parsedInfo));
            Assert.IsNotNull(parsedInfo);

            Assert.IsTrue(IndexUtilizationInfo.TryCreateFromDelimitedBase64String("",
                out IndexUtilizationInfo parsedInfo2));
            Assert.IsNotNull(parsedInfo2);

            Assert.IsTrue(IndexUtilizationInfo.TryCreateFromDelimitedBase64String(string.Empty,
                out IndexUtilizationInfo parsedInfo3));
            Assert.AreEqual(IndexUtilizationInfo.Empty, parsedInfo3);
        }
        
        [TestMethod]
        public void TestParse()
        {
            Assert.IsTrue(IndexUtilizationInfo.TryCreateFromDelimitedString("{\"UtilizedSingleIndexes\": [{\"FilterExpression\": \"(ROOT.name = \\\"Julien\\\")\",\"IndexSpec\": \"\\/name\\/?\",\"FilterPreciseSet\": true,\"IndexPreciseSet\": true,\"IndexImpactScore\": \"High\"},{\"FilterExpression\": \"(ROOT.age > 12)\",\"IndexSpec\": \"\\/age\\/?\",\"FilterPreciseSet\": true,\"IndexPreciseSet\": true,\"IndexImpactScore\": \"High\"}],\"PotentialSingleIndexes\": [],\"UtilizedCompositeIndexes\": [],\"PotentialCompositeIndexes\": [{\"IndexSpecs\": [ \"\\/name ASC\", \"\\/age ASC\"],\"IndexPreciseSet\": false,\"IndexImpactScore\": \"High\"}] }",
                out IndexUtilizationInfo parsedInfo));
            Assert.IsNotNull(parsedInfo);

            Assert.IsTrue(IndexUtilizationInfo.TryCreateFromDelimitedBase64String("",
                out IndexUtilizationInfo parsedInfo2));
            Assert.IsNotNull(parsedInfo2);

            Assert.IsTrue(IndexUtilizationInfo.TryCreateFromDelimitedString(string.Empty,
                out IndexUtilizationInfo parsedInfo3));
            Assert.AreEqual(IndexUtilizationInfo.Empty, parsedInfo3);

            // Valid Json missing fields on SingleIndexUtilization objects
            Assert.IsTrue(IndexUtilizationInfo.TryCreateFromDelimitedString("{\"UtilizedSingleIndexes\": [{\"IndexSpec\": \"\\/name\\/?\",\"IndexPreciseSet\": true,\"IndexImpactScore\": \"High\"},{\"FilterExpression\": \"(ROOT.age > 12)\",\"IndexSpec\": \"\\/age\\/?\",\"FilterPreciseSet\": true,\"IndexPreciseSet\": true}],\"PotentialSingleIndexes\": [],\"UtilizedCompositeIndexes\": [],\"PotentialCompositeIndexes\": [{\"IndexSpecs\": [],\"IndexPreciseSet\": false}] }",
               out IndexUtilizationInfo parsedInfo4));
            Assert.IsNotNull(parsedInfo4);

            // Valid Json having SingleIndexUtilization objects as empty object
            Assert.IsTrue(IndexUtilizationInfo.TryCreateFromDelimitedString("{\"UtilizedSingleIndexes\": [{}],\"PotentialSingleIndexes\": [],\"UtilizedCompositeIndexes\": [],\"PotentialCompositeIndexes\": [{\"IndexSpecs\": [ \"\\/name ASC\", \"\\/age ASC\"],\"IndexPreciseSet\": false,\"IndexImpactScore\": \"High\"}] }",
               out IndexUtilizationInfo parsedInfo5));
            Assert.IsNotNull(parsedInfo5);

            // Valid Json missing entire IndexUtilization arrays
            Assert.IsTrue(IndexUtilizationInfo.TryCreateFromDelimitedString("{\"UtilizedCompositeIndexes\": [],\"PotentialCompositeIndexes\": [{\"IndexSpecs\": [ \"\\/name ASC\", \"\\/age ASC\"],\"IndexPreciseSet\": false,\"IndexImpactScore\": \"High\"}] }",
               out IndexUtilizationInfo parsedInfo6));
            Assert.IsNotNull(parsedInfo6);

            // Unicode character
            Assert.IsTrue(IndexUtilizationInfo.TryCreateFromDelimitedString("{\"UtilizedSingleIndexes\": [{\"FilterExpression\": \"(ROOT[\\\"unicode㐀㐁㨀㨁䶴䶵\\\"] = \\\"unicode㐀㐁㨀㨁䶴䶵\\\")\",\"IndexSpec\": \"\\/namÉunicode㐀㐁㨀㨁䶴䶵\\/?\",\"FilterPreciseSet\": true,\"IndexPreciseSet\": true,\"IndexImpactScore\": \"High\"},{\"FilterExpression\": \"(ROOT.age > 12)\",\"IndexSpec\": \"\\/age\\/?\",\"FilterPreciseSet\": true,\"IndexPreciseSet\": true,\"IndexImpactScore\": \"High\"}],\"PotentialSingleIndexes\": [],\"UtilizedCompositeIndexes\": [],\"PotentialCompositeIndexes\": [{\"IndexSpecs\": [ \"\\/name ASC\", \"\\/age ASC\"],\"IndexPreciseSet\": false,\"IndexImpactScore\": \"High\"}] }",
                out IndexUtilizationInfo parsedInfo7));
            Assert.IsNotNull(parsedInfo7);

            StringBuilder stringBuilder = new StringBuilder();
            IndexMetricWriter indexMetricWriter = new IndexMetricWriter(stringBuilder);
            indexMetricWriter.WriteIndexMetrics(parsedInfo7);
            Console.WriteLine(stringBuilder.ToString());
        }
    }
}
