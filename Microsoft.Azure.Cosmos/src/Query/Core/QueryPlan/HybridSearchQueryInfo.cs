//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.QueryPlan
{
    using System.Collections.Generic;

    public sealed class HybridSearchQueryInfo
    {
        [System.Text.Json.Serialization.JsonPropertyName("globalStatisticsQuery")]
        public string GlobalStatisticsQuery
        {
            get;
            set;
        }

        [System.Text.Json.Serialization.JsonPropertyName("componentQueryInfos")]
        public List<QueryInfo> ComponentQueryInfos
        {
            get;
            set;
        }

        [System.Text.Json.Serialization.JsonPropertyName("componentWithoutPayloadQueryInfos")]
        public List<QueryInfo> ComponentWithoutPayloadQueryInfos
        {
            get;
            set;
        }

        [System.Text.Json.Serialization.JsonPropertyName("projectionQueryInfo")]
        public QueryInfo ProjectionQueryInfo
        {
            get;
            set;
        }

        [System.Text.Json.Serialization.JsonPropertyName("componentWeights")]
        public List<double> ComponentWeights
        {
            get;
            set;
        }

        [System.Text.Json.Serialization.JsonPropertyName("skip")]
        public uint? Skip
        {
            get;
            set;
        }

        [System.Text.Json.Serialization.JsonPropertyName("take")]
        public uint? Take
        {
            get;
            set;
        }

        [System.Text.Json.Serialization.JsonPropertyName("requiresGlobalStatistics")]
        public bool RequiresGlobalStatistics
        {
            get;
            set;
        }
    }
}