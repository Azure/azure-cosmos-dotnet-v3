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
    /// Converter used to support System.Text.Json serialization/deserialization of Position.
    /// Handles 2D positions (longitude, latitude) and 3D positions (longitude, latitude, altitude).
    /// Ensures output format matches Newtonsoft.Json exactly.
    /// </summary>
    internal sealed class PositionSTJConverter : JsonConverter<Position>
    {
        /// <summary>
        /// Deserializes a Position from a JSON array of coordinates.
        /// Requires at least 2 coordinates (longitude, latitude). Altitude is optional.
        /// </summary>
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
        /// <summary>
        /// Serializes a Position to a JSON array.
        /// Integer coordinates are formatted with .0 decimal to match Newtonsoft.Json output.
        /// Uses "R" format for non-integer values to preserve full precision.
        /// </summary>
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
