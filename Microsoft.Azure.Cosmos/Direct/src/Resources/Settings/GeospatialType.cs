//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    /// <summary>
    /// Specifies the supported geospatial types in the Azure Cosmos DB service.
    /// </summary> 
#if COSMOSCLIENT
    internal
#else
    public
#endif
    enum GeospatialType
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