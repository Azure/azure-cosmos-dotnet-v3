namespace Microsoft.Azure.Cosmos.Tests.ChangeFeed
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Cosmos.ChangeFeed;
    using Microsoft.Azure.Cosmos.ChangeFeed.Pagination;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class TokenMigrationTest
    {
        private static class TestData
        {
            public static readonly InputAndExpectedOutput SingleItemArray = new InputAndExpectedOutput(
                input: "[{\"token\":\"\\\"54976933\\\"\",\"range\":{\"min\":\"05C1ED193161BC\",\"max\":\"05C1ED49932542\"}}]",
                expectedOutput: "{\"V\":2,\"Rid\":\"\",\"Continuation\":[{\"FeedRange\":{\"type\":\"Effective Partition Key Range\",\"value\":{\"min\":\"05C1ED193161BC\",\"max\":\"05C1ED49932542\"}},\"State\":{\"type\":\"continuation\",\"value\":\"\\\"54976933\\\"\"}}]}");

            public readonly struct InputAndExpectedOutput
            {
                public InputAndExpectedOutput(string input, string expectedOutput)
                {
                    this.Input = input ?? throw new ArgumentNullException(nameof(input));
                    this.ExpectedOutput = expectedOutput ?? throw new ArgumentNullException(nameof(expectedOutput));
                }

                public string Input { get; }

                public string ExpectedOutput { get; }
            }
        }

        [TestMethod]
        public void TestMigration()
        {
            TryCatch<string> monadicMigratedSingleItemArray = MigrateToken(TestData.SingleItemArray.Input);
            monadicMigratedSingleItemArray.ThrowIfFailed();

            Assert.AreEqual(TestData.SingleItemArray.ExpectedOutput, monadicMigratedSingleItemArray.Result);
        }

        private static TryCatch<string> MigrateToken(string input)
        {
            TryCatch<CosmosArray> monadicArray = CosmosArray.Monadic.Parse(input);
            if (monadicArray.Failed)
            {
                return TryCatch<string>.FromException(monadicArray.Exception);
            }

            List<FeedRangeState<ChangeFeedState>> feedRangeStates = new List<FeedRangeState<ChangeFeedState>>();
            foreach (CosmosElement arrayItem in monadicArray.Result)
            {
                if (!(arrayItem is CosmosObject arrayItemAsObject))
                {
                    return TryCatch<string>.FromException(new FormatException("Array Item was not an object"));
                }

                if (!arrayItemAsObject.TryGetValue("token", out CosmosString continuationTokenAsString))
                {
                    return TryCatch<string>.FromException(new FormatException("Failed to get token"));
                }

                if (!arrayItemAsObject.TryGetValue("range", out CosmosObject rangeAsObject))
                {
                    return TryCatch<string>.FromException(new FormatException("Failed to get range"));
                }

                if (!rangeAsObject.TryGetValue("min", out CosmosString minAsString))
                {
                    return TryCatch<string>.FromException(new FormatException("Failed to get min"));
                }

                if (!rangeAsObject.TryGetValue("max", out CosmosString maxAsString))
                {
                    return TryCatch<string>.FromException(new FormatException("Failed to get max"));
                }

                FeedRangeState<ChangeFeedState> feedRangeState = new FeedRangeState<ChangeFeedState>(
                    new FeedRangeEpk(
                        new Documents.Routing.Range<string>(minAsString.Value, maxAsString.Value, isMinInclusive: true, isMaxInclusive: false)),
                    ChangeFeedState.Continuation(continuationTokenAsString));

                feedRangeStates.Add(feedRangeState);
            }

            ChangeFeedCrossFeedRangeState crossFeedRangeState = new ChangeFeedCrossFeedRangeState(feedRangeStates);
            CosmosObject versionedCheckedContinuationToken = CosmosObject.Create(
                new Dictionary<string, CosmosElement>()
                {
                    { "V", CosmosNumber64.Create(2) },
                    { "Rid", CosmosString.Empty }, // Need to replace this with the actual rid in prod.
                    { "Continuation", crossFeedRangeState.ToCosmosElement() }
                });

            return TryCatch<string>.FromResult(versionedCheckedContinuationToken.ToString());
        }
    }
}
