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
    /// Represents a geometry consisting of multiple <see cref="LineString"/>.
    /// </summary>
    /// <seealso cref="LineString"/>.
    /// <see link="https://tools.ietf.org/html/rfc7946#section-3.1.5"/>
    [DataContract]
    internal class MultiLineString : GeoJson, IEquatable<MultiLineString>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MultiLineString"/> class. 
        /// </summary>
        /// <param name="coordinates">
        /// List of <see cref="LineStringCoordinates"/> instances.
        /// </param>
        public MultiLineString(IReadOnlyList<LineStringCoordinates> coordinates)
            : this(coordinates, boundingBox: default)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MultiLineString"/> class.
        /// </summary>
        /// <param name="coordinates">
        /// List of <see cref="LineStringCoordinates"/> instances.
        /// </param>
        /// <param name="boundingBox">
        /// Additional geometry parameters.
        /// </param>
        public MultiLineString(IReadOnlyList<LineStringCoordinates> coordinates, BoundingBox boundingBox)
            : base(boundingBox)
        {
            this.Coordinates = coordinates ?? throw new ArgumentNullException(nameof(coordinates));
        }

        /// <inheritdoc/>
        public override GeoJsonType Type => GeoJsonType.MultiLineString;

        /// <summary>
        /// Gets collection of <see cref="LineStringCoordinates"/> representing individual line strings.
        /// </summary>
        /// <value>
        /// Collection of <see cref="LineStringCoordinates"/> representing individual line strings.
        /// </value>
        [DataMember(Name = "coordinates")]
        public IReadOnlyList<LineStringCoordinates> Coordinates { get; private set; }

        /// <summary>
        /// Serves as a hash function for the <see cref="MultiLineString" /> type.
        /// </summary>
        /// <returns>
        /// A hash code for the current <see cref="MultiLineString"/>.
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
        public override bool Equals(GeoJson other) => other is MultiLineString multiLineString && this.Equals(multiLineString);

        /// <summary>
        /// Determines if this <see cref="MultiLineString"/> is equal to <paramref name="other" />.
        /// </summary>
        /// <param name="other"><see cref="MultiLineString"/> to compare to this <see cref="MultiLineString"/>.</param>
        /// <returns><c>true</c> if line strings are equal. <c>false</c> otherwise.</returns>
        public bool Equals(MultiLineString other)
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
