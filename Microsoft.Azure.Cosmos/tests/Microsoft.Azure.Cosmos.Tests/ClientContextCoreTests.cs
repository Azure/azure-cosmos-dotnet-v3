//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Reflection;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.HybridSearch;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ClientContextCoreTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void DisabledCacheRemainsDisabledAfterOptionsMutation()
        {
            List<CachePolicyTestCase> testCases = new List<CachePolicyTestCase>
            {
                new CachePolicyTestCase("Null age disables caching", null),
            };

            for (int index = 0; index < testCases.Count; index++)
            {
                CachePolicyTestCase testCase = testCases[index];
                this.TestContext.WriteLine($"Case {index}: {testCase}");
                CosmosClientOptions options = new CosmosClientOptions
                {
                    FullTextStatisticsCacheMaxAge = testCase.MaxAge,
                };
                using CosmosClient client = CreateClient(options);
                CosmosClientContext context = client.ClientContext;
                CosmosQueryClient queryClient = new CosmosQueryClientCore(context, null);
                options.FullTextStatisticsCacheMaxAge = TimeSpan.FromMinutes(2);
                client.ClientOptions.FullTextStatisticsCacheMaxAge = TimeSpan.FromMinutes(3);

                Assert.AreSame(HybridSearchStatisticsCache.GetNoOpImpl(), GetCache(context.HybridSearchStatisticsCacheFactory));
                Assert.AreSame(HybridSearchStatisticsCache.GetNoOpImpl(), GetCache(queryClient.HybridSearchStatisticsCacheFactory));
                Assert.AreSame(HybridSearchStatisticsCache.GetNoOpImpl(), GetInjectedCache(context));
            }
        }

        [TestMethod]
        public void ContainerHandlesAndQueryClientsShareTheClientCache()
        {
            using CosmosClient client = CreateClient(new CosmosClientOptions { FullTextStatisticsCacheMaxAge = TimeSpan.FromMinutes(2) });

            ContainerInternal first = (ContainerInternal)client.GetContainer("database", "container");
            ContainerInternal second = (ContainerInternal)client.GetContainer("database", "container");
            ContainerInternal other = (ContainerInternal)client.GetContainer("database", "other-container");

            CosmosQueryClient firstQueryClient = new CosmosQueryClientCore(first.ClientContext, first);
            CosmosQueryClient secondQueryClient = new CosmosQueryClientCore(second.ClientContext, second);
            CosmosQueryClient otherQueryClient = new CosmosQueryClientCore(other.ClientContext, other);
            
            Assert.AreNotSame(first, second);
            
            HybridSearchStatisticsCache cache = GetCache(firstQueryClient.HybridSearchStatisticsCacheFactory);
            
            Assert.AreNotSame(HybridSearchStatisticsCache.GetNoOpImpl(), cache);
            Assert.AreSame(cache, GetInjectedCache(client.ClientContext));
            Assert.AreSame(cache, GetCache(secondQueryClient.HybridSearchStatisticsCacheFactory));
            Assert.AreSame(cache, GetCache(otherQueryClient.HybridSearchStatisticsCacheFactory));
            Assert.AreSame(cache, GetCache(client.ClientContext.HybridSearchStatisticsCacheFactory));
            
            Assert.IsTrue(cache.TryAdd("rid", "etag", CreatePlan("run"), CreateStatistics(10), cache.GetElapsedTime()));
            Assert.IsTrue(GetCache(secondQueryClient.HybridSearchStatisticsCacheFactory).TryGet(
                "rid", "etag", CreatePlan("run"), NoOpTrace.Singleton, out GlobalFullTextSearchStatistics result));
            Assert.AreEqual(10L, result.DocumentCount);
        }

        [TestMethod]
        public void InjectedCachePolicyIsIndependentOfLaterOptionsMutation()
        {
            TimeSpan originalAge = TimeSpan.FromMinutes(2);
            CosmosClientOptions options = new CosmosClientOptions
            {
                FullTextStatisticsCacheMaxAge = originalAge,
                FullTextStatisticsCacheMaxEntries = 4,
            };
            using CosmosClient client = CreateClient(options);
            HybridSearchStatisticsCache originalCache = GetInjectedCache(client.ClientContext);
            Assert.AreNotSame(HybridSearchStatisticsCache.GetNoOpImpl(), originalCache);

            options.FullTextStatisticsCacheMaxAge = TimeSpan.FromMinutes(3);
            options.FullTextStatisticsCacheMaxEntries = 8;
            client.ClientOptions.FullTextStatisticsCacheMaxAge = null;
            client.ClientOptions.FullTextStatisticsCacheMaxEntries = 16;

            HybridSearchStatisticsCache cache = GetCache(client.ClientContext.HybridSearchStatisticsCacheFactory);
            Assert.AreSame(originalCache, cache);
            Assert.AreEqual(originalAge, GetPrivateField<TimeSpan>(cache, "maxAge"));

            for (int index = 0; index < 3; index++)
            {
                cache.TryAdd("rid", "etag", CreatePlan(index.ToString()), CreateStatistics(10), cache.GetElapsedTime());
            }

            Assert.AreEqual(4, StoredCount(cache));
        }

        [TestMethod]
        public void DifferentClientsHaveIndependentCachesAndPolicies()
        {
            using CosmosClient first = CreateClient(new CosmosClientOptions
            {
                FullTextStatisticsCacheMaxAge = TimeSpan.FromMinutes(2),
                FullTextStatisticsCacheMaxEntries = 3,
            });

            using CosmosClient second = CreateClient(new CosmosClientOptions
            {
                FullTextStatisticsCacheMaxAge = TimeSpan.FromMinutes(3),
                FullTextStatisticsCacheMaxEntries = 4,
            });

            HybridSearchStatisticsCache firstCache = GetCache(first.ClientContext.HybridSearchStatisticsCacheFactory);
            HybridSearchStatisticsCache secondCache = GetCache(second.ClientContext.HybridSearchStatisticsCacheFactory);
            
            Assert.AreNotSame(firstCache, secondCache);
            Assert.AreEqual(TimeSpan.FromMinutes(2), GetPrivateField<TimeSpan>(firstCache, "maxAge"));
            Assert.AreEqual(TimeSpan.FromMinutes(3), GetPrivateField<TimeSpan>(secondCache, "maxAge"));
            
            firstCache.TryAdd("rid", "etag", CreatePlan("run"), CreateStatistics(10), firstCache.GetElapsedTime());
            Assert.IsFalse(secondCache.TryGet("rid", "etag", CreatePlan("run"), NoOpTrace.Singleton, out _));
            
            secondCache.TryAdd("rid", "etag", CreatePlan("run"), CreateStatistics(20), secondCache.GetElapsedTime());
            secondCache.TryAdd("rid", "etag", CreatePlan("swim"), CreateStatistics(20), secondCache.GetElapsedTime());
            
            Assert.AreEqual(3, StoredCount(firstCache));
            Assert.AreEqual(4, StoredCount(secondCache));
            Assert.IsTrue(secondCache.TryGet("rid", "etag", CreatePlan("run"), NoOpTrace.Singleton, out GlobalFullTextSearchStatistics result));
            Assert.AreEqual(20L, result.DocumentCount);
        }

        private readonly struct CachePolicyTestCase
        {
            public CachePolicyTestCase(string name, TimeSpan? maxAge)
            {
                this.Name = name;
                this.MaxAge = maxAge;
            }

            public string Name { get; }

            public TimeSpan? MaxAge { get; }

            public override string ToString() => $"{this.Name}: MaxAge={this.MaxAge?.ToString() ?? "<null>"}";
        }

        private static CosmosClient CreateClient(CosmosClientOptions options)
        {
            return new CosmosClient(
                CosmosClientTests.AccountEndpoint,
                MockCosmosUtil.RandomInvalidCorrectlyFormatedAuthKey,
                options,
                new MockDocumentClient());
        }

        private static HybridSearchStatisticsCache GetInjectedCache(CosmosClientContext context)
        {
            return GetCache(GetPrivateField<HybridSearchStatisticsCacheFactory>(context, "hybridSearchStatisticsCacheFactory"));
        }

        private static HybridSearchStatisticsCache GetCache(HybridSearchStatisticsCacheFactory cacheFactory)
        {
            return cacheFactory.GetCache(FullTextScoreScope.Global, CreatePlan("run"));
        }

        private static T GetPrivateField<T>(object instance, string name)
        {
            FieldInfo field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            return (T)field.GetValue(instance);
        }

        private static int StoredCount(HybridSearchStatisticsCache cache)
        {
            return GetPrivateField<ICollection>(cache, "counts").Count;
        }

        private static HybridSearchQueryInfo CreatePlan(string term)
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
