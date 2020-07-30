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
    /// <see link="https://tools.ietf.org/html/rfc7946#section-3.1.8"/>
    [DataContract]
    internal class GeometryCollection : GeoJson, IEquatable<GeometryCollection>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GeometryCollection"/> class.
        /// </summary>
        /// <param name="geometries">
        /// Child geometries.
        /// </param>
        public GeometryCollection(IReadOnlyList<GeoJson> geometries)
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
        public GeometryCollection(IReadOnlyList<GeoJson> geometries, BoundingBox boundingBox)
            : base(boundingBox)
        {
            this.Geometries = new List<GeoJson>(geometries ?? throw new ArgumentNullException(nameof(geometries)));
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
        public IReadOnlyList<GeoJson> Geometries { get; }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                return this.Geometries.Aggregate(base.GetHashCodeBase(), (current, value) => (current * 397) ^ value.GetHashCode());
            }
        }

        /// <inheritdoc/>
        public override bool Equals(GeoJson other) => other is GeometryCollection geometryCollection && this.Equals(geometryCollection);

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

            if (!base.EqualsBase(other))
            {
                return false;
            }

            return this.Geometries.SequenceEqual(other.Geometries);
        }
    }
}
