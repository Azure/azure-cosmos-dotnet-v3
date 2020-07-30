//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.Spatial
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>
    /// Base class for spatial geometry objects in the Azure Cosmos DB service.
    /// </summary>
    [DataContract]
    internal abstract class GeoJson : IEquatable<GeoJson>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GeoJson" /> class in the Azure Cosmos DB service.
        /// </summary>
        protected GeoJson()
            : this(boundingBox: default)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GeoJson" /> class in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="boundingBox">Bounding box.</param>
        protected GeoJson(BoundingBox boundingBox)
        {
            this.BoundingBox = boundingBox;
        }

        /// <summary>
        /// Gets geometry type in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// Type of geometry.
        /// </value>
        [DataMember(Name = "type")]
        public abstract GeoJsonType Type { get; }

        /// <summary>
        /// Gets bounding box for this geometry in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// Bounding box of the geometry.
        /// </value>
        [DataMember(Name = "bbox")]
        public BoundingBox BoundingBox { get; }

        /// <summary>
        /// Determines whether the specified <see cref="GeoJson" /> is equal to the current <see cref="GeoJson" /> in the Azure Cosmos DB service.
        /// </summary>
        /// <returns>
        /// true if the specified object  is equal to the current object; otherwise, false.
        /// </returns>
        /// <param name="obj">The object to compare with the current object. </param>
        public override bool Equals(object obj) => obj is GeoJson geometry && this.Equals(geometry);

        /// <summary>
        /// Serves as a hash function for the <see cref="GeoJson" /> type in the Azure Cosmos DB service.
        /// </summary>
        /// <returns>
        /// A hash code for the current geometry.
        /// </returns>
        public override abstract int GetHashCode();

        protected int GetHashCodeBase()
        {
            unchecked
            {
                int hashCode = this.Type.GetHashCode();
                hashCode = (hashCode * 397) ^ (this.BoundingBox != null ? this.BoundingBox.GetHashCode() : 0);

                return hashCode;
            }
        }

        /// <summary>
        /// Determines if this <see cref="GeoJson" /> is equal to the <paramref name="other" /> in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="other"><see cref="GeoJson" /> to compare to this <see cref="GeoJson" />.</param>
        /// <returns><c>true</c> if geometries are equal. <c>false</c> otherwise.</returns>
        public abstract bool Equals(GeoJson other);

        protected bool EqualsBase(GeoJson other)
        {
            return this.Type == other.Type && this.BoundingBox.Equals(other.BoundingBox);
        }
    }
}
