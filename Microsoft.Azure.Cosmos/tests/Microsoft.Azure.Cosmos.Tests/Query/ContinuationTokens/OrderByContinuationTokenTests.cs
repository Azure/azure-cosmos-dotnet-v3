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
    using Microsoft.Azure.Cosmos.Query.Core.ContinuationTokens;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.OrderBy;
    using Microsoft.Azure.Cosmos.Monads;
    using Microsoft.Azure.Documents.Routing;
    using Newtonsoft.Json;
    using VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class OrderByContinuationTokenTests
    {
        [TestMethod]
        public void TestRoundTripAsCosmosElement()
        {
            CompositeContinuationToken compositeContinuationToken = new CompositeContinuationToken()
            {
                Token = "someToken",
                Range = new Documents.Routing.Range<string>("asdf", "asdf", false, false),
            };

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
                compositeContinuationToken,
                orderByItems,
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
        public void TestOrderByQueryLiterals()
        {
            StringBuilder sb = new StringBuilder();
            CosmosElement element = CosmosObject.Create(
                new Dictionary<string, CosmosElement>() {{"item", CosmosString.Create("asdf")}});
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
                $@"{{""item"":C_Guid(""{guid.ToString()}""),""item2"":C_Binary(""0x{hexString}"")}}",
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
                $@"{{""item"":C_Guid(""{guid.ToString()}""),""item2"":[],""item3"":{{}},""item4"":[{{""a"":C_Int8(3),""b"":""adf""}},C_Int16(25)]}}",
                sb.ToString());
        }
    }
}
