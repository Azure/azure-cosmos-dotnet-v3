//-----------------------------------------------------------------------
// <copyright file="LinqAttributeContractTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Collections.ObjectModel;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Pagination;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests;
    using Microsoft.Azure.Cosmos.Serializer;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [SDK.EmulatorTests.TestClass]
    public sealed class ChangeFeedAsyncEnumerableTests : BaseCosmosClientHelper
    {
        private ContainerInternal Container = null;

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit();
            string PartitionKey = "/status";
            ContainerResponse response = await this.database.CreateContainerAsync(
                new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: PartitionKey),
                throughput: 20000,
                cancellationToken: this.cancellationToken);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Container);
            Assert.IsNotNull(response.Resource);

            this.Container = (ContainerInlineCore)response;
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        public async Task StartFromBeginning()
        {
            int batchSize = 25;

            await this.CreateRandomItems(this.Container, batchSize, randomPartitionKey: true);
            (int totalCount, ChangeFeedCrossFeedRangeState state) countAndState;
            IAsyncEnumerable<TryCatch<ChangeFeedPage>> asyncEnumerable = this.Container.GetChangeFeedAsyncEnumerable(
                ChangeFeedCrossFeedRangeState.CreateFromBeginning(),
                ChangeFeedMode.Incremental);
            countAndState = await PartialDrainAsync(asyncEnumerable);
            Assert.AreEqual(batchSize, countAndState.totalCount);

            // Insert another batch of 25 and use the state from the first cycle
            await this.CreateRandomItems(this.Container, batchSize, randomPartitionKey: true);
            asyncEnumerable = this.Container.GetChangeFeedAsyncEnumerable(countAndState.state, ChangeFeedMode.Incremental);
            countAndState = await PartialDrainAsync(asyncEnumerable);

            Assert.AreEqual(batchSize, countAndState.totalCount);
        }

        [TestMethod]
        public async Task StartFromNow()
        {
            int batchSize = 25;

            (int totalCount, ChangeFeedCrossFeedRangeState state) countAndState;
            IAsyncEnumerable<TryCatch<ChangeFeedPage>> asyncEnumerable = this.Container.GetChangeFeedAsyncEnumerable(
                ChangeFeedCrossFeedRangeState.CreateFromNow(),
                ChangeFeedMode.Incremental);
            await this.CreateRandomItems(this.Container, batchSize, randomPartitionKey: true);
            countAndState = await PartialDrainAsync(asyncEnumerable);
            Assert.AreEqual(0, countAndState.totalCount);

            await this.CreateRandomItems(this.Container, batchSize, randomPartitionKey: true);
            asyncEnumerable = this.Container.GetChangeFeedAsyncEnumerable(countAndState.state, ChangeFeedMode.Incremental);
            countAndState = await PartialDrainAsync(asyncEnumerable);

            Assert.AreEqual(batchSize, countAndState.totalCount);
        }

        [TestMethod]
        public async Task StartFromTime()
        {
            int batchSize = 25;

            (int totalCount, ChangeFeedCrossFeedRangeState state) countAndState;
            IAsyncEnumerable<TryCatch<ChangeFeedPage>> asyncEnumerable = this.Container.GetChangeFeedAsyncEnumerable(
                ChangeFeedCrossFeedRangeState.CreateFromTime(DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(1))),
                ChangeFeedMode.Incremental);
            await this.CreateRandomItems(this.Container, batchSize, randomPartitionKey: true);
            countAndState = await PartialDrainAsync(asyncEnumerable);
            Assert.AreEqual(batchSize, countAndState.totalCount);

            await this.CreateRandomItems(this.Container, batchSize, randomPartitionKey: true);
            asyncEnumerable = this.Container.GetChangeFeedAsyncEnumerable(countAndState.state, ChangeFeedMode.Incremental);
            countAndState = await PartialDrainAsync(asyncEnumerable);

            Assert.AreEqual(batchSize, countAndState.totalCount);
        }

        [TestMethod]
        public async Task SerializeAndDeserializeContinuationToken()
        {
            int batchSize = 25;

            (int totalCount, ChangeFeedCrossFeedRangeState state) countAndState;
            IAsyncEnumerable<TryCatch<ChangeFeedPage>> asyncEnumerable = this.Container.GetChangeFeedAsyncEnumerable(
                ChangeFeedCrossFeedRangeState.CreateFromBeginning(),
                ChangeFeedMode.Incremental);
            await this.CreateRandomItems(this.Container, batchSize, randomPartitionKey: true);
            countAndState = await PartialDrainAsync(asyncEnumerable);
            Assert.AreEqual(batchSize, countAndState.totalCount);

            // Serialize the state and send it over the wire for your user to resume execution.
            string continuationToken = countAndState.state.ToString();

            await this.CreateRandomItems(this.Container, batchSize, randomPartitionKey: true);

            // Deserialize the state that the user came back with to resume from.
            ChangeFeedCrossFeedRangeState state = ChangeFeedCrossFeedRangeState.Parse(continuationToken);
            asyncEnumerable = this.Container.GetChangeFeedAsyncEnumerable(state, ChangeFeedMode.Incremental);
            countAndState = await PartialDrainAsync(asyncEnumerable);

            Assert.AreEqual(batchSize, countAndState.totalCount);
        }

        [TestMethod]
        public async Task ParallelizeAcrossFeedRanges()
        {
            int batchSize = 25;
            await this.CreateRandomItems(this.Container, batchSize, randomPartitionKey: true);

            // Create one start state for each physical partition.
            List<ChangeFeedCrossFeedRangeState> startStates = new List<ChangeFeedCrossFeedRangeState>();
            IReadOnlyList<FeedRange> feedRanges = await this.Container.GetFeedRangesAsync();
            foreach (FeedRange feedRange in feedRanges)
            {
                startStates.Add(ChangeFeedCrossFeedRangeState.CreateFromBeginning(feedRange));
            }

            // Create an independant enumerable for each of those start states.
            List<IAsyncEnumerable<TryCatch<ChangeFeedPage>>> asyncEnumerables = new List<IAsyncEnumerable<TryCatch<ChangeFeedPage>>>();
            foreach (ChangeFeedCrossFeedRangeState state in startStates)
            {
                IAsyncEnumerable<TryCatch<ChangeFeedPage>> asyncEnumerable = this.Container.GetChangeFeedAsyncEnumerable(state, ChangeFeedMode.Incremental);
                asyncEnumerables.Add(asyncEnumerable);
            }

            int totalCount = 0;
            foreach (IAsyncEnumerable<TryCatch<ChangeFeedPage>> asyncEnumerable in asyncEnumerables)
            {
                // This part can be done in parallel on the same machine or on different machines,
                // since they are independant enumerables.
                (int totalCount, ChangeFeedCrossFeedRangeState state) countAndState = await PartialDrainAsync(asyncEnumerable);
                totalCount += countAndState.totalCount;
            }

            Assert.AreEqual(batchSize, totalCount);
        }

        [TestMethod]
        public async Task TargetMultipleLogicalPartitionKeys()
        {
            int batchSize = 25;

            string pkToRead1 = "pkToRead1";
            string pkToRead2 = "pkToRead2";
            string otherPK = "otherPK";

            for (int i = 0; i < batchSize; i++)
            {
                await this.Container.CreateItemAsync(this.CreateRandomToDoActivity(pkToRead1));
            }

            for (int i = 0; i < batchSize; i++)
            {
                await this.Container.CreateItemAsync(this.CreateRandomToDoActivity(pkToRead2));
            }

            for (int i = 0; i < batchSize; i++)
            {
                await this.Container.CreateItemAsync(this.CreateRandomToDoActivity(otherPK));
            }

            // Create one start state for each logical partition key.
            List<FeedRangeState<ChangeFeedState>> feedRangeStates = new List<FeedRangeState<ChangeFeedState>>();
            IReadOnlyList<string> partitionKeysToTarget = new List<string>()
            {
                pkToRead1,
                pkToRead2
            };

            foreach (string partitionKeyToTarget in partitionKeysToTarget)
            {
                feedRangeStates.Add(
                    new FeedRangeState<ChangeFeedState>(
                        (FeedRangeInternal)FeedRange.FromPartitionKey(
                            new Cosmos.PartitionKey(partitionKeyToTarget)),
                        ChangeFeedState.Beginning()));
            }

            // Use the list composition property of the constructor to merge them in to a single state.
            ChangeFeedCrossFeedRangeState multipleLogicalPartitionKeyState = new ChangeFeedCrossFeedRangeState(feedRangeStates.ToImmutableArray());
            IAsyncEnumerable<TryCatch<ChangeFeedPage>> asyncEnumerable = this.Container.GetChangeFeedAsyncEnumerable(multipleLogicalPartitionKeyState, ChangeFeedMode.Incremental);
            (int totalCount, ChangeFeedCrossFeedRangeState _) = await PartialDrainAsync(asyncEnumerable);

            Assert.AreEqual(2 * batchSize, totalCount);
        }

        [TestMethod]
        public async Task TestScaleUpAndScaleDown()
        {
            int batchSize = 25;

            await this.CreateRandomItems(this.Container, batchSize, randomPartitionKey: true);

            // Start draining as 1 iterator
            IAsyncEnumerable<TryCatch<ChangeFeedPage>> asyncEnumerable = this.Container.GetChangeFeedAsyncEnumerable(
                ChangeFeedCrossFeedRangeState.CreateFromBeginning(),
                ChangeFeedMode.Incremental);
            (int totalCount, ChangeFeedCrossFeedRangeState state) = await PartialDrainAsync(asyncEnumerable);
            Assert.AreEqual(batchSize, totalCount);

            // Continue draining as two iterators
            if (!state.TrySplit(out ChangeFeedCrossFeedRangeState first, out ChangeFeedCrossFeedRangeState second))
            {
                Assert.Fail("Failed to split");
            }

            await this.CreateRandomItems(this.Container, batchSize, randomPartitionKey: true);

            IAsyncEnumerable<TryCatch<ChangeFeedPage>> leftEnumerable = this.Container.GetChangeFeedAsyncEnumerable(first, ChangeFeedMode.Incremental);
            (int leftTotalCount, ChangeFeedCrossFeedRangeState leftResumeState) = await PartialDrainAsync(leftEnumerable);

            IAsyncEnumerable<TryCatch<ChangeFeedPage>> rightEnumerable = this.Container.GetChangeFeedAsyncEnumerable(second, ChangeFeedMode.Incremental);
            (int rightTotalCount, ChangeFeedCrossFeedRangeState rightResumeState) = await PartialDrainAsync(rightEnumerable);

            Assert.AreEqual(batchSize, leftTotalCount + rightTotalCount);

            // Finish draining again as a single enumerator
            ChangeFeedCrossFeedRangeState mergedState = leftResumeState.Merge(rightResumeState);

            await this.CreateRandomItems(this.Container, batchSize, randomPartitionKey: true);

            IAsyncEnumerable<TryCatch<ChangeFeedPage>> mergedEnumerable = this.Container.GetChangeFeedAsyncEnumerable(mergedState, ChangeFeedMode.Incremental);
            (int mergedTotalCount, ChangeFeedCrossFeedRangeState _) = await PartialDrainAsync(mergedEnumerable);

            Assert.AreEqual(batchSize, mergedTotalCount);
        }

        [TestMethod]
        [ExpectedException(typeof(OperationCanceledException))]
        public async Task TestCancellationToken()
        {
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            IAsyncEnumerable<TryCatch<ChangeFeedPage>> asyncEnumerable = this.Container.GetChangeFeedAsyncEnumerable(
                ChangeFeedCrossFeedRangeState.CreateFromBeginning(),
                ChangeFeedMode.Incremental);
            await foreach (TryCatch<ChangeFeedPage> monadicPage in asyncEnumerable.WithCancellation(cancellationTokenSource.Token))
            {
                monadicPage.ThrowIfFailed();
            }
        }

        [TestMethod]
        public async Task TestContentSerializationOptions()
        {
            {
                // Native format
                IAsyncEnumerable<TryCatch<ChangeFeedPage>> asyncEnumerable = this.Container.GetChangeFeedAsyncEnumerable(
                    ChangeFeedCrossFeedRangeState.CreateFromBeginning(),
                    ChangeFeedMode.Incremental,
                    new ChangeFeedRequestOptions()
                    {
                        JsonSerializationFormatOptions = JsonSerializationFormatOptions.Create(JsonSerializationFormat.Binary)
                    });
                await foreach (TryCatch<ChangeFeedPage> monadicPage in asyncEnumerable)
                {
                    monadicPage.ThrowIfFailed();

                    if (monadicPage.Result.NotModified)
                    {
                        break;
                    }
                }
            }

            {
                // Custom format
                IAsyncEnumerable<TryCatch<ChangeFeedPage>> asyncEnumerable = this.Container.GetChangeFeedAsyncEnumerable(
                    ChangeFeedCrossFeedRangeState.CreateFromBeginning(),
                    ChangeFeedMode.Incremental,
                    new ChangeFeedRequestOptions()
                    {
                        JsonSerializationFormatOptions = JsonSerializationFormatOptions.Create(
                            JsonSerializationFormat.Binary,
                            (content) => JsonNavigator.Create(content))
                    });
                await foreach (TryCatch<ChangeFeedPage> monadicPage in asyncEnumerable)
                {
                    monadicPage.ThrowIfFailed();

                    if (monadicPage.Result.NotModified)
                    {
                        break;
                    }
                }
            }
        }

        private static async Task<(int, ChangeFeedCrossFeedRangeState)> PartialDrainAsync(IAsyncEnumerable<TryCatch<ChangeFeedPage>> asyncEnumerable)
        {
            ChangeFeedCrossFeedRangeState state = default;
            int totalCount = 0;
            await foreach (TryCatch<ChangeFeedPage> monadicPage in asyncEnumerable)
            {
                monadicPage.ThrowIfFailed();

                ChangeFeedPage page = monadicPage.Result;
                state = page.State;
                if (page.NotModified)
                {
                    break;
                }

                totalCount += page.Documents.Count;
            }

            return (totalCount, state);
        }

        private async Task<IList<ToDoActivity>> CreateRandomItems(ContainerInternal container, int pkCount, int perPKItemCount = 1, bool randomPartitionKey = true)
        {
            Assert.IsFalse(!randomPartitionKey && perPKItemCount > 1);

            List<ToDoActivity> createdList = new List<ToDoActivity>();
            for (int i = 0; i < pkCount; i++)
            {
                string pk = "TBD";
                if (randomPartitionKey)
                {
                    pk += Guid.NewGuid().ToString();
                }

                for (int j = 0; j < perPKItemCount; j++)
                {
                    ToDoActivity temp = this.CreateRandomToDoActivity(pk);

                    createdList.Add(temp);

                    await container.CreateItemAsync<ToDoActivity>(item: temp);
                }
            }

            return createdList;
        }

        private ToDoActivity CreateRandomToDoActivity(string pk = null)
        {
            if (string.IsNullOrEmpty(pk))
            {
                pk = "TBD" + Guid.NewGuid().ToString();
            }

            return new ToDoActivity()
            {
                id = Guid.NewGuid().ToString(),
                description = "CreateRandomToDoActivity",
                status = pk,
                taskNum = 42,
                cost = double.MaxValue
            };
        }

        public class ToDoActivity
        {
            public string id { get; set; }
            public int taskNum { get; set; }
            public double cost { get; set; }
            public string description { get; set; }
            public string status { get; set; }
        }
    }
}
