//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Spatial
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Spatial.Converters;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary>
    /// Base class for spatial geometry objects in the Azure Cosmos DB service.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    [JsonConverter(typeof(GeometryJsonConverter))]
    public abstract class Geometry
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Geometry" /> class in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="type">
        /// Geometry type.
        /// </param>
        /// <param name="geometryParams">
        /// Coordinate reference system, additional properties etc.
        /// </param>
        protected Geometry(GeometryType type, GeometryParams geometryParams)
        {
            if (geometryParams == null)
            {
                throw new ArgumentNullException("geometryParams");
            }

            this.Type = type;

            if (geometryParams.Crs == null || geometryParams.Crs.Equals(Crs.Default))
            {
                this.CrsForSerialization = null;
            }
            else
            {
                this.CrsForSerialization = geometryParams.Crs;
            }

            this.BoundingBox = geometryParams.BoundingBox;
            this.AdditionalProperties = geometryParams.AdditionalProperties ?? new Dictionary<string, object>();
        }

        /// <summary>
        /// Gets the Coordinate Reference System for this geometry in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The Coordinate Reference System for this geometry.
        /// </value>
        public Crs Crs
        {
            get
            {
                return this.CrsForSerialization ?? Crs.Default;
            }
        }

        /// <summary>
        /// Gets geometry type in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// Type of geometry.
        /// </value>
        [JsonProperty("type", Required = Required.Always, Order = 0)]
        [JsonConverter(typeof(StringEnumConverter))]
        public GeometryType Type { get; private set; }

        /// <summary>
        /// Gets bounding box for this geometry in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// Bounding box of the geometry.
        /// </value>
        [JsonProperty("bbox", DefaultValueHandling = DefaultValueHandling.Ignore, Order = 3)]
        public BoundingBox BoundingBox { get; private set; }

        /// <summary>
        /// Gets additional properties in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// Additional geometry properties.
        /// </value>
        [JsonExtensionData]
        public IDictionary<string, object> AdditionalProperties { get; private set; }

        /// <summary>
        /// Gets or sets CRS value used for serialization in the Azure Cosmos DB service.
        /// </summary>
        /// <remarks>
        /// This is artificial property needed for serialization. If CRS is default one, we don't want
        /// to serialize anything.
        /// </remarks>
        [JsonProperty("crs", DefaultValueHandling = DefaultValueHandling.Ignore, Order = 2)]
        private Crs CrsForSerialization { get; set; }

        /// <summary>
        /// Determines whether the specified <see cref="Geometry" /> is equal to the current <see cref="Geometry" /> in the Azure Cosmos DB service.
        /// </summary>
        /// <returns>
        /// true if the specified object  is equal to the current object; otherwise, false.
        /// </returns>
        /// <param name="obj">The object to compare with the current object. </param>
        public override bool Equals(object obj)
        {
            return this.Equals(obj as Geometry);
        }

        /// <summary>
        /// Serves as a hash function for the <see cref="Geometry" /> type in the Azure Cosmos DB service.
        /// </summary>
        /// <returns>
        /// A hash code for the current geometry.
        /// </returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = this.Crs.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)this.Type;
                hashCode = (hashCode * 397) ^ (this.BoundingBox != null ? this.BoundingBox.GetHashCode() : 0);
                hashCode = this.AdditionalProperties.Aggregate(
                    hashCode,
                    (current, value) => (current * 397) ^ value.GetHashCode());

                return hashCode;
            }
        }

        /// <summary>
        /// Determines if this <see cref="Geometry" /> is equal to the <paramref name="other" /> in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="other"><see cref="Geometry" /> to compare to this <see cref="Geometry" />.</param>
        /// <returns><c>true</c> if geometries are equal. <c>false</c> otherwise.</returns>
        private bool Equals(Geometry other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return this.Crs.Equals(other.Crs) && this.Type == other.Type
                   && object.Equals(this.BoundingBox, other.BoundingBox)
                   && this.AdditionalProperties.SequenceEqual(other.AdditionalProperties);
        }
    }
}
