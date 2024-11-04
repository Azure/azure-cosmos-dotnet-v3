// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Spatial.Converters.STJConverters
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Microsoft.Azure.Cosmos.Spatial;
    using Microsoft.Azure.Documents;
    /// <summary>
    /// Converter used to support System.Text.Json de/serialization of type LineStringCoordinates/>.
    /// </summary>
    internal class LineStringCoordinatesSTJConverter : JsonConverter<LineStringCoordinates>
    {
        public override LineStringCoordinates Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException(RMResources.JsonUnexpectedToken);
            }
            IList<Position> positions = null;
            JsonElement rootElement = JsonDocument.ParseValue(ref reader).RootElement;
            foreach (JsonProperty property in rootElement.EnumerateObject())
            {
                if (property.NameEquals(STJMetaDataFields.Positions))
                {
                    positions = new List<Position>();
                    foreach (JsonElement arrayElement in property.Value.EnumerateArray())
                    {
                        Position pos = JsonSerializer.Deserialize<Position>(arrayElement.GetRawText(), options);
                        positions.Add(pos);
                    }
                }

            }
            return new LineStringCoordinates(positions);

        }
        public override void Write(Utf8JsonWriter writer, LineStringCoordinates lineStringCorodinates, JsonSerializerOptions options)
        {
            writer.WriteStartArray(STJMetaDataFields.Positions);
            foreach (Position position in lineStringCorodinates.Positions)
            {
                writer.WriteStartObject();
                JsonSerializer.Serialize(writer, position, options);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();

        }

    }

}
