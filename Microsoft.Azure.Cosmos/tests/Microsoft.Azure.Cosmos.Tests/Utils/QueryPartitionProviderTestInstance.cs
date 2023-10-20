//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;

    public static class QueryPartitionProviderTestInstance
    {
        internal static QueryPartitionProvider Create(string key = null, object value = null)
        {
            Dictionary<string, object> queryEngineConfiguration = new Dictionary<string, object>()
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
                {"sqlDisableFilterPlanOptimization", false},
                {"clientDisableOptimisticDirectExecution", false}
            };

            if (key != null && value != null)
            {
                if (queryEngineConfiguration.TryGetValue(key, out _))
                {
                    queryEngineConfiguration[key] = value;
                }
            }

            return new QueryPartitionProvider(queryEngineConfiguration);
        }

        internal static readonly QueryPartitionProvider Object = Create();

        internal static QueryPartitionProvider CreateModifiedQueryConfigPartitionProviderObject(string key = null, string value = null)
        { 
            return Create(key, value);
        }
    }
}