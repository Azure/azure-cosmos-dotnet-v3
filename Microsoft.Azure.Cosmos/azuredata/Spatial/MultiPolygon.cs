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
    /// Geometry which is comprised of multiple polygons.
    /// </summary>
    /// <seealso cref="Polygon"/>
    /// <see link="https://tools.ietf.org/html/rfc7946#section-3.1.6"/>
    [DataContract]
    internal class MultiPolygon : GeoJson, IEquatable<MultiPolygon>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MultiPolygon"/> class.
        /// </summary>
        /// <param name="polygons">
        /// List of <see cref="PolygonCoordinates"/> instances. Each <see cref="PolygonCoordinates"/> represents separate polygon.
        /// </param>
        public MultiPolygon(IReadOnlyList<PolygonCoordinates> polygons)
            : this(polygons, boundingBox: default)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MultiPolygon"/> class.
        /// </summary>
        /// <param name="polygons">
        /// List of <see cref="PolygonCoordinates"/> instances. Each <see cref="PolygonCoordinates"/> represents separate polygon.
        /// </param>
        /// <param name="boundingBox">Additional geometry parameters.</param>
        public MultiPolygon(IReadOnlyList<PolygonCoordinates> polygons, BoundingBox boundingBox)
            : base(boundingBox)
        {
            this.Coordinates = polygons ?? throw new ArgumentNullException(nameof(polygons));
        }

        /// <inheritdoc/>
        public override GeoJsonType Type => GeoJsonType.MultiPolygon;

        /// <summary>
        /// Gets collection of <see cref="PolygonCoordinates"/> instances. Each <see cref="PolygonCoordinates"/> represents separate polygon.
        /// </summary>
        /// <value>
        /// Collection of <see cref="PolygonCoordinates"/> instances. Each <see cref="PolygonCoordinates"/> represents separate polygon.
        /// </value>
        [DataMember(Name = "coordinates")]
        public IReadOnlyList<PolygonCoordinates> Coordinates { get; }

        /// <summary>
        /// Serves as a hash function for the <see cref="MultiPolygon" /> type.
        /// </summary>
        /// <returns>
        /// A hash code for the current <see cref="MultiPolygon" />.
        /// </returns>
        public override int GetHashCode()
        {
            unchecked
            {
                return this.Coordinates.Aggregate(
                    base.GetHashCodeBase(),
                    (current, value) => (current * 397) ^ value.GetHashCode());
            }
        }

        /// <inheritdoc/>
        public override bool Equals(GeoJson other) => other is MultiPolygon multiPolygon && this.Equals(multiPolygon);

        /// <summary>
        /// Determines if this <see cref="MultiPolygon"/> is equal to <paramref name="other" />.
        /// </summary>
        /// <param name="other"><see cref="MultiPolygon"/> to compare to this <see cref="MultiPolygon"/>.</param>
        /// <returns><c>true</c> if objects are equal. <c>false</c> otherwise.</returns>
        public bool Equals(MultiPolygon other)
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
