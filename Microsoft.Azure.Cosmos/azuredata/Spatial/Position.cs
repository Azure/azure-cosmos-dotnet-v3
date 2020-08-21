//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.Spatial
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Runtime.Serialization;

    /// <summary>
    /// <para>
    /// A position is represented by an array of numbers in the Azure Cosmos DB service. There must be at least two elements, and may be more.
    /// </para>
    /// <para>
    /// The order of elements must follow longitude, latitude, altitude.
    /// Any number of additional elements are allowed - interpretation and meaning of additional elements is up to the application.
    /// </para>
    /// </summary>
    /// <see link="https://tools.ietf.org/html/rfc7946#section-3.1.1"/>
    [DataContract]
    internal sealed class Position : IEquatable<Position>, IReadOnlyList<double>
    {
        private readonly IReadOnlyList<double> elements;

        /// <summary>
        /// Initializes a new instance of the <see cref="Position"/> class in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="easting">Longitude value.</param>
        /// <param name="northing">Latitude value.</param>
        public Position(double easting, double northing)
            : this(easting, northing, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Position"/> class in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="easting">Longitude value.</param>
        /// <param name="northing">Latitude value.</param>
        /// <param name="elevation">Optional altitude value.</param>
        public Position(double easting, double northing, double? elevation)
        {
            if (elevation.HasValue)
            {
                this.elements = new double[] { easting, northing, elevation.Value };
            }
            else
            {
                this.elements = new double[] { easting, northing };
            }
        }

        public double this[int index] => this.elements[index];

        /// <summary>
        /// Gets longitude in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// Longitude value.
        /// </value>
        public double Easting => this.elements[0];

        /// <summary>
        /// Gets latitude in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// Latitude value.
        /// </value>
        public double Northing => this.elements[1];

        /// <summary>
        /// Gets optional altitude in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// Altitude value.
        /// </value>
        public double? Elevation => this.elements.Count == 3 ? (double?)this.elements[2] : (double?)null;

        public int Count => this.elements.Count;

        /// <summary>
        /// Determines whether the specified <see cref="Position"/> is equal to the current <see cref="Position"/> in the Azure Cosmos DB service.
        /// </summary>
        /// <returns>
        /// true if the specified <see cref="Position"/> is equal to the current object; otherwise, false.
        /// </returns>
        /// <param name="obj">The <see cref="Position"/> to compare to the current object. </param>
        public override bool Equals(object obj) => obj is Position position && this.Equals(position);

        /// <summary>
        /// Serves as a hash function for the <see cref="Position" /> type in the Azure Cosmos DB service.
        /// </summary>
        /// <returns>
        /// A hash code for the current <see cref="Position"/>.
        /// </returns>
        public override int GetHashCode()
        {
            unchecked
            {
                return this.elements.Aggregate(0, (current, value) => (current * 397) ^ value.GetHashCode());
            }
        }

        /// <summary>
        /// Determines if this <see cref="Position"/> is equal to the <paramref name="other" /> in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="other"><see cref="Position"/> to compare to this <see cref="Position"/>.</param>
        /// <returns><c>true</c> if objects are equal. <c>false</c> otherwise.</returns>
        public bool Equals(Position other)
        {
            if (other is null)
            {
                return false;
            }

            if (object.ReferenceEquals(this, other))
            {
                return true;
            }

            return this.elements.SequenceEqual(other.elements);
        }

        public IEnumerator<double> GetEnumerator() => this.elements.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    }
}
