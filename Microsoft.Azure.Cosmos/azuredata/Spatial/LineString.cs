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
    /// Represents a geometry consisting of connected line segments.
    /// </summary>
    /// <see link="https://tools.ietf.org/html/rfc7946#section-3.1.4"/>
    [DataContract]
    internal class LineString : GeoJson, IEquatable<LineString>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LineString"/> class. 
        /// </summary>
        /// <param name="coordinates">
        /// List of positions through which the line string goes.
        /// </param>
        public LineString(IReadOnlyList<Position> coordinates)
            : this(coordinates, boundingBox: default)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LineString"/> class.
        /// </summary>
        /// <param name="coordinates">
        /// The coordinates.
        /// </param>
        /// <param name="boundingBox">
        /// Additional geometry parameters.
        /// </param>
        public LineString(IReadOnlyList<Position> coordinates, BoundingBox boundingBox)
            : base(boundingBox)
        {
            if (coordinates == null)
            {
                throw new ArgumentNullException(nameof(coordinates));
            }

            if (coordinates.Count < 2)
            {
                throw new ArgumentException("The \"coordinates\" member is an array of two or more positions.");
            }

            this.Coordinates = coordinates;
        }

        /// <inheritdoc/>
        public override GeoJsonType Type => GeoJsonType.LineString;

        /// <summary>
        /// Gets line string positions.
        /// </summary>
        /// <value>
        /// Positions of the line string.
        /// </value>
        [DataMember(Name = "coordinates")]
        public IReadOnlyList<Position> Coordinates { get; }

        /// <summary>
        /// Serves as a hash function for the <see cref="LineString" /> type.
        /// </summary>
        /// <returns>
        /// A hash code for the current <see cref="LineString"/>.
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

        public override bool Equals(GeoJson other) => other is LineString lineString && this.Equals(lineString);

        /// <summary>
        /// Determines if this <see cref="LineString"/> is equal to the <paramref name="other" />.
        /// </summary>
        /// <param name="other">LineString to compare to this <see cref="LineString"/>.</param>
        /// <returns><c>true</c> if line strings are equal. <c>false</c> otherwise.</returns>
        public bool Equals(LineString other)
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
