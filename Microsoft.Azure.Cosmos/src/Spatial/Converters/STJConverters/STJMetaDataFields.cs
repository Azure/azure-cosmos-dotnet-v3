// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Spatial.Converters.STJConverters
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    internal class STJMetaDataFields
    {
        private STJMetaDataFields() 
        {
            throw new InvalidOperationException(" cannot instantitate utility class. use it as static reference");
        }

        public const string Position = "position";
        public const string AdditionalProperties = "additionalProperties";
        public const string Crs = "crs";
        public const string BoundingBox = "boundingBox";
        public const string Type = "type";
        public const string Points = "points";
        public const string Positions = "positions";
        public const string LineStrings = "lineStrings";
        public const string Rings = "rings";
        public const string Polygons = "polygons";
        public const string Max = "max";
        public const string Min = "min";
        public const string Properties = "properties";
        public const string Name = "name";
        public const string Link = "link";
        public const string Href = "href";
        public const string Geometries = "geometries";
        public const string Longitude = "longitude";
        public const string Altitude = "altitude";
        public const string Latitude = "latitude";
        public const string Coordinates = "coordinates";

    }
}
