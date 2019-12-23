//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    using System;
    using System.Globalization;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Microsoft.Azure.Documents;

    internal class TextJsonIndexConverter : JsonConverter<Index>
    {
        public override Index Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException(string.Format(CultureInfo.CurrentCulture, RMResources.InvalidIndexSpecFormat));
            }

            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return TextJsonIndexConverter.ReadProperty(document.RootElement);
        }

        public override void Write(
            Utf8JsonWriter writer,
            Index index,
            JsonSerializerOptions options)
        {
            TextJsonIndexConverter.WritePropertyValues(writer, index, options);
        }

        public static void WritePropertyValues(
            Utf8JsonWriter writer,
            Index index,
            JsonSerializerOptions options)
        {
            if (index == null)
            {
                return;
            }

            writer.WriteStartObject();

            writer.WriteString(Constants.Properties.IndexKind, index.Kind.ToString());

            writer.WritePropertyName(Constants.Properties.DataType);
            switch (index.Kind)
            {
                case IndexKind.Hash:
                    HashIndex hashIndex = index as HashIndex;
                    writer.WriteStringValue(hashIndex.DataType.ToString());
                    if (hashIndex.Precision.HasValue)
                    {
                        writer.WriteNumber(Constants.Properties.Precision, hashIndex.Precision.Value);
                    }

                    break;
                case IndexKind.Range:
                    RangeIndex rangeIndex = index as RangeIndex;
                    writer.WriteStringValue(rangeIndex.DataType.ToString());
                    if (rangeIndex.Precision.HasValue)
                    {
                        writer.WriteNumber(Constants.Properties.Precision, rangeIndex.Precision.Value);
                    }

                    break;
                case IndexKind.Spatial:
                    SpatialIndex spatialIndex = index as SpatialIndex;
                    writer.WriteStringValue(spatialIndex.DataType.ToString());
                    break;
            }

            writer.WriteEndObject();
        }

        public static Index ReadProperty(JsonElement root)
        {
            if (!root.TryGetProperty(Constants.Properties.IndexKind, out JsonElement indexKindElement)
                || indexKindElement.ValueKind != JsonValueKind.String)
            {
                throw new JsonException(string.Format(CultureInfo.CurrentCulture, RMResources.InvalidIndexSpecFormat));
            }

            if (!root.TryGetProperty(Constants.Properties.DataType, out JsonElement dataTypeElement)
                || dataTypeElement.ValueKind != JsonValueKind.String)
            {
                throw new JsonException(string.Format(CultureInfo.CurrentCulture, RMResources.InvalidIndexSpecFormat));
            }

            short? precision = null;
            if (root.TryGetProperty(Constants.Properties.Precision, out JsonElement precisionElement)
                && precisionElement.ValueKind != JsonValueKind.Null)
            {
                precision = precisionElement.GetInt16();
            }

            string indexKindString = indexKindElement.GetString();
            string dataTypeString = dataTypeElement.GetString();
            if (!Enum.TryParse(indexKindString, out IndexKind indexKind))
            {
                throw new JsonException(string.Format(CultureInfo.CurrentCulture, RMResources.InvalidIndexKindValue, dataTypeString));
            }

            if (!Enum.TryParse(dataTypeString, out DataType dataType))
            {
                throw new JsonException(string.Format(CultureInfo.CurrentCulture, RMResources.InvalidIndexKindValue, dataTypeString));
            }

            switch (indexKind)
            {
                case IndexKind.Hash:
                    HashIndex hashIndex = new HashIndex(dataType);
                    hashIndex.Precision = precision;
                    return hashIndex;
                case IndexKind.Range:
                    RangeIndex rangeIndex = new RangeIndex(dataType);
                    rangeIndex.Precision = precision;
                    return rangeIndex;
                case IndexKind.Spatial:
                    return new SpatialIndex(dataType);
                default:
                    throw new JsonException(string.Format(CultureInfo.CurrentCulture, RMResources.InvalidIndexKindValue, indexKind));
            }
        }
    }
}
