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

    [TestClass]
    public class SecondaryIndexMetadataCacheTests
    {
        [TestMethod]
        public async Task CacheCoalescesConcurrentPopulationAndReusesEmptySnapshot()
        {
            TaskCompletionSource<bool> releaseProvider = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TestProvider provider = new TestProvider(async (_, _, _) =>
            {
                await releaseProvider.Task;
                return Array.Empty<ISecondaryIndexMetadata>();
            });
            SecondaryIndexMetadataCache cache = new SecondaryIndexMetadataCache(provider);

            Task<IReadOnlyList<ISecondaryIndexMetadata>> first = cache.TryGetSecondaryIndexMetadataAsync("sourceRid", NoOpTrace.Singleton);
            Task<IReadOnlyList<ISecondaryIndexMetadata>> second = cache.TryGetSecondaryIndexMetadataAsync("sourceRid", NoOpTrace.Singleton);

            releaseProvider.SetResult(true);
            await Task.WhenAll(first, second);
            IReadOnlyList<ISecondaryIndexMetadata> third = await cache.TryGetSecondaryIndexMetadataAsync("sourceRid", NoOpTrace.Singleton);

            Assert.AreEqual(1, provider.CallCount);
            Assert.AreSame(first.Result, second.Result);
            Assert.AreSame(first.Result, third);
        }

        [TestMethod]
        public async Task ForceRefreshAndInvalidationReplaceWholeSnapshot()
        {
            TestProvider provider = new TestProvider((_, _, _) =>
                Task.FromResult<IReadOnlyList<ISecondaryIndexMetadata>>(new[] { CreateMetadata($"gsi{providerCallSequence++}") }));
            SecondaryIndexMetadataCache cache = new SecondaryIndexMetadataCache(provider);

            IReadOnlyList<ISecondaryIndexMetadata> first = await cache.TryGetSecondaryIndexMetadataAsync("sourceRid", NoOpTrace.Singleton);
            IReadOnlyList<ISecondaryIndexMetadata> cached = await cache.TryGetSecondaryIndexMetadataAsync("sourceRid", NoOpTrace.Singleton);
            IReadOnlyList<ISecondaryIndexMetadata> refreshed = await cache.TryGetSecondaryIndexMetadataAsync("sourceRid", NoOpTrace.Singleton, forceRefresh: true);

            cache.Invalidate("sourceRid");
            IReadOnlyList<ISecondaryIndexMetadata> repopulated = await cache.TryGetSecondaryIndexMetadataAsync("sourceRid", NoOpTrace.Singleton);

            Assert.AreSame(first, cached);
            Assert.AreNotSame(first, refreshed);
            Assert.AreNotSame(refreshed, repopulated);
            Assert.AreEqual(3, provider.CallCount);
            Assert.AreEqual("gsi0", first[0].Rid);
            Assert.AreEqual("gsi1", refreshed[0].Rid);
            Assert.AreEqual("gsi2", repopulated[0].Rid);
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
                cache.TryGetSecondaryIndexMetadataAsync("sourceRid", NoOpTrace.Singleton));
            IReadOnlyList<ISecondaryIndexMetadata> result = await cache.TryGetSecondaryIndexMetadataAsync("sourceRid", NoOpTrace.Singleton);

            Assert.AreEqual(2, provider.CallCount);
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public async Task NullProviderResultIsRejectedAndNotCached()
        {
            TestProvider provider = new TestProvider((_, _, _) => Task.FromResult<IReadOnlyList<ISecondaryIndexMetadata>>(null));
            SecondaryIndexMetadataCache cache = new SecondaryIndexMetadataCache(provider);

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
                cache.TryGetSecondaryIndexMetadataAsync("sourceRid", NoOpTrace.Singleton));
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
                cache.TryGetSecondaryIndexMetadataAsync("sourceRid", NoOpTrace.Singleton));

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
                cache.TryGetSecondaryIndexMetadataAsync(
                    "sourceRid",
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

        private static ISecondaryIndexMetadata CreateMetadata(string rid)
        {
            return new SecondaryIndexMetadata(
                rid,
                "sourceRid",
                new Documents.PartitionKeyDefinition(),
                new IndexingPolicy(),
                new Dictionary<string, string>(),
                ConsistencyLevel.Eventual);
        }

        private sealed class TestProvider : ISecondaryIndexMetadataProvider
        {
            private readonly Func<string, ITrace, CancellationToken, Task<IReadOnlyList<ISecondaryIndexMetadata>>> callback;

            public TestProvider(Func<string, ITrace, CancellationToken, Task<IReadOnlyList<ISecondaryIndexMetadata>>> callback)
            {
                this.callback = callback;
            }

            public int CallCount { get; private set; }

            public Task<IReadOnlyList<ISecondaryIndexMetadata>> GetSecondaryIndexMetadataAsync(
                string collectionRid,
                ITrace trace,
                CancellationToken cancellationToken = default)
            {
                this.CallCount++;
                return this.callback(collectionRid, trace, cancellationToken);
            }
        }
    }
}
