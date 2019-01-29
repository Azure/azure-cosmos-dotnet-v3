//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Cosmos.Collections;
    using Microsoft.Azure.Cosmos.Query.ParallelQuery;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Test "DocumentProducer"
    /// </summary>
    [TestClass]
    public class DocumentProducerTest
    {
        /// <summary>
        /// Test possible race conditions in "DocumentProducer"
        /// </summary>
        [TestMethod]
        [Owner("sboshra")]
        [Ignore] // This test doesn't seem to function.
        public async Task ConcurrentMoveNextTryScheduleTestAsync()
        {
            int seed = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            Random rand = new Random(seed);
            int maxValue = 100;
            int trials = 1000;
            int maxTicks = 100;

            IEnumerable<int> expectedValues = Enumerable.Range(1, maxValue);
            IDocumentClientRetryPolicy retryPolicy = new MockRetryPolicy(rand);
            ComparableTaskScheduler scheduler = new ComparableTaskScheduler(1);
            for (int trial = 0; trial < trials; ++trial)
            {
                DocumentProducer<int> producer = new DocumentProducer<int>(
                    scheduler,
                    (continuation, pageSize) => DocumentServiceRequest.Create(
                        OperationType.Query,
                        "/dbs/db/colls/coll",
                        ResourceType.Document,
                        new MemoryStream(Encoding.UTF8.GetBytes(continuation)),
                        AuthorizationTokenType.PrimaryMasterKey,
                        new StringKeyValueCollection
                        {
                            {HttpConstants.HttpHeaders.Continuation, continuation}
                        }),
                    new PartitionKeyRange { Id = "test", MinInclusive = "", MaxExclusive = "ff" },
                    p => 0,
                    (request, token) =>
                    {
                        if (rand.Next(4) == 0)
                        {
                            throw new Exception();
                        }

                        if (rand.Next(10) == 0)
                        {
                            return Task.FromResult(new FeedResponse<int>(new int[] { }, 0, request.Headers));
                        }

                        using (StreamReader reader = new StreamReader(request.Body))
                        {
                            int value = int.Parse(reader.ReadToEnd()) + 1;
                            INameValueCollection headers = new StringKeyValueCollection
                            {
                                {HttpConstants.HttpHeaders.Continuation, value >= maxValue? null : value.ToString(CultureInfo.InvariantCulture)}
                            };
                            return Task.FromResult(new FeedResponse<int>(new int[] { value }, 1, headers));
                        }
                    },
                    () => retryPolicy,
                    (produer, metadata, token) => { },
                    (produer, metadata, token) => { },
                    Guid.NewGuid(),
                    1000,
                    "0");

                Timer timer = new Timer(
                    (state) => producer.TryScheduleFetch(TimeSpan.FromTicks(rand.Next(maxTicks))),
                    null,
                    TimeSpan.FromTicks(rand.Next(maxTicks)),
                    TimeSpan.FromTicks(rand.Next(maxTicks)));

                List<int> actualValues = new List<int>();
                CancellationTokenSource tokenSource = new CancellationTokenSource(5000);
                while (await producer.MoveNextAsync(tokenSource.Token))
                {
                    actualValues.Add(producer.Current);
                }

                Assert.AreEqual(
                    string.Join(", ", expectedValues),
                    string.Join(", ", actualValues),
                    string.Format(CultureInfo.InvariantCulture, "seed: {0}", seed));
            }

        }

        [TestMethod]
        [Owner("olivert")]
        public async Task TestPreCompleteFetchCallbackAvoidsRace()
        {
            int seed = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            Random rand = new Random(seed);
            IDocumentClientRetryPolicy retryPolicy = new MockRetryPolicy(rand);
            ComparableTaskScheduler scheduler = new ComparableTaskScheduler(1);

            int itemCountPerResponse = 5;
            List<FeedResponse<int>> producedFeedResponses = new List<FeedResponse<int>>();
            List<int> actualFeedItems = new List<int>();
            int preFetchCallbackItemCount = 0;

            var responeHeaders = new StringKeyValueCollection()
            {
                {HttpConstants.HttpHeaders.ActivityId, Guid.NewGuid().ToString() },
            };

            DocumentProducer<int> producer = new DocumentProducer<int>(
                scheduler,
                (continuation, pageSize) => null,
                new PartitionKeyRange { Id = "test", MinInclusive = "", MaxExclusive = "ff" },
                p => 0,
                (request, token) =>
                {
                    var response = new FeedResponse<int>(
                        Enumerable.Repeat(42, itemCountPerResponse),
                        itemCountPerResponse,
                        responeHeaders);

                    // Add a continution on the first response so that at least two fetches are scheduled.
                    if (producedFeedResponses.Count == 0)
                    {
                        response.ResponseContinuation = "testct";
                    }

                    producedFeedResponses.Add(response);
                    return Task.FromResult(response);
                },
                () => retryPolicy,
                postCompletFetchCallback: (p, metadata, token) => {
                    p.TryScheduleFetch();
                },
                preCompleteFetchCallback: (p, metadata, token) => {

                    // Sleep before item count update to help simulate the consumer thread enumeration
                    // occurring before than the statistics increment on the callback.
                    // With the fix, it should be guaranteed that this callback happens before new
                    // items are buffered, so the enumeration will be paused until this callback completes.
                    Thread.Sleep(1000);
                    Interlocked.Add(ref preFetchCallbackItemCount, metadata.TotalItemsFetched);
                },
                correlatedActivityId: Guid.NewGuid(),
                bufferCapacity: 1)
            {
                PageSize = 1000
            };

            int consumerTrackedItemCount = 0;
            while (await producer.MoveNextAsync(CancellationToken.None))
            {
                consumerTrackedItemCount += Interlocked.Exchange(ref preFetchCallbackItemCount, 0);
                actualFeedItems.Add(producer.Current);
            }

            // Check that the consumed items and aggregate statistics match the total that was produced.
            int expectedItemCount = producedFeedResponses.Sum(f => f.Count);
            Assert.AreEqual(expectedItemCount, actualFeedItems.Count);
            Assert.AreEqual(expectedItemCount, consumerTrackedItemCount);
        }

        /// <summary>
        /// Test possible InvalidOperationException in "DocumentProducer.MoveNextAsync"
        /// </summary>
        [TestMethod]
        [Owner("sboshra")]
        [ExpectedException(typeof(InvalidOperationException))]
        public async Task TestInvalidOperationExceptionAsync()
        {
            int seed = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            Random rand = new Random(seed);
            IDocumentClientRetryPolicy retryPolicy = new MockRetryPolicy(rand);
            ComparableTaskScheduler scheduler = new ComparableTaskScheduler(1);

            DocumentProducer<int> producer = new DocumentProducer<int>(
                scheduler,
                (continuation, pageSize) => null,
                new PartitionKeyRange { Id = "test", MinInclusive = "", MaxExclusive = "ff" },
                p => 0,
                (request, token) =>
                {
                    return Task.FromResult(new FeedResponse<int>(new int[] { }, 0, new StringKeyValueCollection()));
                },
                () => retryPolicy,
                (produer, metadata, token) => { },
                (produer, metadata, token) => { },
                Guid.NewGuid()
                )
                {
                    PageSize = 1000
                };

            scheduler.Stop();
            await producer.MoveNextAsync(new CancellationTokenSource(100).Token);
        }

        /// <summary>
        /// Test possible InvalidOperationException in "DocumentProducer.FetchAsync"
        /// </summary>
        [TestMethod]
        [Owner("sboshra")]
        [ExpectedException(typeof(OperationCanceledException))]
        public async Task TestOperationCanceledExceptionAsync()
        {
            int seed = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            Random rand = new Random(seed);
            IDocumentClientRetryPolicy retryPolicy = new MockRetryPolicy(rand);
            ComparableTaskScheduler scheduler = new ComparableTaskScheduler(1);

            DocumentProducer<int> producer = new DocumentProducer<int>(
                scheduler,
                (continuation, pageSize) => null,
                new PartitionKeyRange { Id = "test", MinInclusive = "", MaxExclusive = "ff" },
                p => 0,
                (request, token) =>
                {
                    scheduler.Stop();
                    throw new Exception();
                },
                () => retryPolicy,
                (produer, metadata, token) => { },
                (produer, metadata, token) => { },
                Guid.NewGuid()
                )
                {
                    PageSize = 1000
                };

            await producer.MoveNextAsync(new CancellationTokenSource().Token);
        }

        private sealed class MockRetryPolicy : IDocumentClientRetryPolicy
        {
            private readonly Random rand;
            public MockRetryPolicy(Random rand)
            {
                this.rand = rand;
            }

            public void OnBeforeSendRequest(DocumentServiceRequest request)
            {
            }

            public Task<ShouldRetryResult> ShouldRetryAsync(Exception exception, CancellationToken cancellationToken)
            {
                return Task.FromResult(ShouldRetryResult.RetryAfter(TimeSpan.FromTicks(this.rand.Next(25))));
            }

            public Task<ShouldRetryResult> ShouldRetryAsync(CosmosResponseMessage cosmosResponseMessage, CancellationToken cancellationToken)
            {
                return Task.FromResult(ShouldRetryResult.RetryAfter(TimeSpan.FromTicks(this.rand.Next(25))));
            }
        }
    }
}
