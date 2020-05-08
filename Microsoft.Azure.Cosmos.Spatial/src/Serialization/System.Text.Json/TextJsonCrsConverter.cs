//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Spatial
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    internal sealed class TextJsonCrsConverter : JsonConverter<Crs>
    {
        private const string LinkType = "link";
        private const string NameType = "name";

        public override Crs Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return Crs.Unspecified;
            }

            using JsonDocument json = JsonDocument.ParseValue(ref reader);
            JsonElement root = json.RootElement;
            return TextJsonCrsConverter.ReadProperty(root);
        }

        public override void Write(
            Utf8JsonWriter writer,
            Crs crs,
            JsonSerializerOptions options)
        {
            TextJsonCrsConverter.WritePropertyValues(writer, crs, options);
        }

        public static void WritePropertyValues(
            Utf8JsonWriter writer,
            Crs crs,
            JsonSerializerOptions options)
        {
            if (crs == null)
            {
                writer.WriteNullValue();
                return;
            }

            switch (crs.Type)
            {
                case CrsType.Linked:
                    LinkedCrs linkedCrs = (LinkedCrs)crs;
                    writer.WriteStartObject();
                    writer.WriteString(JsonEncodedStrings.Type, LinkType);
                    writer.WritePropertyName(JsonEncodedStrings.Properties);
                    writer.WriteStartObject();
                    writer.WriteString(JsonEncodedStrings.Href, linkedCrs.Href);
                    if (linkedCrs.HrefType != null)
                    {
                        writer.WriteString(JsonEncodedStrings.Type, linkedCrs.HrefType);
                    }

                    writer.WriteEndObject();
                    writer.WriteEndObject();
                    break;

                case CrsType.Named:
                    NamedCrs namedCrs = (NamedCrs)crs;
                    writer.WriteStartObject();
                    writer.WriteString(JsonEncodedStrings.Type, NameType);
                    writer.WritePropertyName(JsonEncodedStrings.Properties);
                    writer.WriteStartObject();
                    writer.WriteString(JsonEncodedStrings.Name, namedCrs.Name);
                    writer.WriteEndObject();
                    writer.WriteEndObject();
                    break;

                case CrsType.Unspecified:
                    writer.WriteNullValue();
                    break;
            }
        }

        public static Crs ReadProperty(JsonElement root)
        {
            if (root.ValueKind == JsonValueKind.Null)
            {
                return Crs.Unspecified;
            }

            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException(RMResources.SpatialFailedToDeserializeCrs);
            }

            if (!root.TryGetProperty(JsonEncodedStrings.Properties.EncodedUtf8Bytes, out JsonElement propertiesJsonElement))
            {
                throw new JsonException(RMResources.SpatialFailedToDeserializeCrs);
            }

            if (!root.TryGetProperty(JsonEncodedStrings.Type.EncodedUtf8Bytes, out JsonElement typeJsonElement))
            {
                throw new JsonException(RMResources.SpatialFailedToDeserializeCrs);
            }

            switch (typeJsonElement.GetString())
            {
                case NameType:
                    if (!propertiesJsonElement.TryGetProperty(JsonEncodedStrings.Name.EncodedUtf8Bytes, out JsonElement nameJsonElement)
                        || nameJsonElement.ValueKind != JsonValueKind.String)
                    {
                        throw new JsonException(RMResources.SpatialFailedToDeserializeCrs);
                    }

                    return new NamedCrs(nameJsonElement.GetString());

                case LinkType:
                    if (!propertiesJsonElement.TryGetProperty(JsonEncodedStrings.Href.EncodedUtf8Bytes, out JsonElement hrefJsonElement)
                        || hrefJsonElement.ValueKind != JsonValueKind.String)
                    {
                        throw new JsonException(RMResources.SpatialFailedToDeserializeCrs);
                    }

                    if (!propertiesJsonElement.TryGetProperty(JsonEncodedStrings.Type.EncodedUtf8Bytes, out JsonElement typeLinkJsonElement)
                        || typeLinkJsonElement.ValueKind != JsonValueKind.String)
                    {
                        throw new JsonException(RMResources.SpatialFailedToDeserializeCrs);
                    }

                    return new LinkedCrs(hrefJsonElement.GetString(), typeLinkJsonElement.GetString());

                default:
                    throw new JsonException(RMResources.SpatialFailedToDeserializeCrs);
            }
        }
    }
}
