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
                new CachePolicyTestCase("Zero age disables caching", TimeSpan.Zero),
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
                options.FullTextStatisticsCacheMaxAge = TimeSpan.FromMinutes(1);
                client.ClientOptions.FullTextStatisticsCacheMaxAge = TimeSpan.FromMinutes(2);

                Assert.IsNull(context.HybridSearchStatisticsCache);
                Assert.IsNull(queryClient.HybridSearchStatisticsCache);
                Assert.IsNull(GetInjectedCache(context));
            }
        }

        [TestMethod]
        public void ContainerHandlesAndQueryClientsShareTheClientCache()
        {
            using CosmosClient client = CreateClient(new CosmosClientOptions { FullTextStatisticsCacheMaxAge = TimeSpan.FromMinutes(1) });
            ContainerInternal first = (ContainerInternal)client.GetContainer("database", "container");
            ContainerInternal second = (ContainerInternal)client.GetContainer("database", "container");
            ContainerInternal other = (ContainerInternal)client.GetContainer("database", "other-container");
            CosmosQueryClient firstQueryClient = new CosmosQueryClientCore(first.ClientContext, first);
            CosmosQueryClient secondQueryClient = new CosmosQueryClientCore(second.ClientContext, second);
            CosmosQueryClient otherQueryClient = new CosmosQueryClientCore(other.ClientContext, other);
            Assert.AreNotSame(first, second);
            HybridSearchStatisticsCache cache = firstQueryClient.HybridSearchStatisticsCache;
            Assert.IsNotNull(cache);
            Assert.AreSame(cache, GetInjectedCache(client.ClientContext));
            Assert.AreSame(cache, secondQueryClient.HybridSearchStatisticsCache);
            Assert.AreSame(cache, otherQueryClient.HybridSearchStatisticsCache);
            Assert.AreSame(cache, client.ClientContext.HybridSearchStatisticsCache);
            cache.Add("rid", "etag", CreatePlan("run"), CreateStatistics(10), cache.GetElapsedTime());
            Assert.IsTrue(secondQueryClient.HybridSearchStatisticsCache.TryGet(
                "rid", "etag", CreatePlan("run"), out GlobalFullTextSearchStatistics result));
            Assert.AreEqual(10L, result.DocumentCount);
        }

        [TestMethod]
        public void InjectedCachePolicyIsIndependentOfLaterOptionsMutation()
        {
            TimeSpan originalAge = TimeSpan.FromMinutes(1);
            CosmosClientOptions options = new CosmosClientOptions
            {
                FullTextStatisticsCacheMaxAge = originalAge,
                FullTextStatisticsCacheMaxEntries = 4,
            };
            using CosmosClient client = CreateClient(options);
            HybridSearchStatisticsCache originalCache = GetInjectedCache(client.ClientContext);
            Assert.IsNotNull(originalCache);

            options.FullTextStatisticsCacheMaxAge = TimeSpan.FromMinutes(3);
            options.FullTextStatisticsCacheMaxEntries = 8;
            client.ClientOptions.FullTextStatisticsCacheMaxAge = TimeSpan.Zero;
            client.ClientOptions.FullTextStatisticsCacheMaxEntries = 16;

            HybridSearchStatisticsCache cache = client.ClientContext.HybridSearchStatisticsCache;
            Assert.AreSame(originalCache, cache);
            Assert.AreEqual(originalAge, GetPrivateField<TimeSpan>(cache, "maxAge"));
            for (int index = 0; index < 3; index++)
            {
                cache.Add("rid", "etag", CreatePlan(index.ToString()), CreateStatistics(10), cache.GetElapsedTime());
            }

            Assert.AreEqual(4, StoredCount(cache));
        }

        [TestMethod]
        public void DifferentClientsHaveIndependentCachesAndPolicies()
        {
            using CosmosClient first = CreateClient(new CosmosClientOptions
            {
                FullTextStatisticsCacheMaxAge = TimeSpan.FromMinutes(1),
                FullTextStatisticsCacheMaxEntries = 3,
            });
            using CosmosClient second = CreateClient(new CosmosClientOptions
            {
                FullTextStatisticsCacheMaxAge = TimeSpan.FromMinutes(2),
                FullTextStatisticsCacheMaxEntries = 4,
            });
            HybridSearchStatisticsCache firstCache = first.ClientContext.HybridSearchStatisticsCache;
            HybridSearchStatisticsCache secondCache = second.ClientContext.HybridSearchStatisticsCache;
            Assert.AreNotSame(firstCache, secondCache);
            Assert.AreEqual(TimeSpan.FromMinutes(1), GetPrivateField<TimeSpan>(firstCache, "maxAge"));
            Assert.AreEqual(TimeSpan.FromMinutes(2), GetPrivateField<TimeSpan>(secondCache, "maxAge"));
            firstCache.Add("rid", "etag", CreatePlan("run"), CreateStatistics(10), firstCache.GetElapsedTime());
            Assert.IsFalse(secondCache.TryGet("rid", "etag", CreatePlan("run"), out _));
            secondCache.Add("rid", "etag", CreatePlan("run"), CreateStatistics(20), secondCache.GetElapsedTime());
            secondCache.Add("rid", "etag", CreatePlan("swim"), CreateStatistics(20), secondCache.GetElapsedTime());
            Assert.AreEqual(3, StoredCount(firstCache));
            Assert.AreEqual(4, StoredCount(secondCache));
            Assert.IsTrue(secondCache.TryGet("rid", "etag", CreatePlan("run"), out GlobalFullTextSearchStatistics result));
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
            return GetPrivateField<HybridSearchStatisticsCache>(context, "hybridSearchStatisticsCache");
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
