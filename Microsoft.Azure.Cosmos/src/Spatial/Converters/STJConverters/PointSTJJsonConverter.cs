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
    using Microsoft.Azure.Cosmos.Spatial;
    using Microsoft.Azure.Documents;
    internal class PointSTJJsonConverter : JsonConverter<Point>
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
            Dictionary<string, object> additionalProperties = null;
            Crs crs = null;
            BoundingBox boundingBox = null;
            foreach (JsonProperty property in rootElement.EnumerateObject())
            {
                if (property.NameEquals("position"))
                {
                    pos = System.Text.Json.JsonSerializer.Deserialize<Position>(property.Value.ToString(), options);
                    //Console.WriteLine(pos.ToString());
                }
                else if (property.NameEquals("additionalProperties"))
                {
                    additionalProperties = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(property.Value.ToString(), options);
                    //Console.WriteLine(additionalProperties.ToString());
                    //Point point = new Point(pos, new GeometryParams() { AdditionalProperties = additionalProperties });
                    //return point;
                    
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

            /*Point point = new Point(
                    new Position(20, 30),
                    new GeometryParams
                    {
                        AdditionalProperties = new Dictionary<string, object>
                        {
                            ["battle"] = "a large abttle",
                            ["cruise"] = "a new cruise"
                        },
                        //BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                        Crs = Crs.Named("SomeCrs")
                        //Crs = new UnspecifiedCrs()
                    });
            /*JsonElement rootElement = JsonDocument.ParseValue(ref reader).RootElement;
                foreach (JsonProperty property in rootElement.EnumerateObject())
                {
                    if (property.NameEquals(PositionMetadataFields.Coordinates))
                    {
                        IList<double> coordinates = new List<double>();
                        foreach (JsonElement arrayElement in property.Value.EnumerateArray())
                        {
                            coordinates.Add(arrayElement.GetDouble());
                        }

                        return new Position(new ReadOnlyCollection<double>(coordinates));

                    }

            }*/
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
            
            //writer.WriteStartObject("bbox");
            System.Text.Json.JsonSerializer.Serialize(writer, point.BoundingBox, options);
            //writer.WriteEndObject();

            if (point.AdditionalProperties.Count > 0)
            {
                writer.WriteStartObject("additionalProperties");
                foreach (KeyValuePair<string, object> keyValue in point.AdditionalProperties)
                {
                    writer.WritePropertyName(keyValue.Key);
                    System.Text.Json.JsonSerializer.Serialize(writer, keyValue.Value, options);
                }
                writer.WriteEndObject();
            }
            
            //System.Text.Json.JsonSerializer.Serialize(writer, point.Crs, options);
            //System.Text.Json.JsonSerializer.Serialize(writer, point., options);

            /*writer.WriteStartArray(PositionMetadataFields.Coordinates);
            writer.WriteNumberValue(position.Longitude);
            writer.WriteNumberValue(position.Latitude);
            if (position.Altitude.HasValue)
            {
                writer.WriteNumberValue(position.Altitude.Value);
            }
            writer.WriteEndArray();
            writer.WriteNumber(PositionMetadataFields.Longitude, position.Longitude);
            writer.WriteNumber(PositionMetadataFields.Latitude, position.Latitude);
            if (position.Altitude.HasValue)
            {
                writer.WriteNumber(PositionMetadataFields.Altitude, position.Altitude.Value);
            }
            else
            {
                writer.WriteNull(PositionMetadataFields.Altitude);

            }*/
            writer.WriteEndObject();
            //writer.WriteEndObject();

        }

    }
}
