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

        [TestMethod]
        public void ContinuationAndStartTime()
        {
            CosmosString continuation = CosmosString.Create("someEtag");
            DateTime startTime = new DateTime(2023, 1, 15, 10, 30, 0, DateTimeKind.Utc);
            ChangeFeedState state = ChangeFeedState.ContinuationAndStartTime(continuation, startTime);
            CosmosElement cosmosElement = ChangeFeedStateCosmosElementSerializer.ToCosmosElement(state);
            TryCatch<ChangeFeedState> monadicState = ChangeFeedStateCosmosElementSerializer.MonadicFromCosmosElement(cosmosElement);
            Assert.IsTrue(monadicState.Succeeded);
            if (!(monadicState.Result is ChangeFeedStateContinuationAndStartTime changeFeedContinuationAndStartTime))
            {
                Assert.Fail();
                return;
            }

            Assert.AreEqual(changeFeedContinuationAndStartTime.ContinuationToken, continuation);
            Assert.AreEqual(changeFeedContinuationAndStartTime.StartTime, startTime);
        }

        [TestMethod]
        public void ContinuationAndStartTime_SerializesAsContinuationTypeWithOptionalStartTime()
        {
            CosmosString continuation = CosmosString.Create("someEtag");
            DateTime startTime = new DateTime(2023, 1, 15, 10, 30, 0, DateTimeKind.Utc);
            ChangeFeedState state = ChangeFeedState.ContinuationAndStartTime(continuation, startTime);

            CosmosElement cosmosElement = ChangeFeedStateCosmosElementSerializer.ToCosmosElement(state);
            CosmosObject cosmosObject = (CosmosObject)cosmosElement;

            // For backward compatibility the token uses the existing "continuation" type with an
            // optional "startTime" field, rather than a dedicated (new) type value.
            Assert.IsTrue(cosmosObject.TryGetValue("type", out CosmosString typeValue));
            Assert.AreEqual("continuation", typeValue.Value);
            Assert.IsTrue(cosmosObject.TryGetValue("value", out CosmosString _));
            Assert.IsTrue(cosmosObject.TryGetValue("startTime", out CosmosString _));
        }

        [TestMethod]
        public void Continuation_LegacyContinuationAndStartTimeType_StillParses()
        {
            // Tokens produced by an intermediate build could carry the legacy dedicated type value.
            // Ensure they still deserialize into ChangeFeedStateContinuationAndStartTime.
            CosmosObject legacyToken = CosmosObject.Create(
                new System.Collections.Generic.Dictionary<string, CosmosElement>()
                {
                    { "type", CosmosString.Create("continuationAndStartTime") },
                    { "value", CosmosString.Create("someEtag") },
                    { "startTime", CosmosString.Create(new DateTime(2023, 1, 15, 10, 30, 0, DateTimeKind.Utc).ToString("o", System.Globalization.CultureInfo.InvariantCulture)) },
                });

            TryCatch<ChangeFeedState> monadicState = ChangeFeedStateCosmosElementSerializer.MonadicFromCosmosElement(legacyToken);
            Assert.IsTrue(monadicState.Succeeded);
            Assert.IsTrue(monadicState.Result is ChangeFeedStateContinuationAndStartTime);
        }

        [TestMethod]
        public void Continuation_TypeWithoutStartTime_ParsesAsPlainContinuation()
        {
            // An older token (no startTime) must continue to deserialize as a plain continuation.
            CosmosObject token = CosmosObject.Create(
                new System.Collections.Generic.Dictionary<string, CosmosElement>()
                {
                    { "type", CosmosString.Create("continuation") },
                    { "value", CosmosString.Create("someEtag") },
                });

            TryCatch<ChangeFeedState> monadicState = ChangeFeedStateCosmosElementSerializer.MonadicFromCosmosElement(token);
            Assert.IsTrue(monadicState.Succeeded);
            Assert.IsTrue(monadicState.Result is ChangeFeedStateContinuation);
        }
    }
}