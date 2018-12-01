//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Spatial
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Not frequently used geometry parameters in the Azure Cosmos DB service.
    /// </summary>
    internal class GeometryParams
    {
        /// <summary>
        /// Gets or sets any additional properties to be stored as part of a geometry in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// Additional geometry properties.
        /// </value>
        public IDictionary<string, object> AdditionalProperties { get; set; }

        /// <summary>
        /// Gets or sets Coordinate Reference System for the geometry in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// Coordinate Reference System for the geometry.
        /// </value>
        public Crs Crs { get; set; }

        /// <summary>
        /// Gets or sets a bounding box for the geometry in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// Bounding box for the geometry.
        /// </value>
        public BoundingBox BoundingBox { get; set; }
    }
}
