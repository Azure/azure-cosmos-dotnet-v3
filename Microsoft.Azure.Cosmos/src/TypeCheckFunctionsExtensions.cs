//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SystemFunctions
{
    using System;

    /// <summary>
    /// Provide methods for type checking.
    /// These methods are to be used in LINQ expressions only and will be evaluated on server.
    /// There's no implementation provided in the client library.
    /// </summary>
    internal static class TypeCheckFunctionsExtensions
    {
        /// <summary>
        /// Determines if a certain property is defined or not.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns>Returns true if this property is defined otherwise returns false.</returns>
        /// <example>
        /// <code>
        /// <![CDATA[
        /// var isDefinedQuery = documents.Where(document => document.Name.IsDefined());
        /// ]]>
        /// </code>
        /// </example>
        public static bool IsDefined(this object obj)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Determines if a certain property is null or not.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns>Returns true if this property is null otherwise returns false.</returns>
        /// <example>
        /// <code>
        /// <![CDATA[
        /// var isNullQuery = documents.Where(document => document.Name.IsNull());
        /// ]]>
        /// </code>
        /// </example>s>
        public static bool IsNull(this object obj)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Determines if a certain property is of premitive JSON type.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns>Returns true if this property is null otherwise returns false.</returns>
        /// <remarks>
        /// Premitive JSON types (Double, String, Boolean and Null)
        /// </remarks>
        /// <example>
        /// <code>
        /// <![CDATA[
        /// var isPrimitiveQuery = documents.Where(document => document.Name.IsPrimitive());
        /// ]]>
        /// </code>
        /// </example>s>
        public static bool IsPrimitive(this object obj)
        {
            throw new NotImplementedException();
        }
    }
}