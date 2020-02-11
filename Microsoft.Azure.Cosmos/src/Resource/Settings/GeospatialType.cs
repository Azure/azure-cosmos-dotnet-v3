//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// Specifies the supported geospatial types in the Azure Cosmos DB service.
    /// </summary> 
    public enum GeospatialType
    {
        /// <summary>
        /// Represents data in round-earth coordinate system.
        /// </summary>
        Geography,

        /// <summary>
        /// Represents data in Eucledian(flat) coordinate system.
        /// </summary>
        Geometry
    }
}