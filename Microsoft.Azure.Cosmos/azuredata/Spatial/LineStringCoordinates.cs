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
    /// Line string coordinates.
    /// </summary>
    /// <seealso cref="MultiLineString"/>
    [DataContract]
    internal sealed class LineStringCoordinates : IEquatable<LineStringCoordinates>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LineStringCoordinates"/> class.
        /// </summary>
        /// <param name="positions">
        /// Line string positions..
        /// </param>
        public LineStringCoordinates(IList<Position> positions)
        {
            if (positions == null)
            {
                throw new ArgumentException("points");
            }

            this.Positions = new ReadOnlyCollection<Position>(positions);
        }

        /// <summary>
        /// Gets line string positions.
        /// </summary>
        /// <value>
        /// Positions of the line string.
        /// </value>
        [DataMember(Name = "coordinates")]
        public ReadOnlyCollection<Position> Positions { get; private set; }

        /// <summary>
        /// Determines whether the specified <see cref="LineStringCoordinates"/> is equal to the current <see cref="LineStringCoordinates"/>.
        /// </summary>
        /// <returns>
        /// true if the specified object is equal to the current object; otherwise, false.
        /// </returns>
        /// <param name="obj">The object to compare with the current object. </param>
        public override bool Equals(object obj)
        {
            return this.Equals(obj as LineStringCoordinates);
        }

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
                return this.Positions.Aggregate(0, (current, value) => (current * 397) ^ value.GetHashCode());
            }
        }

        /// <summary>
        /// Determines if this <see cref="LineStringCoordinates"/> is equal to the <paramref name="other" />.
        /// </summary>
        /// <param name="other"><see cref="LineStringCoordinates"/> to compare to this <see cref="LineStringCoordinates"/>.</param>
        /// <returns><c>true</c> if objects are equal. <c>false</c> otherwise.</returns>
        public bool Equals(LineStringCoordinates other)
        {
            if (object.ReferenceEquals(null, other))
            {
                return false;
            }

            if (object.ReferenceEquals(this, other))
            {
                return true;
            }

            return this.Positions.SequenceEqual(other.Positions);
        }
    }
}
