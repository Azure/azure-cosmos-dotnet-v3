// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Spatial.Converters.STJConverters
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Microsoft.Azure.Documents;
    /// <summary>
    /// Converter used to support System.Text.Json de/serialization of type Position/>.
    /// </summary>
    internal sealed class PositionSTJConverter : JsonConverter<Position>
    {
        public override Position Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
            {
                throw new JsonException(RMResources.JsonUnexpectedToken);
            }

            IList<double> coordinates = JsonSerializer.Deserialize<List<double>>(ref reader, options);
            if (coordinates == null || coordinates.Count < 2)
            {
                throw new JsonException(RMResources.SpatialInvalidPosition);
            }

            return new Position(coordinates);
        }
        public override void Write(Utf8JsonWriter writer, Position position, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            foreach (double coordinate in position.Coordinates)
            {
                // Check if the number is effectively an integer.
                if (coordinate == Math.Truncate(coordinate))
                {
                    // If so, write it with one decimal place to match Newtonsoft's [x.0] format.
                    writer.WriteRawValue(coordinate.ToString("0.0", CultureInfo.InvariantCulture));
                }
                else
                {
                    writer.WriteRawValue(coordinate.ToString("R", CultureInfo.InvariantCulture));
                }
            }
            writer.WriteEndArray();
        }

    }
}
