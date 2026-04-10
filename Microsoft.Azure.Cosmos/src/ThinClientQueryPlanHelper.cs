//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.QueryPlan
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;
    using Newtonsoft.Json;
    using PartitionKeyDefinition = Documents.PartitionKeyDefinition;
    using PartitionKeyInternal = Documents.Routing.PartitionKeyInternal;

    /// <summary>
    /// Handles conversion of thin client query plan responses where query ranges
    /// are returned in PartitionKeyInternal format instead of EPK hex strings.
    /// Mirrors the conversion logic in <see cref="QueryPartitionProvider.ConvertPartitionedQueryExecutionInfo"/>.
    /// </summary>
    /// <remarks>
    /// Uses System.Text.Json for primary parsing and structural validation.
    /// Newtonsoft.Json is used only for deserializing QueryInfo, HybridSearchQueryInfo,
    /// and Range&lt;PartitionKeyInternal&gt; because these types and their deep type hierarchies
    /// (including the external Direct package types) use Newtonsoft [JsonProperty] attributes
    /// and [JsonObject(MemberSerialization.OptIn)] semantics that have no System.Text.Json equivalent.
    /// </remarks>
    internal static class ThinClientQueryPlanHelper
    {
        private static readonly Newtonsoft.Json.JsonSerializerSettings NewtonsoftSettings =
            new Newtonsoft.Json.JsonSerializerSettings
            {
                DateParseHandling = Newtonsoft.Json.DateParseHandling.None,
                MaxDepth = 64,
            };

        /// <summary>
        /// Deserializes a thin client query plan response stream into a
        /// <see cref="PartitionedQueryExecutionInfo"/> with EPK string ranges.
        /// The response contains query ranges in PartitionKeyInternal format
        /// which are converted to EPK hex strings and sorted.
        /// </summary>
        /// <param name="stream">The response stream containing the raw query plan JSON.</param>
        /// <param name="partitionKeyDefinition">The partition key definition for the container.</param>
        /// <returns><see cref="PartitionedQueryExecutionInfo"/> with sorted EPK string ranges.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> or <paramref name="partitionKeyDefinition"/> is null.</exception>
        /// <exception cref="FormatException">Thrown when the response JSON is malformed or missing required properties.</exception>
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

            using JsonDocument doc = JsonDocument.Parse(stream);
            JsonElement root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException(
                    $"Thin client query plan response must be a JSON object, but was {root.ValueKind}.");
            }

            // Validate and extract queryRanges (required)
            if (!root.TryGetProperty("queryRanges", out JsonElement queryRangesElement))
            {
                throw new FormatException(
                    "Thin client query plan response is missing the required 'queryRanges' property.");
            }

            if (queryRangesElement.ValueKind != JsonValueKind.Array)
            {
                throw new FormatException(
                    $"Expected 'queryRanges' to be a JSON array, but was {queryRangesElement.ValueKind}.");
            }

            if (queryRangesElement.GetArrayLength() == 0)
            {
                throw new FormatException(
                    "Thin client query plan response 'queryRanges' array must not be empty.");
            }

            // Deserialize QueryInfo using Newtonsoft because QueryInfo uses
            // [JsonObject(MemberSerialization.OptIn)] and Newtonsoft-only [JsonProperty] attributes.
            QueryInfo queryInfo = null;
            if (root.TryGetProperty("queryInfo", out JsonElement queryInfoElement)
                && queryInfoElement.ValueKind != JsonValueKind.Null)
            {
                queryInfo = Newtonsoft.Json.JsonConvert.DeserializeObject<QueryInfo>(
                    queryInfoElement.GetRawText(),
                    ThinClientQueryPlanHelper.NewtonsoftSettings);
            }

            // Deserialize HybridSearchQueryInfo using Newtonsoft (same constraint as QueryInfo).
            HybridSearchQueryInfo hybridSearchQueryInfo = null;
            if (root.TryGetProperty("hybridSearchQueryInfo", out JsonElement hybridElement)
                && hybridElement.ValueKind != JsonValueKind.Null)
            {
                hybridSearchQueryInfo = Newtonsoft.Json.JsonConvert.DeserializeObject<HybridSearchQueryInfo>(
                    hybridElement.GetRawText(),
                    ThinClientQueryPlanHelper.NewtonsoftSettings);
            }

            // Parse and convert query ranges to EPK string ranges.
            // Range<PartitionKeyInternal> requires Newtonsoft because PartitionKeyInternal
            // is from the external Direct package with Newtonsoft-based serialization.
            List<Documents.Routing.Range<string>> effectiveRanges =
                new List<Documents.Routing.Range<string>>(queryRangesElement.GetArrayLength());

            foreach (JsonElement rangeElement in queryRangesElement.EnumerateArray())
            {
                if (rangeElement.ValueKind != JsonValueKind.Object)
                {
                    throw new FormatException(
                        $"Each query range must be a JSON object, but was {rangeElement.ValueKind}.");
                }

                if (!rangeElement.TryGetProperty("min", out _))
                {
                    throw new FormatException(
                        "Query range is missing the required 'min' property.");
                }

                if (!rangeElement.TryGetProperty("max", out _))
                {
                    throw new FormatException(
                        "Query range is missing the required 'max' property.");
                }

                Documents.Routing.Range<PartitionKeyInternal> internalRange =
                    Newtonsoft.Json.JsonConvert.DeserializeObject<Documents.Routing.Range<PartitionKeyInternal>>(
                        rangeElement.GetRawText(),
                        ThinClientQueryPlanHelper.NewtonsoftSettings);

                if (internalRange == null)
                {
                    throw new FormatException(
                        "Failed to deserialize query range from thin client response.");
                }

                effectiveRanges.Add(PartitionKeyInternal.GetEffectivePartitionKeyRange(
                    partitionKeyDefinition,
                    internalRange));
            }

            effectiveRanges.Sort(Documents.Routing.Range<string>.MinComparer.Instance);

            return new PartitionedQueryExecutionInfo()
            {
                QueryInfo = queryInfo,
                QueryRanges = effectiveRanges,
                HybridSearchQueryInfo = hybridSearchQueryInfo,
            };
        }
    }
}

There's a mismatch in this task. The issue describes fixing `targetRanges.Last().MaxExclusive` on line 126 of `IRoutingMapProviderExtensions.cs`, but the fix constraint limits changes to `ThinClientQueryPlanHelper.cs` — which doesn't contain that code pattern at all.

`ThinClientQueryPlanHelper.cs` has no `targetRanges`, no `MaxExclusive`, and no loop where that hoisting optimization would apply. The fix described belongs in `IRoutingMapProviderExtensions.cs` (line 126), where `targetRanges.Last().MaxExclusive` is accessed inside a loop.

Should I apply the fix to `IRoutingMapProviderExtensions.cs` instead?
