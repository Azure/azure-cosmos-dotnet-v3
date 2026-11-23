//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.HybridSearch
{
    using System;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;

    internal sealed class HybridSearchStatisticsCacheFactory
    {
        private readonly HybridSearchStatisticsCache cache;

        private HybridSearchStatisticsCacheFactory(HybridSearchStatisticsCache cache)
        {
            this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        public static HybridSearchStatisticsCacheFactory Create(TimeSpan? maxAge, int maxEntries)
        {
            return Create(maxAge, maxEntries, null);
        }

        public static HybridSearchStatisticsCacheFactory Create(TimeSpan? maxAge, int maxEntries, Func<TimeSpan> getElapsedTime)
        {
            return new HybridSearchStatisticsCacheFactory(HybridSearchStatisticsCache.Create(maxAge, maxEntries, getElapsedTime));
        }

        public HybridSearchStatisticsCache GetCache(FullTextScoreScope scope, HybridSearchQueryInfo queryInfo)
        {
            return scope == FullTextScoreScope.Global && HybridSearchStatisticsCache.IsQuerySupported(queryInfo) ? this.cache : HybridSearchStatisticsCache.GetNoOpImpl();
        }
    }
}