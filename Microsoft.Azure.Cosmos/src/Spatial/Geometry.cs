//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Spatial
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.Serialization;
    using Microsoft.Azure.Cosmos.Spatial.Converters;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary>
    /// Base class for spatial geometry objects in the Azure Cosmos DB service.
    /// </summary>
    [DataContract]
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
                throw new ArgumentNullException(nameof(geometryParams));
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
        public Crs Crs => this.CrsForSerialization ?? Crs.Default;

        /// <summary>
        /// Gets geometry type in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// Type of geometry.
        /// </value>
        [DataMember(Name = "type")]
        [JsonProperty("type", Required = Required.Always, Order = 0)]
        [JsonConverter(typeof(StringEnumConverter))]
        public GeometryType Type { get; private set; }

        /// <summary>
        /// Gets bounding box for this geometry in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// Bounding box of the geometry.
        /// </value>
        [DataMember(Name = "bbox")]
        [JsonProperty("bbox", DefaultValueHandling = DefaultValueHandling.Ignore, Order = 3)]
        public BoundingBox BoundingBox { get; private set; }

        /// <summary>
        /// Gets additional properties in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// Additional geometry properties.
        /// </value>
        [JsonExtensionData]
        [DataMember(Name = "properties")]
        public IDictionary<string, object> AdditionalProperties { get; private set; }

        /// <summary>
        /// Gets or sets CRS value used for serialization in the Azure Cosmos DB service.
        /// </summary>
        /// <remarks>
        /// This is artificial property needed for serialization. If CRS is default one, we don't want
        /// to serialize anything.
        /// </remarks>
        [DataMember(Name = "crs")]
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
        /// Distance in meters between two geometries in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="to">Second <see cref="Geometry"/>.</param>
        /// <returns>Returns distance in meters between two geometries.</returns>
        /// <remarks>
        /// Today this function support only geometries of <see cref="GeometryType.Point"/> type.
        /// </remarks>
        /// <example>
        /// <code>
        /// <![CDATA[
        /// var distanceQuery = documents.Where(document => document.Location.Distance(new Point(20.1, 20)) < 20000);
        /// ]]>
        /// </code>
        /// </example>
        public double Distance(Geometry to)
        {
            throw new NotImplementedException(RMResources.SpatialExtensionMethodsNotImplemented);
        }

        /// <summary>
        /// Determines if current inner <see cref="Geometry"/> is fully contained inside <paramref name="outer" /> <see cref="Geometry"/> in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="outer">Outer <see cref="Geometry"/>.</param>
        /// <returns>
        /// <c>true</c> if current inner <see cref="Geometry"/> is fully contained inside <paramref name="outer" /> <see cref="Geometry"/>.
        /// <c>false</c> otherwise.
        /// </returns>
        /// <remarks>
        /// Currently this function supports current geometry of type <see cref="GeometryType.Point"/> and outer geometry of type <see cref="GeometryType.Polygon"/>.
        /// </remarks>
        /// <example>
        /// <code>
        /// <![CDATA[
        /// Polygon polygon = new Polygon(
        ///        new[]
        ///        {
        ///             new Position(10, 10),
        ///             new Position(30, 10),
        ///             new Position(30, 30),
        ///             new Position(10, 30),
        ///             new Position(10, 10)
        ///        });
        /// var withinQuery = documents.Where(document => document.Location.Within(polygon));
        /// ]]>
        /// </code>
        /// </example>
        public bool Within(Geometry outer)
        {
            throw new NotImplementedException(RMResources.SpatialExtensionMethodsNotImplemented);
        }

        /// <summary>
        /// <para>
        /// Determines if the geometry specified is valid and can be indexed
        /// or used in queries by Azure Cosmos DB service.
        /// </para>
        /// <para>
        /// If a geometry is not valid, it will not be indexed. Also during query time invalid geometries are equivalent to <c>undefined</c>.
        /// </para>
        /// </summary>
        /// <returns><c>true</c> if geometry is valid. <c>false</c> otherwise.</returns>
        /// <remarks>
        /// Currently this function supports geometry of type <see cref="GeometryType.Point"/> and <see cref="GeometryType.Polygon"/>.
        /// </remarks>
        /// <example>
        /// <para>
        /// This example select all the documents which contain invalid geometries which were not indexed.
        /// </para>
        /// <code>
        /// <![CDATA[
        /// var invalidDocuments = documents.Where(document => !document.Location.IsValid());
        /// ]]>
        /// </code>
        /// </example>
        public bool IsValid()
        {
            throw new NotImplementedException(RMResources.SpatialExtensionMethodsNotImplemented);
        }

        /// <summary>
        /// <para>
        /// Determines if the geometry specified is valid and can be indexed
        /// or used in queries by Azure Cosmos DB service
        /// and if invalid, gives the additional reason as a string value.
        /// </para>
        /// <para>
        /// If a geometry is not valid, it will not be indexed. Also during query time invalid geometries are equivalent to <c>undefined</c>.
        /// </para>
        /// </summary>
        /// <returns>Instance of <see cref="GeometryValidationResult"/>.</returns>
        /// <remarks>
        /// Currently this function supports geometry of type <see cref="GeometryType.Point"/> and <see cref="GeometryType.Polygon"/>.
        /// </remarks>
        /// <example>
        /// <para>
        /// This example select all the documents which contain invalid geometries which were not indexed.
        /// </para>
        /// <code>
        /// <![CDATA[
        /// var invalidReason = documents.Where(document => !document.Location.IsValid()).Select(document => document.Location.IsValidDetailed());
        /// ]]>
        /// </code>
        /// </example>
        public GeometryValidationResult IsValidDetailed()
        {
            throw new NotImplementedException(RMResources.SpatialExtensionMethodsNotImplemented);
        }

        /// <summary>
        /// Checks if current geometry1 intersects with geometry2.
        /// </summary>
        /// <param name="geometry2">Second <see cref="Geometry"/>.</param>
        /// <returns>Returns true if geometry1 intersects with geometry2, otherwise returns false.</returns>
        /// <example>
        /// <code>
        /// <![CDATA[
        /// var distanceQuery = documents.Where(document => document.Location.Intersects(new Point(20.1, 20)));
        /// ]]>
        /// </code>
        /// </example>
        public bool Intersects(Geometry geometry2)
        {
            throw new NotImplementedException(RMResources.SpatialExtensionMethodsNotImplemented);
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
