//-----------------------------------------------------------------------
// <copyright file="LinqAttributeContractTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ReadFeed
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ReadFeed.Pagination;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [SDK.EmulatorTests.TestClass]
    public sealed class ReadFeedAsyncEnumerableTests : BaseCosmosClientHelper
    {
        private ContainerInternal Container = null;

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit();
            string PartitionKey = "/pk";
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
        public async Task DrainUsingState()
        {
            int batchSize = 25;

            await this.CreateRandomItems(this.Container, batchSize, randomPartitionKey: true);

            int totalCount = 0;
            ReadFeedCrossFeedRangeState? state = ReadFeedCrossFeedRangeState.CreateFromBeginning();
            do
            {
                IAsyncEnumerable<TryCatch<ReadFeedPage>> asyncEnumerable = this.Container.GetReadFeedAsyncEnumerable(
                    state.Value,
                    new QueryRequestOptions()
                    {
                        MaxItemCount = 1,
                    });
                (int localCount, ReadFeedCrossFeedRangeState? newState) = await DrainOnePageAsync(asyncEnumerable);
                totalCount += localCount;
                state = newState;
            }
            while (state.HasValue);
            Assert.AreEqual(batchSize, totalCount);
        }

        [TestMethod]
        public async Task SerializeAndDeserializeContinuationToken()
        {
            int batchSize = 25;

            await this.CreateRandomItems(this.Container, batchSize, randomPartitionKey: true);

            int totalCount = 0;

            IAsyncEnumerable<TryCatch<ReadFeedPage>> asyncEnumerable = this.Container.GetReadFeedAsyncEnumerable(
                ReadFeedCrossFeedRangeState.CreateFromBeginning());
            (int localCount, ReadFeedCrossFeedRangeState? state) = await DrainOnePageAsync(asyncEnumerable);
            totalCount += localCount;

            // Serialize the state and send it over the wire for your user to resume execution.
            string continuationToken = state.Value.ToString();

            // Deserialize the state that the user came back with to resume from.
            ReadFeedCrossFeedRangeState parsedState = ReadFeedCrossFeedRangeState.Parse(continuationToken);
            asyncEnumerable = this.Container.GetReadFeedAsyncEnumerable(parsedState);
            (localCount, _) = await DrainAllAsync(asyncEnumerable);
            totalCount += localCount;

            Assert.AreEqual(batchSize, totalCount);
        }

        [TestMethod]
        public async Task ParallelizeAcrossFeedRanges()
        {
            int batchSize = 25;
            await this.CreateRandomItems(this.Container, batchSize, randomPartitionKey: true);

            // Create one start state for each physical partition.
            List<ReadFeedCrossFeedRangeState> startStates = new List<ReadFeedCrossFeedRangeState>();
            IReadOnlyList<FeedRange> feedRanges = await this.Container.GetFeedRangesAsync();
            foreach (FeedRange feedRange in feedRanges)
            {
                startStates.Add(ReadFeedCrossFeedRangeState.CreateFromBeginning(feedRange));
            }

            // Create an independant enumerable for each of those start states.
            List<IAsyncEnumerable<TryCatch<ReadFeedPage>>> asyncEnumerables = new List<IAsyncEnumerable<TryCatch<ReadFeedPage>>>();
            foreach (ReadFeedCrossFeedRangeState state in startStates)
            {
                IAsyncEnumerable<TryCatch<ReadFeedPage>> asyncEnumerable = this.Container.GetReadFeedAsyncEnumerable(state);
                asyncEnumerables.Add(asyncEnumerable);
            }

            int totalCount = 0;
            foreach (IAsyncEnumerable<TryCatch<ReadFeedPage>> asyncEnumerable in asyncEnumerables)
            {
                // This part can be done in parallel on the same machine or on different machines,
                // since they are independant enumerables.
                (int totalCount, ReadFeedCrossFeedRangeState? state) countAndState = await DrainAllAsync(asyncEnumerable);
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
                await this.Container.CreateItemAsync(ToDoActivity.CreateRandomToDoActivity(pk: pkToRead1));
            }

            for (int i = 0; i < batchSize; i++)
            {
                await this.Container.CreateItemAsync(ToDoActivity.CreateRandomToDoActivity(pk: pkToRead2));
            }

            for (int i = 0; i < batchSize; i++)
            {
                await this.Container.CreateItemAsync(ToDoActivity.CreateRandomToDoActivity(pk: otherPK));
            }

            // Create one start state for each logical partition key.
            List<FeedRangeState<ReadFeedState>> feedRangeStates = new List<FeedRangeState<ReadFeedState>>();
            IReadOnlyList<string> partitionKeysToTarget = new List<string>()
            {
                pkToRead1,
                pkToRead2
            };

            foreach (string partitionKeyToTarget in partitionKeysToTarget)
            {
                feedRangeStates.Add(
                    new FeedRangeState<ReadFeedState>(
                        (FeedRangeInternal)FeedRange.FromPartitionKey(
                            new Cosmos.PartitionKey(partitionKeyToTarget)),
                        ReadFeedState.Beginning()));
            }

            // Use the list composition property of the constructor to merge them in to a single state.
            ReadFeedCrossFeedRangeState multipleLogicalPartitionKeyState = new ReadFeedCrossFeedRangeState(feedRangeStates.ToImmutableArray());
            IAsyncEnumerable<TryCatch<ReadFeedPage>> asyncEnumerable = this.Container.GetReadFeedAsyncEnumerable(multipleLogicalPartitionKeyState);
            (int totalCount, ReadFeedCrossFeedRangeState? _) = await DrainAllAsync(asyncEnumerable);

            Assert.AreEqual(2 * batchSize, totalCount);
        }

        [TestMethod]
        public async Task TestScaleUpAndScaleDown()
        {
            int batchSize = 25;

            await this.CreateRandomItems(this.Container, batchSize, randomPartitionKey: true);

            int totalCount = 0;
            // Start draining as 1 iterator
            IAsyncEnumerable<TryCatch<ReadFeedPage>> asyncEnumerable = this.Container.GetReadFeedAsyncEnumerable(
                ReadFeedCrossFeedRangeState.CreateFromBeginning(),
                new QueryRequestOptions() 
                { 
                    MaxItemCount = 1,
                });
            (int localCount, ReadFeedCrossFeedRangeState? state) = await DrainOnePageAsync(asyncEnumerable);
            totalCount += localCount;
            // Continue draining as two iterators
            if (!state.Value.TrySplit(out ReadFeedCrossFeedRangeState first, out ReadFeedCrossFeedRangeState second))
            {
                Assert.Fail("Failed to split");
            }

            IAsyncEnumerable<TryCatch<ReadFeedPage>> firstEnumerable = this.Container.GetReadFeedAsyncEnumerable(
                first, 
                new QueryRequestOptions()
                {
                    MaxItemCount = 1,
                });
            (int leftCount, ReadFeedCrossFeedRangeState? firstResumeState) = await DrainOnePageAsync(firstEnumerable);
            totalCount += leftCount;

            IAsyncEnumerable<TryCatch<ReadFeedPage>> secondEnumerable = this.Container.GetReadFeedAsyncEnumerable(
                second,
                new QueryRequestOptions()
                {
                    MaxItemCount = 1,
                });
            (int rightCount, ReadFeedCrossFeedRangeState? secondResumeState) = await DrainOnePageAsync(secondEnumerable);
            totalCount += rightCount;

            // Finish draining again as a single enumerator
            ReadFeedCrossFeedRangeState mergedState = firstResumeState.Value.Merge(secondResumeState.Value);

            IAsyncEnumerable<TryCatch<ReadFeedPage>> mergedEnumerable = this.Container.GetReadFeedAsyncEnumerable(mergedState);
            (int mergedCount, ReadFeedCrossFeedRangeState? _) = await DrainAllAsync(mergedEnumerable);
            totalCount += mergedCount;

            Assert.AreEqual(batchSize, totalCount);
        }

        [TestMethod]
        [ExpectedException(typeof(OperationCanceledException))]
        public async Task TestCancellationToken()
        {
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            IAsyncEnumerable<TryCatch<ReadFeedPage>> asyncEnumerable = this.Container.GetReadFeedAsyncEnumerable(
                ReadFeedCrossFeedRangeState.CreateFromBeginning());
            await foreach (TryCatch<ReadFeedPage> monadicPage in asyncEnumerable.WithCancellation(cancellationTokenSource.Token))
            {
                monadicPage.ThrowIfFailed();
            }
        }

        private static async Task<(int, ReadFeedCrossFeedRangeState?)> DrainOnePageAsync(IAsyncEnumerable<TryCatch<ReadFeedPage>> asyncEnumerable)
        {
            IAsyncEnumerator<TryCatch<ReadFeedPage>> enumerator = asyncEnumerable.GetAsyncEnumerator();

            await enumerator.MoveNextAsync();
            TryCatch<ReadFeedPage> monadicPage = enumerator.Current;
            monadicPage.ThrowIfFailed();

            ReadFeedPage page = monadicPage.Result;
            ReadFeedCrossFeedRangeState? state = page.State;
            int totalCount = page.Documents.Count;

            return (totalCount, state);
        }

        private static async Task<(int, ReadFeedCrossFeedRangeState?)> DrainAllAsync(IAsyncEnumerable<TryCatch<ReadFeedPage>> asyncEnumerable)
        {
            ReadFeedCrossFeedRangeState? state = default;
            int totalCount = 0;
            await foreach (TryCatch<ReadFeedPage> monadicPage in asyncEnumerable)
            {
                monadicPage.ThrowIfFailed();

                ReadFeedPage page = monadicPage.Result;
                state = page.State;
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
                    ToDoActivity temp = ToDoActivity.CreateRandomToDoActivity(pk: pk);

                    createdList.Add(temp);

                    await container.CreateItemAsync<ToDoActivity>(item: temp);
                }
            }

            return createdList;
        }
    }
}
