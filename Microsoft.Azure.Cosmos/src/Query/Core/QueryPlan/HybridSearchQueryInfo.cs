//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.QueryPlan
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    internal sealed class HybridSearchQueryInfo
    {
        [JsonProperty("globalStatisticsQuery")]
        public string GlobalStatisticsQuery
        {
            get;
            set;
        }

        [JsonProperty("componentQueryInfos")]
        public List<QueryInfo> ComponentQueryInfos
        {
            get;
            set;
        }

        [JsonProperty("componentWithoutPayloadQueryInfos")]
        public List<QueryInfo> ComponentWithoutPayloadQueryInfos
        {
            get;
            set;
        }

        [JsonProperty("projectionQueryInfo")]
        public QueryInfo ProjectionQueryInfo
        {
            get;
            set;
        }

        [JsonProperty("componentWeights")]
        public List<double> ComponentWeights
        {
            get;
            set;
        }

        [JsonProperty("skip")]
        public uint? Skip
        {
            get;
            set;
        }

        [JsonProperty("take")]
        public uint? Take
        {
            get;
            set;
        }

        [JsonProperty("requiresGlobalStatistics")]
        public bool RequiresGlobalStatistics
        {
            get;
            set;
        }

        // An absent discriminator denotes a legacy RRF plan.
        [JsonProperty("scoreCombinationKind", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(ScoreCombinationKindJsonConverter))]
        public ScoreCombinationKind? ScoreCombinationKind
        {
            get;
            set;
        }

        private sealed class ScoreCombinationKindJsonConverter : StringEnumConverter
        {
            public ScoreCombinationKindJsonConverter()
            {
                this.AllowIntegerValues = false;
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType != JsonToken.String)
                {
                    throw new JsonSerializationException("scoreCombinationKind must be Rrf or CombinedScore.");
                }

                return reader.Value switch
                {
                    "Rrf" => QueryPlan.ScoreCombinationKind.Rrf,
                    "CombinedScore" => QueryPlan.ScoreCombinationKind.CombinedScore,
                    _ => throw new JsonSerializationException("Unknown hybrid search scoreCombinationKind."),
                };
            }
        }
    }
}