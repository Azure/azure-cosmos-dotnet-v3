//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.Spatial
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.Serialization;

    /// <summary>
    /// Geometry consisting of several points.
    /// </summary>
    /// <seealso cref="Point"/>.
    /// <see link="https://tools.ietf.org/html/rfc7946#section-3.1.3"/>
    [DataContract]
    internal class MultiPoint : Geometry, IEquatable<MultiPoint>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MultiPoint" /> class.
        /// </summary>
        /// <param name="coordinates">List of <see cref="Position"/>.</param>
        public MultiPoint(IReadOnlyList<Position> coordinates)
            : this(coordinates, boundingBox: default)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MultiPoint" /> class.
        /// </summary>
        /// <param name="coordinates">
        /// List of <see cref="Position"/>.
        /// </param>
        /// <param name="boundingBox">
        /// Additional geometry parameters.
        /// </param>
        public MultiPoint(IReadOnlyList<Position> coordinates, BoundingBox boundingBox)
            : base(boundingBox)
        {
            this.Coordinates = coordinates ?? throw new ArgumentNullException("points");
        }

        /// <inheritdoc/>
        public override GeometryType Type => GeometryType.MultiPoint;

        /// <summary>
        /// Gets collections of <see cref="Position"/> representing individual points.
        /// </summary>
        /// <value>
        /// Collections of <see cref="Position"/> representing individual points.
        /// </value>
        [DataMember(Name = "coordinates")]
        public IReadOnlyList<Position> Coordinates { get; private set; }

        /// <summary>
        /// Serves as a hash function for the <see cref="MultiPoint" /> type.
        /// </summary>
        /// <returns>
        /// A hash code for the current geometry.
        /// </returns>
        public override int GetHashCode()
        {
            return this.Coordinates.Aggregate(
                base.GetHashCodeBase(),
                (current, point) => (current * 397) ^ point.GetHashCode());
        }

        public override bool Equals(Geometry other) => other is MultiPoint multiPoint && this.Equals(multiPoint);

        /// <summary>
        /// Determines if this <see cref="MultiPoint"/> is equal to <paramref name="other" />.
        /// </summary>
        /// <param name="other"> <see cref="MultiPoint"/> to compare to this <see cref="MultiPoint"/>.</param>
        /// <returns><c>true</c> if objects are equal. <c>false</c> otherwise.</returns>
        public bool Equals(MultiPoint other)
        {
            if (other is null)
            {
                return false;
            }

            if (object.ReferenceEquals(this, other))
            {
                return true;
            }

            if (!base.EqualsBase(other))
            {
                return false;
            }

            return this.Coordinates.SequenceEqual(other.Coordinates);
        }
    }
}
