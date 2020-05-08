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

    /// <summary>
    /// Represents a geometry consisting of other geometries.
    /// </summary>
    [DataContract]
    [System.Text.Json.Serialization.JsonConverter(typeof(TextJsonGeometryConverterFactory))]
    internal sealed class GeometryCollection : Geometry, IEquatable<GeometryCollection>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GeometryCollection"/> class. 
        /// </summary>
        /// <param name="geometries">
        /// List of geometries.
        /// </param>
        public GeometryCollection(IList<Geometry> geometries)
            : this(geometries, new GeometryParams())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GeometryCollection"/> class.
        /// </summary>
        /// <param name="geometries">
        /// Child geometries.
        /// </param>
        /// <param name="geometryParams">
        /// Additional geometry parameters.
        /// </param>
        public GeometryCollection(IList<Geometry> geometries, GeometryParams geometryParams)
            : base(GeometryType.GeometryCollection, geometryParams)
        {
            if (geometries == null)
            {
                throw new ArgumentNullException("geometries");
            }

            this.Geometries = new ReadOnlyCollection<Geometry>(geometries);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GeometryCollection"/> class.
        /// </summary>
        /// <remarks>
        /// This constructor is used only during deserialization.
        /// </remarks>
        internal GeometryCollection()
            : base(GeometryType.GeometryCollection, new GeometryParams())
        {
        }

        /// <summary>
        /// Gets child geometries.
        /// </summary>
        /// <value>
        /// Child geometries.
        /// </value>
        [DataMember(Name = "geometries")]
        [Newtonsoft.Json.JsonProperty("geometries", Required = Newtonsoft.Json.Required.Always, Order = 1)]
        public ReadOnlyCollection<Geometry> Geometries { get; private set; }

        /// <summary>
        /// Determines whether the specified <see cref="GeometryCollection" /> is equal to the current <see cref="GeometryCollection" />.
        /// </summary>
        /// <returns>
        /// true if the specified object is equal to the current object; otherwise, false.
        /// </returns>
        /// <param name="obj">The object to compare with the current object. </param>
        public override bool Equals(object obj)
        {
            return this.Equals(obj as GeometryCollection);
        }

        /// <summary>
        /// Serves as a hash function for the <see cref="GeometryCollection" /> type.
        /// </summary>
        /// <returns>
        /// A hash code for the current <see cref="GeometryCollection" />.
        /// </returns>
        public override int GetHashCode()
        {
            unchecked
            {
                return this.Geometries.Aggregate(base.GetHashCode(), (current, value) => (current * 397) ^ value.GetHashCode());
            }
        }

        /// <summary>
        /// Determines if this <see cref="GeometryCollection" /> is equal to the <paramref name="other" />.
        /// </summary>
        /// <param name="other"><see cref="GeometryCollection" /> to compare to this <see cref="GeometryCollection" />.</param>
        /// <returns><c>true</c> if geometry collections are equal. <c>false</c> otherwise.</returns>
        public bool Equals(GeometryCollection other)
        {
            if (object.ReferenceEquals(null, other))
            {
                return false;
            }

            if (object.ReferenceEquals(this, other))
            {
                return true;
            }

            return base.Equals(other) && this.Geometries.SequenceEqual(other.Geometries);
        }
    }
}
