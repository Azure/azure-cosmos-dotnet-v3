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
    /// Geometry which is comprised of multiple polygons.
    /// </summary>
    /// <seealso cref="Polygon"/>
    [DataContract]
    public sealed class MultiPolygon : Geometry, IEquatable<MultiPolygon>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MultiPolygon"/> class.
        /// </summary>
        /// <param name="polygons">
        /// List of <see cref="PolygonCoordinates"/> instances. Each <see cref="PolygonCoordinates"/> represents separate polygon.
        /// </param>
        public MultiPolygon(IList<PolygonCoordinates> polygons)
            : this(polygons, new GeometryParams())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MultiPolygon"/> class.
        /// </summary>
        /// <param name="polygons">
        /// List of <see cref="PolygonCoordinates"/> instances. Each <see cref="PolygonCoordinates"/> represents separate polygon.
        /// </param>
        /// <param name="geometryParams">Additional geometry parameters.</param>
        public MultiPolygon(IList<PolygonCoordinates> polygons, GeometryParams geometryParams)
            : base(GeometryType.MultiPolygon, geometryParams)
        {
            if (polygons == null)
            {
                throw new ArgumentNullException(nameof(polygons));
            }

            this.Polygons = new ReadOnlyCollection<PolygonCoordinates>(polygons);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MultiPolygon"/> class.
        /// </summary>
        /// <remarks>
        /// This constructor is used only during deserialization.
        /// </remarks>
        internal MultiPolygon()
            : base(GeometryType.MultiPolygon, new GeometryParams())
        {
        }

        /// <summary>
        /// Gets collection of <see cref="PolygonCoordinates"/> instances. Each <see cref="PolygonCoordinates"/> represents separate polygon.
        /// </summary>
        /// <value>
        /// Collection of <see cref="PolygonCoordinates"/> instances. Each <see cref="PolygonCoordinates"/> represents separate polygon.
        /// </value>
        [DataMember(Name = "coordinates")]
        [JsonProperty("coordinates", Required = Required.Always, Order = 1)]
        public ReadOnlyCollection<PolygonCoordinates> Polygons { get; private set; }

        /// <summary>
        /// Determines whether the specified <see cref="MultiPolygon" /> is equal to the current <see cref="MultiPolygon" />.
        /// </summary>
        /// <returns>
        /// true if the specified object  is equal to the current object; otherwise, false.
        /// </returns>
        /// <param name="obj">The object to compare with the current object. </param>
        public override bool Equals(object obj)
        {
            return this.Equals(obj as MultiPolygon);
        }

        /// <summary>
        /// Serves as a hash function for the <see cref="MultiPolygon" /> type.
        /// </summary>
        /// <returns>
        /// A hash code for the current <see cref="MultiPolygon" />.
        /// </returns>
        public override int GetHashCode()
        {
            unchecked
            {
                return this.Polygons.Aggregate(base.GetHashCode(), (current, value) => (current * 397) ^ value.GetHashCode());
            }
        }

        /// <summary>
        /// Determines if this <see cref="MultiPolygon"/> is equal to <paramref name="other" />.
        /// </summary>
        /// <param name="other"><see cref="MultiPolygon"/> to compare to this <see cref="MultiPolygon"/>.</param>
        /// <returns><c>true</c> if objects are equal. <c>false</c> otherwise.</returns>
        public bool Equals(MultiPolygon other)
        {
            if (object.ReferenceEquals(null, other))
            {
                return false;
            }

            if (object.ReferenceEquals(this, other))
            {
                return true;
            }

            return base.Equals(other) && this.Polygons.SequenceEqual(other.Polygons);
        }
    }
}
