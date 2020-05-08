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
    /// Represents a geometry consisting of multiple <see cref="LineString"/>.
    /// </summary>
    /// <seealso cref="LineString"/>.
    [DataContract]
    [System.Text.Json.Serialization.JsonConverter(typeof(TextJsonGeometryConverterFactory))]
    internal sealed class MultiLineString : Geometry, IEquatable<MultiLineString>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MultiLineString"/> class. 
        /// </summary>
        /// <param name="lineStrings">
        /// List of <see cref="LineStringCoordinates"/> instances representing individual line strings.
        /// </param>
        public MultiLineString(IList<LineStringCoordinates> lineStrings)
            : this(lineStrings, new GeometryParams())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MultiLineString"/> class.
        /// </summary>
        /// <param name="lineStrings">
        /// List of <see cref="LineStringCoordinates"/> instances representing individual line strings.
        /// </param>
        /// <param name="geometryParams">
        /// Additional geometry parameters.
        /// </param>
        public MultiLineString(IList<LineStringCoordinates> lineStrings, GeometryParams geometryParams)
            : base(GeometryType.MultiLineString, geometryParams)
        {
            if (lineStrings == null)
            {
                throw new ArgumentNullException("lineStrings");
            }

            this.LineStrings = new ReadOnlyCollection<LineStringCoordinates>(lineStrings);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MultiLineString"/> class.
        /// </summary>
        /// <remarks>
        /// This constructor is used only during deserialization.
        /// </remarks>
        internal MultiLineString()
            : base(GeometryType.MultiLineString, new GeometryParams())
        {
        }

        /// <summary>
        /// Gets collection of <see cref="LineStringCoordinates"/> representing individual line strings.
        /// </summary>
        /// <value>
        /// Collection of <see cref="LineStringCoordinates"/> representing individual line strings.
        /// </value>
        [DataMember(Name = "coordinates")]
        [Newtonsoft.Json.JsonProperty("coordinates", Required = Newtonsoft.Json.Required.Always, Order = 1)]
        public ReadOnlyCollection<LineStringCoordinates> LineStrings { get; private set; }

        /// <summary>
        /// Determines whether the specified <see cref="MultiLineString" /> is equal to the current <see cref="MultiLineString" />.
        /// </summary>
        /// <returns>
        /// true if the specified object is equal to the current object; otherwise, false.
        /// </returns>
        /// <param name="obj">The object to compare with the current object. </param>
        public override bool Equals(object obj)
        {
            return this.Equals(obj as MultiLineString);
        }

        /// <summary>
        /// Serves as a hash function for the <see cref="MultiLineString" /> type.
        /// </summary>
        /// <returns>
        /// A hash code for the current <see cref="MultiLineString"/>.
        /// </returns>
        public override int GetHashCode()
        {
            unchecked
            {
                return this.LineStrings.Aggregate(base.GetHashCode(), (current, value) => (current * 397) ^ value.GetHashCode());
            }
        }

        /// <summary>
        /// Determines if this <see cref="MultiLineString"/> is equal to <paramref name="other" />.
        /// </summary>
        /// <param name="other"><see cref="MultiLineString"/> to compare to this <see cref="MultiLineString"/>.</param>
        /// <returns><c>true</c> if line strings are equal. <c>false</c> otherwise.</returns>
        public bool Equals(MultiLineString other)
        {
            if (object.ReferenceEquals(null, other))
            {
                return false;
            }

            if (object.ReferenceEquals(this, other))
            {
                return true;
            }

            return base.Equals(other) && this.LineStrings.SequenceEqual(other.LineStrings);
        }
    }
}
