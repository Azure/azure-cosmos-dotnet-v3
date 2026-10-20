//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.QueryPlan
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using Newtonsoft.Json.Linq;

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

        [JsonProperty("fullTextSearchTerms", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(FullTextSearchTermsConverter))]
        public JToken FullTextSearchTerms
        {
            get;
            set;
        }

        // An absent discriminator denotes a legacy RRF plan.
        [JsonProperty("scoreCombinationKind", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(StringEnumConverter))]
        public ScoreCombinationKind? ScoreCombinationKind
        {
            get;
            set;
        }

        private sealed class FullTextSearchTermsConverter : JsonConverter
        {
            public override bool CanWrite => false;

            public override bool CanConvert(Type objectType) => objectType == typeof(JToken);

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                DateParseHandling dateParseHandling = reader.DateParseHandling;
                try
                {
                    // Paths and search terms that resemble dates must remain exact strings.
                    reader.DateParseHandling = DateParseHandling.None;
                    return JToken.Load(reader);
                }
                finally
                {
                    reader.DateParseHandling = dateParseHandling;
                }
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                throw new NotSupportedException();
            }
        }
    }
}