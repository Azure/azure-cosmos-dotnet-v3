//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.OrderBy;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.Parallel;
    using Microsoft.Azure.Documents.Routing;
    using VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class OrderByQueryResultTests
    {
        [TestMethod]
        [Owner("ndeshpan")]
        public void TestOrderByUndefined()
        {
            string testResponse = @"{""_rid"":""CuECAN5Z6bM="",""Documents"":[{""_rid"":""CuECAN5Z6bMOAAAAAAAAAA=="",""orderByItems"":[{}]},{""_rid"":""CuECAN5Z6bMNAAAAAAAAAA=="",""orderByItems"":[{}]},{""_rid"":""CuECAN5Z6bMMAAAAAAAAAA=="",""orderByItems"":[{}]},{""_rid"":""CuECAN5Z6bMLAAAAAAAAAA=="",""orderByItems"":[{}]},{""_rid"":""CuECAN5Z6bMKAAAAAAAAAA=="",""orderByItems"":[{}]},{""_rid"":""CuECAN5Z6bMJAAAAAAAAAA=="",""orderByItems"":[{}]},{""_rid"":""CuECAN5Z6bMIAAAAAAAAAA=="",""orderByItems"":[{}]},{""_rid"":""CuECAN5Z6bMHAAAAAAAAAA=="",""orderByItems"":[{}]},{""_rid"":""CuECAN5Z6bMGAAAAAAAAAA=="",""orderByItems"":[{}]},{""_rid"":""CuECAN5Z6bMFAAAAAAAAAA=="",""orderByItems"":[{}]},{""_rid"":""CuECAN5Z6bMEAAAAAAAAAA=="",""orderByItems"":[{}]},{""_rid"":""CuECAN5Z6bMDAAAAAAAAAA=="",""orderByItems"":[{}]},{""_rid"":""CuECAN5Z6bMCAAAAAAAAAA=="",""orderByItems"":[{}]},{""_rid"":""CuECAN5Z6bMBAAAAAAAAAA=="",""orderByItems"":[{}]}],""_count"":14}";

            MemoryStream memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(testResponse));

            CosmosArray documents = CosmosQueryClientCore.ParseElementsFromRestStream(
                memoryStream,
                Documents.ResourceType.Document,
                new CosmosSerializationFormatOptions(
                    "JsonText",
                    (content) => JsonNavigator.Create(content),
                    () => JsonWriter.Create(JsonSerializationFormat.Text)));

            List<OrderByQueryResult> orderByQueryResults = documents.Select(x => new OrderByQueryResult(x)).ToList();
            Assert.AreEqual(14, orderByQueryResults.Count);

            foreach (OrderByQueryResult orderByQueryResult in orderByQueryResults)
            {
                Assert.IsTrue(orderByQueryResult.Payload.Equals(CosmosUndefined.Create()));
                Assert.AreEqual(1, orderByQueryResult.OrderByItems.Count());
                Assert.IsTrue(orderByQueryResult.OrderByItems.First().Item.Equals(CosmosUndefined.Create()));
            }
        }
    }
}