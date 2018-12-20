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
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Collections;
    using Microsoft.Azure.Cosmos.Internal;
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
        [ExpectedException(typeof(OperationCanceledException))]
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
                    (produer, size, ru, queryMetrics, token, length) => { },
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

        /// <summary>
        /// Test possible InvalidOperationException in "DocumentProducer.MoveNextAsync"
        /// </summary>
        [TestMethod]
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
                (produer, size, ru, queryMetrics, token, length) => { },
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
                (produer, size, ru, queryMetrics, token, length) => { },
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

            public Task<ShouldRetryResult> ShouldRetryAsync(CosmosResponseMessage httpResponseMessage, CancellationToken cancellationToken)
            {
                return Task.FromResult(ShouldRetryResult.RetryAfter(TimeSpan.FromTicks(this.rand.Next(25))));
            }
        }
    }
}
