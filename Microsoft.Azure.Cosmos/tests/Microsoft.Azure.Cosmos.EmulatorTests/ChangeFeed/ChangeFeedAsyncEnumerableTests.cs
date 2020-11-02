//-----------------------------------------------------------------------
// <copyright file="LinqAttributeContractTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests;
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
            IAsyncEnumerable<TryCatch<ChangeFeedPage>> asyncEnumerable = this.Container.GetChangeFeedAsyncEnumerable();
            countAndState = await PartialDrainAsync(asyncEnumerable);
            Assert.AreEqual(batchSize, countAndState.totalCount);

            // Insert another batch of 25 and use the state from the first cycle
            await this.CreateRandomItems(this.Container, batchSize, randomPartitionKey: true);
            asyncEnumerable = this.Container.GetChangeFeedAsyncEnumerable(countAndState.state);
            countAndState = await PartialDrainAsync(asyncEnumerable);

            Assert.AreEqual(batchSize, countAndState.totalCount);
        }

        [TestMethod]
        public async Task StartFromNow()
        {
            int batchSize = 25;

            (int totalCount, ChangeFeedCrossFeedRangeState state) countAndState;
            IAsyncEnumerable<TryCatch<ChangeFeedPage>> asyncEnumerable = this.Container.GetChangeFeedAsyncEnumerable(
                ChangeFeedCrossFeedRangeState.CreateFromNow());
            await this.CreateRandomItems(this.Container, batchSize, randomPartitionKey: true);
            countAndState = await PartialDrainAsync(asyncEnumerable);
            Assert.AreEqual(0, countAndState.totalCount);

            await this.CreateRandomItems(this.Container, batchSize, randomPartitionKey: true);
            asyncEnumerable = this.Container.GetChangeFeedAsyncEnumerable(countAndState.state);
            countAndState = await PartialDrainAsync(asyncEnumerable);

            Assert.AreEqual(batchSize, countAndState.totalCount);
        }

        [TestMethod]
        public async Task StartFromTime()
        {
            int batchSize = 25;

            (int totalCount, ChangeFeedCrossFeedRangeState state) countAndState;
            IAsyncEnumerable<TryCatch<ChangeFeedPage>> asyncEnumerable = this.Container.GetChangeFeedAsyncEnumerable(
                ChangeFeedCrossFeedRangeState.CreateFromTime(DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(1))));
            await this.CreateRandomItems(this.Container, batchSize, randomPartitionKey: true);
            countAndState = await PartialDrainAsync(asyncEnumerable);
            Assert.AreEqual(batchSize, countAndState.totalCount);

            await this.CreateRandomItems(this.Container, batchSize, randomPartitionKey: true);
            asyncEnumerable = this.Container.GetChangeFeedAsyncEnumerable(countAndState.state);
            countAndState = await PartialDrainAsync(asyncEnumerable);

            Assert.AreEqual(batchSize, countAndState.totalCount);
        }

        [TestMethod]
        public async Task SerializeAndDeserializeContinuationToken()
        {
            int batchSize = 25;

            (int totalCount, ChangeFeedCrossFeedRangeState state) countAndState;
            IAsyncEnumerable<TryCatch<ChangeFeedPage>> asyncEnumerable = this.Container.GetChangeFeedAsyncEnumerable(
                ChangeFeedCrossFeedRangeState.CreateFromBeginning());
            await this.CreateRandomItems(this.Container, batchSize, randomPartitionKey: true);
            countAndState = await PartialDrainAsync(asyncEnumerable);
            Assert.AreEqual(batchSize, countAndState.totalCount);

            string continuationToken = countAndState.state.ToString();

            await this.CreateRandomItems(this.Container, batchSize, randomPartitionKey: true);
            asyncEnumerable = this.Container.GetChangeFeedAsyncEnumerable(ChangeFeedCrossFeedRangeState.Parse(continuationToken));
            countAndState = await PartialDrainAsync(asyncEnumerable);

            Assert.AreEqual(batchSize, countAndState.totalCount);
        }

        [TestMethod]
        public async Task ParallelizeAcrossFeedRanges()
        {
            int batchSize = 25;
            await this.CreateRandomItems(this.Container, batchSize, randomPartitionKey: true);

            List<ChangeFeedCrossFeedRangeState> startStates = new List<ChangeFeedCrossFeedRangeState>();
            IReadOnlyList<FeedRange> feedRanges = await this.Container.GetFeedRangesAsync();
            foreach (FeedRange feedRange in feedRanges)
            {
                startStates.Add(ChangeFeedCrossFeedRangeState.CreateFromBeginning(feedRange));
            }

            List<IAsyncEnumerable<TryCatch<ChangeFeedPage>>> asyncEnumerables = new List<IAsyncEnumerable<TryCatch<ChangeFeedPage>>>();
            foreach (ChangeFeedCrossFeedRangeState state in startStates)
            {
                IAsyncEnumerable<TryCatch<ChangeFeedPage>> asyncEnumerable = this.Container.GetChangeFeedAsyncEnumerable(state);
                asyncEnumerables.Add(asyncEnumerable);
            }

            List<ChangeFeedCrossFeedRangeState> resumeStates = new List<ChangeFeedCrossFeedRangeState>();
            int totalCount = 0;
            foreach (IAsyncEnumerable<TryCatch<ChangeFeedPage>> asyncEnumerable in asyncEnumerables)
            {
                (int totalCount, ChangeFeedCrossFeedRangeState state) countAndState = await PartialDrainAsync(asyncEnumerable);
                totalCount += countAndState.totalCount;
                resumeStates.Add(countAndState.state);
            }

            Assert.AreEqual(batchSize, totalCount);

            await this.CreateRandomItems(this.Container, batchSize, randomPartitionKey: true);
        }

        [TestMethod]
        public async Task TestSplitAndMerge()
        {
            int batchSize = 25;

            await this.CreateRandomItems(this.Container, batchSize, randomPartitionKey: true);

            // Start draining as 1 iterator
            IAsyncEnumerable<TryCatch<ChangeFeedPage>> asyncEnumerable = this.Container.GetChangeFeedAsyncEnumerable();
            (int totalCount, ChangeFeedCrossFeedRangeState state) = await PartialDrainAsync(asyncEnumerable);
            Assert.AreEqual(batchSize, totalCount);

            // Continue draining as two iterators
            if (!state.TrySplit(out (ChangeFeedCrossFeedRangeState left, ChangeFeedCrossFeedRangeState right)childrenStates))
            {
                Assert.Fail("Failed to split");
            }

            await this.CreateRandomItems(this.Container, batchSize, randomPartitionKey: true);

            IAsyncEnumerable<TryCatch<ChangeFeedPage>> leftEnumerable = this.Container.GetChangeFeedAsyncEnumerable(childrenStates.left);
            (int leftTotalCount, ChangeFeedCrossFeedRangeState leftResumeState) = await PartialDrainAsync(leftEnumerable);

            IAsyncEnumerable<TryCatch<ChangeFeedPage>> rightEnumerable = this.Container.GetChangeFeedAsyncEnumerable(childrenStates.right);
            (int rightTotalCount, ChangeFeedCrossFeedRangeState rightResumeState) = await PartialDrainAsync(rightEnumerable);

            Assert.AreEqual(batchSize, leftTotalCount + rightTotalCount);

            // Finish draining again as a single enumerator
            ChangeFeedCrossFeedRangeState mergedState = leftResumeState.Merge(rightResumeState);

            await this.CreateRandomItems(this.Container, batchSize, randomPartitionKey: true);

            IAsyncEnumerable<TryCatch<ChangeFeedPage>> mergedEnumerable = this.Container.GetChangeFeedAsyncEnumerable(mergedState);
            (int mergedTotalCount, ChangeFeedCrossFeedRangeState _) = await PartialDrainAsync(mergedEnumerable);

            Assert.AreEqual(batchSize, mergedTotalCount);
        }

        private static async Task<(int, ChangeFeedCrossFeedRangeState)> PartialDrainAsync(IAsyncEnumerable<TryCatch<ChangeFeedPage>> asyncEnumerable)
        {
            ChangeFeedCrossFeedRangeState state;
            int totalCount = 0;
            await foreach (TryCatch<ChangeFeedPage> monadicPage in asyncEnumerable)
            {
                monadicPage.ThrowIfFailed();

                ChangeFeedPage page = monadicPage.Result;
                if (page is ChangeFeedNotModifiedPage changeFeedNotModifedPage)
                {
                    state = changeFeedNotModifedPage.State;
                    break;
                }

                ChangeFeedSuccessPage successPage = (ChangeFeedSuccessPage)page;
                Collection<ToDoActivity> response = TestCommon.SerializerCore.FromStream<CosmosFeedResponseUtil<ToDoActivity>>(successPage.Content).Data;
                totalCount += response.Count;
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
