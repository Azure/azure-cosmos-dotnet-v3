// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Spatial.Converters.STJConverters
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Antlr4.Runtime.Sharpen;
    using Microsoft.Azure.Cosmos.Spatial;
    using Microsoft.Azure.Documents;
   
    internal class PointSTJConverter : JsonConverter<Point>
    {
        public override Point Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
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
            Position pos = null;
            IDictionary<string, object> additionalProperties = null;
            Crs crs = null;
            BoundingBox boundingBox = null;
            foreach (JsonProperty property in rootElement.EnumerateObject())
            {
                if (property.NameEquals("position"))
                {
                    pos = System.Text.Json.JsonSerializer.Deserialize<Position>(property.Value.ToString(), options);
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
            return new Point(pos, new GeometryParams
            {
                AdditionalProperties = additionalProperties,
                BoundingBox = boundingBox,
                Crs = crs
            });

        }
        public override void Write(Utf8JsonWriter writer, Point point, JsonSerializerOptions options)
        {
            if (point == null)
            {
                return;
            }

            writer.WriteStartObject();
            
            writer.WriteStartObject("position");
            System.Text.Json.JsonSerializer.Serialize(writer, point.Position, options);
            writer.WriteEndObject();

            System.Text.Json.JsonSerializer.Serialize(writer, point.Crs, options);

            writer.WriteNumber("type", (int)point.Type);

            if (point.BoundingBox != null)
            {
                System.Text.Json.JsonSerializer.Serialize(writer, point.BoundingBox, options);
            }

            if (point.AdditionalProperties.Count > 0)
            {
                writer.WritePropertyName("additionalProperties");
                System.Text.Json.JsonSerializer.Serialize(writer, point.AdditionalProperties, options);

            }
            
            writer.WriteEndObject();

        }
    }
}
