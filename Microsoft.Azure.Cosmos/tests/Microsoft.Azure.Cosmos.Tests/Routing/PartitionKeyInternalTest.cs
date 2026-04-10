//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Routing
{
    using System;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    /// <summary>
    /// Tests for <see cref="PartitionKeyInternal"/> class.
    /// </summary>
    [TestClass]
    public class PartitionKeyInternalTest
    {
        /// <summary>
        /// Tests deserialization of string which contains pattern recognizable as date by Json.Net.
        /// </summary>
        [TestMethod]
        public void TestDateString()
        {
            PartitionKeyInternal partitionKey = PartitionKeyInternal.FromJsonString(@"['\/Date(1234656000000)\/']");
            Assert.AreEqual(@"/Date(1234656000000)/", partitionKey.Components[0].ToObject());
        }

        /// <summary>
        /// Tests serialization of empty partition key.
        /// </summary>
        [TestMethod]
        public void TestEmptyPartitionKey()
        {
            string json = @"[]";
            PartitionKeyInternal partitionKey = PartitionKeyInternal.FromJsonString(json);
            Assert.AreEqual(PartitionKeyInternal.Empty, partitionKey);

            Assert.AreEqual("[]", partitionKey.ToJsonString());
        }

        /// <summary>
        /// Tests serialization of varios types.
        /// </summary>
        [TestMethod]
        public void TestVariousTypes()
        {
            string json = @"[""aa"", null, true, false, {}, 5, 5.5]";
            PartitionKeyInternal partitionKey = PartitionKeyInternal.FromJsonString(json);
            Assert.AreEqual(PartitionKeyInternal.FromObjectArray(new object[] { "aa", null, true, false, Undefined.Value, 5, 5.5 }, true), partitionKey);

            Assert.AreEqual(@"[""aa"",null,true,false,{},5.0,5.5]", partitionKey.ToJsonString());
        }

        /// <summary>
        /// Tests deserialization of empty string
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(JsonException), AllowDerivedTypes = true)]
        public void TestDeserializeEmptyString()
        {
            PartitionKeyInternal.FromJsonString("");
        }

        /// <summary>
        /// Tests deserialization of empty string
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(JsonException), AllowDerivedTypes = true)]
        public void TestDeserializeNull()
        {
            PartitionKeyInternal.FromJsonString(null);
        }

        /// <summary>
        /// Tests serialization of maximum value.
        /// </summary>
        [TestMethod]
        public void TestMaxValue()
        {
            string json = @"""Infinity""";
            PartitionKeyInternal partitionKey = PartitionKeyInternal.FromJsonString(json);
            Assert.AreEqual(PartitionKeyInternal.ExclusiveMaximum, partitionKey);
        }

        /// <summary>
        /// Tests JsonConvert.DefaultSettings that could cause indentation.
        /// </summary>
        [TestMethod]
        public void TestJsonConvertDefaultSettings()
        {
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
            };
            string json = @"[123.0]";
            PartitionKeyInternal partitionKey = PartitionKeyInternal.FromJsonString(json);
            Assert.AreEqual(json, partitionKey.ToJsonString());
        }

        /// <summary>
        /// Tests unicode characters in partition key
        /// </summary>
        [TestMethod]
        public void TestUnicodeCharacters()
        {
            string json = @"[""电脑""]";
            PartitionKeyInternal partitionKey = PartitionKeyInternal.FromJsonString(json);
            Assert.AreEqual(@"[""\u7535\u8111""]", partitionKey.ToJsonString());
        }

        /// <summary>
        /// Tests partition key value comparisons.
        /// </summary>
        [TestMethod]
        public void TestComparison()
        {
            this.VerifyComparison(@"[]", @"[]", 0);
            this.VerifyComparison(@"[]", @"[{}]", -1);
            this.VerifyComparison(@"[]", @"[false]", -1);
            this.VerifyComparison(@"[]", @"[true]", -1);
            this.VerifyComparison(@"[]", @"[null]", -1);
            this.VerifyComparison(@"[]", @"[2]", -1);
            this.VerifyComparison(@"[]", @"[""aa""]", -1);
            this.VerifyComparison(@"[]", @"""Infinity""", -1);

            this.VerifyComparison(@"[{}]", @"[]", 1);
            this.VerifyComparison(@"[{}]", @"[{}]", 0);
            this.VerifyComparison(@"[{}]", @"[false]", -1);
            this.VerifyComparison(@"[{}]", @"[true]", -1);
            this.VerifyComparison(@"[{}]", @"[null]", -1);
            this.VerifyComparison(@"[{}]", @"[2]", -1);
            this.VerifyComparison(@"[{}]", @"[""aa""]", -1);
            this.VerifyComparison(@"[{}]", @"""Infinity""", -1);

            this.VerifyComparison(@"[false]", @"[]", 1);
            this.VerifyComparison(@"[false]", @"[{}]", 1);
            this.VerifyComparison(@"[false]", @"[null]", 1);
            this.VerifyComparison(@"[false]", @"[false]", 0);
            this.VerifyComparison(@"[false]", @"[true]", -1);
            this.VerifyComparison(@"[false]", @"[2]", -1);
            this.VerifyComparison(@"[false]", @"[""aa""]", -1);
            this.VerifyComparison(@"[false]", @"""Infinity""", -1);

            this.VerifyComparison(@"[true]", @"[]", 1);
            this.VerifyComparison(@"[true]", @"[{}]", 1);
            this.VerifyComparison(@"[true]", @"[null]", 1);
            this.VerifyComparison(@"[true]", @"[false]", 1);
            this.VerifyComparison(@"[true]", @"[true]", 0);
            this.VerifyComparison(@"[true]", @"[2]", -1);
            this.VerifyComparison(@"[true]", @"[""aa""]", -1);
            this.VerifyComparison(@"[true]", @"""Infinity""", -1);

            this.VerifyComparison(@"[null]", @"[]", 1);
            this.VerifyComparison(@"[null]", @"[{}]", 1);
            this.VerifyComparison(@"[null]", @"[null]", 0);
            this.VerifyComparison(@"[null]", @"[false]", -1);
            this.VerifyComparison(@"[null]", @"[true]", -1);
            this.VerifyComparison(@"[null]", @"[2]", -1);
            this.VerifyComparison(@"[null]", @"[""aa""]", -1);
            this.VerifyComparison(@"[null]", @"""Infinity""", -1);

            this.VerifyComparison(@"[2]", @"[]", 1);
            this.VerifyComparison(@"[2]", @"[{}]", 1);
            this.VerifyComparison(@"[2]", @"[null]", 1);
            this.VerifyComparison(@"[2]", @"[false]", 1);
            this.VerifyComparison(@"[2]", @"[true]", 1);
            this.VerifyComparison(@"[1]", @"[2]", -1);
            this.VerifyComparison(@"[2]", @"[2]", 0);
            this.VerifyComparison(@"[3]", @"[2]", 1);
            this.VerifyComparison(@"[2.1234344]", @"[2]", 1);
            this.VerifyComparison(@"[2]", @"[""aa""]", -1);
            this.VerifyComparison(@"[2]", @"""Infinity""", -1);

            this.VerifyComparison(@"[""aa""]", @"[]", 1);
            this.VerifyComparison(@"[""aa""]", @"[{}]", 1);
            this.VerifyComparison(@"[""aa""]", @"[null]", 1);
            this.VerifyComparison(@"[""aa""]", @"[false]", 1);
            this.VerifyComparison(@"[""aa""]", @"[true]", 1);
            this.VerifyComparison(@"[""aa""]", @"[2]", 1);
            this.VerifyComparison(@"[""""]", @"[""aa""]", -1);
            this.VerifyComparison(@"[""aa""]", @"[""aa""]", 0);
            this.VerifyComparison(@"[""b""]", @"[""aa""]", 1);
            this.VerifyComparison(@"[""aa""]", @"""Infinity""", -1);

            this.VerifyComparison(@"""Infinity""", @"[]", 1);
            this.VerifyComparison(@"""Infinity""", @"[{}]", 1);
            this.VerifyComparison(@"""Infinity""", @"[null]", 1);
            this.VerifyComparison(@"""Infinity""", @"[false]", 1);
            this.VerifyComparison(@"""Infinity""", @"[true]", 1);
            this.VerifyComparison(@"""Infinity""", @"[2]", 1);
            this.VerifyComparison(@"""Infinity""", @"[""aa""]", 1);
            this.VerifyComparison(@"""Infinity""", @"""Infinity""", 0);
        }

        /// <summary>
        /// Tests that invalid partition key value will throw an exception.
        /// </summary>
        [TestMethod]
        [ExpectedException(
            typeof(InvalidOperationException),
            "Unsupported PartitionKey value component ''. Numeric, string, bool, null, Undefined are the only supported types.")]
        public void TestInvalidPartitionKeyValue()
        {
            PartitionKeyInternal.FromObjectArray(new object[] { 2, true, new StringBuilder() }, true);
        }

        /// <summary>
        /// Tests <see cref="PartitionKeyInternal.Contains"/> method.
        /// </summary>
        [TestMethod]
        public void ContainsTest()
        {
            Func<string, string, bool> verifyContains = (parentPartitionKey, childPartitionKey) =>
                PartitionKeyInternal.FromJsonString(parentPartitionKey)
                    .Contains(PartitionKeyInternal.FromJsonString(childPartitionKey));

            Assert.IsTrue(verifyContains("[]", "[]"));
            Assert.IsTrue(verifyContains("[]", "[{}]"));
            Assert.IsTrue(verifyContains("[]", "[null]"));
            Assert.IsTrue(verifyContains("[]", "[true]"));
            Assert.IsTrue(verifyContains("[]", "[false]"));
            Assert.IsTrue(verifyContains("[]", "[2]"));
            Assert.IsTrue(verifyContains("[]", @"[""fdfd""]"));

            Assert.IsFalse(verifyContains("[2]", "[]"));
            Assert.IsTrue(verifyContains("[2]", "[2]"));
            Assert.IsTrue(verifyContains("[2]", @"[2, ""USA""]"));
            Assert.IsFalse(verifyContains("[1]", @"[2, ""USA""]"));
        }

        /// <summary>
        /// Tests constructing partition key value in non strict mode.
        /// </summary>
        [TestMethod]
        public void TestInvalidPartitionKeyValueNonStrict()
        {
            Assert.AreEqual(
                PartitionKeyInternal.FromObjectArray(new object[] { 2, true, Undefined.Value }, true),
                PartitionKeyInternal.FromObjectArray(new object[] { 2, true, new StringBuilder() }, false));
        }

        /// <summary>
        /// Tests constructing effective partition key value.
        /// </summary>
        [TestMethod]
        public void TestHashEffectivePartitionKey()
        {
            Assert.AreEqual(
                PartitionKeyInternal.InclusiveMinimum.GetEffectivePartitionKeyString(new PartitionKeyDefinition()),
                PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey);

            Assert.AreEqual(
                PartitionKeyInternal.ExclusiveMaximum.GetEffectivePartitionKeyString(new PartitionKeyDefinition()),
                PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey);

            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition { Paths = new Collection<string> { "/A", "/B", "/C", "/E", "/F", "/G" }, Kind = PartitionKind.Hash };
            PartitionKeyInternal partitionKey = PartitionKeyInternal.FromObjectArray(new object[] { 2, true, false, null, Undefined.Value, "Привет!" }, true);
            string effectivePartitionKey = partitionKey.GetEffectivePartitionKeyString(partitionKeyDefinition);

            Assert.AreEqual("05C1D19581B37C05C0000302010008D1A0D281D1B9D1B3D1B6D2832200", effectivePartitionKey);
        }

        /// <summary>
        /// Tests range effective partition key.
        /// </summary>
        [TestMethod]
        public void TestRangeEffectivePartitionKey()
        {
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition { Paths = new Collection<string> { "/A", "/B", "/C", "/E", "/F", "/G" }, Kind = PartitionKind.Range };
            PartitionKeyInternal partitionKey = PartitionKeyInternal.FromObjectArray(new object[] { 2, true, false, null, Undefined.Value, "Привет!" }, true);
            string effectivePartitionKey = partitionKey.GetEffectivePartitionKeyString(partitionKeyDefinition);

            Assert.AreEqual("05C0000302010008D1A0D281D1B9D1B3D1B6D2832200", effectivePartitionKey);
        }

        /// <summary>
        /// Tests binary encoding of partition key
        /// </summary>
        [TestMethod]
        public void TestPartitionKeyBinaryEncoding()
        {
            Tuple<string, string>[] testCases =
            {
                Tuple.Create<string, string>(@"[5.0]", "05C014"),
                Tuple.Create<string, string>(@"[5.12312419050912359123]", "05C0153F858949735550"),
                Tuple.Create<string, string>(@"[""redmond""]", "087366656E706F6500"),
                Tuple.Create<string, string>(@"[true]", "03"),
                Tuple.Create<string, string>(@"[false]", "02"),
                Tuple.Create<string, string>(@"[]", string.Empty),
                Tuple.Create<string, string>(@"[null]", "01"),
                Tuple.Create<string, string>(@"[undefined]", "01"),
                Tuple.Create<string, string>(@"""Infinity""", "FF"),
                Tuple.Create<string, string>(@"[5.0, ""redmond"", true, null]", "05C014087366656E706F65000301"),
            };

            foreach (Tuple<string, string> testCase in testCases)
            {
                ValidateRoundTripHexEncoding(testCase.Item1, testCase.Item2);
            }
        }

        /// <summary>
        /// Tests binary encoding of partition key
        /// </summary>
        [TestMethod]
        public void TestPartitionKeyBinaryEncodingV2()
        {
            Tuple<string, string>[] testCases =
            {
                Tuple.Create<string, string>(@"[5.0]", "19C08621B135968252FB34B4CF66F811"),
                Tuple.Create<string, string>(@"[5.12312419050912359123]", "0EF2E2D82460884AF0F6440BE4F726A8"),
                Tuple.Create<string, string>(@"[""redmond""]", "22E342F38A486A088463DFF7838A5963"),
                Tuple.Create<string, string>(@"[true]", "0E711127C5B5A8E4726AC6DD306A3E59"),
                Tuple.Create<string, string>(@"[false]", "2FE1BE91E90A3439635E0E9E37361EF2"),
                Tuple.Create<string, string>(@"[]", string.Empty),
                Tuple.Create<string, string>(@"[null]", "378867E4430E67857ACE5C908374FE16"),
                Tuple.Create<string, string>(@"[{}]", "11622DAA78F835834610ABE56EFF5CB5"),
                Tuple.Create<string, string>(@"[5.0, ""redmond"", true, null]", "3032DECBE2AB1768D8E0AEDEA35881DF"),
                Tuple.Create<string, string>(@"[""aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa""]", "36375D21568760E891C9CB7002D5E059"),
            };

            foreach (Tuple<string, string> testCase in testCases)
            {
                ValidateEffectivePartitionKeyV2(testCase.Item1, testCase.Item2);
            }
        }

        /// <summary>
        /// Tests finding middle of two effective partition keys for hash partitioning.
        /// </summary>
        [TestMethod]
        public void TestMidPoint()
        {
            PartitionKeyDefinition partitionKey = new PartitionKeyDefinition
            {
                Kind = PartitionKind.Hash
            };
            string middle1 = PartitionKeyInternal.GetMiddleRangeEffectivePartitionKey(
                PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
                PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
                partitionKey);

            string expected1 = PartitionKeyInternal.GetMaxExclusiveEffectivePartitionKey(0, 2, partitionKey);

            Assert.AreEqual(expected1, middle1);

            // Because of the way ParttitionKeyInternal.GetMaxExclusvieEffectivePartiitonKey calculates
            // range bounds, we cannot compare exactly - compare approximately.
            string middle2 = PartitionKeyInternal.GetMiddleRangeEffectivePartitionKey(
                PartitionKeyInternal.GetMinInclusiveEffectivePartitionKey(3, 8, partitionKey),
                PartitionKeyInternal.GetMinInclusiveEffectivePartitionKey(3, 8, partitionKey),
                partitionKey);

            string middle2Left = PartitionKeyInternal.GetMinInclusiveEffectivePartitionKey(384, 1024, partitionKey);
            string middle2Right = PartitionKeyInternal.GetMaxExclusiveEffectivePartitionKey(384, 1024, partitionKey);

            Assert.IsTrue(StringComparer.Ordinal.Compare(middle2, middle2Left) >= 0);
            Assert.IsTrue(StringComparer.Ordinal.Compare(middle2, middle2Right) < 0);

            string middle3 = PartitionKeyInternal.GetMiddleRangeEffectivePartitionKey(
                PartitionKeyInternal.GetMinInclusiveEffectivePartitionKey(24, 25, partitionKey),
                PartitionKeyInternal.GetMaxExclusiveEffectivePartitionKey(24, 25, partitionKey),
                partitionKey);


            Assert.AreEqual("05C1EFAF0B1F46", middle3);
            Assert.IsTrue(StringComparer.Ordinal.Compare(middle3, PartitionKeyInternal.GetMinInclusiveEffectivePartitionKey(24, 25, partitionKey)) > 0);
            Assert.IsTrue(StringComparer.Ordinal.Compare(middle3, PartitionKeyInternal.GetMaxExclusiveEffectivePartitionKey(24, 25, partitionKey)) < 0);
        }

        /// <summary>
        /// Tests finding middle of two effective partition keys for hash partitioning.
        /// </summary>
        [TestMethod]
        public void TestMidPointV2()
        {
            PartitionKeyDefinition partitionKey = new PartitionKeyDefinition
            {
                Kind = PartitionKind.Hash,
                Version = PartitionKeyDefinitionVersion.V2
            };
            string middle1 = PartitionKeyInternal.GetMiddleRangeEffectivePartitionKey(
                PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
                PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
                partitionKey);

            string expected1 = PartitionKeyInternal.GetMaxExclusiveEffectivePartitionKey(0, 2, partitionKey);

            Assert.AreEqual(expected1, middle1);

            // Because of the way ParttitionKeyInternal.GetMaxExclusvieEffectivePartiitonKey calculates
            // range bounds, we cannot compare exactly - compare approximately.
            string middle2 = PartitionKeyInternal.GetMiddleRangeEffectivePartitionKey(
                PartitionKeyInternal.GetMinInclusiveEffectivePartitionKey(3, 8, partitionKey),
                PartitionKeyInternal.GetMinInclusiveEffectivePartitionKey(3, 8, partitionKey),
                partitionKey);

            string middle2Left = PartitionKeyInternal.GetMinInclusiveEffectivePartitionKey(384, 1024, partitionKey);
            string middle2Right = PartitionKeyInternal.GetMaxExclusiveEffectivePartitionKey(384, 1024, partitionKey);

            Assert.IsTrue(StringComparer.Ordinal.Compare(middle2, middle2Left) >= 0);
            Assert.IsTrue(StringComparer.Ordinal.Compare(middle2, middle2Right) < 0);

            string middle3 = PartitionKeyInternal.GetMiddleRangeEffectivePartitionKey(
                PartitionKeyInternal.GetMinInclusiveEffectivePartitionKey(24, 25, partitionKey),
                PartitionKeyInternal.GetMaxExclusiveEffectivePartitionKey(24, 25, partitionKey),
                partitionKey);


            //Assert.AreEqual("05C1EFAF0B1F46", middle3);
            Assert.IsTrue(StringComparer.Ordinal.Compare(middle3, PartitionKeyInternal.GetMinInclusiveEffectivePartitionKey(24, 25, partitionKey)) > 0);
            Assert.IsTrue(StringComparer.Ordinal.Compare(middle3, PartitionKeyInternal.GetMaxExclusiveEffectivePartitionKey(24, 25, partitionKey)) < 0);
        }

        private static void TestEffectivePartitionKeyEncoding(string buffer, int length, string expectedValue, bool v2)
        {
            PartitionKeyDefinition pkDefinition = new PartitionKeyDefinition();
            pkDefinition.Paths.Add("/field1");
            pkDefinition.Version = v2 ? PartitionKeyDefinitionVersion.V2 : PartitionKeyDefinitionVersion.V1;

            PartitionKeyInternal pk = new PartitionKeyInternal(new[] { new StringPartitionKeyComponent(buffer[..length]) });
            Assert.AreEqual(expectedValue, pk.GetEffectivePartitionKeyString(pkDefinition));
        }

        /// <summary>
        /// Tests that effective partition key produced by managed code and backend is the same.
        /// </summary>
        [TestMethod]
        public void TestManagedNativeCompatibility()
        {
            PartitionKeyInternal partitionKey =
                PartitionKeyInternal.FromJsonString("[\"по-русски\",null,true,false,{},5.5]");

            PartitionKeyDefinition pkDefinition = new PartitionKeyDefinition();
            pkDefinition.Paths.Add("/field1");
            pkDefinition.Paths.Add("/field2");
            pkDefinition.Paths.Add("/field3");
            pkDefinition.Paths.Add("/field4");
            pkDefinition.Paths.Add("/field5");
            pkDefinition.Paths.Add("/field6");
            string effectivePartitionKey = partitionKey.GetEffectivePartitionKeyString(pkDefinition);
            Assert.AreEqual("05C1D39FA55F0408D1C0D1BF2ED281D284D282D282D1BBD1B9000103020005C016", effectivePartitionKey);

            string latin = "abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz";
            string nonLatin = "абвгдеёжзийклмнопрстуфхцчшщъыьэюяабвгдеёжзийклмнопрстуфхцчшщъыьэюяабвгдеёжзийклмнопрстуфхцчшщъыьэюяабвгдеёжзийклмнопрстуфхцчшщъыьэюя";

            TestEffectivePartitionKeyEncoding(latin, 99, "05C19B2DC38FC00862636465666768696A6B6C6D6E6F707172737475767778797A7B62636465666768696A6B6C6D6E6F707172737475767778797A7B62636465666768696A6B6C6D6E6F707172737475767778797A7B62636465666768696A6B6C6D6E6F7071727374757600", false);
            TestEffectivePartitionKeyEncoding(latin, 99, "072D8FA3228DD2A6C0A7129C845700E6", true);

            TestEffectivePartitionKeyEncoding(latin, 100, "05C1DD5D8149640862636465666768696A6B6C6D6E6F707172737475767778797A7B62636465666768696A6B6C6D6E6F707172737475767778797A7B62636465666768696A6B6C6D6E6F707172737475767778797A7B62636465666768696A6B6C6D6E6F707172737475767700", false);
            TestEffectivePartitionKeyEncoding(latin, 100, "023D5F0B62EBEF22A43564F267193B4D", true);

            TestEffectivePartitionKeyEncoding(latin, 101, "05C1DD5D8149640862636465666768696A6B6C6D6E6F707172737475767778797A7B62636465666768696A6B6C6D6E6F707172737475767778797A7B62636465666768696A6B6C6D6E6F707172737475767778797A7B62636465666768696A6B6C6D6E6F707172737475767700", false);
            TestEffectivePartitionKeyEncoding(latin, 101, "357D83181DB32D35F58CDA3C9F2E0742", true);

            TestEffectivePartitionKeyEncoding(latin, 102, "05C1DD5D8149640862636465666768696A6B6C6D6E6F707172737475767778797A7B62636465666768696A6B6C6D6E6F707172737475767778797A7B62636465666768696A6B6C6D6E6F707172737475767778797A7B62636465666768696A6B6C6D6E6F707172737475767700", false);
            TestEffectivePartitionKeyEncoding(latin, 102, "12B320F72959AB449FD8E090C6B23B88", true);

            TestEffectivePartitionKeyEncoding(latin, 103, "05C1DD5D8149640862636465666768696A6B6C6D6E6F707172737475767778797A7B62636465666768696A6B6C6D6E6F707172737475767778797A7B62636465666768696A6B6C6D6E6F707172737475767778797A7B62636465666768696A6B6C6D6E6F707172737475767700", false);
            TestEffectivePartitionKeyEncoding(latin, 103, "25FD21A31C69A8C8AD994F7FAC2B2B9F", true);

            TestEffectivePartitionKeyEncoding(latin, 104, "05C1DD5D8149640862636465666768696A6B6C6D6E6F707172737475767778797A7B62636465666768696A6B6C6D6E6F707172737475767778797A7B62636465666768696A6B6C6D6E6F707172737475767778797A7B62636465666768696A6B6C6D6E6F707172737475767700", false);
            TestEffectivePartitionKeyEncoding(latin, 104, "1DC6FB1CF6E1228C506AA6C8735023C4", true);

            TestEffectivePartitionKeyEncoding(latin, 105, "05C1DD5D8149640862636465666768696A6B6C6D6E6F707172737475767778797A7B62636465666768696A6B6C6D6E6F707172737475767778797A7B62636465666768696A6B6C6D6E6F707172737475767778797A7B62636465666768696A6B6C6D6E6F707172737475767700", false);
            TestEffectivePartitionKeyEncoding(latin, 105, "308E1E7870956CE5D9BDAD01200E09BD", true);

            TestEffectivePartitionKeyEncoding(latin, 106, "05C1DD5D8149640862636465666768696A6B6C6D6E6F707172737475767778797A7B62636465666768696A6B6C6D6E6F707172737475767778797A7B62636465666768696A6B6C6D6E6F707172737475767778797A7B62636465666768696A6B6C6D6E6F707172737475767700", false);
            TestEffectivePartitionKeyEncoding(latin, 106, "362E21ABDEA7179DBDF7BF549DD8303B", true);

            TestEffectivePartitionKeyEncoding(latin, 107, "05C1DD5D8149640862636465666768696A6B6C6D6E6F707172737475767778797A7B62636465666768696A6B6C6D6E6F707172737475767778797A7B62636465666768696A6B6C6D6E6F707172737475767778797A7B62636465666768696A6B6C6D6E6F707172737475767700", false);
            TestEffectivePartitionKeyEncoding(latin, 107, "1EBE932ECEFA4F53CE339D31B6BF53FD", true);

            TestEffectivePartitionKeyEncoding(latin, 108, "05C1DD5D8149640862636465666768696A6B6C6D6E6F707172737475767778797A7B62636465666768696A6B6C6D6E6F707172737475767778797A7B62636465666768696A6B6C6D6E6F707172737475767778797A7B62636465666768696A6B6C6D6E6F707172737475767700", false);
            TestEffectivePartitionKeyEncoding(latin, 108, "3BFA3A6E9CBABA0EF756AEDEC66B1B3C", true);

            TestEffectivePartitionKeyEncoding(latin, 109, "05C1DD5D8149640862636465666768696A6B6C6D6E6F707172737475767778797A7B62636465666768696A6B6C6D6E6F707172737475767778797A7B62636465666768696A6B6C6D6E6F707172737475767778797A7B62636465666768696A6B6C6D6E6F707172737475767700", false);
            TestEffectivePartitionKeyEncoding(latin, 109, "2880BF78DE0CE2CD1B0120EDA22601C4", true);

            TestEffectivePartitionKeyEncoding(latin, 110, "05C1DD5D8149640862636465666768696A6B6C6D6E6F707172737475767778797A7B62636465666768696A6B6C6D6E6F707172737475767778797A7B62636465666768696A6B6C6D6E6F707172737475767778797A7B62636465666768696A6B6C6D6E6F707172737475767700", false);
            TestEffectivePartitionKeyEncoding(latin, 110, "1F3577D1D9CA7FC56100AED11F4DC646", true);

            TestEffectivePartitionKeyEncoding(latin, 111, "05C1DD5D8149640862636465666768696A6B6C6D6E6F707172737475767778797A7B62636465666768696A6B6C6D6E6F707172737475767778797A7B62636465666768696A6B6C6D6E6F707172737475767778797A7B62636465666768696A6B6C6D6E6F707172737475767700", false);
            TestEffectivePartitionKeyEncoding(latin, 111, "205A9EB61F3B063E61C6ED655C9220E6", true);

            TestEffectivePartitionKeyEncoding(latin, 112, "05C1DD5D8149640862636465666768696A6B6C6D6E6F707172737475767778797A7B62636465666768696A6B6C6D6E6F707172737475767778797A7B62636465666768696A6B6C6D6E6F707172737475767778797A7B62636465666768696A6B6C6D6E6F707172737475767700", false);
            TestEffectivePartitionKeyEncoding(latin, 112, "1152A43F1A852AFDDD4518C9CDD48616", true);

            TestEffectivePartitionKeyEncoding(latin, 113, "05C1DD5D8149640862636465666768696A6B6C6D6E6F707172737475767778797A7B62636465666768696A6B6C6D6E6F707172737475767778797A7B62636465666768696A6B6C6D6E6F707172737475767778797A7B62636465666768696A6B6C6D6E6F707172737475767700", false);
            TestEffectivePartitionKeyEncoding(latin, 113, "38E2EB2EF54012B5CA40CDA34F1C7736", true);

            TestEffectivePartitionKeyEncoding(latin, 114, "05C1DD5D8149640862636465666768696A6B6C6D6E6F707172737475767778797A7B62636465666768696A6B6C6D6E6F707172737475767778797A7B62636465666768696A6B6C6D6E6F707172737475767778797A7B62636465666768696A6B6C6D6E6F707172737475767700", false);
            TestEffectivePartitionKeyEncoding(latin, 114, "19BCC416843B9085DBBC18E8C7C80D72", true);

            TestEffectivePartitionKeyEncoding(latin, 115, "05C1DD5D8149640862636465666768696A6B6C6D6E6F707172737475767778797A7B62636465666768696A6B6C6D6E6F707172737475767778797A7B62636465666768696A6B6C6D6E6F707172737475767778797A7B62636465666768696A6B6C6D6E6F707172737475767700", false);
            TestEffectivePartitionKeyEncoding(latin, 115, "03F1BB89FD8E9747B047281E80FA2E84", true);

            TestEffectivePartitionKeyEncoding(latin, 116, "05C1DD5D8149640862636465666768696A6B6C6D6E6F707172737475767778797A7B62636465666768696A6B6C6D6E6F707172737475767778797A7B62636465666768696A6B6C6D6E6F707172737475767778797A7B62636465666768696A6B6C6D6E6F707172737475767700", false);
            TestEffectivePartitionKeyEncoding(latin, 116, "2BA0757B833F3922A3CBBB6DDA3803B4", true);

            TestEffectivePartitionKeyEncoding(nonLatin, 49, "05C1C1BD37FE08D1B1D1B2D1B3D1B4D1B5D1B6D292D1B7D1B8D1B9D1BAD1BBD1BCD1BDD1BED1BFD1C0D281D282D283D284D285D286D287D288D289D28AD28BD28CD28DD28ED28FD290D1B1D1B2D1B3D1B4D1B5D1B6D292D1B7D1B8D1B9D1BAD1BBD1BCD1BDD1BED1BF00", false);
            TestEffectivePartitionKeyEncoding(nonLatin, 49, "3742C1AF65AFA809282539F4BCDF2F6F", true);

            TestEffectivePartitionKeyEncoding(nonLatin, 50, "05C1B339EF472008D1B1D1B2D1B3D1B4D1B5D1B6D292D1B7D1B8D1B9D1BAD1BBD1BCD1BDD1BED1BFD1C0D281D282D283D284D285D286D287D288D289D28AD28BD28CD28DD28ED28FD290D1B1D1B2D1B3D1B4D1B5D1B6D292D1B7D1B8D1B9D1BAD1BBD1BCD1BDD1BED1BFD1C000", false);
            TestEffectivePartitionKeyEncoding(nonLatin, 50, "399CF1F141E066E09CC7557EA7F0977A", true);

            TestEffectivePartitionKeyEncoding(nonLatin, 51, "05C1EB1F29DBFA08D1B1D1B2D1B3D1B4D1B5D1B6D292D1B7D1B8D1B9D1BAD1BBD1BCD1BDD1BED1BFD1C0D281D282D283D284D285D286D287D288D289D28AD28BD28CD28DD28ED28FD290D1B1D1B2D1B3D1B4D1B5D1B6D292D1B7D1B8D1B9D1BAD1BBD1BCD1BDD1BED1BFD1C0D2", false);
            TestEffectivePartitionKeyEncoding(nonLatin, 51, "2D63C2F5FDAC6EFE5660CD509A723A90", true);

            TestEffectivePartitionKeyEncoding(nonLatin, 99, "05C1E72F79C71608D1B1D1B2D1B3D1B4D1B5D1B6D292D1B7D1B8D1B9D1BAD1BBD1BCD1BDD1BED1BFD1C0D281D282D283D284D285D286D287D288D289D28AD28BD28CD28DD28ED28FD290D1B1D1B2D1B3D1B4D1B5D1B6D292D1B7D1B8D1B9D1BAD1BBD1BCD1BDD1BED1BFD1C0D2", false);
            TestEffectivePartitionKeyEncoding(nonLatin, 99, "1E9836D9BCB67FDB2B5C984BD40AFAF9", true);

            TestEffectivePartitionKeyEncoding(nonLatin, 100, "05C1E3653D9F3E08D1B1D1B2D1B3D1B4D1B5D1B6D292D1B7D1B8D1B9D1BAD1BBD1BCD1BDD1BED1BFD1C0D281D282D283D284D285D286D287D288D289D28AD28BD28CD28DD28ED28FD290D1B1D1B2D1B3D1B4D1B5D1B6D292D1B7D1B8D1B9D1BAD1BBD1BCD1BDD1BED1BFD1C0D2", false);
            TestEffectivePartitionKeyEncoding(nonLatin, 100, "16102F19448867537E51BB4377962AF9", true);

            TestEffectivePartitionKeyEncoding(nonLatin, 101, "05C1E3653D9F3E08D1B1D1B2D1B3D1B4D1B5D1B6D292D1B7D1B8D1B9D1BAD1BBD1BCD1BDD1BED1BFD1C0D281D282D283D284D285D286D287D288D289D28AD28BD28CD28DD28ED28FD290D1B1D1B2D1B3D1B4D1B5D1B6D292D1B7D1B8D1B9D1BAD1BBD1BCD1BDD1BED1BFD1C0D2", false);
            TestEffectivePartitionKeyEncoding(nonLatin, 101, "0B6D25D07748AB9CA0F523D4BAD146C8", true);
        }

        private static void ValidateEffectivePartitionKeyV2(string partitionKeyRangeJson, string expectedHexEncoding)
        {
            PartitionKeyInternal partitionKey = PartitionKeyInternal.FromJsonString(partitionKeyRangeJson);

            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition
            {
                Kind = PartitionKind.Hash,
                Version = PartitionKeyDefinitionVersion.V2
            };
            for (int i = 0; i < partitionKey.Components.Count; i++)
            {
                partitionKeyDefinition.Paths.Add("/path" + i);
            }

            string hexEncodedEffectivePartitionKey = partitionKey.GetEffectivePartitionKeyString(partitionKeyDefinition);

            Assert.AreEqual(expectedHexEncoding, hexEncodedEffectivePartitionKey);
        }

        private static void ValidateRoundTripHexEncoding(string partitionKeyRangeJson, string expectedHexEncoding)
        {
            PartitionKeyInternal partitionKey = PartitionKeyInternal.FromJsonString(partitionKeyRangeJson);

            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition
            {
                Kind = PartitionKind.Range
            };
            for (int i = 0; i < partitionKey.Components.Count; i++)
            {
                partitionKeyDefinition.Paths.Add("/path" + i);
            }

            string hexEncodedEffectivePartitionKey = partitionKey.GetEffectivePartitionKeyString(partitionKeyDefinition);

#pragma warning disable 0612
            PartitionKeyInternal partitionKeyRoundTrip = PartitionKeyInternal.FromHexEncodedBinaryString(hexEncodedEffectivePartitionKey);
            _ = PartitionKeyInternal.FromHexEncodedBinaryString("05C1E149CFCD84087071667362756A706F736674766D7500");
#pragma warning restore 0612

            Assert.AreEqual(partitionKey, partitionKeyRoundTrip);
            StringAssert.Equals(hexEncodedEffectivePartitionKey, expectedHexEncoding);
        }

        private void VerifyComparison(string leftKey, string rightKey, int result)
        {
            Assert.AreEqual(result, PartitionKeyInternal.FromJsonString(leftKey).CompareTo(PartitionKeyInternal.FromJsonString(rightKey)));
        }
    }
}