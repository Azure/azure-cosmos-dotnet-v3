//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.Spatial
{
    /// <summary>
    /// Geometry type in the Azure Cosmos DB service.
    /// </summary>
    internal enum GeometryType
    {
        /// <summary>
        /// Represents single point.
        /// </summary>
        Point,

        /// <summary>
        /// Represents geometry consisting of several points.
        /// </summary>
        MultiPoint,

        /// <summary>
        /// Sequence of connected line segments.
        /// </summary>
        LineString,

        /// <summary>
        /// Geometry consisting of several LineStrings.
        /// </summary>
        MultiLineString,

        /// <summary>
        /// Represents a polygon with optional holes.
        /// </summary>
        Polygon,

        /// <summary>
        /// Represents a geometry comprised of several polygons.
        /// </summary>
        MultiPolygon,

        /// <summary>
        /// Represents a geometry comprised of other geometries.
        /// </summary>
        GeometryCollection
    }
}
