//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.Spatial
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Runtime.Serialization;

    /// <summary>
    /// Represents a geometry consisting of other geometries.
    /// </summary>
    [DataContract]
    internal class GeometryCollection : Geometry, IEquatable<GeometryCollection>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GeometryCollection"/> class.
        /// </summary>
        /// <param name="geometries">
        /// Child geometries.
        /// </param>
        public GeometryCollection(IList<Geometry> geometries)
            : this(geometries, boundingBox: default)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GeometryCollection"/> class.
        /// </summary>
        /// <param name="geometries">
        /// Child geometries.
        /// </param>
        /// <param name="boundingBox">
        /// The bounding box.
        /// </param>
        public GeometryCollection(IList<Geometry> geometries, BoundingBox boundingBox)
            : base(boundingBox)
        {
            if (geometries == null)
            {
                throw new ArgumentNullException("geometries");
            }

            this.Geometries = new ReadOnlyCollection<Geometry>(geometries);
        }

        /// <inheritdoc/>
        public override GeometryType Type => GeometryType.GeometryCollection;

        /// <summary>
        /// Gets child geometries.
        /// </summary>
        /// <value>
        /// Child geometries.
        /// </value>
        [DataMember(Name = "geometries")]
        public ReadOnlyCollection<Geometry> Geometries { get; }

        /// <summary>
        /// Determines whether the specified <see cref="GeometryCollection" /> is equal to the current <see cref="GeometryCollection" />.
        /// </summary>
        /// <returns>
        /// true if the specified object is equal to the current object; otherwise, false.
        /// </returns>
        /// <param name="obj">The object to compare with the current object. </param>
        public override bool Equals(object obj)
        {
            return this.Equals(obj as GeometryCollection);
        }

        /// <summary>
        /// Serves as a hash function for the <see cref="GeometryCollection" /> type.
        /// </summary>
        /// <returns>
        /// A hash code for the current <see cref="GeometryCollection" />.
        /// </returns>
        public override int GetHashCode()
        {
            unchecked
            {
                return this.Geometries.Aggregate(base.GetHashCode(), (current, value) => (current * 397) ^ value.GetHashCode());
            }
        }

        /// <summary>
        /// Determines if this <see cref="GeometryCollection" /> is equal to the <paramref name="other" />.
        /// </summary>
        /// <param name="other"><see cref="GeometryCollection" /> to compare to this <see cref="GeometryCollection" />.</param>
        /// <returns><c>true</c> if geometry collections are equal. <c>false</c> otherwise.</returns>
        public bool Equals(GeometryCollection other)
        {
            if (other is null)
            {
                return false;
            }

            if (object.ReferenceEquals(this, other))
            {
                return true;
            }

            return base.Equals(other) && this.Geometries.SequenceEqual(other.Geometries);
        }
    }
}
