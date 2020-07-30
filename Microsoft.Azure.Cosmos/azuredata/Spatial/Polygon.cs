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
    /// <para>
    /// Polygon geometry class in the Azure Cosmos DB service.
    /// </para>
    /// <para>
    /// A polygon is represented by the set of "polygon rings". Each ring is closed line string.
    /// First ring defines external ring. All subsequent rings define "holes" in the external ring.
    /// </para>
    /// <para>
    /// Rings must be specified using Left Hand Rule: traversing the ring in the order of its points, should result
    /// in internal area of the polygon being to the left side.
    /// </para>
    /// </summary>
    /// <example>
    /// This example shows how to define a polygon which covers small portion of the Earth:
    /// <code language="c#">
    /// <![CDATA[
    /// var polygon = new Polygon(
    ///         new[]
    ///         {
    ///             new Position(20.0, 20.0),
    ///             new Position(30.0, 20.0),
    ///             new Position(30.0, 30.0),
    ///             new Position(20.0, 30.0)
    ///             new Position(20.0, 20.0)
    ///         });
    /// ]]>        
    /// </code>
    /// </example>
    /// <example>
    /// This example shows how to define a polygon which covers area more than one hemisphere:
    /// (Notice that only order of coordinates was reversed).
    /// <code language="c#">
    /// <![CDATA[
    /// var polygon = new Polygon(
    ///         new[]
    ///         {
    ///             new Position(20.0, 20.0),
    ///             new Position(20.0, 30.0),
    ///             new Position(30.0, 30.0),
    ///             new Position(30.0, 20.0)
    ///             new Position(20.0, 20.0)
    ///         });
    /// ]]>        
    /// </code>
    /// </example>
    /// <see link="https://tools.ietf.org/html/rfc7946#section-3.1.6"/>
    [DataContract]
    internal class Polygon : GeoJson, IEquatable<Polygon>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Polygon"/> class,
        /// from external ring (the polygon contains no holes) in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="externalRing">The exterior ring bounds the surface.</param>
        /// <param name="boundingBox">The bounding box.</param>
        public Polygon(LinearRing externalRing, BoundingBox boundingBox = null)
            : this(new LinearRing[] { externalRing }, boundingBox)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Polygon"/> class in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="externalRing">The exterior ring bounds the surface.</param>
        /// <param name="interiorRings">Holes within the surface.</param>
        /// <param name="boundingBox">The bounding box.</param>
        public Polygon(LinearRing externalRing, IReadOnlyList<LinearRing> interiorRings, BoundingBox boundingBox = null)
            : this(new LinearRing[] { externalRing }.Concat(interiorRings).ToList(), boundingBox)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Polygon"/> class in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="coordinates">
        /// Polygon rings.
        /// </param>
        /// <param name="boundingBox">
        /// Additional geometry parameters.
        /// </param>
        public Polygon(IReadOnlyList<LinearRing> coordinates, BoundingBox boundingBox = null)
            : base(boundingBox)
        {
            if (coordinates == null)
            {
                throw new ArgumentNullException(nameof(coordinates));
            }

            if (coordinates.Count == 0)
            {
                throw new ArgumentException("The \"coordinates\" member MUST be an array of linear ring coordinate arrays. The first MUST be the exterior ring");
            }

            this.Coordinates = coordinates;
        }

        /// <inheritdoc/>
        public override GeoJsonType Type => GeoJsonType.Polygon;

        /// <summary>
        /// Gets the polygon rings in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// Polygon rings.
        /// </value>
        [DataMember(Name = "coordinates")]
        public IReadOnlyList<LinearRing> Coordinates { get; }

        /// <summary>
        /// Serves as a hash function for the <see cref="Polygon" /> type in the Azure Cosmos DB service.
        /// </summary>
        /// <returns>
        /// A hash code for the current <see cref="Polygon"/>.
        /// </returns>
        public override int GetHashCode()
        {
            unchecked
            {
                return this.Coordinates.Aggregate(
                    base.GetHashCodeBase(),
                    (current, value) => (current * 397) ^ value.GetHashCode());
            }
        }

        public override bool Equals(GeoJson other) => other is Polygon polygon && this.Equals(polygon);

        /// <summary>
        /// Determines if this <see cref="Polygon"/> is equal to the <paramref name="other" /> in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="other"><see cref="Polygon"/> to compare to this <see cref="Polygon"/>.</param>
        /// <returns><c>true</c> if objects are equal. <c>false</c> otherwise.</returns>
        public bool Equals(Polygon other)
        {
            if (other is null)
            {
                return false;
            }

            if (object.ReferenceEquals(this, other))
            {
                return true;
            }

            if (!base.EqualsBase(other))
            {
                return false;
            }

            return this.Coordinates.SequenceEqual(other.Coordinates);
        }
    }
}
