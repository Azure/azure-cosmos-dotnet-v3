//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.Spatial
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>
    /// Point geometry class in the Azure Cosmos DB service.
    /// </summary>
    /// <see link="https://tools.ietf.org/html/rfc7946#section-3.1.2"/>
    [DataContract]
    internal class Point : GeoJson, IEquatable<Point>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Point" /> class in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="coordinates">
        /// Position of the point.
        /// </param>
        public Point(Position coordinates)
            : this(coordinates, boundingBox: default)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Point" /> class in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="coordinates">
        /// Point coordinates.
        /// </param>
        /// <param name="boundingBox">
        /// Additional geometry parameters.
        /// </param>
        public Point(Position coordinates, BoundingBox boundingBox)
            : base(boundingBox)
        {
            this.Coordinates = coordinates ?? throw new ArgumentNullException(nameof(coordinates));
        }

        /// <inheritdoc/>
        public override GeometryType Type => GeometryType.Point;

        /// <summary>
        /// Gets point coordinates in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// Coordinates of the point.
        /// </value>
        [DataMember(Name = "coordinates")]
        public Position Coordinates { get; }

        /// <summary>
        /// Determines if this <see cref="Point"/> is equal to <paramref name="other" /> in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="other"><see cref="Point"/> to compare to this <see cref="Point"/>.</param>
        /// <returns><c>true</c> if objects are equal. <c>false</c> otherwise.</returns>
        public bool Equals(Point other)
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

            return this.Coordinates.Equals(other.Coordinates);
        }

        public override bool Equals(GeoJson other) => other is Point point && this.Equals(point);

        /// <summary>
        /// Serves as a hash function for the <see cref="Point" /> type in the Azure Cosmos DB service.
        /// </summary>
        /// <returns>
        /// A hash code for the current geometry.
        /// </returns>
        public override int GetHashCode()
        {
            unchecked
            {
                return (base.GetHashCodeBase() * 397) ^ this.Coordinates.GetHashCode();
            }
        }
    }
}
