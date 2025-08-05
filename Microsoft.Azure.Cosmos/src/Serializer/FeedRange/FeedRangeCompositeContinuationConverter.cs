// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.ClientModel;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Microsoft.Azure.Cosmos.Routing;

    internal sealed class FeedRangeCompositeContinuationConverter : JsonConverter<FeedRangeCompositeContinuation>
    {
        private const string VersionPropertyName = "V";
        private const string RidPropertyName = "Rid";
        private const string ContinuationPropertyName = "Continuation";

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(FeedRangeCompositeContinuation);
        }

        public override FeedRangeCompositeContinuation Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException();
            }

            string containerRid = null;
            List<CompositeContinuationToken> ranges = null;
            FeedRangeInternal feedRangeInternal = null;

            using (JsonDocument document = JsonDocument.ParseValue(ref reader))
            {
                JsonElement root = document.RootElement;

                if (root.TryGetProperty(RidPropertyName, out JsonElement ridElement))
                {
                    containerRid = ridElement.GetString();
                }

                if (!root.TryGetProperty(ContinuationPropertyName, out JsonElement continuationElement))
                {
                    throw new JsonException();
                }

                ranges = JsonSerializer.Deserialize<List<CompositeContinuationToken>>(continuationElement.GetRawText(), CosmosSerializerContext.Default.ListCompositeContinuationToken);

                feedRangeInternal = FeedRangeInternalConverter.ReadJsonElement(root, options);
            }

            return new FeedRangeCompositeContinuation(
                containerRid: containerRid,
                feedRange: feedRangeInternal,
                deserializedTokens: ranges);
        }

        public override void Write(Utf8JsonWriter writer, FeedRangeCompositeContinuation value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStartObject();
            writer.WritePropertyName(VersionPropertyName);
            writer.WriteNumberValue((int)FeedRangeContinuationVersion.V1);
            writer.WritePropertyName(RidPropertyName);
            writer.WriteStringValue(value.ContainerRid);
            writer.WritePropertyName(ContinuationPropertyName);
            JsonSerializer.Serialize(writer, value.CompositeContinuationTokens, CosmosSerializerContext.Default.ListCompositeContinuationToken);

            FeedRangeInternalConverter.WriteJsonElement(writer, value.FeedRange, options);

            writer.WriteEndObject();
        }
    }
}
