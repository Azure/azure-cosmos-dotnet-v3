//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Spatial
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Runtime.Serialization;
    using Converters;
    using Newtonsoft.Json;

    /// <summary>
    /// <para>
    /// A position is represented by an array of numbers in the Azure Cosmos DB service. There must be at least two elements, and may be more.
    /// </para>
    /// <para>
    /// The order of elements must follow longitude, latitude, altitude.
    /// Any number of additional elements are allowed - interpretation and meaning of additional elements is up to the application.
    /// </para>
    /// </summary>
    [DataContract]
    [JsonConverter(typeof(PositionJsonConverter))]
    public sealed class Position : IEquatable<Position>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Position"/> class in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="longitude">
        /// Longitude value.
        /// </param>
        /// <param name="latitude">
        /// Latitude value.
        /// </param>
        public Position(double longitude, double latitude)
            : this(longitude, latitude, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Position"/> class in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="longitude">
        /// Longitude value.
        /// </param>
        /// <param name="latitude">
        /// Latitude value.
        /// </param>
        /// <param name="altitude">
        /// Optional altitude value.
        /// </param>
        public Position(double longitude, double latitude, double? altitude)
        {
            if (altitude != null)
            {
                this.Coordinates = new ReadOnlyCollection<double>(new[] { longitude, latitude, altitude.Value });
            }
            else
            {
                this.Coordinates = new ReadOnlyCollection<double>(new[] { longitude, latitude });
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Position"/> class in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="coordinates">
        /// Position values.
        /// </param>
        public Position(IList<double> coordinates)
        {
            if (coordinates.Count < 2)
            {
                throw new ArgumentException(nameof(coordinates));
            }

            this.Coordinates = new ReadOnlyCollection<double>(coordinates);
        }

        /// <summary>
        /// Gets position coordinates in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// Coordinate values.
        /// </value>
        [DataMember(Name = "Coordinates")]
        public ReadOnlyCollection<double> Coordinates { get; private set; }

        /// <summary>
        /// Gets longitude in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// Longitude value.
        /// </value>
        public double Longitude => this.Coordinates[0];

        /// <summary>
        /// Gets latitude in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// Latitude value.
        /// </value>
        public double Latitude => this.Coordinates[1];

        /// <summary>
        /// Gets optional altitude in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// Altitude value.
        /// </value>
        public double? Altitude => this.Coordinates.Count > 2 ? (double?)this.Coordinates[2] : null;

        /// <summary>
        /// Determines whether the specified <see cref="Position"/> is equal to the current <see cref="Position"/> in the Azure Cosmos DB service.
        /// </summary>
        /// <returns>
        /// true if the specified <see cref="Position"/> is equal to the current object; otherwise, false.
        /// </returns>
        /// <param name="obj">The <see cref="Position"/> to compare to the current object. </param>
        public override bool Equals(object obj)
        {
            return this.Equals(obj as Position);
        }

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
                return this.Coordinates.Aggregate(0, (current, value) => (current * 397) ^ value.GetHashCode());
            }
        }

        /// <summary>
        /// Determines if this <see cref="Position"/> is equal to the <paramref name="other" /> in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="other"><see cref="Position"/> to compare to this <see cref="Position"/>.</param>
        /// <returns><c>true</c> if objects are equal. <c>false</c> otherwise.</returns>
        public bool Equals(Position other)
        {
            if (object.ReferenceEquals(null, other))
            {
                return false;
            }

            if (object.ReferenceEquals(this, other))
            {
                return true;
            }

            return this.Coordinates.SequenceEqual(other.Coordinates);
        }
    }
}
