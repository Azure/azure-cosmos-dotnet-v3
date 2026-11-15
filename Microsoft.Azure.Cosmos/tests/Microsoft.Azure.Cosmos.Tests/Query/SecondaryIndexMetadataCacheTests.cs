//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.SecondaryIndexRouting;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System.Linq;
    using PartitionKeyDefinition = Microsoft.Azure.Documents.PartitionKeyDefinition;

    [TestClass]
    public class SecondaryIndexMetadataCacheTests
    {
        [TestMethod]
        public async Task CacheCoalescesConcurrentPopulationAndReusesEmptySnapshot()
        {
            ContainerProperties source = CreateSource();
            TaskCompletionSource<bool> releaseProvider = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TestProvider provider = new TestProvider(async (container, _, _) =>
            {
                Assert.AreSame(source, container);
                await releaseProvider.Task;
                return Array.Empty<ISecondaryIndexMetadata>();
            });
            SecondaryIndexMetadataCache cache = new SecondaryIndexMetadataCache(provider);

            Task<IEnumerable<ISecondaryIndexMetadata>> first = cache.GetSecondaryIndexMetadataAsync(source, NoOpTrace.Singleton, CancellationToken.None);
            Task<IEnumerable<ISecondaryIndexMetadata>> second = cache.GetSecondaryIndexMetadataAsync(CreateSource(), NoOpTrace.Singleton, CancellationToken.None);

            releaseProvider.SetResult(true);
            await Task.WhenAll(first, second);
            IEnumerable<ISecondaryIndexMetadata> third = await cache.GetSecondaryIndexMetadataAsync(CreateSource(), NoOpTrace.Singleton, CancellationToken.None);

            Assert.AreEqual(1, provider.CallCount);
            Assert.AreSame(first.Result, second.Result);
            Assert.AreSame(first.Result, third);
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
            private readonly Func<ContainerProperties, ITrace, CancellationToken, Task<IReadOnlyList<ISecondaryIndexMetadata>>> callback;

            public TestProvider(Func<ContainerProperties, ITrace, CancellationToken, Task<IReadOnlyList<ISecondaryIndexMetadata>>> callback)
            {
                this.callback = callback;
            }

            public int CallCount { get; private set; }

            public Task<IEnumerable<ISecondaryIndexMetadata>> GetSecondaryIndexMetadataAsync(
                ContainerProperties source,
                ITrace trace,
                CancellationToken cancellationToken)
            {
                this.CallCount++;
                return this.callback(source, trace, cancellationToken).ContinueWith(t => (IEnumerable<ISecondaryIndexMetadata>)t.Result, cancellationToken);
            }
        }
    }
}
