//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.QueryPlan
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using PartitionKeyDefinition = Documents.PartitionKeyDefinition;
    using PartitionKeyInternal = Documents.Routing.PartitionKeyInternal;

    /// <summary>
    /// Handles conversion of thin client query plan responses where query ranges
    /// are returned in PartitionKeyInternal format instead of EPK hex strings.
    /// </summary>
    internal static class ThinClientQueryPlanHelper
    {
        /// <summary>
        /// Reads the raw query plan JSON from a stream, converts PartitionKeyInternal
        /// ranges to EPK hex string ranges, and deserializes into a clean
        /// <see cref="PartitionedQueryExecutionInfo"/> DTO.
        /// </summary>
        /// <param name="stream">The response stream containing the raw query plan JSON.</param>
        /// <param name="partitionKeyDefinition">The partition key definition for the container.</param>
        /// <returns><see cref="PartitionedQueryExecutionInfo"/> with EPK string ranges.</returns>
        public static PartitionedQueryExecutionInfo DeserializeQueryPlanResponse(
            Stream stream,
            PartitionKeyDefinition partitionKeyDefinition)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (partitionKeyDefinition == null)
            {
                throw new ArgumentNullException(nameof(partitionKeyDefinition));
            }

            JObject queryPlanJson;
            using (StreamReader reader = new StreamReader(stream))
            using (JsonTextReader jsonReader = new JsonTextReader(reader))
            {
                queryPlanJson = JObject.Load(jsonReader);
            }

            if (queryPlanJson[Documents.Constants.Properties.QueryRanges] is JArray rawQueryRanges)
            {
                List<Documents.Routing.Range<string>> epkRanges = ThinClientQueryPlanHelper.ConvertToEpkRanges(
                    rawQueryRanges,
                    partitionKeyDefinition);

                queryPlanJson[Documents.Constants.Properties.QueryRanges] = JToken.FromObject(epkRanges);
            }

            return queryPlanJson.ToObject<PartitionedQueryExecutionInfo>();
        }

        private static List<Documents.Routing.Range<string>> ConvertToEpkRanges(
            JArray rawQueryRanges,
            PartitionKeyDefinition partitionKeyDefinition)
        {
            List<Documents.Routing.Range<string>> epkRanges = new List<Documents.Routing.Range<string>>(rawQueryRanges.Count);

            foreach (JToken rangeToken in rawQueryRanges)
            {
                if (!(rangeToken is JObject rangeObject))
                {
                    continue;
                }

                JToken minToken = rangeObject["min"];
                JToken maxToken = rangeObject["max"];

                PartitionKeyInternal minPk = ThinClientQueryPlanHelper.ParsePartitionKeyInternal(minToken);
                PartitionKeyInternal maxPk = ThinClientQueryPlanHelper.ParsePartitionKeyInternal(maxToken);

                string minEpk = minPk.GetEffectivePartitionKeyString(partitionKeyDefinition);
                string maxEpk = maxPk.GetEffectivePartitionKeyString(partitionKeyDefinition);

                bool isMinInclusive = rangeObject["isMinInclusive"]?.Value<bool>() ?? true;
                bool isMaxInclusive = rangeObject["isMaxInclusive"]?.Value<bool>() ?? false;

                epkRanges.Add(new Documents.Routing.Range<string>(minEpk, maxEpk, isMinInclusive, isMaxInclusive));
            }

            return epkRanges;
        }

        private static PartitionKeyInternal ParsePartitionKeyInternal(JToken token)
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