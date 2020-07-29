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
    /// Geometry consisting of several points.
    /// </summary>
    /// <seealso cref="Point"/>.
    [DataContract]
    internal class MultiPoint : Geometry, IEquatable<MultiPoint>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MultiPoint" /> class.
        /// </summary>
        /// <param name="points">List of <see cref="Position"/> representing individual points.</param>
        public MultiPoint(IList<Position> points)
            : this(points, boundingBox: default)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MultiPoint" /> class.
        /// </summary>
        /// <param name="points">
        /// List of <see cref="Position"/> representing individual points.
        /// </param>
        /// <param name="boundingBox">
        /// Additional geometry parameters.
        /// </param>
        public MultiPoint(IList<Position> points, BoundingBox boundingBox)
            : base(boundingBox)
        {
            if (points == null)
            {
                throw new ArgumentNullException("points");
            }

            this.Points = new ReadOnlyCollection<Position>(points);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MultiPoint"/> class.
        /// </summary>
        /// <remarks>
        /// This constructor is used only during deserialization.
        /// </remarks>
        internal MultiPoint()
            : base(boundingBox: default)
        {
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
        public ReadOnlyCollection<Position> Points { get; private set; }

        /// <summary>
        /// Determines whether the specified <see cref="MultiPoint" /> is equal to the current <see cref="MultiPoint" />.
        /// </summary>
        /// <returns>
        /// true if the specified object  is equal to the current object; otherwise, false.
        /// </returns>
        /// <param name="obj">The object to compare with the current object. </param>
        public override bool Equals(object obj)
        {
            return this.Equals(obj as MultiPoint);
        }

        /// <summary>
        /// Serves as a hash function for the <see cref="MultiPoint" /> type.
        /// </summary>
        /// <returns>
        /// A hash code for the current geometry.
        /// </returns>
        public override int GetHashCode()
        {
            return this.Points.Aggregate(base.GetHashCode(), (current, point) => (current * 397) ^ point.GetHashCode());
        }

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

            return base.Equals(other) && this.Points.SequenceEqual(other.Points);
        }
    }
}
