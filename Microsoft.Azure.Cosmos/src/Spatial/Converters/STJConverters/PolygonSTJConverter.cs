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

    internal class PolygonSTJConverter : JsonConverter<Polygon>
    {
        public override Polygon Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
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
            IList<LinearRing> linearRings = null;
            IDictionary<string, object> additionalProperties = null;
            Crs crs = null;
            BoundingBox boundingBox = null;
            foreach (JsonProperty property in rootElement.EnumerateObject())
            {
                if (property.NameEquals("rings"))
                {
                    linearRings = new List<LinearRing>();
                    foreach (JsonElement arrayElement in property.Value.EnumerateArray())
                    {
                        LinearRing linearRing = System.Text.Json.JsonSerializer.Deserialize<LinearRing>(arrayElement.GetRawText(), options);
                        linearRings.Add(linearRing);
                    }
                }
                else if (property.NameEquals("additionalProperties"))
                {
                    additionalProperties = System.Text.Json.JsonSerializer.Deserialize<IDictionary<string, object>>(property.Value.ToString(), options);
                    Console.WriteLine(additionalProperties.ToString());
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
            return new Polygon(linearRings, new GeometryParams
            {
                AdditionalProperties = additionalProperties,
                BoundingBox = boundingBox,
                Crs = crs
            });

        }
        public override void Write(Utf8JsonWriter writer, Polygon polygon, JsonSerializerOptions options)
        {
            if (polygon == null)
            {
                return;
            }

            writer.WriteStartObject();

            writer.WriteStartArray("rings");
            foreach (LinearRing linearRing in polygon.Rings)
            {
                writer.WriteStartObject();
                System.Text.Json.JsonSerializer.Serialize(writer, linearRing, options);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();

            System.Text.Json.JsonSerializer.Serialize(writer, polygon.Crs, options);
            writer.WriteNumber("type", (int)polygon.Type);
            if (polygon.BoundingBox != null)
            {
                System.Text.Json.JsonSerializer.Serialize(writer, polygon.BoundingBox, options);
            }
            if (polygon.AdditionalProperties.Count > 0)
            {
                writer.WritePropertyName("additionalProperties");
                System.Text.Json.JsonSerializer.Serialize(writer, polygon.AdditionalProperties, options);

            }

            writer.WriteEndObject();
        }

    }
}
