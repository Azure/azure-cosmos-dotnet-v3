//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.Parallel;
    using Newtonsoft.Json;
    using VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ParallelContinuationTokenTests
    {
        [TestMethod]
        [DataRow(null, DisplayName = "null token")]
        [DataRow("some token", DisplayName = "some token")]
        public void TestRoundTripAsCosmosElement(string token)
        {
            ParallelContinuationToken compositeContinuationToken = new ParallelContinuationToken(
                token,
                new Documents.Routing.Range<string>("asdf", "asdf", true, false));

            CosmosElement cosmosElementToken = ParallelContinuationToken.ToCosmosElement(compositeContinuationToken);
            TryCatch<ParallelContinuationToken> tryCompositeContinuationTokenFromCosmosElement = ParallelContinuationToken.TryCreateFromCosmosElement(cosmosElementToken);
            Assert.IsTrue(tryCompositeContinuationTokenFromCosmosElement.Succeeded);
            ParallelContinuationToken compositeContinuationTokenFromCosmosElement = tryCompositeContinuationTokenFromCosmosElement.Result;

            Assert.AreEqual(compositeContinuationToken.Token, compositeContinuationTokenFromCosmosElement.Token);
            Assert.AreEqual(compositeContinuationToken.Range.Min, compositeContinuationTokenFromCosmosElement.Range.Min);
            Assert.AreEqual(compositeContinuationToken.Range.Max, compositeContinuationTokenFromCosmosElement.Range.Max);
            Assert.AreEqual(compositeContinuationToken.Range.IsMinInclusive, compositeContinuationTokenFromCosmosElement.Range.IsMinInclusive);
            Assert.AreEqual(compositeContinuationToken.Range.IsMaxInclusive, compositeContinuationTokenFromCosmosElement.Range.IsMaxInclusive);
        }
    }
}