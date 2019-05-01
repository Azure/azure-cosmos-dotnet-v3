//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Spatial
{
    using System;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Operations supported on <see cref="Geometry" /> type in the Azure Cosmos DB service. These operations are to be used in LINQ expressions only
    /// and will be evaluated on server. There's no implementation provided in the client library.
    /// </summary>
    internal static class GeometryOperationExtensions
    {
        /// <summary>
        /// Distance in meters between two geometries in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="from">First <see cref="Geometry"/>.</param>
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
        public static double Distance(this Geometry from, Geometry to)
        {
            throw new NotImplementedException(RMResources.SpatialExtensionMethodsNotImplemented);
        }

        /// <summary>
        /// Determines if <paramref name="inner" /> <see cref="Geometry"/> is fully contained inside <paramref name="outer" /> <see cref="Geometry"/> in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="inner">Inner <see cref="Geometry"/>.</param>
        /// <param name="outer">Outer <see cref="Geometry"/>.</param>
        /// <returns>
        /// <c>true</c> if <paramref name="inner" /> <see cref="Geometry"/> is fully contained inside <paramref name="outer" /> <see cref="Geometry"/>.
        /// <c>false</c> otherwise.
        /// </returns>
        /// <remarks>
        /// Currently this function supports <paramref name="inner"/> geometry of type <see cref="GeometryType.Point"/> and outer geometry of type <see cref="GeometryType.Polygon"/>.
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
        public static bool Within(this Geometry inner, Geometry outer)
        {
            throw new NotImplementedException(RMResources.SpatialExtensionMethodsNotImplemented);
        }

        /// <summary>
        /// <para>
        /// Determines if the <paramref name="geometry"/> specified is valid and can be indexed
        /// or used in queries by Azure Cosmos DB service.
        /// </para>
        /// <para>
        /// If a geometry is not valid, it will not be indexed. Also during query time invalid geometries are equivalent to <c>undefined</c>.
        /// </para>
        /// </summary>
        /// <param name="geometry">The geometry to check for validity.</param>
        /// <returns><c>true</c> if geometry is valid. <c>false</c> otherwise.</returns>
        /// <remarks>
        /// Currently this function supports <paramref name="geometry"/> of type <see cref="GeometryType.Point"/> and <see cref="GeometryType.Polygon"/>.
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
        public static bool IsValid(this Geometry geometry)
        {
            throw new NotImplementedException(RMResources.SpatialExtensionMethodsNotImplemented);
        }

        /// <summary>
        /// <para>
        /// Determines if the <paramref name="geometry"/> specified is valid and can be indexed
        /// or used in queries by Azure Cosmos DB service.
        /// </para>
        /// <para>
        /// If a geometry is not valid, it will not be indexed. Also during query time invalid geometries are equivalent to <c>undefined</c>.
        /// </para>
        /// </summary>
        /// <param name="geometry">The geometry to check for validity.</param>
        /// <returns>Instance of <see cref="GeometryValidationResult"/>.</returns>
        /// <remarks>
        /// Currently this function supports <paramref name="geometry"/> of type <see cref="GeometryType.Point"/> and <see cref="GeometryType.Polygon"/>.
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
        public static GeometryValidationResult IsValidDetailed(this Geometry geometry)
        {
            throw new NotImplementedException(RMResources.SpatialExtensionMethodsNotImplemented);
        }

        /// <summary>
        /// Checks if geometry1 intersects with geometry2.
        /// </summary>
        /// <param name="geometry1">First <see cref="Geometry"/>.</param>
        /// <param name="geometry2">Second <see cref="Geometry"/>.</param>
        /// <returns>Returns true if geometry1 intersects with geometry2, otherwise returns false.</returns>
        /// <example>
        /// <code>
        /// <![CDATA[
        /// var distanceQuery = documents.Where(document => document.Location.Intersects(new Point(20.1, 20)));
        /// ]]>
        /// </code>
        /// </example>
        public static bool Intersects(this Geometry geometry1, Geometry geometry2)
        {
            throw new NotImplementedException(RMResources.SpatialExtensionMethodsNotImplemented);
        }
    }
}