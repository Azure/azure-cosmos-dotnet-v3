//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.ContinuationTokens;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.OrderBy;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
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
    }
}
