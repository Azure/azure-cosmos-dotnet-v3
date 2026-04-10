//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query.Metrics
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;
    using VisualStudio.TestTools.UnitTesting;

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
            IndexUtilizationInfoAccumulator accumulator = new IndexUtilizationInfoAccumulator();
            accumulator.Accumulate(MockIndexUtilizationInfo);
            accumulator.Accumulate(MockIndexUtilizationInfo);

            IndexUtilizationInfo doubleInfo = accumulator.GetIndexUtilizationInfo();
            Assert.AreEqual(2 * MockIndexUtilizationInfo.PotentialSingleIndexes.Count, doubleInfo.PotentialSingleIndexes.Count);
            Assert.AreEqual(2 * MockIndexUtilizationInfo.UtilizedSingleIndexes.Count, doubleInfo.UtilizedSingleIndexes.Count);
            Assert.AreEqual(2 * MockIndexUtilizationInfo.PotentialCompositeIndexes.Count, doubleInfo.PotentialCompositeIndexes.Count);
            Assert.AreEqual(2 * MockIndexUtilizationInfo.UtilizedCompositeIndexes.Count, doubleInfo.UtilizedCompositeIndexes.Count);
        }

        [TestMethod]
        public void TestBase64Parse()
        {
            TestParses(isBase64Encoded: true);
        }

        [TestMethod]
        public void TestTextParse()
        {
            TestParses(isBase64Encoded: false);
        }

        private static void TestParses(bool isBase64Encoded)
        {
            string[] testStrings = new string[]
            {
                // V1 Format 
                "{\"UtilizedSingleIndexes\": [{\"FilterExpression\": \"(ROOT.name = \\\"Julien\\\")\",\"IndexSpec\": \"\\/name\\/?\",\"FilterPreciseSet\": true,\"IndexPreciseSet\": true,\"IndexImpactScore\": \"High\"},{\"FilterExpression\": \"(ROOT.age > 12)\",\"IndexSpec\": \"\\/age\\/?\",\"FilterPreciseSet\": true,\"IndexPreciseSet\": true,\"IndexImpactScore\": \"High\"}],\"PotentialSingleIndexes\": [],\"UtilizedCompositeIndexes\": [],\"PotentialCompositeIndexes\": [{\"IndexSpecs\": [ \"\\/name ASC\", \"\\/age ASC\"],\"IndexPreciseSet\": false,\"IndexImpactScore\": \"High\"}] }",
                
                // Empty String
                "",
                string.Empty,
                
                // Valid Json missing fields on SingleIndexUtilization objects
                "{\"UtilizedSingleIndexes\": [{\"IndexSpec\": \"\\/name\\/?\",\"IndexPreciseSet\": true,\"IndexImpactScore\": \"High\"},{\"FilterExpression\": \"(ROOT.age > 12)\",\"IndexSpec\": \"\\/age\\/?\",\"FilterPreciseSet\": true,\"IndexPreciseSet\": true}],\"PotentialSingleIndexes\": [],\"UtilizedCompositeIndexes\": [],\"PotentialCompositeIndexes\": [{\"IndexSpecs\": [],\"IndexPreciseSet\": false}] }",

                // Valid Json having SingleIndexUtilization objects as empty object
                "{\"UtilizedSingleIndexes\": [{}],\"PotentialSingleIndexes\": [],\"UtilizedCompositeIndexes\": [],\"PotentialCompositeIndexes\": [{\"IndexSpecs\": [ \"\\/name ASC\", \"\\/age ASC\"],\"IndexPreciseSet\": false,\"IndexImpactScore\": \"High\"}] }",

                // Valid Json missing entire IndexUtilization arrays
                "{\"UtilizedCompositeIndexes\": [],\"PotentialCompositeIndexes\": [{\"IndexSpecs\": [ \"\\/name ASC\", \"\\/age ASC\"],\"IndexPreciseSet\": false,\"IndexImpactScore\": \"High\"}] }",

                // Unicode character
                "{\"UtilizedSingleIndexes\": [{\"FilterExpression\": \"(ROOT[\\\"unicode㐀㐁㨀㨁䶴䶵\\\"] = \\\"unicode㐀㐁㨀㨁䶴䶵\\\")\",\"IndexSpec\": \"\\/namÉunicode㐀㐁㨀㨁䶴䶵\\/?\",\"FilterPreciseSet\": true,\"IndexPreciseSet\": true,\"IndexImpactScore\": \"High\"},{\"FilterExpression\": \"(ROOT.age > 12)\",\"IndexSpec\": \"\\/age\\/?\",\"FilterPreciseSet\": true,\"IndexPreciseSet\": true,\"IndexImpactScore\": \"High\"}],\"PotentialSingleIndexes\": [],\"UtilizedCompositeIndexes\": [],\"PotentialCompositeIndexes\": [{\"IndexSpecs\": [ \"\\/name ASC\", \"\\/age ASC\"],\"IndexPreciseSet\": false,\"IndexImpactScore\": \"High\"}] }",

                // Valid Json adding new fields in SingleIndexUtilization objects
                "{\"UtilizedSingleIndexes\": [{\"FilterExpression\": \"(ROOT.name = \\\"Julien\\\")\",\"IndexSpec\": \"\\/name\\/?\",\"FilterPreciseSet\": true,\"IndexPreciseSet\": true,\"IndexImpactScore\": \"High\"},{\"FilterExpression\": \"(ROOT.age > 12)\",\"IndexSpec\": \"\\/age\\/?\",\"FilterPreciseSet\": true,\"IndexPreciseSet\": true,\"IndexImpactScore\": \"High\", \"RU Cost\": 10}],\"PotentialSingleIndexes\": [],\"UtilizedCompositeIndexes\": [],\"PotentialCompositeIndexes\": [{\"IndexSpecs\": [ \"\\/name ASC\", \"\\/age ASC\"],\"IndexPreciseSet\": false,\"IndexImpactScore\": \"High\"}] }",
                
                // Valid Json adding entire IndexUtilization arrays
                "{\"UtilizedSingleIndexes\": [{\"FilterExpression\": \"(ROOT.name = \\\"Julien\\\")\",\"IndexSpec\": \"\\/name\\/?\",\"FilterPreciseSet\": true,\"IndexPreciseSet\": true,\"IndexImpactScore\": \"High\"},{\"FilterExpression\": \"(ROOT.age > 12)\",\"IndexSpec\": \"\\/age\\/?\",\"FilterPreciseSet\": true,\"IndexPreciseSet\": true,\"IndexImpactScore\": \"High\", \"RU Cost\": 10}],\"PotentialSingleIndexes\": [],\"UtilizedCompositeIndexes\": [],\"PotentialCompositeIndexes\": [{\"IndexSpecs\": [ \"\\/name ASC\", \"\\/age ASC\"],\"IndexPreciseSet\": false,\"IndexImpactScore\": \"High\"}], \"DefinitelyNotUsefulCompositeIndexes\": [{\"IndexSpecs\": [ \"\\/name ASC\", \"\\/age ASC\"],\"IndexPreciseSet\": false,\"IndexImpactScore\": \"High\"}] }",
            };

            foreach (string testString in testStrings)
            {
                if (isBase64Encoded)
                {
                    Assert.IsTrue(IndexUtilizationInfo.TryCreateFromDelimitedBase64String(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(testString)),
                        out IndexUtilizationInfo parsedInfo));
                    Assert.IsNotNull(parsedInfo);
                }
                else
                {
                    Assert.IsTrue(IndexMetricsInfo.TryCreateFromString(testString, out _));
                }
            }
        }
    }
}