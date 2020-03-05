//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Azure.Cosmos.Spatial
{
    using System;
    using System.Collections.ObjectModel;

    /// <summary>
    /// Spatial index specification
    /// </summary>
    public sealed class SpatialPath
    {
        private Collection<SpatialType> spatialTypesInternal;

        /// <summary>
        /// Path in JSON document to index
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Path's spatial type
        /// </summary>
        public Collection<SpatialType> SpatialTypes
        {
            get
            {
                if (this.spatialTypesInternal == null)
                {
                    this.spatialTypesInternal = new Collection<SpatialType>();
                }
                return this.spatialTypesInternal;
            }
            internal set
            {
                if (value == null)
                {
                    throw new ArgumentNullException();
                }

                this.spatialTypesInternal = value;
            }
        }
    }
}
