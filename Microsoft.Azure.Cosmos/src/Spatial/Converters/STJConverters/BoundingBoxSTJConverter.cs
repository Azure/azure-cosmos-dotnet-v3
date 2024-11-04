// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Spatial.Converters.STJConverters
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Microsoft.Azure.Documents;
    /// <summary>
    /// Converter used to support System.Text.Json de/serialization of type BoundingBox/>.
    /// </summary>
    internal class BoundingBoxSTJConverter : JsonConverter<BoundingBox>
    {
        public override BoundingBox Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException(RMResources.JsonUnexpectedToken);
            }
            Position min = null;
            Position max = null;

            JsonElement rootElement = JsonDocument.ParseValue(ref reader).RootElement;
            foreach (JsonProperty property in rootElement.EnumerateObject())
            {
                if (property.NameEquals(STJMetaDataFields.Min))
                {
                    min = JsonSerializer.Deserialize<Position>(property.Value.ToString(), options);
                    
                }
                else if (property.NameEquals(STJMetaDataFields.Max))
                {
                    max = JsonSerializer.Deserialize<Position>(property.Value.ToString(), options);

                }
            }

            return new BoundingBox(min, max);
        }

        public override void Write(Utf8JsonWriter writer, BoundingBox box, JsonSerializerOptions options)
        {
            writer.WriteStartObject(STJMetaDataFields.BoundingBox);
            
            writer.WriteStartObject(STJMetaDataFields.Min);
            JsonSerializer.Serialize(writer, box.Min, options);
            writer.WriteEndObject();

            writer.WriteStartObject(STJMetaDataFields.Max);
            JsonSerializer.Serialize(writer, box.Max, options);
            writer.WriteEndObject();

            writer.WriteEndObject();
        }
    }
}
