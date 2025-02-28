//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Scenarios;
    using Microsoft.Azure.Cosmos.Serializer;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class CosmosAsyncEnumerableTests
    {
        [TestMethod]
        public async Task FeedIteratorAsAsyncEnumerableTest()
        {
            // Create list of expected results
            List<Item> expected = new List<Item>
            {
                new Item("1", "A"),
                new Item("2", "A"),
                new Item("3", "A"),
                new Item("4", "B"),
                new Item("5", "B"),
                new Item("6", "C")
            };

            // Generate dictionary by grouping by partition key
            Dictionary<string, List<Item>> itemsByPartition = expected.GroupBy(item => item.PartitionKey).ToDictionary(group => group.Key, group => group.ToList());
            IEnumerator<string> enumerator = itemsByPartition.Keys.GetEnumerator();

            // Create mock FeedIterator<Item> that returns the expected results
            Mock<FeedIterator<Item>> mockFeedIterator = new Mock<FeedIterator<Item>>();
            mockFeedIterator.Setup(i => i.HasMoreResults).Returns(() => enumerator.MoveNext());
            mockFeedIterator.Setup(i => i.ReadNextAsync(It.IsAny<CancellationToken>())).Returns((CancellationToken _) =>
            {
                Mock<FeedResponse<Item>> mockFeedResponse = new Mock<FeedResponse<Item>>();
                mockFeedResponse.Setup(i => i.GetEnumerator()).Returns(() => itemsByPartition[enumerator.Current].GetEnumerator());
                return Task.FromResult(mockFeedResponse.Object);
            });
            FeedIterator<Item> feedIterator = mockFeedIterator.Object;

            // Method under test: Convert FeedIterator<Item> to IAsyncEnumerable<FeedResponse<Item>>
            IAsyncEnumerable<FeedResponse<Item>> asyncEnumerable = feedIterator.AsAsyncEnumerable();

            // Parse results using asynchronous foreach iteration
            List<Item> actual = new List<Item> {};
            await foreach (FeedResponse<Item> page in asyncEnumerable)
            {
                foreach(Item item in page)
                {
                    actual.Add(item);
                }
            }

            // Verify results
            CollectionAssert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void IQueryableAsAsyncEnumerableTest()
        {
            // Create mock Container that returns the expected results
            Mock<IQueryable<Item>> mockQueryable = new Mock<IQueryable<Item>>();
            IQueryable<Item> queryable = mockQueryable.Object;

            // Method under test: Convert IQueryable<Item> to IAsyncEnumerable<FeedResponse<Item>> with expected exception
            Assert.ThrowsException<NotSupportedException>(() => queryable.AsAsyncEnumerable());
        }

        public record Item(
            string Id,
            string PartitionKey
        );
    }
}