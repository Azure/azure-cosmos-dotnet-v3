//-----------------------------------------------------------------------
// <copyright file="Utils.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Tests.Query.OfflineEngine
{
    using System;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Utility class for Offline Query Engine.
    /// </summary>
    internal static class Utils
    {
        /// <summary>
        /// Tries to compare two JTokens with respect to their types.
        /// </summary>
        /// <param name="left">The left JToken.</param>
        /// <param name="right">The right JToken.</param>
        /// <param name="comparison">The comparison if comparable.</param>
        /// <returns>Whether or not the two JTokens were comparable.</returns>
        public static bool TryCompare(CosmosElement left, CosmosElement right, out int comparison)
        {

            if ((left == null) || (right == null))
            {
                comparison = default;
                return false;
            }

            JsonType leftJsonType = left.Accept(CosmosElementToJsonType.Singleton);
            JsonType rightJsonType = right.Accept(CosmosElementToJsonType.Singleton);
            if (leftJsonType != rightJsonType)
            {
                comparison = default;
                return false;
            }

            if (!Utils.IsPrimitive(left))
            {
                comparison = default;
                return false;
            }

            comparison = left.CompareTo(right);
            return true;
        }

        public static bool IsPrimitive(CosmosElement value)
        {
            if (value == null)
            {
                return false;
            }

            JsonType type = value.Accept(CosmosElementToJsonType.Singleton);
            return (type == JsonType.Boolean)
                || (type == JsonType.Null)
                || (type == JsonType.Number)
                || (type == JsonType.String);
        }
    }
}