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
    /// Converter used to support System.Text.Json serialization/deserialization of LineStringCoordinates.
    /// LineStringCoordinates represents an array of positions forming a line string.
    /// Used within MultiLineString geometries.
    /// </summary>
    internal sealed class LineStringCoordinatesSTJConverter : JsonConverter<LineStringCoordinates>
    {
        /// <inheritdoc />
        /// <summary>
        /// Deserializes LineStringCoordinates from a JSON array of Position objects.
        /// </summary>
        public override LineStringCoordinates Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            // A LineStringCoordinate is just an array of positions.
            // System.Text.Json cannot deserialize into a ReadOnlyCollection, so we deserialize into a List first.
            List<Position> positions = JsonSerializer.Deserialize<List<Position>>(ref reader, options);
            return new LineStringCoordinates(positions);
        }

        /// <inheritdoc />
        /// <summary>
        /// Serializes LineStringCoordinates to a JSON array of positions.
        /// </summary>
        public override void Write(
            Utf8JsonWriter writer,
            LineStringCoordinates value,
            JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value.Positions, options);
        }

    }

}
