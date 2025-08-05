// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Microsoft.Azure.Cosmos.Routing;

    internal sealed class FeedRangeInternalConverter : JsonConverter<FeedRangeInternal>
    {
        private const string PartitionKeyNoneValue = "None";
        private const string RangePropertyName = "Range";
        private const string PartitionKeyPropertyName = "PK";
        private const string PartitionKeyRangeIdPropertyName = "PKRangeId";
        private static readonly RangeJsonConverter rangeJsonConverter = new RangeJsonConverter();

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(FeedRangeEpk)
                || objectType == typeof(FeedRangePartitionKey)
                || objectType == typeof(FeedRangePartitionKeyRange);
        }

        public override FeedRangeInternal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException();
            }

            string pkValue = null;
            string pkRangeId = null;
            Documents.Routing.Range<string> range = null;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    break;

                if (reader.TokenType != JsonTokenType.PropertyName)
                    throw new JsonException();

                string propertyName = reader.GetString();
                reader.Read();

                if (propertyName == RangePropertyName)
                {
                    // Assumes you have a System.Text.Json converter for Range<string>
                    range = JsonSerializer.Deserialize<Documents.Routing.Range<string>>(ref reader, options);
                }
                else if (propertyName == PartitionKeyPropertyName)
                {
                    pkValue = reader.GetString();
                }
                else if (propertyName == PartitionKeyRangeIdPropertyName)
                {
                    pkRangeId = reader.GetString();
                }
                else
                {
                    // Unknown property, skip
                    reader.Skip();
                }
            }

            if (range != null)
            {
                return new FeedRangeEpk(range);
            }
            if (pkValue != null)
            {
                if (PartitionKeyNoneValue.Equals(pkValue, StringComparison.OrdinalIgnoreCase))
                {
                    return new FeedRangePartitionKey(PartitionKey.None);
                }
                if (!PartitionKey.TryParseJsonString(pkValue, out PartitionKey partitionKey))
                {
                    throw new JsonException();
                }
                return new FeedRangePartitionKey(partitionKey);
            }
            if (pkRangeId != null)
            {
                return new FeedRangePartitionKeyRange(pkRangeId);
            }

            throw new JsonException();
        }

        public override void Write(Utf8JsonWriter writer, FeedRangeInternal value, JsonSerializerOptions options)
        {
            FeedRangeInternalConverter.WriteJsonElement(writer, value, options);
        }

        internal static FeedRangeInternal ReadJsonElement(JsonElement element, JsonSerializerOptions options)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException();
            }

            // Check for "Range"
            if (element.TryGetProperty(RangePropertyName, out JsonElement rangeElement))
            {
                try
                {
                    Documents.Routing.Range<string> range = JsonSerializer.Deserialize<Documents.Routing.Range<string>>(rangeElement.GetRawText(), CosmosSerializerContext.Default.RangeString);
                    return new FeedRangeEpk(range);
                }
                catch (JsonException)
                {
                    throw;
                }
            }

            // Check for "PK"
            if (element.TryGetProperty(PartitionKeyPropertyName, out JsonElement pkElement))
            {
                string value = pkElement.GetString();
                if (PartitionKeyNoneValue.Equals(value, StringComparison.OrdinalIgnoreCase))
                {
                    return new FeedRangePartitionKey(PartitionKey.None);
                }

                if (!PartitionKey.TryParseJsonString(value, out PartitionKey partitionKey))
                {
                    throw new JsonException();
                }

                return new FeedRangePartitionKey(partitionKey);
            }

            // Check for "PKRangeId"
            if (element.TryGetProperty(PartitionKeyRangeIdPropertyName, out JsonElement pkRangeIdElement))
            {
                return new FeedRangePartitionKeyRange(pkRangeIdElement.GetString());
            }

            throw new JsonException();
        }

        internal static void WriteJsonElement(Utf8JsonWriter writer, FeedRangeInternal value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            if (value is FeedRangeEpk feedRangeEpk)
            {
                writer.WritePropertyName(RangePropertyName);
                JsonSerializer.Serialize(writer, feedRangeEpk.Range, CosmosSerializerContext.Default.RangeString);
            }
            else if (value is FeedRangePartitionKey feedRangePartitionKey)
            {
                writer.WritePropertyName(PartitionKeyPropertyName);
                if (feedRangePartitionKey.PartitionKey.IsNone)
                {
                    writer.WriteStringValue(PartitionKeyNoneValue);
                }
                else
                {
                    writer.WriteStringValue(feedRangePartitionKey.PartitionKey.ToJsonString());
                }
            }
            else if (value is FeedRangePartitionKeyRange feedRangePartitionKeyRange)
            {
                writer.WritePropertyName(PartitionKeyRangeIdPropertyName);
                writer.WriteStringValue(feedRangePartitionKeyRange.PartitionKeyRangeId);
            }
            else
            {
                throw new JsonException(ClientResources.FeedToken_UnrecognizedFeedToken);
            }

            writer.WriteEndObject();
        }
    }
}