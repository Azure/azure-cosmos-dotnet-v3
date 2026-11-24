//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.SecondaryIndexRouting;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using HttpConstants = Microsoft.Azure.Documents.HttpConstants;
    using IClientSideRequestStatistics = Microsoft.Azure.Documents.IClientSideRequestStatistics;
    using PartitionKeyDefinition = Microsoft.Azure.Documents.PartitionKeyDefinition;
    using ResourceId = Microsoft.Azure.Documents.ResourceId;
    using ServerStoreModel = Microsoft.Azure.Documents.ServerStoreModel;

    [TestClass]
    public class SecondaryIndexMetadataCacheTests
    {
        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task CacheReusesSnapshotBeforeExpiration(bool hasIndexes)
        {
            ContainerProperties source = CreateSource();
            TestProvider provider = new TestProvider((container, _, _) =>
            {
                Assert.AreSame(source, container);
                return Task.FromResult<IReadOnlyList<ISecondaryIndexMetadata>>(
                    hasIndexes
                        ? new[] { CreateMetadata("gsi") }
                        : Array.Empty<ISecondaryIndexMetadata>());
            });
            TestClock clock = new TestClock();
            SecondaryIndexMetadataCache cache = new SecondaryIndexMetadataCache(provider, utcNowProvider: clock.GetUtcNow);

            IEnumerable<ISecondaryIndexMetadata> first = await cache.GetSecondaryIndexMetadataAsync(
                source,
                NoOpTrace.Singleton,
                CancellationToken.None);
            clock.Advance(SecondaryIndexMetadataCache.SnapshotTimeToLive - TimeSpan.FromTicks(1));
            IEnumerable<ISecondaryIndexMetadata> second = await cache.GetSecondaryIndexMetadataAsync(
                CreateSource(),
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual(1, provider.CallCount);
            Assert.AreSame(first, second);
            Assert.AreEqual(hasIndexes ? 1 : 0, second.Count());
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task SnapshotRefreshesAtExpiration(bool hasIndexes)
        {
            TestProvider provider = new TestProvider((_, _, _) =>
                Task.FromResult<IReadOnlyList<ISecondaryIndexMetadata>>(
                    hasIndexes
                        ? new[] { CreateMetadata($"gsi{providerCallSequence++}") }
                        : Array.Empty<ISecondaryIndexMetadata>()));
            TestClock clock = new TestClock();
            SecondaryIndexMetadataCache cache = new SecondaryIndexMetadataCache(provider, utcNowProvider: clock.GetUtcNow);

            IEnumerable<ISecondaryIndexMetadata> first = await cache.GetSecondaryIndexMetadataAsync(
                CreateSource(),
                NoOpTrace.Singleton,
                CancellationToken.None);
            clock.Advance(SecondaryIndexMetadataCache.SnapshotTimeToLive);
            IEnumerable<ISecondaryIndexMetadata> refreshed = await cache.GetSecondaryIndexMetadataAsync(
                CreateSource(),
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual(2, provider.CallCount);
            if (hasIndexes)
            {
                Assert.AreNotSame(first, refreshed);
            }
        }

        [TestMethod]
        public async Task ConcurrentExpiredReadsMayRefreshIndependently()
        {
            TaskCompletionSource<bool> refreshesStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> releaseRefresh = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            int refreshCount = 0;
            TestProvider provider = new TestProvider(async (_, _, _, forceRefresh) =>
            {
                if (forceRefresh)
                {
                    if (Interlocked.Increment(ref refreshCount) == 2)
                    {
                        refreshesStarted.SetResult(true);
                    }

                    await releaseRefresh.Task;
                }

                return new[] { CreateMetadata("gsi") };
            });
            TestClock clock = new TestClock();
            SecondaryIndexMetadataCache cache = new SecondaryIndexMetadataCache(provider, utcNowProvider: clock.GetUtcNow);

            await cache.GetSecondaryIndexMetadataAsync(CreateSource(), NoOpTrace.Singleton, CancellationToken.None);
            clock.Advance(SecondaryIndexMetadataCache.SnapshotTimeToLive);

            Task<IEnumerable<ISecondaryIndexMetadata>> firstRefresh = cache.GetSecondaryIndexMetadataAsync(
                CreateSource(),
                NoOpTrace.Singleton,
                CancellationToken.None);
            Task<IEnumerable<ISecondaryIndexMetadata>> secondRefresh = cache.GetSecondaryIndexMetadataAsync(
                CreateSource(),
                NoOpTrace.Singleton,
                CancellationToken.None);

            await refreshesStarted.Task;
            Assert.AreEqual(3, provider.CallCount);
            releaseRefresh.SetResult(true);
            await Task.WhenAll(firstRefresh, secondRefresh);

            IEnumerable<ISecondaryIndexMetadata> cached = await cache.GetSecondaryIndexMetadataAsync(
                CreateSource(),
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual(3, provider.CallCount);
            Assert.IsTrue(ReferenceEquals(cached, firstRefresh.Result) || ReferenceEquals(cached, secondRefresh.Result));
        }

        [TestMethod]
        public async Task SourceMembershipChangeRefreshesBeforeExpiration()
        {
            ContainerProperties initialSource = CreateSource();
            ContainerProperties updatedSource = CreateSource();
            updatedSource.MaterializedViews = new[]
            {
                new MaterializedViewProperties { ResourceId = "gsiRid" },
            };
            TestProvider provider = new TestProvider((_, _, _) =>
                Task.FromResult<IReadOnlyList<ISecondaryIndexMetadata>>(new[] { CreateMetadata($"gsi{providerCallSequence++}") }));
            TestClock clock = new TestClock();
            SecondaryIndexMetadataCache cache = new SecondaryIndexMetadataCache(provider, utcNowProvider: clock.GetUtcNow);

            IEnumerable<ISecondaryIndexMetadata> first = await cache.GetSecondaryIndexMetadataAsync(
                initialSource,
                NoOpTrace.Singleton,
                CancellationToken.None);
            IEnumerable<ISecondaryIndexMetadata> refreshed = await cache.GetSecondaryIndexMetadataAsync(
                updatedSource,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual(2, provider.CallCount);
            Assert.AreEqual("gsi0", first.Single().Rid);
            Assert.AreEqual("gsi1", refreshed.Single().Rid);
        }

        [TestMethod]
        public async Task ConcurrentMembershipChangeStartsIndependentDiscovery()
        {
            ContainerProperties initialSource = CreateSource();
            ContainerProperties updatedSource = CreateSource();
            updatedSource.MaterializedViews = new[]
            {
                new MaterializedViewProperties { ResourceId = "gsiRid" },
            };
            TaskCompletionSource<bool> initialDiscoveryStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> releaseInitialDiscovery = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TestProvider provider = new TestProvider(async (source, _, _) =>
            {
                if (ReferenceEquals(source, initialSource))
                {
                    initialDiscoveryStarted.SetResult(true);
                    await releaseInitialDiscovery.Task;
                    return new[] { CreateMetadata("initialGsi") };
                }

                return new[] { CreateMetadata("updatedGsi") };
            });
            SecondaryIndexMetadataCache cache = new SecondaryIndexMetadataCache(provider);

            Task<IEnumerable<ISecondaryIndexMetadata>> initialDiscovery = cache.GetSecondaryIndexMetadataAsync(
                initialSource,
                NoOpTrace.Singleton,
                CancellationToken.None);
            await initialDiscoveryStarted.Task;
            IEnumerable<ISecondaryIndexMetadata> updated = await cache.GetSecondaryIndexMetadataAsync(
                updatedSource,
                NoOpTrace.Singleton,
                CancellationToken.None);
            releaseInitialDiscovery.SetResult(true);
            await initialDiscovery;
            IEnumerable<ISecondaryIndexMetadata> cached = await cache.GetSecondaryIndexMetadataAsync(
                updatedSource,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual("updatedGsi", updated.Single().Rid);
            Assert.AreSame(updated, cached);
            Assert.AreEqual(2, provider.CallCount);
        }

        [TestMethod]
        public async Task ExpiredRefreshFailureEvictsStaleSnapshotAndIsNotCached()
        {
            List<bool> forceRefreshes = new List<bool>();
            CosmosException refreshException = new CosmosException(
                "refresh failed",
                HttpStatusCode.ServiceUnavailable,
                0,
                "activity",
                requestCharge: 0);
            TestProvider provider = new TestProvider((_, _, _, forceRefresh) =>
            {
                forceRefreshes.Add(forceRefresh);
                if (forceRefresh && providerCallSequence++ == 0)
                {
                    throw refreshException;
                }

                return Task.FromResult<IReadOnlyList<ISecondaryIndexMetadata>>(new[] { CreateMetadata($"gsi{providerCallSequence}") });
            });
            TestClock clock = new TestClock();
            SecondaryIndexMetadataCache cache = new SecondaryIndexMetadataCache(provider, utcNowProvider: clock.GetUtcNow);

            IEnumerable<ISecondaryIndexMetadata> initial = await cache.GetSecondaryIndexMetadataAsync(
                CreateSource(),
                NoOpTrace.Singleton,
                CancellationToken.None);
            clock.Advance(SecondaryIndexMetadataCache.SnapshotTimeToLive);

            CosmosException exception =
                await Assert.ThrowsExceptionAsync<CosmosException>(() =>
                    cache.GetSecondaryIndexMetadataAsync(CreateSource(), NoOpTrace.Singleton, CancellationToken.None));
            IEnumerable<ISecondaryIndexMetadata> recovered = await cache.GetSecondaryIndexMetadataAsync(
                CreateSource(),
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual(refreshException.StatusCode, exception.StatusCode);
            Assert.AreEqual(refreshException.ActivityId, exception.ActivityId);
            Assert.AreEqual(3, provider.CallCount);
            Assert.AreNotSame(initial, recovered);
            CollectionAssert.AreEqual(new[] { false, true, false }, forceRefreshes);
        }

        [TestMethod]
        public async Task ConcurrentReadDuringFailedRefreshDoesNotReuseStaleSnapshot()
        {
            TaskCompletionSource<bool> refreshesStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> releaseRefresh = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            InvalidOperationException refreshException = new InvalidOperationException("refresh failed");
            int refreshCount = 0;
            TestProvider provider = new TestProvider(async (_, _, _, forceRefresh) =>
            {
                if (forceRefresh)
                {
                    if (Interlocked.Increment(ref refreshCount) == 2)
                    {
                        refreshesStarted.SetResult(true);
                    }

                    await releaseRefresh.Task;
                    throw refreshException;
                }

                return new[] { CreateMetadata("gsi") };
            });
            TestClock clock = new TestClock();
            SecondaryIndexMetadataCache cache = new SecondaryIndexMetadataCache(provider, utcNowProvider: clock.GetUtcNow);

            await cache.GetSecondaryIndexMetadataAsync(CreateSource(), NoOpTrace.Singleton, CancellationToken.None);
            clock.Advance(SecondaryIndexMetadataCache.SnapshotTimeToLive);

            Task<IEnumerable<ISecondaryIndexMetadata>> firstRefresh = cache.GetSecondaryIndexMetadataAsync(
                CreateSource(),
                NoOpTrace.Singleton,
                CancellationToken.None);
            Task<IEnumerable<ISecondaryIndexMetadata>> concurrentRead = cache.GetSecondaryIndexMetadataAsync(
                CreateSource(),
                NoOpTrace.Singleton,
                CancellationToken.None);
            await refreshesStarted.Task;
            releaseRefresh.SetResult(true);

            InvalidOperationException firstException =
                await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () => await firstRefresh);
            InvalidOperationException concurrentException =
                await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () => await concurrentRead);
            IEnumerable<ISecondaryIndexMetadata> recovered = await cache.GetSecondaryIndexMetadataAsync(
                CreateSource(),
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreSame(refreshException, firstException);
            Assert.AreSame(refreshException, concurrentException);
            Assert.AreEqual("gsi", recovered.Single().Rid);
            Assert.AreEqual(4, provider.CallCount);
        }

        [TestMethod]
        public async Task FailedRefreshDoesNotRemoveSnapshotPopulatedAfterInvalidation()
        {
            TaskCompletionSource<bool> refreshStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> releaseRefresh = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TestProvider provider = new TestProvider(async (_, _, _, forceRefresh) =>
            {
                if (forceRefresh)
                {
                    refreshStarted.SetResult(true);
                    await releaseRefresh.Task;
                    throw new InvalidOperationException("refresh failed");
                }

                return new[] { CreateMetadata($"gsi{providerCallSequence++}") };
            });
            TestClock clock = new TestClock();
            SecondaryIndexMetadataCache cache = new SecondaryIndexMetadataCache(provider, utcNowProvider: clock.GetUtcNow);
            ContainerProperties source = CreateSource();

            await cache.GetSecondaryIndexMetadataAsync(source, NoOpTrace.Singleton, CancellationToken.None);
            clock.Advance(SecondaryIndexMetadataCache.SnapshotTimeToLive);
            Task<IEnumerable<ISecondaryIndexMetadata>> failedRefresh = cache.GetSecondaryIndexMetadataAsync(
                source,
                NoOpTrace.Singleton,
                CancellationToken.None);
            await refreshStarted.Task;

            cache.Invalidate(source.ResourceId);
            IEnumerable<ISecondaryIndexMetadata> replacement = await cache.GetSecondaryIndexMetadataAsync(
                source,
                NoOpTrace.Singleton,
                CancellationToken.None);
            releaseRefresh.SetResult(true);
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () => await failedRefresh);
            IEnumerable<ISecondaryIndexMetadata> cached = await cache.GetSecondaryIndexMetadataAsync(
                source,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreSame(replacement, cached);
            Assert.AreEqual("gsi1", cached.Single().Rid);
            Assert.AreEqual(3, provider.CallCount);
        }

        [TestMethod]
        public async Task ForceRefreshAndInvalidationReplaceWholeSnapshot()
        {
            ContainerProperties source = CreateSource();
            ContainerProperties updatedSource = CreateSource();
            List<ContainerProperties> discoveredSources = new List<ContainerProperties>();
            TestProvider provider = new TestProvider((container, _, _) =>
            {
                discoveredSources.Add(container);
                return Task.FromResult<IReadOnlyList<ISecondaryIndexMetadata>>(new[] { CreateMetadata($"gsi{providerCallSequence++}") });
            });
            SecondaryIndexMetadataCache cache = new SecondaryIndexMetadataCache(provider);

            IEnumerable<ISecondaryIndexMetadata> first = await cache.GetSecondaryIndexMetadataAsync(source, NoOpTrace.Singleton, CancellationToken.None);
            IEnumerable<ISecondaryIndexMetadata> cached = await cache.GetSecondaryIndexMetadataAsync(updatedSource, NoOpTrace.Singleton, CancellationToken.None);
            IEnumerable<ISecondaryIndexMetadata> refreshed = await cache.GetSecondaryIndexMetadataAsync(updatedSource, NoOpTrace.Singleton, CancellationToken.None, forceRefresh: true);

            cache.Invalidate(source.ResourceId);
            IEnumerable<ISecondaryIndexMetadata> repopulated = await cache.GetSecondaryIndexMetadataAsync(updatedSource, NoOpTrace.Singleton, CancellationToken.None);

            Assert.AreSame(first, cached);
            Assert.AreNotSame(first, refreshed);
            Assert.AreNotSame(refreshed, repopulated);
            Assert.AreEqual(3, provider.CallCount);
            Assert.AreEqual("gsi0", first.ElementAt(0).Rid);
            Assert.AreEqual("gsi1", refreshed.ElementAt(0).Rid);
            Assert.AreEqual("gsi2", repopulated.ElementAt(0).Rid);
            Assert.AreSame(source, discoveredSources[0]);
            Assert.AreSame(updatedSource, discoveredSources[1]);
            Assert.AreSame(updatedSource, discoveredSources[2]);
        }

        [TestMethod]
        public void InvalidationRejectsNullResourceId()
        {
            TestProvider provider = new TestProvider((_, _, _) =>
                Task.FromResult<IReadOnlyList<ISecondaryIndexMetadata>>(Array.Empty<ISecondaryIndexMetadata>()));
            SecondaryIndexMetadataCache cache = new SecondaryIndexMetadataCache(provider);

            ArgumentNullException exception = Assert.ThrowsException<ArgumentNullException>(() => cache.Invalidate(null));

            Assert.AreEqual("sourceCollectionRid", exception.ParamName);
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow(" ")]
        [DataRow("\t\r\n")]
        public void InvalidationAcceptsEmptyAndWhitespaceResourceIds(string sourceCollectionRid)
        {
            TestProvider provider = new TestProvider((_, _, _) =>
                Task.FromResult<IReadOnlyList<ISecondaryIndexMetadata>>(Array.Empty<ISecondaryIndexMetadata>()));
            SecondaryIndexMetadataCache cache = new SecondaryIndexMetadataCache(provider);

            cache.Invalidate(sourceCollectionRid);

            Assert.AreEqual(0, provider.CallCount);
        }

        [TestMethod]
        public async Task CacheSeparatesSourcesWithDifferentResourceIds()
        {
            TestProvider provider = new TestProvider((_, _, _) =>
                Task.FromResult<IReadOnlyList<ISecondaryIndexMetadata>>(new[] { CreateMetadata($"gsi{providerCallSequence++}") }));
            SecondaryIndexMetadataCache cache = new SecondaryIndexMetadataCache(provider);

            IEnumerable<ISecondaryIndexMetadata> first = await cache.GetSecondaryIndexMetadataAsync(CreateSource(), NoOpTrace.Singleton, CancellationToken.None);
            IEnumerable<ISecondaryIndexMetadata> second = await cache.GetSecondaryIndexMetadataAsync(CreateSource("otherSourceRid"), NoOpTrace.Singleton, CancellationToken.None);

            Assert.AreEqual(2, provider.CallCount);
            Assert.AreNotSame(first, second);
            Assert.AreEqual("gsi0", first.Single().Rid);
            Assert.AreEqual("gsi1", second.Single().Rid);
        }

        [TestMethod]
        public async Task CacheRejectsNullSource()
        {
            TestProvider provider = new TestProvider((_, _, _) =>
                Task.FromResult<IReadOnlyList<ISecondaryIndexMetadata>>(Array.Empty<ISecondaryIndexMetadata>()));
            SecondaryIndexMetadataCache cache = new SecondaryIndexMetadataCache(provider);

            ArgumentNullException exception = await Assert.ThrowsExceptionAsync<ArgumentNullException>(() =>
                cache.GetSecondaryIndexMetadataAsync(null, NoOpTrace.Singleton, CancellationToken.None));

            Assert.AreEqual("source", exception.ParamName);
            Assert.AreEqual(0, provider.CallCount);
        }

        [TestMethod]
        public async Task CacheRejectsSourceWithoutResourceId()
        {
            TestProvider provider = new TestProvider((_, _, _) =>
                Task.FromResult<IReadOnlyList<ISecondaryIndexMetadata>>(Array.Empty<ISecondaryIndexMetadata>()));
            SecondaryIndexMetadataCache cache = new SecondaryIndexMetadataCache(provider);

            ArgumentException exception = await Assert.ThrowsExceptionAsync<ArgumentException>(() =>
                cache.GetSecondaryIndexMetadataAsync(new ContainerProperties("source", "/pk"), NoOpTrace.Singleton, CancellationToken.None));

            Assert.AreEqual("source", exception.ParamName);
            Assert.AreEqual(0, provider.CallCount);
        }

        [TestMethod]
        public async Task ProviderFailureIsNotCached()
        {
            TestProvider provider = new TestProvider((_, _, _) =>
            {
                if (providerCallSequence++ == 0)
                {
                    throw new InvalidOperationException("discovery failed");
                }

                return Task.FromResult<IReadOnlyList<ISecondaryIndexMetadata>>(Array.Empty<ISecondaryIndexMetadata>());
            });
            SecondaryIndexMetadataCache cache = new SecondaryIndexMetadataCache(provider);

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
                cache.GetSecondaryIndexMetadataAsync(CreateSource(), NoOpTrace.Singleton, CancellationToken.None));
            IEnumerable<ISecondaryIndexMetadata> result = await cache.GetSecondaryIndexMetadataAsync(CreateSource(), NoOpTrace.Singleton, CancellationToken.None);

            Assert.AreEqual(2, provider.CallCount);
            Assert.AreEqual(0, result.Count());
        }

        [TestMethod]
        public async Task ForcedCollectionRefreshInvalidatesSecondaryIndexMetadata()
        {
            string sourceRid = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
            ContainerProperties source = CreateSource(sourceRid);
            TestProvider provider = new TestProvider((_, _, _) =>
                Task.FromResult<IReadOnlyList<ISecondaryIndexMetadata>>(new[] { CreateMetadata($"gsi{providerCallSequence++}") }));
            SecondaryIndexMetadataCache cache = new SecondaryIndexMetadataCache(provider);
            SecondaryIndexMetadataInvalidator invalidator = new SecondaryIndexMetadataInvalidator();
            TestClientCollectionCache collectionCache = new TestClientCollectionCache(source, invalidator);
            invalidator.Register(cache);

            IEnumerable<ISecondaryIndexMetadata> initial = await cache.GetSecondaryIndexMetadataAsync(
                source,
                NoOpTrace.Singleton,
                CancellationToken.None);
            await collectionCache.ResolveByNameAsync(
                HttpConstants.Versions.CurrentVersion,
                "dbs/database/colls/source",
                forceRefesh: false,
                NoOpTrace.Singleton,
                clientSideRequestStatistics: null,
                CancellationToken.None);
            await collectionCache.ResolveByNameAsync(
                HttpConstants.Versions.CurrentVersion,
                "dbs/database/colls/source",
                forceRefesh: true,
                NoOpTrace.Singleton,
                clientSideRequestStatistics: null,
                CancellationToken.None);
            IEnumerable<ISecondaryIndexMetadata> refreshed = await cache.GetSecondaryIndexMetadataAsync(
                source,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual(2, provider.CallCount);
            Assert.AreEqual("gsi0", initial.Single().Rid);
            Assert.AreEqual("gsi1", refreshed.Single().Rid);
            Assert.AreEqual(2, collectionCache.ResolveByNameCount);
        }

        [TestMethod]
        public async Task NullProviderResultIsReturnedAndNotReused()
        {
            TestProvider provider = new TestProvider((_, _, _) => Task.FromResult<IReadOnlyList<ISecondaryIndexMetadata>>(null));
            SecondaryIndexMetadataCache cache = new SecondaryIndexMetadataCache(provider);

            IEnumerable<ISecondaryIndexMetadata> first = await cache.GetSecondaryIndexMetadataAsync(CreateSource(), NoOpTrace.Singleton, CancellationToken.None);
            IEnumerable<ISecondaryIndexMetadata> second = await cache.GetSecondaryIndexMetadataAsync(CreateSource(), NoOpTrace.Singleton, CancellationToken.None);

            Assert.IsNull(first);
            Assert.IsNull(second);
            Assert.AreEqual(2, provider.CallCount);
        }

        [TestMethod]
        public async Task CallerCancellationIsForwarded()
        {
            TestProvider provider = new TestProvider((_, _, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult<IReadOnlyList<ISecondaryIndexMetadata>>(Array.Empty<ISecondaryIndexMetadata>());
            });
            SecondaryIndexMetadataCache cache = new SecondaryIndexMetadataCache(provider);
            using CancellationTokenSource cancellationSource = new CancellationTokenSource();
            cancellationSource.Cancel();

            await Assert.ThrowsExceptionAsync<OperationCanceledException>(() =>
                cache.GetSecondaryIndexMetadataAsync(
                    CreateSource(),
                    NoOpTrace.Singleton,
                    cancellationToken: cancellationSource.Token));
            Assert.AreEqual(0, provider.CallCount);
        }

        [TestMethod]
        public async Task ConcurrentCallersUseTheirOwnCancellationTokens()
        {
            TaskCompletionSource<bool> cancelableCallStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TestProvider provider = new TestProvider(async (_, _, cancellationToken) =>
            {
                if (cancellationToken.CanBeCanceled)
                {
                    cancelableCallStarted.SetResult(true);
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }

                return new[] { CreateMetadata("gsi") };
            });
            SecondaryIndexMetadataCache cache = new SecondaryIndexMetadataCache(provider);
            using CancellationTokenSource cancellationSource = new CancellationTokenSource();

            Task<IEnumerable<ISecondaryIndexMetadata>> cancelableCall = cache.GetSecondaryIndexMetadataAsync(
                CreateSource(),
                NoOpTrace.Singleton,
                cancellationSource.Token);
            await cancelableCallStarted.Task;
            IEnumerable<ISecondaryIndexMetadata> successfulCall = await cache.GetSecondaryIndexMetadataAsync(
                CreateSource(),
                NoOpTrace.Singleton,
                CancellationToken.None);
            cancellationSource.Cancel();

            await Assert.ThrowsExceptionAsync<TaskCanceledException>(async () => await cancelableCall);
            IEnumerable<ISecondaryIndexMetadata> cached = await cache.GetSecondaryIndexMetadataAsync(
                CreateSource(),
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreSame(successfulCall, cached);
            Assert.AreEqual(2, provider.CallCount);
        }

        [TestMethod]
        public void InvalidatorRejectsNullResourceId()
        {
            SecondaryIndexMetadataInvalidator invalidator = new SecondaryIndexMetadataInvalidator();

            ArgumentNullException exception = Assert.ThrowsException<ArgumentNullException>(() => invalidator.Invalidate(null));

            Assert.AreEqual("sourceCollectionRid", exception.ParamName);
        }

        private static int providerCallSequence;

        [TestInitialize]
        public void ResetSequence()
        {
            providerCallSequence = 0;
        }

        private static ContainerProperties CreateSource(string rid = "sourceRid")
        {
            ContainerProperties source = ContainerProperties.CreateWithResourceId(rid);
            source.Id = "source";
            return source;
        }

        private static ISecondaryIndexMetadata CreateMetadata(string rid)
        {
            return new SecondaryIndexMetadata(
                rid,
                rid,
                "sourceRid",
                new PartitionKeyDefinition(),
                new IndexingPolicy(),
                new Dictionary<string, PropertyPath>(),
                ConsistencyLevel.Eventual);
        }

        private sealed class TestProvider : ISecondaryIndexMetadataProvider
        {
            private readonly Func<ContainerProperties, ITrace, CancellationToken, bool, Task<IReadOnlyList<ISecondaryIndexMetadata>>> callback;
            private int callCount;

            public TestProvider(Func<ContainerProperties, ITrace, CancellationToken, Task<IReadOnlyList<ISecondaryIndexMetadata>>> callback)
                : this((source, trace, cancellationToken, _) => callback(source, trace, cancellationToken))
            {
            }

            public TestProvider(Func<ContainerProperties, ITrace, CancellationToken, bool, Task<IReadOnlyList<ISecondaryIndexMetadata>>> callback)
            {
                this.callback = callback;
            }

            public int CallCount => Volatile.Read(ref this.callCount);

            public async Task<IEnumerable<ISecondaryIndexMetadata>> GetSecondaryIndexMetadataAsync(
                ContainerProperties source,
                ITrace trace,
                CancellationToken cancellationToken,
                bool forceRefresh = false)
            {
                Interlocked.Increment(ref this.callCount);
                return await this.callback(source, trace, cancellationToken, forceRefresh);
            }
        }

        private sealed class TestClock
        {
            public TestClock()
            {
                this.UtcNow = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            }

            private DateTime UtcNow { get; set; }

            public DateTime GetUtcNow()
            {
                return this.UtcNow;
            }

            public void Advance(TimeSpan duration)
            {
                this.UtcNow += duration;
            }
        }

        private sealed class TestClientCollectionCache : ClientCollectionCache
        {
            private readonly ContainerProperties source;

            public TestClientCollectionCache(
                ContainerProperties source,
                SecondaryIndexMetadataInvalidator secondaryIndexMetadataInvalidator)
                : base(
                    new SessionContainer("testhost"),
                    new ServerStoreModel(null),
                    tokenProvider: null,
                    retryPolicy: null,
                    telemetryToServiceHelper: null,
                    enableAsyncCacheExceptionNoSharing: true,
                    metadataHedgingStrategy: null,
                    secondaryIndexMetadataInvalidator: secondaryIndexMetadataInvalidator)
            {
                this.source = source;
            }

            public int ResolveByNameCount { get; private set; }

            protected override Task<ContainerProperties> GetByNameAsync(
                string apiVersion,
                string resourceAddress,
                ITrace trace,
                IClientSideRequestStatistics clientSideRequestStatistics,
                CancellationToken cancellationToken)
            {
                this.ResolveByNameCount++;
                return Task.FromResult(this.source);
            }
        }
    }
}
