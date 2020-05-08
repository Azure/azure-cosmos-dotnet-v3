//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Spatial
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Azure.Cosmos.Spatial;

    internal sealed class TextJsonGeometryConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
        {
            return typeToConvert == typeof(Geometry)
                || typeToConvert == typeof(GeometryCollection)
                || typeToConvert == typeof(LineString)
                || typeToConvert == typeof(MultiLineString)
                || typeToConvert == typeof(Point)
                || typeToConvert == typeof(MultiPoint)
                || typeToConvert == typeof(Polygon)
                || typeToConvert == typeof(MultiPolygon);
        }

        public override JsonConverter CreateConverter(
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            return new TextJsonGeometryConverter();
        }
    }
}
