//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Text;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    /// <summary>
    /// Tests for <see cref="PartitionKeyInternal"/> class.
    /// </summary>
    [TestClass]
    public class DirectPartitionKeyInternalTests
    {
        /// <summary>
        /// Tests binary encoding of partition key
        /// </summary>
        [TestMethod]
        [Owner("vichhabr")]
        public void TestPartitionKeyBinaryEncodingMultiHash()
        {
            Tuple<string, string>[] testCases =
            {
                Tuple.Create<string, string>(@"[]", string.Empty),
                Tuple.Create<string, string>(@"[null]", "378867E4430E67857ACE5C908374FE16"),
                Tuple.Create<string, string>(@"[5.0]", "19C08621B135968252FB34B4CF66F811"),
                Tuple.Create<string, string>(@"[5.12312419050912359123]", "0EF2E2D82460884AF0F6440BE4F726A8"),
                Tuple.Create<string, string>(@"[5.0, 5.12312419050912359123]", "19C08621B135968252FB34B4CF66F8110EF2E2D82460884AF0F6440BE4F726A8"),
                Tuple.Create<string, string>(@"[5.0, ""redmond"", true]", "19C08621B135968252FB34B4CF66F81122E342F38A486A088463DFF7838A59630E711127C5B5A8E4726AC6DD306A3E59"),
                Tuple.Create<string, string>(@"[5.0, ""redmond"", null]", "19C08621B135968252FB34B4CF66F81122E342F38A486A088463DFF7838A5963378867E4430E67857ACE5C908374FE16"),
                Tuple.Create<string, string>(@"[10000, ""redmond"", null]","372CADABC129BCCBFF7994234D07922022E342F38A486A088463DFF7838A5963378867E4430E67857ACE5C908374FE16"),
                Tuple.Create<string, string>(@"[""redmond"", ""redmond""]", "22E342F38A486A088463DFF7838A596322E342F38A486A088463DFF7838A5963"),
                Tuple.Create<string, string>(@"[""seattle"", ""redmond"",]", "07E4D14180A45153F00B44907886F85622E342F38A486A088463DFF7838A5963"),
            };

            foreach (Tuple<string, string> testCase in testCases)
            {
                ValidateEffectivePartitionKeyForMultiHash(testCase.Item1, testCase.Item2);
            }
        }

        /// <summary>
        /// Tests binary encoding of partition key
        /// </summary>
        [TestMethod]
        [Owner("vichhabr")]
        public void TestPrefixPartitionKeyExtraction()
        {
            Tuple<string, string>[] testCases =
            {
                Tuple.Create<string, string>(@"[]", string.Empty),
                Tuple.Create<string, string>(@"[null]", "378867E4430E67857ACE5C908374FE16"),
                Tuple.Create<string, string>(@"[5.0]", "19C08621B135968252FB34B4CF66F811"),
                Tuple.Create<string, string>(@"[5.12312419050912359123]", "0EF2E2D82460884AF0F6440BE4F726A8"),
                Tuple.Create<string, string>(@"[5.0, 5.12312419050912359123]", "19C08621B135968252FB34B4CF66F8110EF2E2D82460884AF0F6440BE4F726A8"),
                Tuple.Create<string, string>(@"[5.0, ""redmond"", true]", "19C08621B135968252FB34B4CF66F81122E342F38A486A088463DFF7838A59630E711127C5B5A8E4726AC6DD306A3E59"),
                Tuple.Create<string, string>(@"[5.0, ""redmond"", null]", "19C08621B135968252FB34B4CF66F81122E342F38A486A088463DFF7838A5963378867E4430E67857ACE5C908374FE16"),
                Tuple.Create<string, string>(@"[10000, ""redmond"", null]","372CADABC129BCCBFF7994234D07922022E342F38A486A088463DFF7838A5963378867E4430E67857ACE5C908374FE16"),
                Tuple.Create<string, string>(@"[""redmond"", ""redmond""]", "22E342F38A486A088463DFF7838A596322E342F38A486A088463DFF7838A5963"),
                Tuple.Create<string, string>(@"[""seattle"", ""redmond"",]", "07E4D14180A45153F00B44907886F85622E342F38A486A088463DFF7838A5963"),
            };

            foreach (Tuple<string, string> testCase in testCases)
            {
                ValidatePrefixEffectivePartitionKeyForMultiHash(testCase.Item1, testCase.Item2);
            }
        }

        /// <summary>
        /// Tests finding Nth point of two effective partition keys for hash partitioning.
        /// </summary>
        [TestMethod]
        [Owner("harsudan")]
        public void TestNEqualRangeEffectivePartitionKeysPointForV1()
        {
            PartitionKeyDefinition partitionKey = new PartitionKeyDefinition
            {
                Kind = PartitionKind.Hash
            };

            string middle1 = PartitionKeyInternal.GetNEqualRangeEffectivePartitionKeys(
                PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
                PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
                partitionKey,
                2)[0];

            string expected1 = PartitionKeyInternal.GetMaxExclusiveEffectivePartitionKey(0, 2, partitionKey);

            Assert.AreEqual(expected1, middle1);

            try
            {
                string middle2 = PartitionKeyInternal.GetNEqualRangeEffectivePartitionKeys(
                    PartitionKeyInternal.GetMinInclusiveEffectivePartitionKey(3, 8, partitionKey),
                    PartitionKeyInternal.GetMinInclusiveEffectivePartitionKey(3, 8, partitionKey),
                    partitionKey,
                    2)[0];
                Assert.Fail();
            }
            catch (InvalidOperationException)
            {
            }

            string middle3 = PartitionKeyInternal.GetNEqualRangeEffectivePartitionKeys(
                PartitionKeyInternal.GetMinInclusiveEffectivePartitionKey(24, 25, partitionKey),
                PartitionKeyInternal.GetMaxExclusiveEffectivePartitionKey(24, 25, partitionKey),
                partitionKey,
                2)[0];

            Assert.AreEqual("05C1EFAF0B1F46", middle3);
            Assert.IsTrue(StringComparer.Ordinal.Compare(middle3, PartitionKeyInternal.GetMinInclusiveEffectivePartitionKey(24, 25, partitionKey)) > 0);
            Assert.IsTrue(StringComparer.Ordinal.Compare(middle3, PartitionKeyInternal.GetMaxExclusiveEffectivePartitionKey(24, 25, partitionKey)) < 0);

            string[] ranges = PartitionKeyInternal.GetNEqualRangeEffectivePartitionKeys(
                PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
                PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
                partitionKey,
                10);

            Assert.AreEqual(ranges.Length, 9);

            for (int i = 0; i < ranges.Length; i++)
            {
                Assert.AreEqual(ranges[i], PartitionKeyInternal.GetMinInclusiveEffectivePartitionKey(i + 1, 10, partitionKey));
            }
        }

        /// <summary>
        /// Tests finding middle of two effective partition keys for hash partitioning.
        /// </summary>
        [TestMethod]
        [Owner("harsudan")]
        public void TestNEqualRangeEffectivePartitionKeysPointForV2()
        {
            PartitionKeyDefinition partitionKey = new PartitionKeyDefinition
            {
                Kind = PartitionKind.Hash,
                Version = PartitionKeyDefinitionVersion.V2
            };
            string middle1 = PartitionKeyInternal.GetNEqualRangeEffectivePartitionKeys(
                PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
                PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
                partitionKey,
                2)[0];

            string expected1 = PartitionKeyInternal.GetMaxExclusiveEffectivePartitionKey(0, 2, partitionKey);

            Assert.AreEqual(expected1, middle1);

            try
            {
                string middle2 = PartitionKeyInternal.GetNEqualRangeEffectivePartitionKeys(
                    PartitionKeyInternal.GetMinInclusiveEffectivePartitionKey(3, 8, partitionKey),
                    PartitionKeyInternal.GetMinInclusiveEffectivePartitionKey(3, 8, partitionKey),
                    partitionKey,
                    2)[0];
                Assert.Fail("An empty effective partition-key range must be rejected.");
            }
            catch (InvalidOperationException)
            {
            }

            string middle3 = PartitionKeyInternal.GetNEqualRangeEffectivePartitionKeys(
                PartitionKeyInternal.GetMinInclusiveEffectivePartitionKey(24, 25, partitionKey),
                PartitionKeyInternal.GetMaxExclusiveEffectivePartitionKey(24, 25, partitionKey),
                partitionKey,
                2)[0];

            Assert.IsTrue(StringComparer.Ordinal.Compare(middle3, PartitionKeyInternal.GetMinInclusiveEffectivePartitionKey(24, 25, partitionKey)) > 0);
            Assert.IsTrue(StringComparer.Ordinal.Compare(middle3, PartitionKeyInternal.GetMaxExclusiveEffectivePartitionKey(24, 25, partitionKey)) < 0);

            string[] ranges = PartitionKeyInternal.GetNEqualRangeEffectivePartitionKeys(
                PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
                PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
                partitionKey,
                10);

            Assert.AreEqual(ranges.Length, 9);

            for (int i = 0; i < ranges.Length; i++)
            {
                Assert.AreEqual(ranges[i], PartitionKeyInternal.GetMinInclusiveEffectivePartitionKey(i + 1, 10, partitionKey));
            }
        }



        private static void ValidateMidPointForMultiHash(
            PartitionKeyDefinition partitionKeyDefinition,
            string minEPK,
            string maxEPK,
            string expectedMidPointEPK)
        {
            string determinedMidPoint = PartitionKeyInternal.GetMiddleRangeEffectivePartitionKey(
               minEPK,
               maxEPK,
               partitionKeyDefinition);

            Assert.AreEqual(expectedMidPointEPK, determinedMidPoint, $"Expected MidPoint {expectedMidPointEPK} not equal to {determinedMidPoint}");

            Range<string> epkRange = new Range<string>(minEPK, maxEPK, true, false);
            _ = new Range<string>(determinedMidPoint, determinedMidPoint, true, true);

            Assert.IsTrue(epkRange.Contains(determinedMidPoint), $"Computed Midpoint {determinedMidPoint} must be in range of {minEPK} and {maxEPK}");
        }

        /// <summary>
        /// Tests finding middle of two effective partition keys for hash partitioning.
        /// </summary>
        [TestMethod]
        [Owner("vichhabr")]
        public void TestMidPointMultiHash()
        {
            PartitionKeyDefinition partitionKey = new PartitionKeyDefinition
            {
                Kind = PartitionKind.MultiHash,
                Version = PartitionKeyDefinitionVersion.V2
            };
            partitionKey.Paths.Add("/Path1");
            partitionKey.Paths.Add("/Path2");
            partitionKey.Paths.Add("/Path3");

            /* Test Suite 1: Midpoint between 0 and FF is 1FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF
             * Ensure that if prefix values for min and max are the same,
             * the right midpoint for the next immediate subrange is considered.
            */
            {
                ValidateMidPointForMultiHash(partitionKey,
                    PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey, //0
                    PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey, //FF
                    "1FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF");

                ValidateMidPointForMultiHash(partitionKey,
                    "07E4D14180A45153F00B44907886F856",
                    "07E4D14180A45153F00B44907886F856" + "FF",
                    "07E4D14180A45153F00B44907886F856" + "1FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF");

                ValidateMidPointForMultiHash(partitionKey,
                    "07E4D14180A45153F00B44907886F856" + "07E4D14180A45153F00B44907886F856",
                    "07E4D14180A45153F00B44907886F856" + "07E4D14180A45153F00B44907886F856" + "FF",
                    "07E4D14180A45153F00B44907886F856" + "07E4D14180A45153F00B44907886F856" + "1FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF");

            }

            /* Test Suite 2: Validate midpoints for EPK that differ in length is determined correctly.,
             */
            {
                ValidateMidPointForMultiHash(partitionKey,
                    "07E4D14180A45153F00B44907886F856",
                    "07E4D14180A45153F00B44907886F856" + "20000000000000000000000000000000",
                    "07E4D14180A45153F00B44907886F856" + "10000000000000000000000000000000");

                ValidateMidPointForMultiHash(partitionKey,
                    "07E4D14180A45153F00B44907886F856",
                    "07E4D14180A45153F00B44907886F856" + "20000000000000000000000000000000" + "10000000000000000000000000000000",
                    "07E4D14180A45153F00B44907886F856" + "10000000000000000000000000000000");
            }

            /* Test Suite 3: Validate midpoints for EPK's which differ by value 1 in the prefix correctly determine the midpoint
            */
            {
                ValidateMidPointForMultiHash(partitionKey,
                    "07E4D14180A45153F00B44907886F856" + "1FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF",
                    "07E4D14180A45153F00B44907886F857" + "10000000000000000000000000000000",
                    "07E4D14180A45153F00B44907886F856" + "2FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF");

                ValidateMidPointForMultiHash(partitionKey,
                   "07E4D14180A45153F00B44907886F856" + "07E4D14180A45153F00B44907886F856" + "1FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF",
                   "07E4D14180A45153F00B44907886F856" + "07E4D14180A45153F00B44907886F857" + "10000000000000000000000000000000",
                   "07E4D14180A45153F00B44907886F856" + "07E4D14180A45153F00B44907886F856" + "2FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF");
            }
        }

        [TestMethod]
        [Owner("vichhabr")]
        public void TestPrefixPartitionKeyExtractionWithRange()
        {
            PartitionKeyDefinition multiPartPartitionKeyDefinition = new PartitionKeyDefinition
            {
                Kind = PartitionKind.MultiHash,
                Version = PartitionKeyDefinitionVersion.V2
            };
            multiPartPartitionKeyDefinition.Paths.Add("/Path1");
            multiPartPartitionKeyDefinition.Paths.Add("/Path2");
            multiPartPartitionKeyDefinition.Paths.Add("/Path3");

            Tuple<string, string>[] testCases =
            {
                Tuple.Create<string, string>(@"[]", string.Empty),
                Tuple.Create<string, string>(@"[null]", "378867E4430E67857ACE5C908374FE16"),
                Tuple.Create<string, string>(@"[5.0]", "19C08621B135968252FB34B4CF66F811"),
                Tuple.Create<string, string>(@"[5.12312419050912359123]", "0EF2E2D82460884AF0F6440BE4F726A8"),
                Tuple.Create<string, string>(@"[5.0, 5.12312419050912359123]", "19C08621B135968252FB34B4CF66F8110EF2E2D82460884AF0F6440BE4F726A8"),
                Tuple.Create<string, string>(@"[""redmond"", ""redmond""]", "22E342F38A486A088463DFF7838A596322E342F38A486A088463DFF7838A5963"),
                Tuple.Create<string, string>(@"[""seattle"", ""redmond"",]", "07E4D14180A45153F00B44907886F85622E342F38A486A088463DFF7838A5963"),
            };

            foreach (Tuple<string, string> testCase in testCases)
            {
                ValidatePrefixPartitionKeyRange(
                    testCase.Item1,
                    Tuple.Create<string, string>(testCase.Item2, testCase.Item2 + PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey),
                    multiPartPartitionKeyDefinition);
            }
        }

        private static void TestRangePropertiesOnEPKRangesForMultiHash(
            PartitionKeyDefinition partitionKeyDefinition,
            object[] minKey,
            object[] maxKey,
            Predicate<Range<string>> validator)
        {
            Range<PartitionKeyInternal> prefixKey = new Range<PartitionKeyInternal>(
                min: PartitionKeyInternal.FromObjectArray(minKey, true),
                max: PartitionKeyInternal.FromObjectArray(maxKey, true),
                isMinInclusive: true,
                isMaxInclusive: true);

            Range<string> epkRange = PartitionKeyInternal.GetEffectivePartitionKeyRange(partitionKeyDefinition, prefixKey);

            Assert.IsTrue(validator(epkRange));
        }

        [TestMethod]
        [Owner("vichhabr")]
        public void TestRangePropertiesOnEPKRangesForMultiHash()
        {
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition()
            {
                Paths = new Collection<string>() { "/state", "/city", "/zipcode" },
                Version = PartitionKeyDefinitionVersion.V2,
                Kind = PartitionKind.MultiHash,
            };

            TestRangePropertiesOnEPKRangesForMultiHash(
                partitionKeyDefinition,
                new object[] { "seattle" },
                new object[] { "seattle" },
                epkRange => !epkRange.IsEmpty &&
                    !epkRange.IsSingleValue);

            TestRangePropertiesOnEPKRangesForMultiHash(
                partitionKeyDefinition,
                new object[] { "seattle" },
                new object[] { "seattle", "redmond" },
                epkRange => !epkRange.IsEmpty &&
                    !epkRange.IsSingleValue);

            TestRangePropertiesOnEPKRangesForMultiHash(
                partitionKeyDefinition,
                new object[] { "seattle", "redmond" },
                new object[] { "seattle", "redmond" },
                epkRange => !epkRange.IsEmpty &&
                    !epkRange.IsSingleValue);

            TestRangePropertiesOnEPKRangesForMultiHash(
                partitionKeyDefinition,
                new object[] { "seattle", "redmond", "98052" },
                new object[] { "seattle", "redmond", "98052" },
                epkRange => !epkRange.IsEmpty &&
                    epkRange.IsSingleValue);

            TestRangePropertiesOnEPKRangesForMultiHash(
               partitionKeyDefinition,
               Array.Empty<object>(),
               Array.Empty<object>(),
               epkRange => !epkRange.IsEmpty &&
                   !epkRange.IsSingleValue);

            TestRangePropertiesOnEPKRangesForMultiHash(
               partitionKeyDefinition,
               Array.Empty<object>(),
               new object[] { "seattle" },
               epkRange => !epkRange.IsEmpty &&
                   !epkRange.IsSingleValue);

            TestRangePropertiesOnEPKRangesForMultiHash(
               partitionKeyDefinition,
               new object[] { "seattle", "redmond" },
               new object[] { "seattle", "redmond", "98052" },
               epkRange => !epkRange.IsEmpty &&
                   !epkRange.IsSingleValue);
        }

        [TestMethod]
        [Owner("vichhabr")]
        public void TestHierarchicalPropertiesOnEPKRanges()
        {
            PartitionKeyDefinition hierarchicalParitionKeyDefinition = new PartitionKeyDefinition
            {
                Kind = PartitionKind.MultiHash,
                Version = PartitionKeyDefinitionVersion.V2
            };
            hierarchicalParitionKeyDefinition.Paths.Add("/state");
            hierarchicalParitionKeyDefinition.Paths.Add("/city");
            hierarchicalParitionKeyDefinition.Paths.Add("/zipcode");

            //Points
            PartitionKeyInternal waRedPoint1 = PartitionKeyInternal.FromJsonString(@"[""WA"", ""redmond"", ""98052""]");
            PartitionKeyInternal waRedPoint2 = PartitionKeyInternal.FromJsonString(@"[""WA"", ""redmond"", ""98008""]");
            PartitionKeyInternal waSePoint1 = PartitionKeyInternal.FromJsonString(@"[""WA"", ""seattle"", ""98101""]");
            PartitionKeyInternal paPoint1 = PartitionKeyInternal.FromJsonString(@"[""PA"", ""Pittsburgh"", ""15232""]");

            //Areas
            PartitionKeyInternal waRedArea = PartitionKeyInternal.FromJsonString(@"[""WA"", ""redmond""]");
            PartitionKeyInternal waArea = PartitionKeyInternal.FromJsonString(@"[""WA""]");

            //Points belong in areas test
            Routing.Range<string> waRedAreaRange = waRedArea.GetEPKRangeForPrefixPartitionKey(hierarchicalParitionKeyDefinition);
            Assert.IsTrue(waRedAreaRange.Contains(waRedPoint1.GetEffectivePartitionKeyString(hierarchicalParitionKeyDefinition)));
            Assert.IsTrue(waRedAreaRange.Contains(waRedPoint2.GetEffectivePartitionKeyString(hierarchicalParitionKeyDefinition)));

            //higher hierarchy belongs test
            Routing.Range<string> waAreaRange = waArea.GetEPKRangeForPrefixPartitionKey(hierarchicalParitionKeyDefinition);
            Assert.IsTrue(waAreaRange.Contains(waRedPoint1.GetEffectivePartitionKeyString(hierarchicalParitionKeyDefinition)));
            Assert.IsTrue(waAreaRange.Contains(waRedPoint2.GetEffectivePartitionKeyString(hierarchicalParitionKeyDefinition)));
            Assert.IsTrue(waAreaRange.Contains(waSePoint1.GetEffectivePartitionKeyString(hierarchicalParitionKeyDefinition)));

            //Should not belong tests
            Assert.IsFalse(waRedAreaRange.Contains(waSePoint1.GetEffectivePartitionKeyString(hierarchicalParitionKeyDefinition)));
            Assert.IsFalse(waAreaRange.Contains(paPoint1.GetEffectivePartitionKeyString(hierarchicalParitionKeyDefinition)));
        }


        [TestMethod]
        [Owner("vichhabr")]
        public void ValidateWidthForHashV2Containers()
        {
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition
            {
                Kind = PartitionKind.Hash,
                Version = PartitionKeyDefinitionVersion.V2
            };
            partitionKeyDefinition.Paths.Add("/id");

            Assert.IsTrue(
                1 ==
                PartitionKeyInternal.GetWidth(
                    PartitionKeyInternal.InclusiveMinimum.GetEffectivePartitionKeyString(partitionKeyDefinition),
                    PartitionKeyInternal.ExclusiveMaximum.GetEffectivePartitionKeyString(partitionKeyDefinition),
                    partitionKeyDefinition));

            Assert.IsTrue(
                0 ==
                PartitionKeyInternal.GetWidth(
                    "20000000000000000000000000000000",
                    "20000000000000000000000000000000",
                    partitionKeyDefinition));

            Assert.IsTrue(
               0.5 ==
               PartitionKeyInternal.GetWidth(
                   "1FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF",
                   "FF",
                   partitionKeyDefinition));

            Assert.IsTrue(
               0.2 ==
               PartitionKeyInternal.GetWidth(
                   "",
                   "0CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC",
                   partitionKeyDefinition));

            Assert.IsTrue(
               0.1 ==
               PartitionKeyInternal.GetWidth(
                   "39999999999999999999999999999997",
                   "FF",
                   partitionKeyDefinition));

            Assert.IsTrue(
               0.1 ==
               PartitionKeyInternal.GetWidth(
                   "39999999999999990000000000000000",
                   "FF",
                   partitionKeyDefinition));

        }
        [TestMethod]
        [Owner("vichhabr")]
        public void TestMultiHashPartitionKeyValidationAndBoundaries()
        {
            // Test 1: Validate partition key constraints

            // Valid case - single path
            PartitionKeyDefinition validSinglePath = new PartitionKeyDefinition
            {
                Paths = new Collection<string> { "/id" },
                Kind = PartitionKind.MultiHash
            };
            PartitionKeyInternal.ValidateMultiHashPartitionKey(validSinglePath); // Should pass

            // Valid case - multiple paths
            PartitionKeyDefinition validMultiPath = new PartitionKeyDefinition
            {
                Paths = new Collection<string> { "/id", "/pk", "/timestamp" },
                Kind = PartitionKind.MultiHash
            };
            PartitionKeyInternal.ValidateMultiHashPartitionKey(validMultiPath); // Should pass

            // Invalid cases
            PartitionKeyDefinition emptyPaths = new PartitionKeyDefinition
            {
                Paths = new Collection<string>(),
                Kind = PartitionKind.MultiHash
            };

            Assert.ThrowsException<BadRequestException>(() => PartitionKeyInternal.ValidateMultiHashPartitionKey(emptyPaths),
                "Should throw BadRequestException for empty paths");

            // Test 2: Verify EPK boundaries for different partition counts
            int[] testPartitionCounts = new[] { 1, 2, 4, 16 };
            int[] testPathCounts = new[] { 1, 2, 3 };

            foreach (int partitionCount in testPartitionCounts)
            {
                foreach (int pathCount in testPathCounts)
                {
                    // For each partition
                    for (int partitionIndex = 0; partitionIndex < partitionCount; partitionIndex++)
                    {
                        // Get min and max EPKs for current partition
                        string minEpk = PartitionKeyInternal.GetMinInclusiveEffectivePartitionKey(
                            partitionIndex,
                            partitionCount,
                            new PartitionKeyDefinition
                            {
                                Kind = PartitionKind.MultiHash,
                                Paths = new Collection<string>(Enumerable.Range(0, pathCount).Select(i => $"/path{i}").ToList())
                            },
                            useHashV2asDefault: true,
                            enablePadding: true);

                        string maxEpk = PartitionKeyInternal.GetMaxExclusiveEffectivePartitionKey(
                            partitionIndex,
                            partitionCount,
                            new PartitionKeyDefinition
                            {
                                Kind = PartitionKind.MultiHash,
                                Paths = new Collection<string>(Enumerable.Range(0, pathCount).Select(i => $"/path{i}").ToList())
                            },
                            useHashV2asDefault: true,
                            enablePadding: true);

                        Assert.IsNotNull(minEpk);
                        Assert.IsNotNull(maxEpk);


                        if (partitionIndex == 0)
                        {
                            Assert.AreEqual(PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey.Length, minEpk.Length);
                        }
                        else if (partitionIndex == partitionCount - 1)
                        {
                            Assert.AreEqual(PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey.Length, maxEpk.Length);
                        }
                        else
                        {
                            // Verify length (each path contributes 32 hex chars)
                            Assert.AreEqual(pathCount * 32, minEpk.Length);
                            Assert.AreEqual(pathCount * 32, maxEpk.Length);
                        }

                        // Verify continuity between partitions
                        if (partitionIndex > 0)
                        {
                            string prevMaxEpk = PartitionKeyInternal.GetMaxExclusiveEffectivePartitionKey(
                                partitionIndex - 1,
                                partitionCount,
                                new PartitionKeyDefinition
                                {
                                    Kind = PartitionKind.MultiHash,
                                    Paths = new Collection<string>(Enumerable.Range(0, pathCount).Select(i => $"/path{i}").ToList())
                                },
                                useHashV2asDefault: true,
                                enablePadding: true);
                            Assert.AreEqual(minEpk, prevMaxEpk,
                                $"Gap detected between partition {partitionIndex - 1} and {partitionIndex}");
                        }
                    }
                }
            }
        }


        [TestMethod]
        [Owner("vichhabr")]
        public void TestMultiHashPartitionKeyBoundariesWithPadding()
        {
            const int BytesPerPath = 16; // Each path is 16 bytes (128 bits)
            const int HexCharsPerPath = BytesPerPath * 2; // 32 hex chars per path

            int[] partitionCounts = { 1, 2, 4 };
            int[] pathCounts = { 1, 2, 3 };

            Dictionary<(int partitionCount, int pathCount, int partitionIndex), (string minimumEffectivePartitionKeyUnpadded, string maximumEffectivePartitionKeyUnpadded, string minimumEffectivePartitionKeyPadded, string maximumEffectivePartitionKeyPadded)> expectedEpkValues = new System.Collections.Generic.Dictionary<(int partitionCount, int pathCount, int partitionIndex), (string minimumEffectivePartitionKeyUnpadded, string maximumEffectivePartitionKeyUnpadded, string minimumEffectivePartitionKeyPadded, string maximumEffectivePartitionKeyPadded)>
            {
                { (4, 2, 1), ("0FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF", "1FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFE", "0FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF00000000000000000000000000000000", "1FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFE00000000000000000000000000000000") },
                { (4, 2, 2), ("1FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFE", "2FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFD", "1FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFE00000000000000000000000000000000", "2FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFD00000000000000000000000000000000") },
                { (4, 3, 1), ("0FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF", "1FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFE", "0FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF0000000000000000000000000000000000000000000000000000000000000000", "1FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFE0000000000000000000000000000000000000000000000000000000000000000") },
                { (4, 3, 2), ("1FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFE", "2FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFD", "1FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFE0000000000000000000000000000000000000000000000000000000000000000", "2FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFD0000000000000000000000000000000000000000000000000000000000000000") }
            };

            foreach (int partitionCount in partitionCounts)
            {
                foreach (int pathCount in pathCounts)
                {
                    PartitionKeyDefinition pkDef = new PartitionKeyDefinition
                    {
                        Kind = PartitionKind.MultiHash,
                        Version = PartitionKeyDefinitionVersion.V2
                    };
                    for (int i = 0; i < pathCount; i++)
                    {
                        pkDef.Paths.Add($"/path{i}");
                    }

                    for (int partitionIndex = 0; partitionIndex < partitionCount; partitionIndex++)
                    {
                        // No padding
                        string minimumEffectivePartitionKeyUnpadded = PartitionKeyInternal.GetMinInclusiveEffectivePartitionKey(
                            partitionIndex, partitionCount, pkDef, useHashV2asDefault: true, enablePadding: false);
                        string maximumEffectivePartitionKeyUnpadded = PartitionKeyInternal.GetMaxExclusiveEffectivePartitionKey(
                            partitionIndex, partitionCount, pkDef, useHashV2asDefault: true, enablePadding: false);

                        // With padding
                        string minimumEffectivePartitionKeyPadded = PartitionKeyInternal.GetMinInclusiveEffectivePartitionKey(
                            partitionIndex, partitionCount, pkDef, useHashV2asDefault: true, enablePadding: true);
                        string maximumEffectivePartitionKeyPadded = PartitionKeyInternal.GetMaxExclusiveEffectivePartitionKey(
                            partitionIndex, partitionCount, pkDef, useHashV2asDefault: true, enablePadding: true);

                        // Special case: first partition, no padding for extreme case
                        if (partitionIndex == 0)
                        {
                            Assert.AreEqual(0, minimumEffectivePartitionKeyUnpadded.Length, $"NoPad MinEPK: partitionCount={partitionCount}, pathCount={pathCount}, partitionIndex={partitionIndex}");
                            Assert.AreEqual(0, minimumEffectivePartitionKeyPadded.Length, $"Pad MinEPK: partitionCount={partitionCount}, pathCount={pathCount}, partitionIndex={partitionIndex}");
                            continue;
                        }

                        // Special case: last partition, no padding for extreme case
                        if (partitionIndex == partitionCount - 1)
                        {
                            Assert.AreEqual(2, maximumEffectivePartitionKeyUnpadded.Length, $"NoPad MaxEPK: partitionCount={partitionCount}, pathCount={pathCount}, partitionIndex={partitionIndex}");
                            Assert.AreEqual(2, maximumEffectivePartitionKeyPadded.Length, $"Pad MaxEPK: partitionCount={partitionCount}, pathCount={pathCount}, partitionIndex={partitionIndex}");
                            continue;
                        }

                        // All other partitions: padded EPKs should have expected length
                        Assert.AreEqual(HexCharsPerPath * pathCount, minimumEffectivePartitionKeyPadded.Length, $"Pad MinEPK: partitionCount={partitionCount}, pathCount={pathCount}, partitionIndex={partitionIndex}");
                        Assert.AreEqual(HexCharsPerPath * pathCount, maximumEffectivePartitionKeyPadded.Length, $"Pad MaxEPK: partitionCount={partitionCount}, pathCount={pathCount}, partitionIndex={partitionIndex}");

                        // Continuity: min of current == max of previous
                        if (partitionIndex > 0)
                        {
                            string prevmaximumEffectivePartitionKeyPadded = PartitionKeyInternal.GetMaxExclusiveEffectivePartitionKey(
                                partitionIndex - 1,
                                partitionCount,
                                pkDef,
                                useHashV2asDefault: true,
                                enablePadding: true);
                            Assert.AreEqual(minimumEffectivePartitionKeyPadded, prevmaximumEffectivePartitionKeyPadded,
                                $"Gap detected between partition {partitionIndex - 1} and {partitionIndex} (padded)");
                        }

                        // Exact value checks for specific cases
                        if (expectedEpkValues.TryGetValue((partitionCount, pathCount, partitionIndex), out (string minimumEffectivePartitionKeyUnpadded, string maximumEffectivePartitionKeyUnpadded, string minimumEffectivePartitionKeyPadded, string maximumEffectivePartitionKeyPadded) expected))
                        {
                            Assert.AreEqual(expected.minimumEffectivePartitionKeyUnpadded, minimumEffectivePartitionKeyUnpadded, $"minimumEffectivePartitionKeyUnpadded mismatch for (partitionCount={partitionCount}, pathCount={pathCount}, partitionIndex={partitionIndex})");
                            Assert.AreEqual(expected.maximumEffectivePartitionKeyUnpadded, maximumEffectivePartitionKeyUnpadded, $"maximumEffectivePartitionKeyUnpadded mismatch for (partitionCount={partitionCount}, pathCount={pathCount}, partitionIndex={partitionIndex})");
                            Assert.AreEqual(expected.minimumEffectivePartitionKeyPadded, minimumEffectivePartitionKeyPadded, $"minimumEffectivePartitionKeyPadded mismatch for (partitionCount={partitionCount}, pathCount={pathCount}, partitionIndex={partitionIndex})");
                            Assert.AreEqual(expected.maximumEffectivePartitionKeyPadded, maximumEffectivePartitionKeyPadded, $"maximumEffectivePartitionKeyPadded mismatch for (partitionCount={partitionCount}, pathCount={pathCount}, partitionIndex={partitionIndex})");
                        }
                    }
                }
            }
        }

        [TestMethod]
        [Owner("vichhabr")]
        public void TestMultiHashThirdPathPaddingForTwoPathCollections()
        {
            const int BytesPerPath = 16; // Each path is 16 bytes (128 bits)
            const int HexCharsPerPath = BytesPerPath * 2; // 32 hex chars per path

            int[] partitionCounts = { 1, 2, 4, 8 };

            foreach (int partitionCount in partitionCounts)
            {
                // Create a 2-path MultiHash partition key definition (last path != /id)
                PartitionKeyDefinition pkDef = new PartitionKeyDefinition
                {
                    Kind = PartitionKind.MultiHash,
                    Version = PartitionKeyDefinitionVersion.V2
                };
                pkDef.Paths.Add("/path0");
                pkDef.Paths.Add("/path1");

                for (int partitionIndex = 0; partitionIndex < partitionCount; partitionIndex++)
                {
                    // With enablePadding=true but enableLastPathPaddingForMultiHash=false (should be 2 paths = 64 hex chars)
                    string minEpkWithoutThirdPath = PartitionKeyInternal.GetMinInclusiveEffectivePartitionKey(
                        partitionIndex, partitionCount, pkDef,
                        useHashV2asDefault: true,
                        enablePadding: true,
                        enableLastPathPaddingForMultiHash: false);

                    string maxEpkWithoutThirdPath = PartitionKeyInternal.GetMaxExclusiveEffectivePartitionKey(
                        partitionIndex, partitionCount, pkDef,
                        useHashV2asDefault: true,
                        enablePadding: true,
                        enableLastPathPaddingForMultiHash: false);

                    // With enablePadding=true and enableLastPathPaddingForMultiHash=true (should be 3 paths = 96 hex chars)
                    string minEpkWithThirdPath = PartitionKeyInternal.GetMinInclusiveEffectivePartitionKey(
                        partitionIndex, partitionCount, pkDef,
                        useHashV2asDefault: true,
                        enablePadding: true,
                        enableLastPathPaddingForMultiHash: true);

                    string maxEpkWithThirdPath = PartitionKeyInternal.GetMaxExclusiveEffectivePartitionKey(
                        partitionIndex, partitionCount, pkDef,
                        useHashV2asDefault: true,
                        enablePadding: true,
                        enableLastPathPaddingForMultiHash: true);

                    // Special case: first partition min is always empty
                    if (partitionIndex == 0)
                    {
                        Assert.AreEqual(0, minEpkWithoutThirdPath.Length,
                            $"MinEPK without 3rd path: partitionCount={partitionCount}, partitionIndex={partitionIndex}");
                        Assert.AreEqual(0, minEpkWithThirdPath.Length,
                            $"MinEPK with 3rd path: partitionCount={partitionCount}, partitionIndex={partitionIndex}");
                        continue;
                    }

                    // Special case: last partition max is always "FF"
                    if (partitionIndex == partitionCount - 1)
                    {
                        Assert.AreEqual(2, maxEpkWithoutThirdPath.Length,
                            $"MaxEPK without 3rd path: partitionCount={partitionCount}, partitionIndex={partitionIndex}");
                        Assert.AreEqual(2, maxEpkWithThirdPath.Length,
                            $"MaxEPK with 3rd path: partitionCount={partitionCount}, partitionIndex={partitionIndex}");
                        continue;
                    }

                    // Without third path padding: 2 paths = 64 hex chars
                    Assert.AreEqual(HexCharsPerPath * 2, minEpkWithoutThirdPath.Length,
                        $"MinEPK without 3rd path length: partitionCount={partitionCount}, partitionIndex={partitionIndex}");
                    Assert.AreEqual(HexCharsPerPath * 2, maxEpkWithoutThirdPath.Length,
                        $"MaxEPK without 3rd path length: partitionCount={partitionCount}, partitionIndex={partitionIndex}");

                    // With third path padding: 3 paths = 96 hex chars
                    Assert.AreEqual(HexCharsPerPath * 3, minEpkWithThirdPath.Length,
                        $"MinEPK with 3rd path length: partitionCount={partitionCount}, partitionIndex={partitionIndex}");
                    Assert.AreEqual(HexCharsPerPath * 3, maxEpkWithThirdPath.Length,
                        $"MaxEPK with 3rd path length: partitionCount={partitionCount}, partitionIndex={partitionIndex}");

                    // The first 64 chars should be identical (the actual hash value)
                    Assert.AreEqual(minEpkWithoutThirdPath, minEpkWithThirdPath[..(HexCharsPerPath * 2)],
                        $"MinEPK prefix mismatch: partitionCount={partitionCount}, partitionIndex={partitionIndex}");
                    Assert.AreEqual(maxEpkWithoutThirdPath, maxEpkWithThirdPath[..(HexCharsPerPath * 2)],
                        $"MaxEPK prefix mismatch: partitionCount={partitionCount}, partitionIndex={partitionIndex}");

                    // The extra path should be all zeros (32 hex chars of '0')
                    string thirdPathMin = minEpkWithThirdPath[(HexCharsPerPath * 2)..];
                    string thirdPathMax = maxEpkWithThirdPath[(HexCharsPerPath * 2)..];
                    Assert.AreEqual(new string('0', HexCharsPerPath), thirdPathMin,
                        $"MinEPK extra path should be zeros: partitionCount={partitionCount}, partitionIndex={partitionIndex}");
                    Assert.AreEqual(new string('0', HexCharsPerPath), thirdPathMax,
                        $"MaxEPK extra path should be zeros: partitionCount={partitionCount}, partitionIndex={partitionIndex}");

                    // Verify continuity between partitions
                    if (partitionIndex > 0)
                    {
                        string prevMaxEpkWithThirdPath = PartitionKeyInternal.GetMaxExclusiveEffectivePartitionKey(
                            partitionIndex - 1, partitionCount, pkDef,
                            useHashV2asDefault: true,
                            enablePadding: true,
                            enableLastPathPaddingForMultiHash: true);
                        Assert.AreEqual(minEpkWithThirdPath, prevMaxEpkWithThirdPath,
                            $"Gap detected between partition {partitionIndex - 1} and {partitionIndex} (with extra path)");
                    }
                }
            }
        }

        [TestMethod]
        [Owner("vichhabr")]
        public void TestMultiHashPaddingAppliesToAllPathCounts()
        {
            const int BytesPerPath = 16;
            const int HexCharsPerPath = BytesPerPath * 2;

            int partitionCount = 4;
            int partitionIndex = 1; // Middle partition to avoid edge cases

            // Test with 1 path (last path != /id) - flag should add an extra path
            PartitionKeyDefinition pkDef1Path = new PartitionKeyDefinition
            {
                Kind = PartitionKind.MultiHash,
                Version = PartitionKeyDefinitionVersion.V2
            };
            pkDef1Path.Paths.Add("/path0");

            string minEpk1PathWithFlag = PartitionKeyInternal.GetMinInclusiveEffectivePartitionKey(
                partitionIndex, partitionCount, pkDef1Path,
                useHashV2asDefault: true,
                enablePadding: true,
                enableLastPathPaddingForMultiHash: true);

            string minEpk1PathWithoutFlag = PartitionKeyInternal.GetMinInclusiveEffectivePartitionKey(
                partitionIndex, partitionCount, pkDef1Path,
                useHashV2asDefault: true,
                enablePadding: true,
                enableLastPathPaddingForMultiHash: false);

            // Without flag: 1 path = 32 hex chars
            Assert.AreEqual(HexCharsPerPath * 1, minEpk1PathWithoutFlag.Length,
                "1-path without flag should be 32 chars");
            // With flag: 1 path + 1 padded = 64 hex chars
            Assert.AreEqual(HexCharsPerPath * 2, minEpk1PathWithFlag.Length,
                "1-path with flag should be 64 chars (padded)");
            // First path should be the same
            Assert.AreEqual(minEpk1PathWithoutFlag, minEpk1PathWithFlag[..HexCharsPerPath],
                "1-path prefix should match");
            // Extra path should be all zeros
            Assert.AreEqual(new string('0', HexCharsPerPath), minEpk1PathWithFlag[HexCharsPerPath..],
                "1-path extra path should be zeros");

            // Test with 3 paths (last path != /id) - flag should add an extra path
            PartitionKeyDefinition pkDef3Paths = new PartitionKeyDefinition
            {
                Kind = PartitionKind.MultiHash,
                Version = PartitionKeyDefinitionVersion.V2
            };
            pkDef3Paths.Paths.Add("/path0");
            pkDef3Paths.Paths.Add("/path1");
            pkDef3Paths.Paths.Add("/path2");

            string minEpk3PathsWithFlag = PartitionKeyInternal.GetMinInclusiveEffectivePartitionKey(
                partitionIndex, partitionCount, pkDef3Paths,
                useHashV2asDefault: true,
                enablePadding: true,
                enableLastPathPaddingForMultiHash: true);

            string minEpk3PathsWithoutFlag = PartitionKeyInternal.GetMinInclusiveEffectivePartitionKey(
                partitionIndex, partitionCount, pkDef3Paths,
                useHashV2asDefault: true,
                enablePadding: true,
                enableLastPathPaddingForMultiHash: false);

            // Without flag: 3 paths = 96 hex chars
            Assert.AreEqual(HexCharsPerPath * 3, minEpk3PathsWithoutFlag.Length,
                "3-path without flag should be 96 chars");
            // With flag: 3 paths + 1 padded = 128 hex chars
            Assert.AreEqual(HexCharsPerPath * 4, minEpk3PathsWithFlag.Length,
                "3-path with flag should be 128 chars (padded)");
            // First 3 paths should be the same
            Assert.AreEqual(minEpk3PathsWithoutFlag, minEpk3PathsWithFlag[..(HexCharsPerPath * 3)],
                "3-path prefix should match");
            // Extra path should be all zeros
            Assert.AreEqual(new string('0', HexCharsPerPath), minEpk3PathsWithFlag[(HexCharsPerPath * 3)..],
                "3-path extra path should be zeros");
        }

        [TestMethod]
        [Owner("vichhabr")]
        public void TestMultiHashThirdPathPaddingRequiresEnablePadding()
        {
            const int BytesPerPath = 16;
            const int HexCharsPerPath = BytesPerPath * 2;

            int partitionCount = 4;
            int partitionIndex = 1;

            PartitionKeyDefinition pkDef = new PartitionKeyDefinition
            {
                Kind = PartitionKind.MultiHash,
                Version = PartitionKeyDefinitionVersion.V2
            };
            pkDef.Paths.Add("/path0");
            pkDef.Paths.Add("/path1");

            // enablePadding=false, enableLastPathPaddingForMultiHash=true
            // Third path padding should NOT be applied because enablePadding is false
            string minEpkNoPadding = PartitionKeyInternal.GetMinInclusiveEffectivePartitionKey(
                partitionIndex, partitionCount, pkDef,
                useHashV2asDefault: true,
                enablePadding: false,
                enableLastPathPaddingForMultiHash: true);

            // Without enablePadding, the EPK should be 32 chars (single hash value, no path padding)
            Assert.AreEqual(HexCharsPerPath, minEpkNoPadding.Length,
                "Without enablePadding, third path padding should not be applied");
        }

        [TestMethod]
        [Owner("vichhabr")]
        public void TestMultiHashPaddingSkippedWhenLastPathIsId()
        {
            const int BytesPerPath = 16;
            const int HexCharsPerPath = BytesPerPath * 2;

            int partitionCount = 4;
            int partitionIndex = 1;

            // 1 path with /id - no padding should be added
            PartitionKeyDefinition pkDef1PathId = new PartitionKeyDefinition
            {
                Kind = PartitionKind.MultiHash,
                Version = PartitionKeyDefinitionVersion.V2
            };
            pkDef1PathId.Paths.Add("/id");

            string minEpk1PathId = PartitionKeyInternal.GetMinInclusiveEffectivePartitionKey(
                partitionIndex, partitionCount, pkDef1PathId,
                useHashV2asDefault: true,
                enablePadding: true,
                enableLastPathPaddingForMultiHash: true);

            Assert.AreEqual(HexCharsPerPath * 1, minEpk1PathId.Length,
                "1-path with /id should be 32 chars (no extra padding)");

            // 2 paths with /id as last path - no padding should be added
            PartitionKeyDefinition pkDef2PathsId = new PartitionKeyDefinition
            {
                Kind = PartitionKind.MultiHash,
                Version = PartitionKeyDefinitionVersion.V2
            };
            pkDef2PathsId.Paths.Add("/tenantId");
            pkDef2PathsId.Paths.Add("/id");

            string minEpk2PathsId = PartitionKeyInternal.GetMinInclusiveEffectivePartitionKey(
                partitionIndex, partitionCount, pkDef2PathsId,
                useHashV2asDefault: true,
                enablePadding: true,
                enableLastPathPaddingForMultiHash: true);

            Assert.AreEqual(HexCharsPerPath * 2, minEpk2PathsId.Length,
                "2-path with /id as last path should be 64 chars (no extra padding)");

            // 3 paths with /id as last path - no padding should be added
            PartitionKeyDefinition pkDef3PathsId = new PartitionKeyDefinition
            {
                Kind = PartitionKind.MultiHash,
                Version = PartitionKeyDefinitionVersion.V2
            };
            pkDef3PathsId.Paths.Add("/tenantId");
            pkDef3PathsId.Paths.Add("/userId");
            pkDef3PathsId.Paths.Add("/id");

            string minEpk3PathsId = PartitionKeyInternal.GetMinInclusiveEffectivePartitionKey(
                partitionIndex, partitionCount, pkDef3PathsId,
                useHashV2asDefault: true,
                enablePadding: true,
                enableLastPathPaddingForMultiHash: true);

            Assert.AreEqual(HexCharsPerPath * 3, minEpk3PathsId.Length,
                "3-path with /id as last path should be 96 chars (no extra padding)");

            // 2 paths with /id as FIRST path (not last) - padding SHOULD be added
            PartitionKeyDefinition pkDef2PathsIdFirst = new PartitionKeyDefinition
            {
                Kind = PartitionKind.MultiHash,
                Version = PartitionKeyDefinitionVersion.V2
            };
            pkDef2PathsIdFirst.Paths.Add("/id");
            pkDef2PathsIdFirst.Paths.Add("/category");

            string minEpk2PathsIdFirst = PartitionKeyInternal.GetMinInclusiveEffectivePartitionKey(
                partitionIndex, partitionCount, pkDef2PathsIdFirst,
                useHashV2asDefault: true,
                enablePadding: true,
                enableLastPathPaddingForMultiHash: true);

            Assert.AreEqual(HexCharsPerPath * 3, minEpk2PathsIdFirst.Length,
                "2-path with /id as first path (not last) should be 96 chars (extra padding added)");
        }

        private static void ValidateEffectivePartitionKeyForMultiHash(string partitionKeyRangeJson, string expectedHexEncoding)
        {
            PartitionKeyInternal partitionKey = PartitionKeyInternal.FromJsonString(partitionKeyRangeJson);

            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition
            {
                Kind = PartitionKind.MultiHash,
                Version = PartitionKeyDefinitionVersion.V2
            };
            for (int i = 0; i < partitionKey.Components.Count; i++)
            {
                partitionKeyDefinition.Paths.Add("/path" + i);
            }

            string hexEncodedEffectivePartitionKey = partitionKey.GetEffectivePartitionKeyString(partitionKeyDefinition);

            Assert.AreEqual(expectedHexEncoding, hexEncodedEffectivePartitionKey);
        }

        private static void ValidatePrefixEffectivePartitionKeyForMultiHash(string partitionKeyRangeJson, string expectedHexEncoding)
        {
            PartitionKeyInternal partitionKey = PartitionKeyInternal.FromJsonString(partitionKeyRangeJson);

            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition
            {
                Kind = PartitionKind.MultiHash,
                Version = PartitionKeyDefinitionVersion.V2
            };
            for (int i = 0; i < partitionKey.Components.Count + 1; i++)
            {
                partitionKeyDefinition.Paths.Add("/path" + i);
            }

            string hexEncodedEffectivePartitionKey = partitionKey.GetEffectivePartitionKeyString(partitionKeyDefinition);

            Assert.AreEqual(expectedHexEncoding, hexEncodedEffectivePartitionKey);
        }

        private static void ValidatePrefixPartitionKeyRange(string partitionKeyRangeJson, Tuple<string, string> expectedHexEncoding, PartitionKeyDefinition partitionKeyDefinition)
        {
            PartitionKeyInternal partitionKey = PartitionKeyInternal.FromJsonString(partitionKeyRangeJson);
            Range<string> extractedRange = partitionKey.GetEPKRangeForPrefixPartitionKey(partitionKeyDefinition);

            Assert.AreEqual(expectedHexEncoding.Item1, extractedRange.Min);
            Assert.AreEqual(expectedHexEncoding.Item2, extractedRange.Max);
        }

    }
}