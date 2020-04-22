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
    using Newtonsoft.Json;

    /// <summary>
    /// Represents a geometry consisting of connected line segments.
    /// </summary>
    [DataContract]
    public sealed class LineString : Geometry, IEquatable<LineString>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LineString"/> class. 
        /// </summary>
        /// <param name="coordinates">
        /// List of positions through which the line string goes.
        /// </param>
        public LineString(IList<Position> coordinates)
            : this(coordinates, new GeometryParams())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LineString"/> class.
        /// </summary>
        /// <param name="coordinates">
        /// The coordinates.
        /// </param>
        /// <param name="geometryParams">
        /// Additional geometry parameters.
        /// </param>
        public LineString(IList<Position> coordinates, GeometryParams geometryParams)
            : base(GeometryType.LineString, geometryParams)
        {
            if (coordinates == null)
            {
                throw new ArgumentNullException("coordinates");
            }

            this.Positions = new ReadOnlyCollection<Position>(coordinates);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LineString"/> class.
        /// </summary>
        /// <remarks>
        /// This constructor is used only during deserialization.
        /// </remarks>
        internal LineString()
            : base(GeometryType.LineString, new GeometryParams())
        {
        }

        /// <summary>
        /// Gets line string positions.
        /// </summary>
        /// <value>
        /// Positions of the line string.
        /// </value>
        [DataMember(Name = "coordinates")]
        [JsonProperty("coordinates", Required = Required.Always, Order = 1)]
        public ReadOnlyCollection<Position> Positions { get; private set; }

        /// <summary>
        /// Determines whether the specified <see cref="LineString" /> is equal to the current <see cref="LineString" />.
        /// </summary>
        /// <returns>
        /// true if the specified object is equal to the current object; otherwise, false.
        /// </returns>
        /// <param name="obj">The object to compare with the current object. </param>
        public override bool Equals(object obj)
        {
            return this.Equals(obj as LineString);
        }

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
                return this.Positions.Aggregate(base.GetHashCode(), (current, value) => (current * 397) ^ value.GetHashCode());
            }
        }

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

            return base.Equals(other) && this.Positions.SequenceEqual(other.Positions);
        }
    }
}
