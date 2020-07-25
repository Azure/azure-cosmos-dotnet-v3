// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using global::Azure.Core.GeoJson;

    /// <summary>
    /// Extension methods for <see cref="GeoObject"/> used for LINQ translation.
    /// </summary>
    public static class GeoObjectExtensions
    {
        private const string SpatialExtensionMethodsNotImplementedMessage = "Spatial extension methods are not implemented. These method serve as stubs for users to use them in LINQ to Sql query translation.";

        /// <summary>
        /// Distance in meters between two geometries in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="from">First <see cref="GeoObject"/>.</param>
        /// <param name="to">Second <see cref="GeoObject"/>.</param>
        /// <returns>Returns distance in meters between two geometries.</returns>
        /// <example>
        /// <code>
        /// <![CDATA[
        /// var distanceQuery = documents.Where(document => document.Location.Distance(new GeoPoint(20.1, 20)) < 20000);
        /// ]]>
        /// </code>
        /// </example>
        public static double Distance(this GeoObject from, GeoObject to)
            => throw new NotImplementedException(SpatialExtensionMethodsNotImplementedMessage);

        /// <summary>
        /// Determines if the  <paramref name="inner" /> <see cref="GeoObject"/> is fully contained inside the <paramref name="outer" /> <see cref="GeoObject"/> in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="inner">Inner <see cref="GeoObject"/>.</param>
        /// <param name="outer">Outer <see cref="GeoObject"/>.</param>
        /// <returns>
        /// <c>true</c> if current inner <see cref="GeoObject"/> is fully contained inside <paramref name="outer" /> <see cref="GeoObject"/>.
        /// <c>false</c> otherwise.
        /// </returns>
        /// <example>
        /// <code>
        /// <![CDATA[
        /// GeoPolygon polygon = new GeoPolygon(
        ///        new[]
        ///        {
        ///             new GeoPosition(10, 10),
        ///             new GeoPosition(30, 10),
        ///             new GeoPosition(30, 30),
        ///             new GeoPosition(10, 30),
        ///             new GeoPosition(10, 10)
        ///        });
        /// var withinQuery = documents.Where(document => document.Location.Within(polygon));
        /// ]]>
        /// </code>
        /// </example>
        public static bool Within(this GeoObject inner, GeoObject outer)
            => throw new NotImplementedException(SpatialExtensionMethodsNotImplementedMessage);

        /// <summary>
        /// <para>
        /// Determines if the geometry specified is valid and can be indexed
        /// or used in queries by Azure Cosmos DB service.
        /// </para>
        /// <para>
        /// If a geometry is not valid, it will not be indexed. Also during query time invalid geometries are equivalent to <c>undefined</c>.
        /// </para>
        /// </summary>
        /// <param name="geoObject">The <see cref="GeoObject"/> to validate.</param>
        /// <returns><c>true</c> if geometry is valid. <c>false</c> otherwise.</returns>
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
        public static bool IsValid(this GeoObject geoObject)
            => throw new NotImplementedException(SpatialExtensionMethodsNotImplementedMessage);

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
        /// <param name="geoObject">The <see cref="GeoObject"/> to validate.</param>
        /// <returns>Instance of <see cref="GeometryValidationResult"/>.</returns>
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
        public static GeometryValidationResult IsValidDetailed(this GeoObject geoObject)
            => throw new NotImplementedException(SpatialExtensionMethodsNotImplementedMessage);

        /// <summary>
        /// Checks if current geometry1 intersects with geometry2.
        /// </summary>
        /// <param name="geoObject1">First <see cref="GeoObject"/>.</param>
        /// <param name="geoObject2">Second <see cref="GeoObject"/>.</param>
        /// <returns>Returns true if geometry1 intersects with geometry2, otherwise returns false.</returns>
        /// <example>
        /// <code>
        /// <![CDATA[
        /// var distanceQuery = documents.Where(document => document.Location.Intersects(new GeoPoint(20.1, 20)));
        /// ]]>
        /// </code>
        /// </example>
        public static bool Intersects(this GeoObject geoObject1, GeoObject geoObject2)
            => throw new NotImplementedException(SpatialExtensionMethodsNotImplementedMessage);
    }
}
