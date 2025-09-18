//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.QueryPlan
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    internal sealed class HybridSearchQueryInfo
    {
        [JsonPropertyName("globalStatisticsQuery")]
        public string GlobalStatisticsQuery
        {
            get;
            set;
        }

        [JsonPropertyName("componentQueryInfos")]
        public List<QueryInfo> ComponentQueryInfos
        {
            get;
            set;
        }

        [JsonPropertyName("componentWithoutPayloadQueryInfos")]
        public List<QueryInfo> ComponentWithoutPayloadQueryInfos
        {
            get;
            set;
        }

        [JsonPropertyName("projectionQueryInfo")]
        public QueryInfo ProjectionQueryInfo
        {
            get;
            set;
        }

        [JsonPropertyName("componentWeights")]
        public List<double> ComponentWeights
        {
            get;
            set;
        }

        [JsonPropertyName("skip")]
        public uint? Skip
        {
            get;
            set;
        }

        [JsonPropertyName("take")]
        public uint? Take
        {
            get;
            set;
        }

        [JsonPropertyName("requiresGlobalStatistics")]
        public bool RequiresGlobalStatistics
        {
            get;
            set;
        }
    }
}