//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.HybridSearch;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ClientContextCoreTests
    {
        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void DisabledCacheRemainsDisabledAfterOptionsMutation(bool useZero)
        {
            CosmosClientOptions options = new CosmosClientOptions
            {
                FullTextStatisticsCacheMaxAge = useZero ? TimeSpan.Zero : null,
            };
            using CosmosClient client = CreateClient(options);
            CosmosClientContext context = client.ClientContext;
            CosmosQueryClient queryClient = new CosmosQueryClientCore(context, null);
            options.FullTextStatisticsCacheMaxAge = TimeSpan.FromMinutes(1);
            client.ClientOptions.FullTextStatisticsCacheMaxAge = TimeSpan.FromMinutes(2);

            Assert.IsNull(context.HybridSearchStatisticsCache);
            Assert.IsNull(queryClient.HybridSearchStatisticsCache);
            Assert.IsNull(GetInjectedCache(context));
            context.Dispose();
            Assert.IsNull(GetInjectedCache(context));
            Assert.ThrowsException<ObjectDisposedException>(() => _ = context.HybridSearchStatisticsCache);
        }

        [TestMethod]
        public void EnabledCacheIsInjectedBeforePropertyAccess()
        {
            using CosmosClient client = CreateClient(EnabledOptions());
            CosmosClientContext context = client.ClientContext;
            HybridSearchStatisticsCache injectedCache = GetInjectedCache(context);
            Assert.IsNotNull(injectedCache);
            Container container = client.GetContainer("database", "container");
            using FeedIterator<object> iterator = container.GetItemQueryIterator<object>("SELECT * FROM c");
            Assert.AreSame(injectedCache, context.HybridSearchStatisticsCache);
            Assert.AreSame(injectedCache, GetInjectedCache(context));

            client.Dispose();
            Assert.AreSame(injectedCache, GetInjectedCache(context));
            Assert.ThrowsException<ObjectDisposedException>(() => _ = context.HybridSearchStatisticsCache);
        }

        [TestMethod]
        public void ContainerHandlesAndQueryClientsShareTheClientCache()
        {
            using CosmosClient client = CreateClient(EnabledOptions());
            ContainerInternal first = (ContainerInternal)client.GetContainer("database", "container");
            ContainerInternal second = (ContainerInternal)client.GetContainer("database", "container");
            ContainerInternal other = (ContainerInternal)client.GetContainer("database", "other-container");
            CosmosQueryClient firstQueryClient = new CosmosQueryClientCore(first.ClientContext, first);
            CosmosQueryClient secondQueryClient = new CosmosQueryClientCore(second.ClientContext, second);
            CosmosQueryClient otherQueryClient = new CosmosQueryClientCore(other.ClientContext, other);
            Assert.AreNotSame(first, second);
            HybridSearchStatisticsCache cache = firstQueryClient.HybridSearchStatisticsCache;
            Assert.IsNotNull(cache);
            Assert.AreSame(cache, secondQueryClient.HybridSearchStatisticsCache);
            Assert.AreSame(cache, otherQueryClient.HybridSearchStatisticsCache);
            Assert.AreSame(cache, client.ClientContext.HybridSearchStatisticsCache);
            Assert.IsTrue(cache.TryAdd("rid", "etag", CreatePlan(), CreateStatistics(10), cache.GetElapsedTime()));
            Assert.IsTrue(secondQueryClient.HybridSearchStatisticsCache.TryGet(
                "rid", "etag", CreatePlan(), out GlobalFullTextSearchStatistics result));
            Assert.AreEqual(10L, result.DocumentCount);
        }

        [TestMethod]
        public void InjectedCachePolicyIsIndependentOfLaterOptionsMutation()
        {
            TimeSpan originalAge = TimeSpan.FromMinutes(1);
            CosmosClientOptions options = new CosmosClientOptions
            {
                FullTextStatisticsCacheMaxAge = originalAge,
                FullTextStatisticsCacheMaxEntries = 2,
            };
            using CosmosClient client = CreateClient(options);
            HybridSearchStatisticsCache originalCache = GetInjectedCache(client.ClientContext);
            Assert.IsNotNull(originalCache);

            options.FullTextStatisticsCacheMaxAge = TimeSpan.FromMinutes(3);
            options.FullTextStatisticsCacheMaxEntries = 4;
            client.ClientOptions.FullTextStatisticsCacheMaxAge = TimeSpan.Zero;
            client.ClientOptions.FullTextStatisticsCacheMaxEntries = 8;

            HybridSearchStatisticsCache cache = client.ClientContext.HybridSearchStatisticsCache;
            Assert.AreSame(originalCache, cache);

            Assert.AreEqual(originalAge, GetPrivateField<TimeSpan>(cache, "maxAge"));
            for (int index = 0; index < 3; index++)
            {
                Assert.IsTrue(cache.TryAdd(
                    "rid", "etag", CreatePlan(index.ToString()), CreateStatistics(10), cache.GetElapsedTime()));
            }

            Assert.AreEqual(2, cache.Count);
        }

        [TestMethod]
        public void DifferentClientsHaveIndependentCachesAndPolicies()
        {
            using CosmosClient first = CreateClient(new CosmosClientOptions
            {
                FullTextStatisticsCacheMaxAge = TimeSpan.FromMinutes(1),
                FullTextStatisticsCacheMaxEntries = 1,
            });
            using CosmosClient second = CreateClient(new CosmosClientOptions
            {
                FullTextStatisticsCacheMaxAge = TimeSpan.FromMinutes(2),
                FullTextStatisticsCacheMaxEntries = 2,
            });
            HybridSearchStatisticsCache firstCache = first.ClientContext.HybridSearchStatisticsCache;
            HybridSearchStatisticsCache secondCache = second.ClientContext.HybridSearchStatisticsCache;
            Assert.AreNotSame(firstCache, secondCache);
            Assert.AreEqual(TimeSpan.FromMinutes(1), GetPrivateField<TimeSpan>(firstCache, "maxAge"));
            Assert.AreEqual(TimeSpan.FromMinutes(2), GetPrivateField<TimeSpan>(secondCache, "maxAge"));
            Assert.IsTrue(firstCache.TryAdd("rid", "etag", CreatePlan(), CreateStatistics(10), firstCache.GetElapsedTime()));
            Assert.IsFalse(secondCache.TryGet("rid", "etag", CreatePlan(), out _));
            Assert.IsTrue(secondCache.TryAdd("rid", "etag", CreatePlan(), CreateStatistics(20), secondCache.GetElapsedTime()));
            Assert.IsTrue(secondCache.TryAdd("rid", "etag", CreatePlan("swim"), CreateStatistics(20), secondCache.GetElapsedTime()));
            Assert.AreEqual(1, firstCache.Count);
            Assert.AreEqual(2, secondCache.Count);

            first.Dispose();
            Assert.AreEqual(0, firstCache.Count);
            Assert.IsTrue(secondCache.TryGet("rid", "etag", CreatePlan(), out GlobalFullTextSearchStatistics result));
            Assert.AreEqual(20L, result.DocumentCount);
        }

        [TestMethod]
        public async Task ConcurrentAccessReturnsTheInjectedCache()
        {
            using CosmosClient client = CreateClient(EnabledOptions());
            CosmosClientContext context = client.ClientContext;
            CosmosQueryClient queryClient = new CosmosQueryClientCore(context, null);
            TaskCompletionSource<bool> start = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Task<HybridSearchStatisticsCache>[] accessors = Enumerable.Range(0, 32).Select(async index =>
            {
                await start.Task;
                return index % 2 == 0
                    ? context.HybridSearchStatisticsCache
                    : queryClient.HybridSearchStatisticsCache;
            }).ToArray();
            HybridSearchStatisticsCache injectedCache = GetInjectedCache(context);
            Assert.IsNotNull(injectedCache);
            start.SetResult(true);
            HybridSearchStatisticsCache[] caches = await Task.WhenAll(accessors);
            Assert.IsNotNull(caches[0]);
            foreach (HybridSearchStatisticsCache cache in caches)
            {
                Assert.AreSame(injectedCache, cache);
            }
        }

        [TestMethod]
        public void ClientDisposalClearsTheCacheButPreservesSelectedStatistics()
        {
            using CosmosClient client = CreateClient(EnabledOptions());
            CosmosClientContext context = client.ClientContext;
            CosmosQueryClient queryClient = new CosmosQueryClientCore(context, null);
            HybridSearchStatisticsCache cache = queryClient.HybridSearchStatisticsCache;
            Assert.IsTrue(cache.TryAdd("rid", "etag", CreatePlan(), CreateStatistics(10), cache.GetElapsedTime()));
            Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan(), out GlobalFullTextSearchStatistics selected));

            client.Dispose();
            client.Dispose();
            context.Dispose();
            Assert.AreEqual(0, cache.Count);
            Assert.AreEqual(0, cache.ContainerRevisionCount);
            Assert.IsFalse(cache.TryGet("rid", "etag", CreatePlan(), out _));
            Assert.IsFalse(cache.TryAdd("rid", "etag", CreatePlan(), CreateStatistics(20), cache.GetElapsedTime()));
            Assert.AreEqual(10L, selected.DocumentCount);
            Assert.AreSame(cache, GetInjectedCache(context));
            Assert.ThrowsException<ObjectDisposedException>(() => _ = context.HybridSearchStatisticsCache);
            Assert.ThrowsException<ObjectDisposedException>(() => _ = queryClient.HybridSearchStatisticsCache);
            Assert.ThrowsException<ObjectDisposedException>(() => _ = context.DocumentClient);
        }

        [TestMethod]
        public async Task InFlightCollectionCannotPopulateAfterClientDisposal()
        {
            using CosmosClient client = CreateClient(EnabledOptions());
            HybridSearchStatisticsCache cache = client.ClientContext.HybridSearchStatisticsCache;
            TimeSpan collectionStartedAt = cache.GetElapsedTime();
            TaskCompletionSource<bool> completedCollection = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Task<bool> population = Task.Run(async () =>
            {
                await completedCollection.Task;
                return cache.TryAdd("rid", "etag", CreatePlan(), CreateStatistics(10), collectionStartedAt);
            });

            client.Dispose();
            completedCollection.SetResult(true);
            Assert.IsFalse(await population);
            Assert.AreEqual(0, cache.Count);
            Assert.AreEqual(0, cache.ContainerRevisionCount);
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task CacheAccessRacingDisposalCannotReplaceOrReviveTheDependency(bool enabled)
        {
            using CosmosClient client = CreateClient(enabled ? EnabledOptions() : new CosmosClientOptions());
            CosmosClientContext context = client.ClientContext;
            CosmosQueryClient queryClient = new CosmosQueryClientCore(context, null);
            HybridSearchStatisticsCache injectedCache = GetInjectedCache(context);
            Assert.AreEqual(enabled, injectedCache != null);

            TaskCompletionSource<bool> start = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Task<HybridSearchStatisticsCache>[] accessors = Enumerable.Range(0, 32).Select(async _ =>
            {
                await start.Task;
                try
                {
                    return queryClient.HybridSearchStatisticsCache;
                }
                catch (ObjectDisposedException)
                {
                    return null;
                }
            }).ToArray();
            Task disposal = Task.Run(async () =>
            {
                await start.Task;
                client.Dispose();
            });

            start.SetResult(true);
            HybridSearchStatisticsCache[] results = await Task.WhenAll(accessors);
            await disposal;
            HybridSearchStatisticsCache owned = GetInjectedCache(context);
            Assert.AreSame(injectedCache, owned);
            foreach (HybridSearchStatisticsCache cache in results.Where(cache => cache != null))
            {
                Assert.AreSame(owned, cache);
            }

            if (owned != null)
            {
                Assert.AreEqual(0, owned.Count);
                Assert.IsFalse(owned.TryGet("rid", "etag", CreatePlan(), out _));
                Assert.IsFalse(owned.TryAdd("rid", "etag", CreatePlan(), CreateStatistics(10), owned.GetElapsedTime()));
            }

            Assert.ThrowsException<ObjectDisposedException>(() => _ = queryClient.HybridSearchStatisticsCache);
            Assert.AreSame(owned, GetInjectedCache(context));
        }

        private static CosmosClient CreateClient(CosmosClientOptions options)
        {
            return new CosmosClient(
                CosmosClientTests.AccountEndpoint,
                MockCosmosUtil.RandomInvalidCorrectlyFormatedAuthKey,
                options,
                new MockDocumentClient());
        }

        private static CosmosClientOptions EnabledOptions()
        {
            return new CosmosClientOptions { FullTextStatisticsCacheMaxAge = TimeSpan.FromMinutes(1) };
        }

        private static HybridSearchStatisticsCache GetInjectedCache(CosmosClientContext context)
        {
            return GetPrivateField<HybridSearchStatisticsCache>(context, "hybridSearchStatisticsCache");
        }

        private static T GetPrivateField<T>(object instance, string name)
        {
            FieldInfo field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            return (T)field.GetValue(instance);
        }

        private static HybridSearchQueryInfo CreatePlan(string term = "run")
        {
            return new HybridSearchQueryInfo
            {
                RequiresGlobalStatistics = true,
                FullTextSearchTerms = new List<FullTextSearchTerms>
                {
                    new FullTextSearchTerms
                    {
                        Path = new List<string> { "text" },
                        Terms = new List<FullTextSearchTerm> { new FullTextSearchTerm { Term = term } },
                    },
                },
            };
        }

        private static GlobalFullTextSearchStatistics CreateStatistics(long documentCount)
        {
            return new GlobalFullTextSearchStatistics(documentCount, new[]
            {
                new FullTextStatistics(documentCount * 10, new long[] { 1 }),
            });
        }
    }
}
