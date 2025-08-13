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
    internal sealed class LineStringCoordinatesSTJConverter : JsonConverter<LineStringCoordinates>
    {
        /// <inheritdoc />
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
        public override void Write(
            Utf8JsonWriter writer,
            LineStringCoordinates value,
            JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value.Positions, options);
        }

    }

}
