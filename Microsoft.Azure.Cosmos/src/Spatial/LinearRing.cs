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
    using Microsoft.Azure.Cosmos.Spatial.Converters;
    using Newtonsoft.Json;

    /// <summary>
    /// A <see cref="LinearRing" /> is closed LineString with 4 or more positions. The first and last positions are
    /// equivalent (they represent equivalent points).
    /// Though a <see cref="LinearRing" /> is not explicitly represented as a GeoJSON geometry type, it is referred to in
    /// the <see cref="Polygon"/> geometry type definition in the Azure Cosmos DB service.
    /// </summary>
    [DataContract]
    [JsonConverter(typeof(LinearRingJsonConverter))]
    public sealed class LinearRing : IEquatable<LinearRing>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LinearRing" /> class in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="coordinates">
        /// The coordinates. 4 or more positions. The first and last positions are equivalent (they represent equivalent
        /// points).
        /// </param>
        public LinearRing(IList<Position> coordinates)
        {
            if (coordinates == null)
            {
                throw new ArgumentNullException("coordinates");
            }

            this.Positions = new ReadOnlyCollection<Position>(coordinates);
        }

        /// <summary>
        /// Gets the <see cref="LinearRing"/> positions in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// Positions of the <see cref="LinearRing"/>.
        /// </value>
        [DataMember(Name = "coordinates")]
        public ReadOnlyCollection<Position> Positions { get; private set; }

        /// <summary>
        /// Determines whether the specified <see cref="LinearRing"/> is equal to the current <see cref="LinearRing"/> in the Azure Cosmos DB service.
        /// </summary>
        /// <returns>
        /// true if the specified object  is equal to the current object; otherwise, false.
        /// </returns>
        /// <param name="obj">The object to compare with the current object. </param>
        public override bool Equals(object obj)
        {
            return this.Equals(obj as LinearRing);
        }

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
                return this.Positions.Aggregate(0, (current, value) => (current * 397) ^ value.GetHashCode());
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

            return this.Positions.SequenceEqual(other.Positions);
        }
    }
}
