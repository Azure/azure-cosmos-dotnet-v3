//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Data.Cosmos
{
    using System;
    using System.Globalization;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Microsoft.Azure.Documents;

    internal sealed class CosmosIndexTextJsonConverter : JsonConverter<Index>
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(Index).IsAssignableFrom(objectType);
        }

        public override Index Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException(string.Format(CultureInfo.CurrentCulture, RMResources.InvalidIndexSpecFormat));
            }

            string raw = reader.GetString();
            JsonDocument document = JsonDocument.Parse(raw);
            if (!document.RootElement.TryGetProperty(Constants.Properties.IndexKind, out JsonElement indexKindElement)
                || indexKindElement.ValueKind != JsonValueKind.String)
            {
                throw new JsonException(string.Format(CultureInfo.CurrentCulture, RMResources.InvalidIndexSpecFormat));
            }

            string indexKindString = indexKindElement.GetString();
            if (Enum.TryParse(indexKindString, out IndexKind indexKind))
            {
                switch (indexKind)
                {
                    case IndexKind.Hash:
                        return JsonSerializer.Deserialize<HashIndex>(ref reader, options);
                    case IndexKind.Range:
                        return JsonSerializer.Deserialize<RangeIndex>(ref reader, options);
                    case IndexKind.Spatial:
                        return JsonSerializer.Deserialize<SpatialIndex>(ref reader, options);
                    default:
                        throw new JsonException(string.Format(CultureInfo.CurrentCulture, RMResources.InvalidIndexKindValue, indexKind));
                }
            }
            else
            {
                throw new JsonException(string.Format(CultureInfo.CurrentCulture, RMResources.InvalidIndexKindValue, indexKindString));
            }
        }

        public override void Write(Utf8JsonWriter writer, Index value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value, options);
        }
    }
}