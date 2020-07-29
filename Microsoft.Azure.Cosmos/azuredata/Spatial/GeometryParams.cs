//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.Spatial
{
    using System.Runtime.Serialization;

    /// <summary>
    /// Not frequently used geometry parameters in the Azure Cosmos DB service.
    /// </summary>
    [DataContract]
    internal class GeometryParams
    {
        /// <summary>
        /// Gets or sets a bounding box for the geometry in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// Bounding box for the geometry.
        /// </value>
        [DataMember(Name = "bbox")]
        public BoundingBox BoundingBox { get; set; }
    }
}
