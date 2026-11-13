//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
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
        public async Task CacheCoalescesConcurrentPopulationAndReusesSnapshotBeforeExpiration(bool hasIndexes)
        {
            ContainerProperties source = CreateSource();
            TaskCompletionSource<bool> releaseProvider = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TestProvider provider = new TestProvider(async (container, _, _) =>
            {
                Assert.AreSame(source, container);
                await releaseProvider.Task;
                return hasIndexes
                    ? new[] { CreateMetadata("gsi") }
                    : Array.Empty<ISecondaryIndexMetadata>();
            });
            TestClock clock = new TestClock();
            SecondaryIndexMetadataCache cache = new SecondaryIndexMetadataCache(provider, utcNowProvider: clock.GetUtcNow);

            Task<IEnumerable<ISecondaryIndexMetadata>> first = cache.GetSecondaryIndexMetadataAsync(source, NoOpTrace.Singleton, CancellationToken.None);
            Task<IEnumerable<ISecondaryIndexMetadata>> second = cache.GetSecondaryIndexMetadataAsync(CreateSource(), NoOpTrace.Singleton, CancellationToken.None);

            releaseProvider.SetResult(true);
            await Task.WhenAll(first, second);
            clock.Advance(SecondaryIndexMetadataCache.SnapshotTimeToLive - TimeSpan.FromTicks(1));
            IEnumerable<ISecondaryIndexMetadata> third = await cache.GetSecondaryIndexMetadataAsync(CreateSource(), NoOpTrace.Singleton, CancellationToken.None);

            Assert.AreEqual(1, provider.CallCount);
            Assert.AreSame(first.Result, second.Result);
            Assert.AreSame(first.Result, third);
            Assert.AreEqual(hasIndexes ? 1 : 0, third.Count());
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
        public async Task ConcurrentExpiredReadsCoalesceIntoSingleRefresh()
        {
            TaskCompletionSource<bool> refreshStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> releaseRefresh = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TestProvider provider = new TestProvider(async (_, _, _, forceRefresh) =>
            {
                if (forceRefresh)
                {
                    refreshStarted.SetResult(true);
                    await releaseRefresh.Task;
                }

                return new[] { CreateMetadata($"gsi{providerCallSequence++}") };
            });
            TestClock clock = new TestClock();
            SecondaryIndexMetadataCache cache = new SecondaryIndexMetadataCache(provider, utcNowProvider: clock.GetUtcNow);

            await cache.GetSecondaryIndexMetadataAsync(CreateSource(), NoOpTrace.Singleton, CancellationToken.None);
            clock.Advance(SecondaryIndexMetadataCache.SnapshotTimeToLive);

            Task<IEnumerable<ISecondaryIndexMetadata>> firstRefresh = cache.GetSecondaryIndexMetadataAsync(
                CreateSource(),
                NoOpTrace.Singleton,
                CancellationToken.None);
            await refreshStarted.Task;
            Task<IEnumerable<ISecondaryIndexMetadata>> secondRefresh = cache.GetSecondaryIndexMetadataAsync(
                CreateSource(),
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual(2, provider.CallCount);
            releaseRefresh.SetResult(true);
            await Task.WhenAll(firstRefresh, secondRefresh);

            Assert.AreSame(firstRefresh.Result, secondRefresh.Result);
            Assert.AreEqual(2, provider.CallCount);
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
        public async Task ExpiredRefreshFailureEvictsStaleSnapshotAndIsNotCached()
        {
            List<bool> forceRefreshes = new List<bool>();
            TestProvider provider = new TestProvider((_, _, _, forceRefresh) =>
            {
                forceRefreshes.Add(forceRefresh);
                if (forceRefresh && providerCallSequence++ == 0)
                {
                    throw new InvalidOperationException("refresh failed");
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

            SecondaryIndexMetadataRefreshException exception =
                await Assert.ThrowsExceptionAsync<SecondaryIndexMetadataRefreshException>(() =>
                    cache.GetSecondaryIndexMetadataAsync(CreateSource(), NoOpTrace.Singleton, CancellationToken.None));
            IEnumerable<ISecondaryIndexMetadata> recovered = await cache.GetSecondaryIndexMetadataAsync(
                CreateSource(),
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.IsInstanceOfType(exception.InnerException, typeof(InvalidOperationException));
            Assert.AreEqual(3, provider.CallCount);
            Assert.AreNotSame(initial, recovered);
            CollectionAssert.AreEqual(new[] { false, true, true }, forceRefreshes);
        }

        [TestMethod]
        public async Task ConcurrentReadDuringFailedRefreshDoesNotReuseStaleSnapshot()
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
            await refreshStarted.Task;
            Task<IEnumerable<ISecondaryIndexMetadata>> concurrentRead = cache.GetSecondaryIndexMetadataAsync(
                CreateSource(),
                NoOpTrace.Singleton,
                CancellationToken.None);
            releaseRefresh.SetResult(true);

            await Assert.ThrowsExceptionAsync<SecondaryIndexMetadataRefreshException>(async () => await firstRefresh);
            await Assert.ThrowsExceptionAsync<SecondaryIndexMetadataRefreshException>(async () => await concurrentRead);
            Assert.AreEqual(2, provider.CallCount);
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
            TestClientCollectionCache collectionCache = new TestClientCollectionCache(source, cache.Invalidate);

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

            public TestProvider(Func<ContainerProperties, ITrace, CancellationToken, Task<IReadOnlyList<ISecondaryIndexMetadata>>> callback)
                : this((source, trace, cancellationToken, _) => callback(source, trace, cancellationToken))
            {
            }

            public TestProvider(Func<ContainerProperties, ITrace, CancellationToken, bool, Task<IReadOnlyList<ISecondaryIndexMetadata>>> callback)
            {
                this.callback = callback;
            }

            public int CallCount { get; private set; }

            public async Task<IEnumerable<ISecondaryIndexMetadata>> GetSecondaryIndexMetadataAsync(
                ContainerProperties source,
                ITrace trace,
                CancellationToken cancellationToken,
                bool forceRefresh = false)
            {
                this.CallCount++;
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
                Action<string> secondaryIndexMetadataInvalidationCallback)
                : base(
                    new SessionContainer("testhost"),
                    new ServerStoreModel(null),
                    tokenProvider: null,
                    retryPolicy: null,
                    telemetryToServiceHelper: null,
                    enableAsyncCacheExceptionNoSharing: true,
                    metadataHedgingStrategy: null,
                    secondaryIndexMetadataInvalidationCallback: secondaryIndexMetadataInvalidationCallback)
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
