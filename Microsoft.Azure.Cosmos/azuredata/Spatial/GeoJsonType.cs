//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.Spatial
{
    /// <summary>
    /// Geometry type in the Azure Cosmos DB service.
    /// </summary>
    internal enum GeoJsonType
    {
        /// <summary>
        /// Represents single point.
        /// </summary>
        /// <see link="https://tools.ietf.org/html/rfc7946#section-3.1.2"/>
        Point,

        /// <summary>
        /// Represents geometry consisting of several points.
        /// </summary>
        /// <see link="https://tools.ietf.org/html/rfc7946#section-3.1.3"/>
        MultiPoint,

        /// <summary>
        /// Sequence of connected line segments.
        /// </summary>
        /// <see link="https://tools.ietf.org/html/rfc7946#section-3.1.4"/>
        LineString,

        /// <summary>
        /// Geometry consisting of several LineStrings.
        /// </summary>
        /// <see link="https://tools.ietf.org/html/rfc7946#section-3.1.5"/>
        MultiLineString,

        /// <summary>
        /// Represents a polygon with optional holes.
        /// </summary>
        /// <see link="https://tools.ietf.org/html/rfc7946#section-3.1.6"/>
        Polygon,

        /// <summary>
        /// Represents a geometry comprised of several polygons.
        /// </summary>
        /// <see link="https://tools.ietf.org/html/rfc7946#section-3.1.7"/>
        MultiPolygon,

        /// <summary>
        /// Represents a geometry comprised of other geometries.
        /// </summary>
        /// <see link="https://tools.ietf.org/html/rfc7946#section-3.1.8"/>
        GeometryCollection
    }
}
