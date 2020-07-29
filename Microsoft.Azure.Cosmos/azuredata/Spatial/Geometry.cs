//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.Spatial
{
    using System.Runtime.Serialization;

    /// <summary>
    /// Base class for spatial geometry objects in the Azure Cosmos DB service.
    /// </summary>
    [DataContract]
    internal abstract class Geometry
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Geometry" /> class in the Azure Cosmos DB service.
        /// </summary>
        protected Geometry()
            : this(boundingBox: default)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Geometry" /> class in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="boundingBox">Bounding box.</param>
        protected Geometry(BoundingBox boundingBox)
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
        public abstract GeometryType Type { get; }

        /// <summary>
        /// Gets bounding box for this geometry in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// Bounding box of the geometry.
        /// </value>
        [DataMember(Name = "bbox")]
        public BoundingBox BoundingBox { get; }

        /// <summary>
        /// Determines whether the specified <see cref="Geometry" /> is equal to the current <see cref="Geometry" /> in the Azure Cosmos DB service.
        /// </summary>
        /// <returns>
        /// true if the specified object  is equal to the current object; otherwise, false.
        /// </returns>
        /// <param name="obj">The object to compare with the current object. </param>
        public override bool Equals(object obj) => obj is Geometry geometry && this.Equals(geometry);

        /// <summary>
        /// Serves as a hash function for the <see cref="Geometry" /> type in the Azure Cosmos DB service.
        /// </summary>
        /// <returns>
        /// A hash code for the current geometry.
        /// </returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = this.Type.GetHashCode();
                hashCode = (hashCode * 397) ^ (this.BoundingBox != null ? this.BoundingBox.GetHashCode() : 0);

                return hashCode;
            }
        }

        /// <summary>
        /// Determines if this <see cref="Geometry" /> is equal to the <paramref name="other" /> in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="other"><see cref="Geometry" /> to compare to this <see cref="Geometry" />.</param>
        /// <returns><c>true</c> if geometries are equal. <c>false</c> otherwise.</returns>
        private bool Equals(Geometry other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return this.Type == other.Type && this.BoundingBox.Equals(other.BoundingBox);
        }
    }
}
