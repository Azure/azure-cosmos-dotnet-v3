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
    /// Polygon coordinates.
    /// </summary>
    /// <seealso cref="MultiPolygon"/>
    [DataContract]
    internal sealed class PolygonCoordinates : IEquatable<PolygonCoordinates>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PolygonCoordinates"/> class.
        /// </summary>
        /// <param name="rings">
        /// The rings of the polygon.
        /// </param>
        public PolygonCoordinates(IList<LinearRing> rings)
        {
            if (rings == null)
            {
                throw new ArgumentException("rings");
            }

            this.Rings = new ReadOnlyCollection<LinearRing>(rings);
        }

        /// <summary>
        /// Gets polygon rings.
        /// </summary>
        /// <value>
        /// Rings of the polygon.
        /// </value>
        [DataMember(Name = "rings")]
        public ReadOnlyCollection<LinearRing> Rings { get; private set; }

        /// <summary>
        /// Determines whether the specified <see cref="PolygonCoordinates"/> is equal to the current <see cref="PolygonCoordinates"/>.
        /// </summary>
        /// <returns>
        /// true if the specified object  is equal to the current object; otherwise, false.
        /// </returns>
        /// <param name="obj">The object to compare with the current object. </param>
        public override bool Equals(object obj)
        {
            return this.Equals(obj as PolygonCoordinates);
        }

        /// <summary>
        /// Serves as a hash function for a <see cref="PolygonCoordinates"/>. 
        /// </summary>
        /// <returns>
        /// A hash code for the current <see cref="T:System.Object"/>.
        /// </returns>
        public override int GetHashCode()
        {
            unchecked
            {
                return this.Rings.Aggregate(0, (current, value) => (current * 397) ^ value.GetHashCode());
            }
        }

        /// <summary>
        /// Determines if this <see cref="PolygonCoordinates"/> is equal to the <paramref name="other" />.
        /// </summary>
        /// <param name="other"><see cref="PolygonCoordinates"/> to compare to this <see cref="PolygonCoordinates"/>.</param>
        /// <returns><c>true</c> if objects are equal. <c>false</c> otherwise.</returns>
        public bool Equals(PolygonCoordinates other)
        {
            if (object.ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return this.Rings.SequenceEqual(other.Rings);
        }
    }
}
