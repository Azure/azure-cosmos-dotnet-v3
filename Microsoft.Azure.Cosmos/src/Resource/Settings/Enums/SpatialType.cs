//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
#if AZURECORE
namespace Azure.Cosmos
#else
namespace Microsoft.Azure.Cosmos
#endif
{
    /// <summary>
    /// Defines the target data type of an index path specification in the Azure Cosmos DB service.
    /// </summary>
    public enum SpatialType
    {
        /// <summary>
        /// Represent a point data type.
        /// </summary>
        Point,

        /// <summary>
        /// Represent a line string data type.
        /// </summary>
        LineString,

        /// <summary>
        /// Represent a polygon data type.
        /// </summary>
        Polygon,

        /// <summary>
        /// Represent a multi-polygon data type.
        /// </summary>
        MultiPolygon,
    }
}
