//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.Spatial
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.Serialization;

    /// <summary>
    /// A <see cref="LinearRing" /> is closed LineString with 4 or more positions. The first and last positions are
    /// equivalent (they represent equivalent points).
    /// Though a <see cref="LinearRing" /> is not explicitly represented as a GeoJSON geometry type, it is referred to in
    /// the <see cref="Polygon"/> geometry type definition in the Azure Cosmos DB service.
    /// </summary>
    /// <see link="https://tools.ietf.org/html/rfc7946#section-3.1.6"/>
    [DataContract]
    internal class LinearRing : IEquatable<LinearRing>, IReadOnlyList<Position>
    {
        private readonly IReadOnlyList<Position> positions;

        /// <summary>
        /// Initializes a new instance of the <see cref="LinearRing" /> class in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="positions">
        /// The coordinates. 4 or more positions. The first and last positions are equivalent (they represent equivalent
        /// points).
        /// </param>
        public LinearRing(IReadOnlyList<Position> positions)
        {
            if (positions == null)
            {
                throw new ArgumentNullException("coordinates");
            }

            if (positions.Count < 4)
            {
                throw new ArgumentException("A linear ring is a closed LineString with four or more positions.");
            }

            if (!positions.First().Equals(positions.Last()))
            {
                throw new ArgumentException("The first and last positions are equivalent, and they MUST contain identical values; their representation SHOULD also be identical.");
            }

            this.positions = positions;
        }

        public int Count => this.positions.Count;

        public Position this[int index] => this.positions[index];

        /// <summary>
        /// Determines whether the specified <see cref="LinearRing"/> is equal to the current <see cref="LinearRing"/> in the Azure Cosmos DB service.
        /// </summary>
        /// <returns>
        /// true if the specified object  is equal to the current object; otherwise, false.
        /// </returns>
        /// <param name="obj">The object to compare with the current object. </param>
        public override bool Equals(object obj) => obj is LinearRing linearRing && this.Equals(linearRing);

        /// <summary>
        /// Serves as a hash function for the <see cref="LinearRing"/> positions in the Azure Cosmos DB service. 
        /// </summary>
        /// <returns>
        /// A hash code for the current <see cref="LinearRing"/>.
        /// </returns>
        public override int GetHashCode()
        {
            unchecked
            {
                return this.positions.Aggregate(0, (current, value) => (current * 397) ^ value.GetHashCode());
            }
        }

        /// <summary>
        /// Determines if this <see cref="LinearRing"/> is equal to the <paramref name="other" /> in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="other"><see cref="LinearRing"/> to compare to this one.</param>
        /// <returns><c>true</c> if linear rings are equal. <c>false</c> otherwise.</returns>
        public bool Equals(LinearRing other)
        {
            if (other is null)
            {
                return false;
            }

            if (object.ReferenceEquals(this, other))
            {
                return true;
            }

            return this.positions.SequenceEqual(other.positions);
        }

        public IEnumerator<Position> GetEnumerator() => this.positions.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => this.positions.GetEnumerator();
    }
}
