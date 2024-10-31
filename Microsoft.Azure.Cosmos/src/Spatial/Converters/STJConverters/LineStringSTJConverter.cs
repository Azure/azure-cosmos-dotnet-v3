// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Spatial.Converters.STJConverters
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Drawing;
    using System.Globalization;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Microsoft.Azure.Cosmos.Spatial;
    using Microsoft.Azure.Documents;

    internal class LineStringSTJConverter : JsonConverter<LineString>
    {
        public override LineString Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException(RMResources.JsonUnexpectedToken);
            }
            JsonElement rootElement = JsonDocument.ParseValue(ref reader).RootElement;
            IList<Position> positions = null;
            IDictionary<string, object> additionalProperties = null;
            Crs crs = null;
            BoundingBox boundingBox = null;
            foreach (JsonProperty property in rootElement.EnumerateObject())
            {
                if (property.NameEquals("positions"))
                {
                    positions = new List<Position>();
                    foreach (JsonElement arrayElement in property.Value.EnumerateArray())
                    {
                        Position pos = System.Text.Json.JsonSerializer.Deserialize<Position>(arrayElement.GetRawText(), options);
                        positions.Add(pos);
                    }
                }
                else if (property.NameEquals("additionalProperties"))
                {
                    additionalProperties = System.Text.Json.JsonSerializer.Deserialize<IDictionary<string, object>>(property.Value.ToString(), options);
                }
                else if (property.NameEquals("crs"))
                {
                    crs = property.Value.ValueKind == JsonValueKind.Null
                        ? Crs.Unspecified
                        : System.Text.Json.JsonSerializer.Deserialize<Crs>(property.Value.ToString(), options);

                }
                else if (property.NameEquals("boundingBox"))
                {
                    boundingBox = System.Text.Json.JsonSerializer.Deserialize<BoundingBox>(property.Value.ToString(), options);

                }

            }
            return new LineString(positions, new GeometryParams
            {
                AdditionalProperties = additionalProperties,
                BoundingBox = boundingBox,
                Crs = crs
            });
        }
        public override void Write(Utf8JsonWriter writer, LineString lineString, JsonSerializerOptions options)
        {
            if (lineString == null)
            {
                return;
            }

            writer.WriteStartObject();

            writer.WriteStartArray("positions");
            foreach (Position position in lineString.Positions)
            {
                writer.WriteStartObject();
                System.Text.Json.JsonSerializer.Serialize(writer, position, options);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();

            System.Text.Json.JsonSerializer.Serialize(writer, lineString.Crs, options);
            writer.WriteNumber("type", (int)lineString.Type);
            if (lineString.BoundingBox != null)
            {
                System.Text.Json.JsonSerializer.Serialize(writer, lineString.BoundingBox, options);
            }
            if (lineString.AdditionalProperties.Count > 0)
            {
                writer.WritePropertyName("additionalProperties");
                System.Text.Json.JsonSerializer.Serialize(writer, lineString.AdditionalProperties, options);

            }

            writer.WriteEndObject();
        }

    }

}
