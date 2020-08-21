// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Azure.Cosmos.Linq
{
    using System;
    using Azure.Cosmos.Spatial;

    /// <summary>
    /// Extension methods for <see cref="GeoJson"/> used for LINQ translation.
    /// </summary>
    internal static class SpatialExtensions
    {
        private const string SpatialExtensionMethodsNotImplementedMessage = "Spatial extension methods are not implemented. These method serve as stubs for users to use them in LINQ to Sql query translation.";

        /// <summary>
        /// Distance in meters between two geometries in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="from">First <see cref="GeoJson"/>.</param>
        /// <param name="to">Second <see cref="GeoJson"/>.</param>
        /// <returns>Returns distance in meters between two geometries.</returns>
        /// <example>
        /// <code>
        /// <![CDATA[
        /// var distanceQuery = documents.Where(document => document.Location.Distance(new Point(20.1, 20)) < 20000);
        /// ]]>
        /// </code>
        /// </example>
        public static double Distance(this GeoJson from, GeoJson to)
            => throw new NotImplementedException(SpatialExtensionMethodsNotImplementedMessage);

        /// <summary>
        /// Determines if the  <paramref name="inner" /> <see cref="GeoJson"/> is fully contained inside the <paramref name="outer" /> <see cref="GeoJson"/> in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="inner">Inner <see cref="GeoJson"/>.</param>
        /// <param name="outer">Outer <see cref="GeoJson"/>.</param>
        /// <returns>
        /// <c>true</c> if current inner <see cref="GeoJson"/> is fully contained inside <paramref name="outer" /> <see cref="GeoJson"/>.
        /// <c>false</c> otherwise.
        /// </returns>
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
        public static bool Within(this GeoJson inner, GeoJson outer)
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
        /// <param name="geometry">The <see cref="GeoJson"/> to validate.</param>
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
        public static bool IsValid(this GeoJson geometry)
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
        /// <param name="geometry">The <see cref="GeoJson"/> to validate.</param>
        /// <returns>Instance of <see cref="IsValidDetailedResult"/>.</returns>
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
        public static IsValidDetailedResult IsValidDetailed(this GeoJson geometry)
            => throw new NotImplementedException(SpatialExtensionMethodsNotImplementedMessage);

        /// <summary>
        /// Checks if current geometry1 intersects with geometry2.
        /// </summary>
        /// <param name="geometry1">First <see cref="GeoJson"/>.</param>
        /// <param name="geometry2">Second <see cref="GeoJson"/>.</param>
        /// <returns>Returns true if geometry1 intersects with geometry2, otherwise returns false.</returns>
        /// <example>
        /// <code>
        /// <![CDATA[
        /// var distanceQuery = documents.Where(document => document.Location.Intersects(new GeoPoint(20.1, 20)));
        /// ]]>
        /// </code>
        /// </example>
        public static bool Intersects(this GeoJson geometry1, GeoJson geometry2)
            => throw new NotImplementedException(SpatialExtensionMethodsNotImplementedMessage);
    }
}