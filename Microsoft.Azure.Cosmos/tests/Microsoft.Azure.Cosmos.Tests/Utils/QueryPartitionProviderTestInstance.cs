//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;

    public static class QueryPartitionProviderTestInstance
    {
        public static readonly IReadOnlyDictionary<string, object> DefaultQueryEngineConfiguration = new Dictionary<string, object>()
        {
            {"maxSqlQueryInputLength", 262144},
            {"maxJoinsPerSqlQuery", 5},
            {"maxLogicalAndPerSqlQuery", 2000},
            {"maxLogicalOrPerSqlQuery", 2000},
            {"maxUdfRefPerSqlQuery", 10},
            {"maxInExpressionItemsCount", 16000},
            {"queryMaxGroupByTableCellCount", 500000 },
            {"queryMaxInMemorySortDocumentCount", 500},
            {"maxQueryRequestTimeoutFraction", 0.90},
            {"sqlAllowNonFiniteNumbers", false},
            {"sqlAllowAggregateFunctions", true},
            {"sqlAllowSubQuery", true},
            {"sqlAllowScalarSubQuery", true},
            {"allowNewKeywords", true},
            {"sqlAllowLike", true},
            {"sqlAllowGroupByClause", true},
            {"maxSpatialQueryCells", 12},
            {"spatialMaxGeometryPointCount", 256},
            {"sqlDisableQueryILOptimization", false},
            {"sqlDisableFilterPlanOptimization", false}
        };

        internal static readonly QueryPartitionProvider Object = new QueryPartitionProvider(DefaultQueryEngineConfiguration as IDictionary<string, object>);
    }

}