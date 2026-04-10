//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.ChangeFeed
{
    using System;
    using Microsoft.Azure.Cosmos.ChangeFeed.Pagination;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public sealed class ChangeFeedStateCosmosElementSerializerTests
    {
        [TestMethod]
        public void Now()
        {
            ChangeFeedState now = ChangeFeedState.Now();
            CosmosElement cosmosElement = ChangeFeedStateCosmosElementSerializer.ToCosmosElement(now);
            TryCatch<ChangeFeedState> monadicState = ChangeFeedStateCosmosElementSerializer.MonadicFromCosmosElement(cosmosElement);
            Assert.IsTrue(monadicState.Succeeded);
            Assert.IsTrue(monadicState.Result is ChangeFeedStateNow);
        }

        [TestMethod]
        public void Beginning()
        {
            ChangeFeedState beginning = ChangeFeedState.Beginning();
            CosmosElement cosmosElement = ChangeFeedStateCosmosElementSerializer.ToCosmosElement(beginning);
            TryCatch<ChangeFeedState> monadicState = ChangeFeedStateCosmosElementSerializer.MonadicFromCosmosElement(cosmosElement);
            Assert.IsTrue(monadicState.Succeeded);
            Assert.IsTrue(monadicState.Result is ChangeFeedStateBeginning);
        }

        [TestMethod]
        public void Time()
        {
            DateTime startTime = DateTime.MinValue.ToUniversalTime();
            ChangeFeedState time = ChangeFeedState.Time(startTime);
            CosmosElement cosmosElement = ChangeFeedStateCosmosElementSerializer.ToCosmosElement(time);
            TryCatch<ChangeFeedState> monadicState = ChangeFeedStateCosmosElementSerializer.MonadicFromCosmosElement(cosmosElement);
            Assert.IsTrue(monadicState.Succeeded);
            if (!(monadicState.Result is ChangeFeedStateTime stateTime))
            {
                Assert.Fail();
                return;
            }

            Assert.AreEqual(stateTime.StartTime, startTime);
        }

        [TestMethod]
        public void Continuation()
        {
            CosmosString continuation = CosmosString.Create("asdf");
            ChangeFeedState time = ChangeFeedState.Continuation(continuation);
            CosmosElement cosmosElement = ChangeFeedStateCosmosElementSerializer.ToCosmosElement(time);
            TryCatch<ChangeFeedState> monadicState = ChangeFeedStateCosmosElementSerializer.MonadicFromCosmosElement(cosmosElement);
            Assert.IsTrue(monadicState.Succeeded);
            if (!(monadicState.Result is ChangeFeedStateContinuation changeFeedContinuation))
            {
                Assert.Fail();
                return;
            }

            Assert.AreEqual(changeFeedContinuation.ContinuationToken, continuation);
        }
    }
}