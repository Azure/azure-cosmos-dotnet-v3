//-----------------------------------------------------------------------
// <copyright file="Utils.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Tests.Query.OfflineEngine
{
    using System;
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
        public static bool TryCompare(JToken left, JToken right, out int comparison)
        {
            comparison = default(int);
            if (left == null || right == null)
            {
                return false;
            }

            JsonType leftJsonType = JsonTypeUtils.JTokenTypeToJsonType(left.Type);
            JsonType rightJsonType = JsonTypeUtils.JTokenTypeToJsonType(right.Type);
            if (leftJsonType != rightJsonType)
            {
                return false;
            }

            if (!Utils.IsPrimitive(left))
            {
                return false;
            }

            bool isComparable;
            try
            {
                comparison = JTokenComparer.Singleton.Compare(left, right);
                isComparable = true;
            }
            catch (InvalidOperationException)
            {
                isComparable = false;
            }

            return isComparable;
        }

        /// <summary>
        /// Compares the two json based on their type.
        /// </summary>
        /// <param name="left">The left JToken.</param>
        /// <param name="right">The right JToken.</param>
        /// <returns>The comparison.</returns>
        public static int CompareAcrossTypes(JToken left, JToken right)
        {
            return JTokenComparer.Singleton.Compare(left, right);
        }

        /// <summary>
        /// Tries to add two JTokens as numbers.
        /// </summary>
        /// <param name="left">The left JToken.</param>
        /// <param name="right">The right JToken.</param>
        /// <param name="result">The result of the addition if successful.</param>
        /// <returns>Whether or not the addition is successful.</returns>
        public static bool TryAddNumbers(JToken left, JToken right, out JToken result)
        {
            result = default(JToken);
            if (left == null || right == null)
            {
                return false;
            }

            JsonType leftJsonType = JsonTypeUtils.JTokenTypeToJsonType(left.Type);
            JsonType rightJsonType = JsonTypeUtils.JTokenTypeToJsonType(right.Type);

            if (leftJsonType != JsonType.Number || rightJsonType != JsonType.Number)
            {
                return false;
            }

            result = left.ToObject<double>() + right.ToObject<double>();
            return true;
        }

        /// <summary>
        /// Tries to convert the JToken to a number.
        /// </summary>
        /// <param name="numberToken">The token to try and convert to a number.</param>
        /// <param name="number">The number value if convertible.</param>
        /// <returns>Whether or not the JToken was successfully converted.</returns>
        public static bool TryConvertToNumber(JToken numberToken, out double number)
        {
            number = default(double);
            if (numberToken == null)
            {
                return false;
            }

            JsonType jsonType = JsonTypeUtils.JTokenTypeToJsonType(numberToken.Type);
            bool isConvertible = jsonType == JsonType.Number;
            if (isConvertible)
            {
                number = numberToken.Value<double>();
            }

            return isConvertible;
        }

        /// <summary>
        /// Tries to convert the number to an int.
        /// </summary>
        /// <param name="number">The token to try and convert to a number.</param>
        /// <param name="integer">The number value if convertible.</param>
        /// <returns>Whether or not the JToken was successfully converted.</returns>
        public static bool TryConvertToInteger(double number, out long integer)
        {
            // optimistic cast to long
            unchecked
            {
                integer = (long)number;
                return number == (double)integer;
            }
        }

        /// <summary>
        /// Tries to convert the JToken to a string.
        /// </summary>
        /// <param name="stringToken">The token to try and convert to a string.</param>
        /// <param name="stringValue">The string value if convertible.</param>
        /// <returns>Whether or not the JToken was successfully converted.</returns>
        public static bool TryConvertToString(JToken stringToken, out string stringValue)
        {
            stringValue = default(string);
            if (stringToken == null)
            {
                return false;
            }

            JsonType jsonType = JsonTypeUtils.JTokenTypeToJsonType(stringToken.Type);
            bool isConvertible = jsonType == JsonType.String;
            if (isConvertible)
            {
                stringValue = stringToken.Value<string>();
            }

            return isConvertible;
        }

        /// <summary>
        /// Tries to convert the JToken to a boolean.
        /// </summary>
        /// <param name="booleanToken">The token to try and convert to a boolean.</param>
        /// <param name="boolean">The boolean value if convertible.</param>
        /// <returns>Whether or not the JToken was successfully converted.</returns>
        public static bool TryConvertToBoolean(JToken booleanToken, out bool boolean)
        {
            boolean = default(bool);
            if (booleanToken == null)
            {
                return false;
            }

            JsonType jsonType = JsonTypeUtils.JTokenTypeToJsonType(booleanToken.Type);
            bool isConvertible = jsonType == JsonType.Boolean;
            if (isConvertible)
            {
                boolean = booleanToken.Value<bool>();
            }

            return isConvertible;
        }

        /// <summary>
        /// Returns whether or not the JToken is true.
        /// </summary>
        /// <param name="token">The token.</param>
        /// <returns>Whether or not the JToken is true.</returns>
        public static bool IsTrue(JToken token)
        {
            if (!TryConvertToBoolean(token, out bool boolean))
            {
                boolean = false;
            }

            return boolean;
        }

        public static bool IsPrimitive(JToken token)
        {
            if (token == null)
            {
                return false;
            }

            JsonType type = JsonTypeUtils.JTokenTypeToJsonType(token.Type);
            return type == JsonType.Boolean || type == JsonType.Null || type == JsonType.Number || type == JsonType.String;
        }
    }
}
