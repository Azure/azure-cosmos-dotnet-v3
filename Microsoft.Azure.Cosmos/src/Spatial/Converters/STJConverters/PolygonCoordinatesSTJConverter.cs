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
    /// Converter used to support System.Text.Json serialization/deserialization of PolygonCoordinates.
    /// PolygonCoordinates represents an array of LinearRings forming a polygon.
    /// The first ring is the exterior boundary, subsequent rings are holes.
    /// Used within MultiPolygon geometries.
    /// </summary>
    internal sealed class PolygonCoordinatesSTJConverter : JsonConverter<PolygonCoordinates>
    {
        /// <inheritdoc />
        /// <summary>
        /// Deserializes PolygonCoordinates from a JSON array of LinearRing objects.
        /// </summary>
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
        /// <summary>
        /// Serializes PolygonCoordinates to a JSON array of LinearRings.
        /// </summary>
        public override void Write(
            Utf8JsonWriter writer,
            PolygonCoordinates value,
            JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value.Rings, options);
        }

    }

}
