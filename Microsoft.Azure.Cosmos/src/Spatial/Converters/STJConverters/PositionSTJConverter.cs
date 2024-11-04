// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Spatial.Converters.STJConverters
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Microsoft.Azure.Documents;
    /// <summary>
    /// Converter used to support System.Text.Json de/serialization of type Position/>.
    /// </summary>
    internal class PositionSTJConverter : JsonConverter<Position>
    {
        public override Position Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException(RMResources.JsonUnexpectedToken);
            }

            JsonElement rootElement = JsonDocument.ParseValue(ref reader).RootElement;
            foreach (JsonProperty property in rootElement.EnumerateObject())
            {
                if (property.NameEquals(STJMetaDataFields.Coordinates))
                {
                    IList<double> coordinates = new List<double>();
                    foreach (JsonElement arrayElement in property.Value.EnumerateArray())
                    {
                        coordinates.Add(arrayElement.GetDouble());
                    }

                    return new Position(new ReadOnlyCollection<double>(coordinates));
                }

            }
            return null;

        }
        public override void Write(Utf8JsonWriter writer, Position position, JsonSerializerOptions options)
        {
            writer.WriteStartArray(STJMetaDataFields.Coordinates);
            writer.WriteNumberValue(position.Longitude);
            writer.WriteNumberValue(position.Latitude);
            if (position.Altitude.HasValue)
            {
                writer.WriteNumberValue(position.Altitude.Value);
            }
            writer.WriteEndArray();
            writer.WriteNumber(STJMetaDataFields.Longitude, position.Longitude);
            writer.WriteNumber(STJMetaDataFields.Latitude, position.Latitude);
            if (position.Altitude.HasValue)
            {
                writer.WriteNumber(STJMetaDataFields.Altitude, position.Altitude.Value);
            }
            else
            {
                writer.WriteNull(STJMetaDataFields.Altitude);

            }
        }

    }
}
