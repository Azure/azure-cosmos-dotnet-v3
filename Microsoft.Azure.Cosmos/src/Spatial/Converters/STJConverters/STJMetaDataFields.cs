// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Spatial.Converters.STJConverters
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Constants for JSON property names used in spatial geometry serialization.
    /// These field names are part of the GeoJSON specification and must match exactly.
    /// </summary>
    internal static class STJMetaDataFields
    {
        /// <summary>
        /// Position property name.
        /// </summary>
        public const string Position = "position";
        
        /// <summary>
        /// Additional properties field name.
        /// </summary>
        public const string AdditionalProperties = "additionalProperties";
        
        /// <summary>
        /// Coordinate Reference System property name.
        /// </summary>
        public const string Crs = "crs";
        
        /// <summary>
        /// Bounding box property name.
        /// </summary>
        public const string BoundingBox = "boundingBox";
        
        /// <summary>
        /// Geometry type property name.
        /// </summary>
        public const string Type = "type";
        
        /// <summary>
        /// Points array property name (used in MultiPoint).
        /// </summary>
        public const string Points = "points";
        
        /// <summary>
        /// Positions array property name (used in LineString).
        /// </summary>
        public const string Positions = "positions";
        
        /// <summary>
        /// LineStrings array property name (used in MultiLineString).
        /// </summary>
        public const string LineStrings = "lineStrings";
        
        /// <summary>
        /// Rings array property name (used in Polygon).
        /// </summary>
        public const string Rings = "rings";
        
        /// <summary>
        /// Polygons array property name (used in MultiPolygon).
        /// </summary>
        public const string Polygons = "polygons";
        
        /// <summary>
        /// Maximum coordinates in bounding box.
        /// </summary>
        public const string Max = "max";
        
        /// <summary>
        /// Minimum coordinates in bounding box.
        /// </summary>
        public const string Min = "min";
        
        /// <summary>
        /// CRS properties object.
        /// </summary>
        public const string Properties = "properties";
        
        /// <summary>
        /// Name property (used in NamedCrs).
        /// </summary>
        public const string Name = "name";
        
        /// <summary>
        /// Link type property (used in LinkedCrs).
        /// </summary>
        public const string Link = "link";
        
        /// <summary>
        /// Href property (used in LinkedCrs).
        /// </summary>
        public const string Href = "href";
        
        /// <summary>
        /// Geometries array property name (used in GeometryCollection).
        /// </summary>
        public const string Geometries = "geometries";
        
        /// <summary>
        /// Longitude coordinate.
        /// </summary>
        public const string Longitude = "longitude";
        
        /// <summary>
        /// Altitude coordinate.
        /// </summary>
        public const string Altitude = "altitude";
        
        /// <summary>
        /// Latitude coordinate.
        /// </summary>
        public const string Latitude = "latitude";
        
        /// <summary>
        /// Coordinates array property name.
        /// </summary>
        public const string Coordinates = "coordinates";

    }
}
