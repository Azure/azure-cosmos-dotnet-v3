//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Azure.Cosmos.Spatial;
    using Microsoft.Azure.Documents;

    internal sealed class TextJsonCrsConverter : JsonConverter<Crs>
    {
        public override Crs Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
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
                return;
            }

            switch (crs.Type)
            {
                case CrsType.Linked:
                    LinkedCrs linkedCrs = (LinkedCrs)crs;
                    writer.WriteStartObject();
                    writer.WriteString("type", "link");
                    writer.WritePropertyName("properties");
                    writer.WriteStartObject();
                    writer.WriteString("href", linkedCrs.Href);
                    if (linkedCrs.HrefType != null)
                    {
                        writer.WriteString("type", linkedCrs.HrefType);
                    }

                    writer.WriteEndObject();
                    writer.WriteEndObject();
                    break;

                case CrsType.Named:
                    NamedCrs namedCrs = (NamedCrs)crs;
                    writer.WriteStartObject();
                    writer.WriteString("type", "name");
                    writer.WritePropertyName("properties");
                    writer.WriteStartObject();
                    writer.WriteString("name", namedCrs.Name);
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

            if (!root.TryGetProperty("properties", out JsonElement propertiesJsonElement))
            {
                throw new JsonException(RMResources.SpatialFailedToDeserializeCrs);
            }

            if (!root.TryGetProperty("type", out JsonElement typeJsonElement))
            {
                throw new JsonException(RMResources.SpatialFailedToDeserializeCrs);
            }

            switch (typeJsonElement.GetString())
            {
                case "name":
                    if (!propertiesJsonElement.TryGetProperty("name", out JsonElement nameJsonElement)
                        || nameJsonElement.ValueKind != JsonValueKind.String)
                    {
                        throw new JsonException(RMResources.SpatialFailedToDeserializeCrs);
                    }

                    return new NamedCrs(nameJsonElement.GetString());

                case "link":
                    if (!propertiesJsonElement.TryGetProperty("href", out JsonElement hrefJsonElement)
                        || hrefJsonElement.ValueKind != JsonValueKind.String)
                    {
                        throw new JsonException(RMResources.SpatialFailedToDeserializeCrs);
                    }

                    if (!propertiesJsonElement.TryGetProperty("type", out JsonElement typeLinkJsonElement)
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
