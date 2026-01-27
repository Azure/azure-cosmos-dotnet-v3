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
    /// Converter used to support System.Text.Json de/serialization of type PolygonCoordinates/>.
    /// </summary>
    internal sealed class PolygonCoordinatesSTJConverter : JsonConverter<PolygonCoordinates>
    {
        /// <inheritdoc />
        public override PolygonCoordinates Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            // A PolygonCoordinate is just an array of LinearRings.
            // System.Text.Json cannot deserialize into a ReadOnlyCollection, so we deserialize into a List first.
            List<LinearRing> rings = JsonSerializer.Deserialize<List<LinearRing>>(ref reader, options);
            return new PolygonCoordinates(rings);
        }

        /// <inheritdoc />
        public override void Write(
            Utf8JsonWriter writer,
            PolygonCoordinates value,
            JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value.Rings, options);
        }

    }

}
