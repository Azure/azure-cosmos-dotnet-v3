//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.ContinuationTokens;
    using Microsoft.Azure.Cosmos.Monads;
    using Newtonsoft.Json;
    using VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CompositeContinuationTokenTests
    {
        [TestMethod]
        public void TestNewtonsoftConvertsions()
        {
            string serializedContinuationToken = "[{\"token\":null,\"range\":{\"min\":\"05C1C9CD673398\",\"max\":\"05C1D9CD673398\"}}]";
            CompositeContinuationToken[] deserializedTokens = JsonConvert.DeserializeObject<CompositeContinuationToken[]>(serializedContinuationToken);
            Assert.IsNotNull(deserializedTokens);
            Assert.IsTrue(deserializedTokens.Length == 1);
            CompositeContinuationToken deserializedToken = deserializedTokens[0];
            Assert.IsNull(deserializedToken.Token);
            Assert.AreEqual("05C1C9CD673398", deserializedToken.Range.Min);
            Assert.AreEqual("05C1D9CD673398", deserializedToken.Range.Max);
            Assert.AreEqual(serializedContinuationToken, JsonConvert.SerializeObject(deserializedTokens, Formatting.None));
        }

        [TestMethod]
        [DataRow(null, DisplayName = "null token")]
        [DataRow("some token", DisplayName = "some token")]
        public void TestRoundTripAsCosmosElement(string token)
        {
            CompositeContinuationToken compositeContinuationToken = new CompositeContinuationToken()
            {
                Token = token,
                Range = new Documents.Routing.Range<string>("asdf", "asdf", false, false),
            };

            CosmosElement cosmosElementToken = CompositeContinuationToken.ToCosmosElement(compositeContinuationToken);
            TryCatch<CompositeContinuationToken> tryCompositeContinuationTokenFromCosmosElement = CompositeContinuationToken.TryCreateFromCosmosElement(cosmosElementToken);
            Assert.IsTrue(tryCompositeContinuationTokenFromCosmosElement.Succeeded);
            CompositeContinuationToken compositeContinuationTokenFromCosmosElement = tryCompositeContinuationTokenFromCosmosElement.Result;
            Assert.AreEqual(JsonConvert.SerializeObject(compositeContinuationToken), JsonConvert.SerializeObject(compositeContinuationTokenFromCosmosElement));
        }
    }
}
