//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.QueryPlan
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Constants = Documents.Constants;
    using PartitionKeyDefinition = Documents.PartitionKeyDefinition;
    using PartitionKeyInternal = Documents.Routing.PartitionKeyInternal;

    internal sealed class PartitionedQueryExecutionInfo
    {
        private List<Documents.Routing.Range<string>> queryRanges;

        public PartitionedQueryExecutionInfo()
        {
            this.Version = Constants.PartitionedQueryExecutionInfo.CurrentVersion;
        }

        [JsonProperty(Constants.Properties.PartitionedQueryExecutionInfoVersion)]
        public int Version
        {
            get;
            private set;
        }

        [JsonProperty(Constants.Properties.QueryInfo)]
        public QueryInfo QueryInfo
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the query ranges. In thin client mode, this property
        /// lazily converts PartitionKeyInternal ranges to EPK hex string ranges.
        /// </summary>
        [JsonIgnore]
        public List<Documents.Routing.Range<string>> QueryRanges
        {
            get
            {
                if (this.queryRanges != null)
                {
                    return this.queryRanges;
                }

                if (this.RawQueryRanges != null)
                {
                    if (this.UseThinClientMode && this.PartitionKeyDefinition != null)
                    {
                        this.queryRanges = this.ParseQueryRangesForThinClient();
                    }
                    else
                    {
                        // Non-thin client: deserialize directly as string ranges
                        this.queryRanges = this.RawQueryRanges.ToObject<List<Documents.Routing.Range<string>>>();
                    }
                }

                return this.queryRanges;
            }
            set => this.queryRanges = value;
        }

        /// <summary>
        /// Raw query ranges from JSON deserialization. Used for thin client mode parsing.
        /// In non-thin client mode, this is deserialized directly to QueryRanges.
        /// </summary>
        [JsonProperty(Constants.Properties.QueryRanges)]
        internal JArray RawQueryRanges
        {
            get;
            set;
        }

        // Change to the below after Direct package upgrade
        // [JsonProperty(Constants.Properties.HybridSearchQueryInfo)]
        [JsonProperty("hybridSearchQueryInfo")]
        public HybridSearchQueryInfo HybridSearchQueryInfo
        {
            get;
            set;
        }

        /// <summary>
        /// Flag indicating if thin client mode is enabled.
        /// Must be set before accessing QueryRanges property.
        /// </summary>
        [JsonIgnore]
        internal bool UseThinClientMode { get; set; }

        /// <summary>
        /// Partition key definition used for converting PartitionKeyInternal to EPK strings.
        /// Must be set before accessing QueryRanges property in thin client mode.
        /// </summary>
        [JsonIgnore]
        internal PartitionKeyDefinition PartitionKeyDefinition { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }

        public static bool TryParse(string serializedQueryPlan, out PartitionedQueryExecutionInfo partitionedQueryExecutionInfo)
        {
            if (serializedQueryPlan == null)
            {
                throw new ArgumentNullException(nameof(serializedQueryPlan));
            }

            try
            {
                partitionedQueryExecutionInfo = JsonConvert.DeserializeObject<PartitionedQueryExecutionInfo>(serializedQueryPlan);
                return true;
            }
            catch (JsonException)
            {
                partitionedQueryExecutionInfo = default;
                return false;
            }
        }

        /// <summary>
        /// Parses query ranges for thin client mode where the proxy returns ranges 
        /// in PartitionKeyInternal format (e.g., {"min": [[""]], "max": [["Infinity"]]})
        /// and converts them to EPK hex string ranges.
        /// </summary>
        private List<Documents.Routing.Range<string>> ParseQueryRangesForThinClient()
        {
            if (this.RawQueryRanges == null || this.PartitionKeyDefinition == null)
            {
                return null;
            }

            List<Documents.Routing.Range<string>> epkRanges = new List<Documents.Routing.Range<string>>(this.RawQueryRanges.Count);

            foreach (JToken rangeToken in this.RawQueryRanges)
            {
                if (!(rangeToken is JObject rangeObject))
                {
                    continue;
                }

                // Parse min and max as PartitionKeyInternal
                JToken minToken = rangeObject["min"];
                JToken maxToken = rangeObject["max"];

                PartitionKeyInternal minPk = this.ParsePartitionKeyInternal(minToken);
                PartitionKeyInternal maxPk = this.ParsePartitionKeyInternal(maxToken);

                // Convert to EPK hex strings
                string minEpk = minPk.GetEffectivePartitionKeyString(this.PartitionKeyDefinition);
                string maxEpk = maxPk.GetEffectivePartitionKeyString(this.PartitionKeyDefinition);

                // Parse isMinInclusive and isMaxInclusive (defaults: min=true, max=false)
                bool isMinInclusive = rangeObject["isMinInclusive"]?.Value<bool>() ?? true;
                bool isMaxInclusive = rangeObject["isMaxInclusive"]?.Value<bool>() ?? false;

                epkRanges.Add(new Documents.Routing.Range<string>(minEpk, maxEpk, isMinInclusive, isMaxInclusive));
            }

            return epkRanges;
        }

        /// <summary>
        /// Parses a JSON token representing a PartitionKeyInternal.
        /// Handles formats like [[""]] (empty), [["Infinity"]] (infinity), or actual partition key values.
        /// </summary>
        private PartitionKeyInternal ParsePartitionKeyInternal(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                return PartitionKeyInternal.Empty;
            }

            try
            {
                return token.ToObject<PartitionKeyInternal>();
            }
            catch (JsonException)
            {
                return PartitionKeyInternal.Empty;
            }
        }
    }
}
