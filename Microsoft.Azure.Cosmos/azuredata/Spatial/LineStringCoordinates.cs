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
    /// Line string coordinates.
    /// </summary>
    /// <seealso cref="MultiLineString"/>
    /// <see link="https://tools.ietf.org/html/rfc7946#section-3.1.5"/>
    [DataContract]
    internal sealed class LineStringCoordinates : IEquatable<LineStringCoordinates>, IReadOnlyList<Position>
    {
        private readonly IReadOnlyList<Position> positions;

        /// <summary>
        /// Initializes a new instance of the <see cref="LineStringCoordinates"/> class.
        /// </summary>
        /// <param name="positions">
        /// Line string positions..
        /// </param>
        public LineStringCoordinates(IReadOnlyList<Position> positions)
        {
            this.positions = positions ?? throw new ArgumentException(nameof(positions));
        }

        /// <summary>
        /// Determines whether the specified <see cref="LineStringCoordinates"/> is equal to the current <see cref="LineStringCoordinates"/>.
        /// </summary>
        /// <returns>
        /// true if the specified object is equal to the current object; otherwise, false.
        /// </returns>
        /// <param name="obj">The object to compare with the current object. </param>
        public override bool Equals(object obj) => obj is LineStringCoordinates lineStringCoordinates && this.Equals(lineStringCoordinates);

        public int Count => this.positions.Count;

        public Position this[int index] => this.positions[index];

        /// <summary>
        /// Serves as a hash function for <see cref="LineStringCoordinates"/>.
        /// </summary>
        /// <returns>
        /// A hash code for the current <see cref="LineStringCoordinates"/>.
        /// </returns>
        public override int GetHashCode()
        {
            unchecked
            {
                return this.positions.Aggregate(0, (current, value) => (current * 397) ^ value.GetHashCode());
            }
        }

        /// <summary>
        /// Determines if this <see cref="LineStringCoordinates"/> is equal to the <paramref name="other" />.
        /// </summary>
        /// <param name="other"><see cref="LineStringCoordinates"/> to compare to this <see cref="LineStringCoordinates"/>.</param>
        /// <returns><c>true</c> if objects are equal. <c>false</c> otherwise.</returns>
        public bool Equals(LineStringCoordinates other)
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

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    }
}
