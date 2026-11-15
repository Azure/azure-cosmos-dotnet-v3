//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    /// <summary>
    /// Defines the target data type of an index path specification in the Azure Cosmos DB service.
    /// </summary>
#if COSMOSCLIENT
    internal
#else
    public
#endif
    enum SpatialType
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
