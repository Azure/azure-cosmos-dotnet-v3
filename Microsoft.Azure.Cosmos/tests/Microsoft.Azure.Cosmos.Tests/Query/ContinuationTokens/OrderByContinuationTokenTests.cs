//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.OrderBy;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.Parallel;
    using Microsoft.Azure.Documents.Routing;
    using VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class OrderByContinuationTokenTests
    {
        [TestMethod]
        public void TestRoundTripAsCosmosElement()
        {
            ParallelContinuationToken parallelContinuationToken = new ParallelContinuationToken(
                token: "someToken",
                range: new Range<string>("asdf", "asdf", false, false));

            List<OrderByItem> orderByItems = new List<OrderByItem>()
            {
                new OrderByItem(CosmosObject.Create(new Dictionary<string, CosmosElement>(){ { "item", CosmosString.Create("asdf") } })),
                new OrderByItem(CosmosObject.Create(new Dictionary<string, CosmosElement>(){ { "item", CosmosInt64.Create(1337) } })),
                new OrderByItem(CosmosObject.Create(new Dictionary<string, CosmosElement>(){ { "item", CosmosBinary.Create(new byte[] { 1, 2, 3}) } })),
            };

            string rid = "someRid";
            int skipCount = 42;
            string filter = "someFilter";
            OrderByContinuationToken orderByContinuationToken = new OrderByContinuationToken(
                parallelContinuationToken,
                orderByItems,
                resumeValues: null,
                rid,
                skipCount,
                filter);

            CosmosElement cosmosElementToken = OrderByContinuationToken.ToCosmosElement(orderByContinuationToken);
            Assert.AreEqual(
                @"{""compositeToken"":{""token"":""someToken"",""range"":{""min"":""asdf"",""max"":""asdf""}},""orderByItems"":[{""item"":""asdf""},{""item"":LL1337},{""item"":BAQID}],""rid"":""someRid"",""skipCount"":42,""filter"":""someFilter""}",
                cosmosElementToken.ToString());
            TryCatch<OrderByContinuationToken> tryOrderByContinuationTokenFromCosmosElement = OrderByContinuationToken.TryCreateFromCosmosElement(cosmosElementToken);
            Assert.IsTrue(tryOrderByContinuationTokenFromCosmosElement.Succeeded);
            OrderByContinuationToken orderByContinuationTokenFromCosmosElement = tryOrderByContinuationTokenFromCosmosElement.Result;
            Assert.IsNotNull(orderByContinuationTokenFromCosmosElement);
            Assert.AreEqual(cosmosElementToken.ToString(), OrderByContinuationToken.ToCosmosElement(orderByContinuationTokenFromCosmosElement).ToString());
        }

        [TestMethod]
        public void TestResumeFilterRoundTripAsCosmosElement()
        {
            ParallelContinuationToken parallelContinuationToken = new ParallelContinuationToken(
                token: "someToken",
                range: new Range<string>("asdf", "asdf", false, false));

            List<SqlQueryResumeValue> resumeValues = new List<SqlQueryResumeValue>()
            {
                SqlQueryResumeValue.FromCosmosElement(CosmosUndefined.Create()),
                SqlQueryResumeValue.FromCosmosElement(CosmosNull.Create()),
                SqlQueryResumeValue.FromCosmosElement(CosmosBoolean.Create(true)),
                SqlQueryResumeValue.FromCosmosElement(CosmosString.Create("asdf")),
                SqlQueryResumeValue.FromCosmosElement(CosmosNumber64.Create(1337)),
                SqlQueryResumeValue.FromOrderByValue(CosmosArray.Parse("[]")),
                SqlQueryResumeValue.FromOrderByValue(CosmosObject.Parse("{}"))
            };

            string rid = "someRid";
            int skipCount = 42;
            string filter = "someFilter";
            OrderByContinuationToken orderByContinuationToken = new OrderByContinuationToken(
                parallelContinuationToken,
                orderByItems: null,
                resumeValues,
                rid,
                skipCount,
                filter);

            CosmosElement cosmosElementToken = OrderByContinuationToken.ToCosmosElement(orderByContinuationToken);
            Assert.AreEqual(
                @"{""compositeToken"":{""token"":""someToken"",""range"":{""min"":""asdf"",""max"":""asdf""}},""resumeValues"":[[],null,true,""asdf"",1337,{""type"":""array"",""low"":-6706074647855398782,""high"":9031114912533472255},{""type"":""object"",""low"":1457042291250783704,""high"":1493060239874959160}],""rid"":""someRid"",""skipCount"":42}",
                cosmosElementToken.ToString());

            TryCatch<OrderByContinuationToken> tryOrderByContinuationTokenFromCosmosElement = OrderByContinuationToken.TryCreateFromCosmosElement(cosmosElementToken);
            Assert.IsTrue(tryOrderByContinuationTokenFromCosmosElement.Succeeded);

            OrderByContinuationToken orderByContinuationTokenFromCosmosElement = tryOrderByContinuationTokenFromCosmosElement.Result;
            Assert.IsNotNull(orderByContinuationTokenFromCosmosElement);
            Assert.AreEqual(cosmosElementToken.ToString(), OrderByContinuationToken.ToCosmosElement(orderByContinuationTokenFromCosmosElement).ToString());
        }

        [TestMethod]
        public void TestOrderByQueryLiterals()
        {
            StringBuilder sb = new StringBuilder();
            CosmosElement element = CosmosObject.Create(
                new Dictionary<string, CosmosElement>() { { "item", CosmosString.Create("asdf") } });
            element.Accept(new CosmosElementToQueryLiteral(sb));
            Assert.AreEqual(
                @"{""item"":""asdf""}",
                sb.ToString());

            element = CosmosObject.Create(
                new Dictionary<string, CosmosElement>() { { "item", CosmosBoolean.Create(true) } });
            sb.Clear();
            element.Accept(new CosmosElementToQueryLiteral(sb));
            Assert.AreEqual(
                @"{""item"":true}",
                sb.ToString());

            element = CosmosObject.Create(
                new Dictionary<string, CosmosElement>() { { "item", CosmosBoolean.Create(false) } });
            sb.Clear();
            element.Accept(new CosmosElementToQueryLiteral(sb));
            Assert.AreEqual(
                @"{""item"":false}",
                sb.ToString());

            element = CosmosObject.Create(
                new Dictionary<string, CosmosElement>() { { "item", CosmosNull.Create() } });
            sb.Clear();
            element.Accept(new CosmosElementToQueryLiteral(sb));
            Assert.AreEqual(
                @"{""item"":null}",
                sb.ToString());

            element = CosmosObject.Create(
                new Dictionary<string, CosmosElement>() { { "item", CosmosNumber64.Create(1.0) } });
            sb.Clear();
            element.Accept(new CosmosElementToQueryLiteral(sb));
            Assert.AreEqual(
                @"{""item"":1}",
                sb.ToString());

            element = CosmosObject.Create(
                new Dictionary<string, CosmosElement>() { { "item", CosmosNumber64.Create(1L) } });
            sb.Clear();
            element.Accept(new CosmosElementToQueryLiteral(sb));
            Assert.AreEqual(
                @"{""item"":1}",
                sb.ToString());

            element = CosmosObject.Create(
                new Dictionary<string, CosmosElement>()
                {
                    { "item", CosmosInt8.Create(3) },
                    { "item2", CosmosInt16.Create(4) },
                    { "item3", CosmosInt32.Create(5) },
                    { "item5", CosmosUInt32.Create(7) },
                    { "item6", CosmosInt64.Create(8) },
                    { "item7", CosmosFloat32.Create(9.1f) },
                    { "item8", CosmosFloat64.Create(10.2) },
                });
            sb.Clear();
            element.Accept(new CosmosElementToQueryLiteral(sb));
            Assert.AreEqual(
                @"{""item"":C_Int8(3),""item2"":C_Int16(4),""item3"":C_Int32(5),""item5"":C_UInt32(7),""item6"":C_Int64(8)," +
                       @"""item7"":C_Float32(9.1),""item8"":C_Float64(10.2)}",
                sb.ToString());

            Guid guid = Guid.NewGuid();
            byte[] randomBytes = Guid.NewGuid().ToByteArray();
            string hexString = PartitionKeyInternal.HexConvert.ToHex(randomBytes, 0, randomBytes.Length);
            element = CosmosObject.Create(
                new Dictionary<string, CosmosElement>()
                {
                    { "item", CosmosGuid.Create(guid) },
                    { "item2", CosmosBinary.Create(new ReadOnlyMemory<byte>(randomBytes)) },
                });
            sb.Clear();
            element.Accept(new CosmosElementToQueryLiteral(sb));
            Assert.AreEqual(
                $@"{{""item"":C_Guid(""{guid}""),""item2"":C_Binary(""0x{hexString}"")}}",
                sb.ToString());

            // deeply nested arrays and objects
            element = CosmosObject.Create(
                new Dictionary<string, CosmosElement>()
                {
                    { "item", CosmosGuid.Create(guid) },

                    // empty array
                    { "item2", CosmosArray.Create(new CosmosElement[] { }) },

                    // empty object
                    { "item3", CosmosObject.Create(new Dictionary<string, CosmosElement>()) },

                    // array of objects with numbers
                    { "item4", CosmosArray.Create(new CosmosElement[]
                    {
                        CosmosObject.Create(new Dictionary<string, CosmosElement>()
                        {
                            {  "a", CosmosInt8.Create(3) },
                            {  "b", CosmosString.Create("adf") },
                        }),
                        CosmosInt16.Create(25)
                    }) },
                });
            sb.Clear();
            element.Accept(new CosmosElementToQueryLiteral(sb));
            Assert.AreEqual(
                $@"{{""item"":C_Guid(""{guid}""),""item2"":[],""item3"":{{}},""item4"":[{{""a"":C_Int8(3),""b"":""adf""}},C_Int16(25)]}}",
                sb.ToString());
        }

        [TestMethod]
        public void TestSqlQueryResumeValueRoundTrip()
        {
            (CosmosElement orderByValue, string resumeValueString)[] testValues = new (CosmosElement orderByValue, string resumeValueString)[] {
                (CosmosUndefined.Create(), "[]"),
                (CosmosNull.Create(), "null"),
                (CosmosBoolean.Create(true), "true"),
                (CosmosBoolean.Create(false), "false"),
                (CosmosNumber64.Create(1337), "1337"),
                (CosmosNumber64.Create(-1), "-1"),
                (CosmosNumber64.Create(3.416), "3.416"),
                (CosmosString.Create("asdf"), "\"asdf\""),
                (CosmosString.Create("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"), "\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\""),
                (CosmosArray.Parse("[]"), "{\"type\":\"array\",\"low\":-6706074647855398782,\"high\":9031114912533472255}"),
                (CosmosObject.Parse("{}"), "{\"type\":\"object\",\"low\":1457042291250783704,\"high\":1493060239874959160}"),
                (CosmosArray.Parse("[100, 200, true, \"asdf\"]"), "{\"type\":\"array\",\"low\":-343833024563930322,\"high\":-7302890837749904085}"),
                (CosmosObject.Parse("{\"num1\":100, \"num2\":2000, \"str\":\"asdf\"}"), "{\"type\":\"object\",\"low\":-725443068664622182,\"high\":-5851519632801302561}")
            };

            foreach ((CosmosElement orderByValue, string resumeValueString) in testValues)
            {
                // Validate that the order by value serializes as expected
                SqlQueryResumeValue resumeValue = SqlQueryResumeValue.FromOrderByValue(orderByValue);
                Assert.AreEqual(SqlQueryResumeValue.ToCosmosElement(resumeValue).ToString(), resumeValueString);

                // Validate that deserialize works as expected
                SqlQueryResumeValue resumeValueFromString = SqlQueryResumeValue.FromCosmosElement(CosmosElement.Parse(resumeValueString));
                Assert.IsTrue(resumeValueFromString.CompareTo(orderByValue) == 0);
            }
        }

        [TestMethod]
        public void TestSqlQueryResumeValueComparison()
        {
            CosmosElement[] orderedValues = new CosmosElement[]
            {
                CosmosUndefined.Create(),
                CosmosNull.Create(),
                CosmosBoolean.Create(false),
                CosmosBoolean.Create(true),
                CosmosNumber64.Create(-1),
                CosmosNumber64.Create(10.5),
                CosmosNumber64.Create(10000000),
                CosmosNumber64.Create(9031114912533472255),
                CosmosString.Create("abc"),
                CosmosString.Create("abd"),
                CosmosString.Create("zzzzzzz"),
                CosmosArray.Parse("[]"),
                CosmosArray.Parse("[100, 200, true, \"asdf\"]"),
                CosmosArray.Parse("[{}, [200, true]]"),
                CosmosObject.Parse("{}"),
                CosmosObject.Parse("{\"num1\":100, \"num2\":2000, \"str\":\"asdf\"}")
            };

            for (int i = 0; i < orderedValues.Length; i++)
            {
                SqlQueryResumeValue resumeValue = SqlQueryResumeValue.FromOrderByValue(orderedValues[i]);

                for (int j = 0; j < orderedValues.Length; j++)
                {
                    int cosmosElementCompareResult = orderedValues[i].CompareTo(orderedValues[j]);
                    int resumeValueCompareResult = resumeValue.CompareTo(orderedValues[j]);

                    Assert.AreEqual(cosmosElementCompareResult, resumeValueCompareResult);
                }
            }
        }

        [TestMethod]
        public void TestSqlQueryResumeValueNegativeCases()
        {
            CosmosElement[] incorrectResumeValues = new CosmosElement[]
            {
                CosmosArray.Parse("[100]"),
                CosmosObject.Parse("{\"type\":\"obj\",\"low\":1457042291250783704,\"high\":1493060239874959160}"),
                CosmosObject.Parse("{\"low\":1457042291250783704,\"high\":1493060239874959160}"),
                CosmosObject.Parse("{\"type\":\"object\",\"high\":1493060239874959160}"),
            };

            foreach (CosmosElement element in incorrectResumeValues)
            {
                Assert.ThrowsException<ArgumentException>(() => SqlQueryResumeValue.FromCosmosElement(element));
            }

            Guid guid = Guid.Parse("69D5AB17-C94A-4173-A278-B59D0D9C7C37");
            byte[] randomBytes = guid.ToByteArray();

            CosmosElement[] unsupportedResumeValues = new CosmosElement[]
            {
                CosmosInt8.Create(10),
                CosmosInt16.Create(42),
                CosmosInt32.Create(42),
                CosmosInt64.Create(42),
                CosmosUInt32.Create(1234),
                CosmosFloat32.Create(1337.42f),
                CosmosFloat64.Create(1337.42),
                CosmosBinary.Create(new ReadOnlyMemory<byte>(randomBytes)),
                CosmosGuid.Create(guid),
            };

            foreach (CosmosElement element in unsupportedResumeValues)
            {
                Assert.ThrowsException<NotSupportedException>(() => SqlQueryResumeValue.FromOrderByValue(element));
            }
        }
    }
}