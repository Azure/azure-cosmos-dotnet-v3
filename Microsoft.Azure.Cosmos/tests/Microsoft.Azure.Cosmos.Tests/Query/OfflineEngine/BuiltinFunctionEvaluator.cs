//-----------------------------------------------------------------------
// <copyright file="FunctionCallEvaluator.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Tests.Query.OfflineEngine
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using Newtonsoft.Json.Linq;

    internal static class BuiltinFunctionEvaluator
    {
        private static readonly JToken Undefined = null;

        private static readonly HashSet<BuiltinFunctionName> NullableFunctions = new HashSet<BuiltinFunctionName>()
        {
            BuiltinFunctionName.IS_ARRAY,
            BuiltinFunctionName.IS_BOOL,
            BuiltinFunctionName.IS_DEFINED,
            BuiltinFunctionName.IS_NULL,
            BuiltinFunctionName.IS_NUMBER,
            BuiltinFunctionName.IS_OBJECT,
            BuiltinFunctionName.IS_PRIMITIVE,
            BuiltinFunctionName.IS_STRING,
            BuiltinFunctionName.ARRAY_CONTAINS,
        };

        private enum BuiltinFunctionName
        {
            ABS,
            ACOS,
            ARRAY_CONCAT,
            ARRAY_CONTAINS,
            ARRAY_LENGTH,
            ARRAY_SLICE,
            ASIN,
            ATAN,
            ATN2,
            CEILING,
            CONCAT,
            CONTAINS,
            COS,
            COT,
            DEGREES,
            ENDSWITH,
            EXP,
            FLOOR,
            INDEX_OF,
            IS_ARRAY,
            IS_BOOL,
            IS_DEFINED,
            IS_NULL,
            IS_NUMBER,
            IS_OBJECT,
            IS_PRIMITIVE,
            IS_STRING,
            LEFT,
            LENGTH,
            LOG,
            LOG10,
            LOWER,
            LTRIM,
            PI,
            POWER,
            RADIANS,
            REPLACE,
            REPLICATE,
            REVERSE,
            RIGHT,
            ROUND,
            RTRIM,
            SIGN,
            SIN,
            SQRT,
            SQUARE,
            STARTSWITH,
            SUBSTRING,
            TAN,
            TRUNC,
            TOSTRING,
            UPPER
        }

        public static JToken EvaluateFunctionCall(string name, IReadOnlyList<JToken> arguments)
        {
            if (!Enum.TryParse(value: name, ignoreCase: true, result: out BuiltinFunctionName builtinFunction))
            {
                throw new ArgumentException($"Unknown builtin function name: {name}");
            }

            // TODO: make the nullable function check based on the function signature and parameters.
            if (arguments.Any((arugment) => arugment == Undefined) && !NullableFunctions.Contains(builtinFunction))
            {
                return Undefined;
            }

            int argumentCount = arguments.Count;
            JToken result;
            switch (builtinFunction)
            {
                case BuiltinFunctionName.ABS:
                    result = ExecuteOneArgumentFunction(BuiltinFunctionEvaluator.ABS, builtinFunction, arguments);
                    break;

                case BuiltinFunctionName.ACOS:
                    result = ExecuteOneArgumentFunction(BuiltinFunctionEvaluator.ACOS, builtinFunction, arguments);
                    break;

                case BuiltinFunctionName.ARRAY_CONCAT:
                    result = ExecuteAtleastTwoArgumentFunction(BuiltinFunctionEvaluator.ARRAY_CONCAT, builtinFunction, arguments);
                    break;

                case BuiltinFunctionName.ARRAY_CONTAINS:
                    result = ExecuteTwoOrThreeArgumentFunction(BuiltinFunctionEvaluator.ARRAY_CONTAINS, builtinFunction, arguments);
                    break;

                case BuiltinFunctionName.ARRAY_LENGTH:
                    result = ExecuteOneArgumentFunction(BuiltinFunctionEvaluator.ARRAY_LENGTH, builtinFunction, arguments);
                    break;

                case BuiltinFunctionName.ARRAY_SLICE:
                    result = ExecuteTwoOrThreeArgumentFunction(BuiltinFunctionEvaluator.ARRAY_SLICE, builtinFunction, arguments);
                    break;

                case BuiltinFunctionName.ASIN:
                    result = ExecuteOneArgumentFunction(BuiltinFunctionEvaluator.ASIN, builtinFunction, arguments);
                    break;

                case BuiltinFunctionName.ATAN:
                    result = ExecuteOneArgumentFunction(BuiltinFunctionEvaluator.ATAN, builtinFunction, arguments);
                    break;

                case BuiltinFunctionName.ATN2:
                    result = ExecuteTwoArgumentFunction(BuiltinFunctionEvaluator.ATN2, builtinFunction, arguments);
                    break;

                case BuiltinFunctionName.CEILING:
                    result = ExecuteOneArgumentFunction(BuiltinFunctionEvaluator.CEILING, builtinFunction, arguments);
                    break;

                case BuiltinFunctionName.CONCAT:
                    result = ExecuteAtleastTwoArgumentFunction(BuiltinFunctionEvaluator.CONCAT, builtinFunction, arguments);
                    break;

                case BuiltinFunctionName.CONTAINS:
                    result = ExecuteTwoArgumentFunction(BuiltinFunctionEvaluator.CONTAINS, builtinFunction, arguments);
                    break;

                case BuiltinFunctionName.COS:
                    result = ExecuteOneArgumentFunction(BuiltinFunctionEvaluator.COS, builtinFunction, arguments);
                    break;

                case BuiltinFunctionName.COT:
                    result = ExecuteOneArgumentFunction(BuiltinFunctionEvaluator.COT, builtinFunction, arguments);
                    break;

                case BuiltinFunctionName.DEGREES:
                    result = ExecuteOneArgumentFunction(BuiltinFunctionEvaluator.DEGREES, builtinFunction, arguments);
                    break;

                case BuiltinFunctionName.ENDSWITH:
                    result = ExecuteTwoArgumentFunction(BuiltinFunctionEvaluator.ENDSWITH, builtinFunction, arguments);
                    break;

                case BuiltinFunctionName.EXP:
                    result = ExecuteOneArgumentFunction(BuiltinFunctionEvaluator.EXP, builtinFunction, arguments);
                    break;

                case BuiltinFunctionName.FLOOR:
                    result = ExecuteOneArgumentFunction(BuiltinFunctionEvaluator.FLOOR, builtinFunction, arguments);
                    break;

                case BuiltinFunctionName.INDEX_OF:
                    result = ExecuteTwoArgumentFunction(BuiltinFunctionEvaluator.INDEX_OF, builtinFunction, arguments);
                    break;

                case BuiltinFunctionName.IS_ARRAY:
                    result = ExecuteOneArgumentFunction(BuiltinFunctionEvaluator.IS_ARRAY, builtinFunction, arguments);
                    break;

                case BuiltinFunctionName.IS_BOOL:
                    result = ExecuteOneArgumentFunction(BuiltinFunctionEvaluator.IS_BOOL, builtinFunction, arguments);
                    break;

                case BuiltinFunctionName.IS_DEFINED:
                    result = ExecuteOneArgumentFunction(BuiltinFunctionEvaluator.IS_DEFINED, builtinFunction, arguments);
                    break;

                case BuiltinFunctionName.IS_NULL:
                    result = ExecuteOneArgumentFunction(BuiltinFunctionEvaluator.IS_NULL, builtinFunction, arguments);
                    break;

                case BuiltinFunctionName.IS_NUMBER:
                    result = ExecuteOneArgumentFunction(BuiltinFunctionEvaluator.IS_NUMBER, builtinFunction, arguments);
                    break;

                case BuiltinFunctionName.IS_OBJECT:
                    result = ExecuteOneArgumentFunction(BuiltinFunctionEvaluator.IS_OBJECT, builtinFunction, arguments);
                    break;

                case BuiltinFunctionName.IS_PRIMITIVE:
                    result = ExecuteOneArgumentFunction(BuiltinFunctionEvaluator.IS_PRIMITIVE, builtinFunction, arguments);
                    break;

                case BuiltinFunctionName.IS_STRING:
                    result = ExecuteOneArgumentFunction(BuiltinFunctionEvaluator.IS_STRING, builtinFunction, arguments);
                    break;

                case BuiltinFunctionName.LEFT:
                    result = ExecuteTwoArgumentFunction(BuiltinFunctionEvaluator.LEFT, builtinFunction, arguments);
                    break;

                case BuiltinFunctionName.LENGTH:
                    result = ExecuteOneArgumentFunction(BuiltinFunctionEvaluator.LENGTH, builtinFunction, arguments);
                    break;

                case BuiltinFunctionName.LOG:
                    result = ExecuteOneOrTwoArgumentFunction(BuiltinFunctionEvaluator.LOG, builtinFunction, arguments);
                    break;

                case BuiltinFunctionName.LOG10:
                    result = ExecuteOneArgumentFunction(BuiltinFunctionEvaluator.LOG10, builtinFunction, arguments);
                    break;

                case BuiltinFunctionName.LOWER:
                    result = ExecuteOneArgumentFunction(BuiltinFunctionEvaluator.LOWER, builtinFunction, arguments);
                    break;

                case BuiltinFunctionName.LTRIM:
                    result = ExecuteOneArgumentFunction(BuiltinFunctionEvaluator.LTRIM, builtinFunction, arguments);
                    break;

                case BuiltinFunctionName.PI:
                    result = ExecuteZeroArgumentFunction(BuiltinFunctionEvaluator.PI, builtinFunction, arguments);
                    break;

                case BuiltinFunctionName.POWER:
                    result = ExecuteTwoArgumentFunction(BuiltinFunctionEvaluator.POWER, builtinFunction, arguments);
                    break;

                case BuiltinFunctionName.RADIANS:
                    result = ExecuteOneArgumentFunction(BuiltinFunctionEvaluator.RADIANS, builtinFunction, arguments);
                    break;

                case BuiltinFunctionName.REPLACE:
                    result = ExecuteThreeArgumentFunction(BuiltinFunctionEvaluator.REPLACE, builtinFunction, arguments);
                    break;

                case BuiltinFunctionName.REPLICATE:
                    result = ExecuteTwoArgumentFunction(BuiltinFunctionEvaluator.REPLICATE, builtinFunction, arguments);
                    break;

                case BuiltinFunctionName.REVERSE:
                    result = ExecuteOneArgumentFunction(BuiltinFunctionEvaluator.REVERSE, builtinFunction, arguments);
                    break;

                case BuiltinFunctionName.RIGHT:
                    result = ExecuteTwoArgumentFunction(BuiltinFunctionEvaluator.RIGHT, builtinFunction, arguments);
                    break;

                case BuiltinFunctionName.ROUND:
                    result = ExecuteOneArgumentFunction(BuiltinFunctionEvaluator.ROUND, builtinFunction, arguments);
                    break;

                case BuiltinFunctionName.RTRIM:
                    result = ExecuteOneArgumentFunction(BuiltinFunctionEvaluator.RTRIM, builtinFunction, arguments);
                    break;

                case BuiltinFunctionName.SIGN:
                    result = ExecuteOneArgumentFunction(BuiltinFunctionEvaluator.SIGN, builtinFunction, arguments);
                    break;

                case BuiltinFunctionName.SIN:
                    result = ExecuteOneArgumentFunction(BuiltinFunctionEvaluator.SIN, builtinFunction, arguments);
                    break;

                case BuiltinFunctionName.SQRT:
                    result = ExecuteOneArgumentFunction(BuiltinFunctionEvaluator.SQRT, builtinFunction, arguments);
                    break;

                case BuiltinFunctionName.SQUARE:
                    result = ExecuteOneArgumentFunction(BuiltinFunctionEvaluator.SQUARE, builtinFunction, arguments);
                    break;

                case BuiltinFunctionName.STARTSWITH:
                    result = ExecuteTwoArgumentFunction(BuiltinFunctionEvaluator.STARTSWITH, builtinFunction, arguments);
                    break;

                case BuiltinFunctionName.SUBSTRING:
                    result = ExecuteThreeArgumentFunction(BuiltinFunctionEvaluator.SUBSTRING, builtinFunction, arguments);
                    break;

                case BuiltinFunctionName.TAN:
                    result = ExecuteOneArgumentFunction(BuiltinFunctionEvaluator.TAN, builtinFunction, arguments);
                    break;

                case BuiltinFunctionName.TRUNC:
                    result = ExecuteOneArgumentFunction(BuiltinFunctionEvaluator.TRUNC, builtinFunction, arguments);
                    break;

                case BuiltinFunctionName.TOSTRING:
                    result = ExecuteOneArgumentFunction(BuiltinFunctionEvaluator.TOSTRING, builtinFunction, arguments);
                    break;

                case BuiltinFunctionName.UPPER:
                    result = ExecuteOneArgumentFunction(BuiltinFunctionEvaluator.UPPER, builtinFunction, arguments);
                    break;

                default:
                    throw new ArgumentException($"Unknown {nameof(BuiltinFunctionName)}: {builtinFunction}");
            }

            return result;
        }

        /// <summary>
        /// Returns the absolute (positive) value of the specified numeric expression.
        /// </summary>
        /// <param name="number">The numeric expression to take the absolute value of.</param>
        /// <returns>The absolute (positive) value of the specified numeric expression.</returns>
        private static JToken ABS(JToken number)
        {
            return ExecuteOneArgumentNumberFunction(Math.Abs, number);
        }

        /// <summary>
        /// Returns the angle, in radians, whose cosine is the specified numeric expression; also called arccosine.
        /// </summary>
        /// <param name="number">The numeric expression to take the arccosine of.</param>
        /// <returns>The angle, in radians, whose cosine is the specified numeric expression; also called arccosine.</returns>
        private static JToken ACOS(JToken number)
        {
            return ExecuteOneArgumentNumberFunction(Math.Acos, number);
        }

        /// <summary>
        /// Returns an array that is the result of concatenating two or more array values.
        /// </summary>
        /// <param name="first">The first array expression.</param>
        /// <param name="second">The second array expression.</param>
        /// <param name="arrays">The remaining (optional) arrays.</param>
        /// <returns>An array that is the result of concatenating two or more array values.</returns>
        private static JToken ARRAY_CONCAT(JToken first, JToken second, params JToken[] arrays)
        {
            bool allArrays = first.Type == JTokenType.Array && second.Type == JTokenType.Array;
            if (arrays != null)
            {
                foreach (JToken array in arrays)
                {
                    allArrays &= array.Type == JTokenType.Array;
                }
            }

            if (!allArrays)
            {
                return Undefined;
            }

            JArray concatenatedArray = new JArray();
            foreach (JToken arrayItem in (JArray)first)
            {
                concatenatedArray.Add(arrayItem);
            }

            foreach (JToken arrayItem in (JArray)second)
            {
                concatenatedArray.Add(arrayItem);
            }

            if (arrays != null)
            {
                foreach (JArray array in arrays)
                {
                    foreach (JToken arrayItem in array)
                    {
                        concatenatedArray.Add(arrayItem);
                    }
                }
            }

            return concatenatedArray;
        }

        /// <summary>
        /// Returns a Boolean indicating whether the array contains the specified value. Can specify if the match is full or partial.
        /// </summary>
        /// <param name="haystack">The array to look in.</param>
        /// <param name="needle">The value to look for.</param>
        /// <param name="partialMatchToken">If the match is full or partial.</param>
        /// <returns>A Boolean indicating whether the array contains the specified value.</returns>
        private static JToken ARRAY_CONTAINS(JToken haystack, JToken needle, JToken partialMatchToken = null)
        {
            if (partialMatchToken == Undefined)
            {
                partialMatchToken = false;
            }

            if (haystack == Undefined || needle == Undefined || partialMatchToken == Undefined)
            {
                return Undefined;
            }

            bool haystackIsArray = haystack.Type == JTokenType.Array;
            bool partialMatchValue = false;
            bool partialMatchIsBool = partialMatchToken == null || Utils.TryConvertToBoolean(partialMatchToken, out partialMatchValue);

            if (!haystackIsArray || !partialMatchIsBool)
            {
                return Undefined;
            }

            bool contains = false;
            foreach (JToken hay in (JArray)haystack)
            {
                if (partialMatchValue)
                {
                    bool partialMatch;
                    if (needle.Type == JTokenType.Object)
                    {
                        JObject needleObject = (JObject)needle;
                        partialMatch = true;
                        foreach (KeyValuePair<string, JToken> kvp in needleObject)
                        {
                            string name = kvp.Key;
                            JToken value1 = kvp.Value;

                            if (hay.Type == JTokenType.Object && ((JObject)hay).TryGetValue(name, out JToken value2))
                            {
                                partialMatch &= JsonTokenEqualityComparer.Value.Equals(value1, value2);
                            }
                            else
                            {
                                partialMatch = false;
                            }
                        }
                    }
                    else
                    {
                        partialMatch = JsonTokenEqualityComparer.Value.Equals(hay, needle);
                    }

                    contains |= partialMatch;
                }
                else
                {
                    contains |= JsonTokenEqualityComparer.Value.Equals(hay, needle);
                }
            }

            return contains;
        }

        /// <summary>
        /// Returns the number of elements of the specified array expression.
        /// </summary>
        /// <param name="array">The array expression to find the length of.</param>
        /// <returns>The number of elements of the specified array expression.</returns>
        private static JToken ARRAY_LENGTH(JToken array)
        {
            JToken result;
            if (array.Type == JTokenType.Array)
            {
                result = ((JArray)array).Count;
            }
            else
            {
                result = Undefined;
            }

            return result;
        }

        /// <summary>
        /// Returns part of an array expression.
        /// </summary>
        /// <param name="value">The array expression to slice.</param>
        /// <param name="startIndex">The start index to slice from.</param>
        /// <param name="count">The end index to slice to.</param>
        /// <returns>Part of an array expression.</returns>
        private static JToken ARRAY_SLICE(JToken value, JToken startIndex, JToken count = null)
        {
            if (count == null)
            {
                count = int.MaxValue;
            }

            JToken result = Undefined;

            bool valueIsArray = value.Type == JTokenType.Array;
            bool startIndexIsNumber = Utils.TryConvertToNumber(startIndex, out double startIndexAsNumber);
            bool countIsNumber = Utils.TryConvertToNumber(count, out double countAsNumber);

            if (valueIsArray && startIndexIsNumber && countIsNumber)
            {
                JArray array = (JArray)value;

                if (!Utils.TryConvertToInteger(startIndexAsNumber, out long startIndexAsInteger))
                {
                    return Undefined;
                }

                if (startIndexAsInteger < 0)
                {
                    startIndexAsInteger += array.Count;
                    if (startIndexAsInteger < 0)
                    {
                        startIndexAsInteger = 0;
                    }
                }

                if (!Utils.TryConvertToInteger(countAsNumber, out long countAsInteger))
                {
                    return Undefined;
                }

                countAsInteger = Math.Min(array.Count - startIndexAsInteger, countAsInteger);

                if (array.Count <= 0 || (startIndexAsInteger <= 0 && countAsInteger >= array.Count))
                {
                    return array;
                }
                else
                {
                    if (startIndexAsInteger >= array.Count || countAsInteger <= 0)
                    {
                        // Empty Array
                        return new JArray();
                    }

                    return new JArray(
                        array
                        .Skip((int)Math.Min(startIndexAsInteger, int.MaxValue))
                        .Take((int)Math.Min(countAsInteger, int.MaxValue))
                        .ToArray());
                }
            }

            return result;
        }

        /// <summary>
        /// Returns the angle, in radians, whose sine is the specified numeric expression. This is also called arcsine.
        /// </summary>
        /// <param name="number">The numeric expression to take the arcsine of.</param>
        /// <returns>The angle, in radians, whose sine is the specified numeric expression. This is also called arcsine.</returns>
        private static JToken ASIN(JToken number)
        {
            return ExecuteOneArgumentNumberFunction(Math.Asin, number);
        }

        /// <summary>
        /// Returns the angle, in radians, whose tangent is the specified numeric expression. This is also called arctangent.
        /// </summary>
        /// <param name="number">The numeric expression.</param>
        /// <returns>The angle, in radians, whose tangent is the specified numeric expression. This is also called arctangent.</returns>
        private static JToken ATAN(JToken number)
        {
            return ExecuteOneArgumentNumberFunction(Math.Atan, number);
        }

        /// <summary>
        /// Returns the angle, in radians, between the positive x-axis and the ray from the origin to the point (y, x), where x and y are the values of the two specified float expressions.
        /// </summary>
        /// <param name="x">x coordinate of the point.</param>
        /// <param name="y">y coordinate of the point.</param>
        /// <returns>the angle, in radians, between the positive x-axis and the ray from the origin to the point (y, x), where x and y are the values of the two specified float expressions.</returns>
        private static JToken ATN2(JToken x, JToken y)
        {
            return ExecuteTwoArgumentNumberFunction(Math.Atan2, x, y);
        }

        /// <summary>
        /// Returns the smallest integer value greater than, or equal to, the specified numeric expression.
        /// </summary>
        /// <param name="number">The numeric expression.</param>
        /// <returns>The smallest integer value greater than, or equal to, the specified numeric expression.</returns>
        private static JToken CEILING(JToken number)
        {
            return ExecuteOneArgumentNumberFunction(Math.Ceiling, number);
        }

        /// <summary>
        /// Returns a string that is the result of concatenating two or more string values.
        /// </summary>
        /// <param name="string1">The first string expression.</param>
        /// <param name="string2">The second string expression.</param>
        /// <param name="otherStrings">The remaining (optional) strings.</param>
        /// <returns>A string that is the result of concatenating two or more string values.</returns>
        private static JToken CONCAT(JToken string1, JToken string2, params JToken[] otherStrings)
        {
            bool allString = JsonTypeUtils.JTokenTypeToJsonType(string1.Type) == JsonType.String
                && JsonTypeUtils.JTokenTypeToJsonType(string2.Type) == JsonType.String;

            if (otherStrings != null)
            {
                foreach (JToken otherString in otherStrings)
                {
                    allString &= JsonTypeUtils.JTokenTypeToJsonType(otherString.Type) == JsonType.String;
                }
            }

            if (!allString)
            {
                return Undefined;
            }

            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(string1.Value<string>());
            stringBuilder.Append(string2.Value<string>());
            if (otherStrings != null)
            {
                foreach (JToken otherString in otherStrings)
                {
                    stringBuilder.Append(otherString.Value<string>());
                }
            }

            return stringBuilder.ToString();
        }

        /// <summary>
        /// Returns a Boolean indicating whether the first string expression contains the second.
        /// </summary>
        /// <param name="haystack">The string expression to search in.</param>
        /// <param name="needle">The string expression to look search for.</param>
        /// <returns>A Boolean indicating whether the first string expression contains the second.</returns>
        private static JToken CONTAINS(JToken haystack, JToken needle)
        {
            return ExecuteTwoArgumentStringFunction((value1, value2) => value1.Contains(value2), haystack, needle);
        }

        /// <summary>
        /// Returns the trigonometric cosine of the specified angle, in radians, in the specified expression.
        /// </summary>
        /// <param name="number">The numeric expression to take the cosine of.</param>
        /// <returns>The trigonometric cosine of the specified angle, in radians, in the specified expression.</returns>
        private static JToken COS(JToken number)
        {
            return ExecuteOneArgumentNumberFunction(Math.Cos, number);
        }

        /// <summary>
        /// Returns the trigonometric cotangent of the specified angle, in radians, in the specified numeric expression.
        /// </summary>
        /// <param name="number">The numeric expression to take the cotangent of.</param>
        /// <returns>The trigonometric cotangent of the specified angle, in radians, in the specified numeric expression.</returns>
        private static JToken COT(JToken number)
        {
            return ExecuteOneArgumentNumberFunction((value) => 1.0 / Math.Tan(value), number);
        }

        /// <summary>
        /// Returns the corresponding angle in degrees for an angle specified in radians.
        /// </summary>
        /// <param name="number">The angle specified in radians to convert to degrees.</param>
        /// <returns>The corresponding angle in degrees for an angle specified in radians.</returns>
        private static JToken DEGREES(JToken number)
        {
            return ExecuteOneArgumentNumberFunction((radians) => 180 / Math.PI * radians, number);
        }

        /// <summary>
        /// Returns a Boolean indicating whether the first string expression ends with the second.
        /// </summary>
        /// <param name="str">The string expression to check.</param>
        /// <param name="suffix">The suffix to look for.</param>
        /// <returns>a Boolean indicating whether the first string expression ends with the second.</returns>
        private static JToken ENDSWITH(JToken str, JToken suffix)
        {
            return ExecuteTwoArgumentStringFunction((value1, value2) => value1.EndsWith(value2, StringComparison.Ordinal), str, suffix);
        }

        /// <summary>
        /// Returns the exponent of the specified numeric expression.
        /// </summary>
        /// <param name="number">The numeric expression to take the exponent of.</param>
        /// <returns>The exponent of the specified numeric expression.</returns>
        private static JToken EXP(JToken number)
        {
            return ExecuteOneArgumentNumberFunction(Math.Exp, number);
        }

        /// <summary>
        /// Returns the largest integer less than or equal to the specified numeric expression.
        /// </summary>
        /// <param name="number">The number to take the floor of.</param>
        /// <returns>The largest integer less than or equal to the specified numeric expression.</returns>
        private static JToken FLOOR(JToken number)
        {
            return ExecuteOneArgumentNumberFunction(Math.Floor, number);
        }

        /// <summary>
        /// Returns the starting position of the first occurrence of the second string expression within the first specified string expression, or -1 if the string is not found.
        /// </summary>
        /// <param name="str">The string expression to look in.</param>
        /// <param name="substring">The string expression to look for.</param>
        /// <returns>The starting position of the first occurrence of the second string expression within the first specified string expression, or -1 if the string is not found.</returns>
        private static JToken INDEX_OF(JToken str, JToken substring)
        {
            return ExecuteTwoArgumentStringFunction((value1, value2) => value1.IndexOf(value2, StringComparison.Ordinal), str, substring);
        }

        /// <summary>
        /// Returns a Boolean indicating if the type of the value is an array.
        /// </summary>
        /// <param name="value">The expression to check if it's an array.</param>
        /// <returns>A Boolean indicating if the type of the value is an array.</returns>
        private static JToken IS_ARRAY(JToken value)
        {
            if (value == Undefined)
            {
                return false;
            }

            return JsonTypeUtils.JTokenTypeToJsonType(value.Type) == JsonType.Array;
        }

        /// <summary>
        /// Returns a Boolean indicating if the type of the value is a Boolean.
        /// </summary>
        /// <param name="value">The expression to check if it is a boolean.</param>
        /// <returns>A Boolean indicating if the type of the value is a Boolean.</returns>
        private static JToken IS_BOOL(JToken value)
        {
            if (value == Undefined)
            {
                return false;
            }

            return JsonTypeUtils.JTokenTypeToJsonType(value.Type) == JsonType.Boolean;
        }

        /// <summary>
        /// Returns a Boolean indicating if the property has been assigned a value.
        /// </summary>
        /// <param name="value">The expression to check if it is defined.</param>
        /// <returns>A Boolean indicating if the property has been assigned a value.</returns>
        private static JToken IS_DEFINED(JToken value)
        {
            return value != Undefined;
        }

        /// <summary>
        /// Returns a Boolean indicating if the type of the value is null.
        /// </summary>
        /// <param name="value">The expression to check if it is null.</param>
        /// <returns>A Boolean indicating if the type of the value is null.</returns>
        private static JToken IS_NULL(JToken value)
        {
            if (value == Undefined)
            {
                return false;
            }

            return JsonTypeUtils.JTokenTypeToJsonType(value.Type) == JsonType.Null;
        }

        /// <summary>
        /// Returns a Boolean indicating if the type of the value is a number.
        /// </summary>
        /// <param name="value">The expression to check if it is a number.</param>
        /// <returns>A Boolean indicating if the type of the value is a number.</returns>
        private static JToken IS_NUMBER(JToken value)
        {
            if (value == Undefined)
            {
                return false;
            }

            return JsonTypeUtils.JTokenTypeToJsonType(value.Type) == JsonType.Number;
        }

        /// <summary>
        /// Returns a Boolean indicating if the type of the value is a JSON object.
        /// </summary>
        /// <param name="value">The expression to check if it is an object.</param>
        /// <returns>A Boolean indicating if the type of the value is a JSON object.</returns>
        private static JToken IS_OBJECT(JToken value)
        {
            if (value == Undefined)
            {
                return false;
            }

            return JsonTypeUtils.JTokenTypeToJsonType(value.Type) == JsonType.Object;
        }

        /// <summary>
        /// Returns a Boolean indicating if the type of the value is a string, number, Boolean or null.
        /// </summary>
        /// <param name="value">The expression to check if it is a primitive.</param>
        /// <returns>A Boolean indicating if the type of the value is a string, number, Boolean or null.</returns>
        private static JToken IS_PRIMITIVE(JToken value)
        {
            return Utils.IsPrimitive(value);
        }

        /// <summary>
        /// Returns a Boolean indicating if the type of the value is a string.
        /// </summary>
        /// <param name="value">The expression to check if it is a string.</param>
        /// <returns>A Boolean indicating if the type of the value is a string.</returns>
        private static JToken IS_STRING(JToken value)
        {
            if (value == Undefined)
            {
                return false;
            }

            return JsonTypeUtils.JTokenTypeToJsonType(value.Type) == JsonType.String;
        }

        /// <summary>
        /// Returns the left part of a string with the specified number of characters.
        /// </summary>
        /// <param name="str">The string to take the left part of.</param>
        /// <param name="length">The number of characters to take.</param>
        /// <returns>The left part of a string with the specified number of characters.</returns>
        private static JToken LEFT(JToken str, JToken length)
        {
            return ExecuteSubstring(str, 0, length);
        }

        /// <summary>
        /// Returns the number of characters of the specified string expression.
        /// </summary>
        /// <param name="str">The string expression to take the length of.</param>
        /// <returns>The number of characters of the specified string expression.</returns>
        private static JToken LENGTH(JToken str)
        {
            return ExecuteOneArgumentStringFunction((value) => value.Length, str);
        }

        /// <summary>
        /// Returns the natural logarithm of the specified numeric expression, or the logarithm using the specified base.
        /// </summary>
        /// <param name="number">The number to take the log of.</param>
        /// <param name="numberBase">The (optional) base.</param>
        /// <returns>The natural logarithm of the specified numeric expression, or the logarithm using the specified base.</returns>
        private static JToken LOG(JToken number, JToken numberBase = null)
        {
            return ExecuteOneArgumentNumberFunction(Math.Log, number);
        }

        /// <summary>
        /// Returns the base-10 logarithmic value of the specified numeric expression.
        /// </summary>
        /// <param name="number">The number to take the base-10 log of.</param>
        /// <returns>The base-10 logarithmic value of the specified numeric expression.</returns>
        private static JToken LOG10(JToken number)
        {
            return ExecuteOneArgumentNumberFunction(Math.Log10, number);
        }

        /// <summary>
        /// Returns a string expression after converting uppercase character data to lowercase.
        /// </summary>
        /// <param name="str">The string to lowercase.</param>
        /// <returns>A string expression after converting uppercase character data to lowercase.</returns>
        private static JToken LOWER(JToken str)
        {
            return ExecuteOneArgumentStringFunction((value) => value.ToLower(CultureInfo.InvariantCulture), str);
        }

        /// <summary>
        /// Returns a string expression after it removes leading blanks.
        /// </summary>
        /// <param name="str">The string to remove leading blanks from.</param>
        /// <returns>A string expression after it removes leading blanks.</returns>
        private static JToken LTRIM(JToken str)
        {
            return ExecuteOneArgumentStringFunction((value) => value.TrimStart(), str);
        }

        /// <summary>
        /// Returns the constant value of PI.
        /// </summary>
        /// <returns>The constant value of PI.</returns>
        private static JToken PI()
        {
            return Math.PI;
        }

        /// <summary>
        /// Returns the power of the specified numeric expression to the value specified.
        /// </summary>
        /// <param name="baseNumber">The base number to take the power of.</param>
        /// <param name="exponentNumber">The exponent value.</param>
        /// <returns>The power of the specified numeric expression to the value specified.</returns>
        private static JToken POWER(JToken baseNumber, JToken exponentNumber)
        {
            return ExecuteTwoArgumentNumberFunction(Math.Pow, baseNumber, exponentNumber);
        }

        /// <summary>
        /// Returns radians when a numeric expression, in degrees, is entered.
        /// </summary>
        /// <param name="number">The number expression, in degrees, to take the power of.</param>
        /// <returns>Radians when a numeric expression, in degrees, is entered.</returns>
        private static JToken RADIANS(JToken number)
        {
            return ExecuteOneArgumentNumberFunction((degrees) => Math.PI * degrees / 180.0, number);
        }

        /// <summary>
        /// Replaces all occurrences of a specified string value with another string value.
        /// </summary>
        /// <param name="stringValue">The string to have substrings replaced.</param>
        /// <param name="subString">The string to look for.</param>
        /// <param name="replacement">The string to replace with.</param>
        /// <returns>A string with all occurrences of a specified string value with another string value.</returns>
        private static JToken REPLACE(JToken stringValue, JToken subString, JToken replacement)
        {
            if (!Utils.TryConvertToString(stringValue, out string stringValueValue))
            {
                return Undefined;
            }

            if (!Utils.TryConvertToString(subString, out string subStringValue))
            {
                return Undefined;
            }

            if (subStringValue.Length == 0)
            {
                return Undefined;
            }

            if (!Utils.TryConvertToString(replacement, out string replacementValue))
            {
                return Undefined;
            }

            return stringValueValue.Replace(subStringValue, replacementValue);
        }

        /// <summary>
        /// Repeats a string value a specified number of times.
        /// </summary>
        /// <param name="str">The string to replicate.</param>
        /// <param name="repeatCount">The number of times to replicate the value.</param>
        /// <returns>The string value repeated a specified number of times.</returns>
        private static JToken REPLICATE(JToken str, JToken repeatCount)
        {
            if (!Utils.TryConvertToString(str, out string strValue))
            {
                return Undefined;
            }

            if (!Utils.TryConvertToNumber(repeatCount, out double repeatCountValue))
            {
                return Undefined;
            }

            if (repeatCountValue < 0)
            {
                return Undefined;
            }

            try
            {
                checked
                {
                    if (strValue.Length * (long)repeatCountValue > 10000)
                    {
                        return Undefined;
                    }
                }
            }
            catch (ArithmeticException)
            {
                return Undefined;
            }

            StringBuilder stringBuilder = new StringBuilder();
            for (int i = 0; i < (long)repeatCountValue; i++)
            {
                stringBuilder.Append(strValue);
            }

            return stringBuilder.ToString();
        }

        /// <summary>
        /// Returns the reverse order of a string value.
        /// </summary>
        /// <param name="str">The string value to reverse.</param>
        /// <returns>The reverse order of a string value.</returns>
        private static JToken REVERSE(JToken str)
        {
            return ExecuteOneArgumentStringFunction((value) => new string(value.Reverse().ToArray()), str);
        }

        /// <summary>
        /// Returns the right part of a string with the specified number of characters.
        /// </summary>
        /// <param name="value">The string to take the right part of.</param>
        /// <param name="length">The number of characters to take.</param>
        /// <returns>The right part of a string with the specified number of characters.</returns>
        private static JToken RIGHT(JToken value, JToken length)
        {
            if (!Utils.TryConvertToNumber(length, out double lengthValue))
            {
                return Undefined;
            }

            if (!Utils.TryConvertToInteger(lengthValue, out long lengthAsInteger))
            {
                return Undefined;
            }

            if (!Utils.TryConvertToString(value, out string stringValue))
            {
                return Undefined;
            }

            JToken offset = Math.Max(stringValue.Length - lengthAsInteger, 0);
            return ExecuteSubstring(value, offset, stringValue.Length);
        }

        /// <summary>
        /// Returns a numeric value, rounded to the closest integer value.
        /// </summary>
        /// <param name="number">The numeric value to round.</param>
        /// <returns>A numeric value, rounded to the closest integer value.</returns>
        private static JToken ROUND(JToken number)
        {
            return ExecuteOneArgumentNumberFunction((value) => { return Math.Round(value, MidpointRounding.AwayFromZero); }, number);
        }

        /// <summary>
        /// Returns a string expression after truncating all trailing blanks.
        /// </summary>
        /// <param name="str">The string to remove trailing blanks from.</param>
        /// <returns>A string expression after truncating all trailing blanks.</returns>
        private static JToken RTRIM(JToken str)
        {
            return ExecuteOneArgumentStringFunction((value) => value.TrimEnd(), str);
        }

        /// <summary>
        /// Returns the sign value (-1, 0, 1) of the specified numeric expression.
        /// </summary>
        /// <param name="number">The value to take the sign of.</param>
        /// <returns>The sign value (-1, 0, 1) of the specified numeric expression.</returns>
        private static JToken SIGN(JToken number)
        {
            return ExecuteOneArgumentNumberFunction((value) => (double)Math.Sign(value), number);
        }

        /// <summary>
        /// Returns the trigonometric sine of the specified angle, in radians, in the specified expression.
        /// </summary>
        /// <param name="number">The number to take the sine of.</param>
        /// <returns>The trigonometric sine of the specified angle, in radians, in the specified expression.</returns>
        private static JToken SIN(JToken number)
        {
            return ExecuteOneArgumentNumberFunction(Math.Sin, number);
        }

        /// <summary>
        /// Returns the square root of the specified numeric expression.
        /// </summary>
        /// <param name="number">The number to take the square root of.</param>
        /// <returns>The square root of the specified numeric expression.</returns>
        private static JToken SQRT(JToken number)
        {
            return ExecuteOneArgumentNumberFunction(Math.Sqrt, number);
        }

        /// <summary>
        /// Returns the square of the specified numeric expression.
        /// </summary>
        /// <param name="number">The number to square.</param>
        /// <returns>The square of the specified numeric expression.</returns>
        private static JToken SQUARE(JToken number)
        {
            return ExecuteOneArgumentNumberFunction((value) => value * value, number);
        }

        /// <summary>
        /// Returns a Boolean indicating whether the first string expression starts with the second.
        /// </summary>
        /// <param name="str">The string to search.</param>
        /// <param name="prefix">The string to search for.</param>
        /// <returns>A Boolean indicating whether the first string expression starts with the second.</returns>
        private static JToken STARTSWITH(JToken str, JToken prefix)
        {
            return ExecuteTwoArgumentStringFunction((value1, value2) => value1.StartsWith(value2, StringComparison.Ordinal), str, prefix);
        }

        /// <summary>
        /// Returns part of a string expression.
        /// </summary>
        /// <param name="value">The string to take the substring of.</param>
        /// <param name="startIndex">The start index of the string.</param>
        /// <param name="length">The length of the string.</param>
        /// <returns>Part of a string expression.</returns>
        private static JToken SUBSTRING(JToken value, JToken startIndex, JToken length)
        {
            return ExecuteSubstring(value, startIndex, length);
        }

        /// <summary>
        /// Returns the tangent of the input expression, in the specified expression.
        /// </summary>
        /// <param name="number">The number to take the tangent of.</param>
        /// <returns>The tangent of the input expression, in the specified expression.</returns>
        private static JToken TAN(JToken number)
        {
            return ExecuteOneArgumentNumberFunction(Math.Tan, number);
        }

        /// <summary>
        /// Returns a numeric value, truncated to the closest integer value.
        /// </summary>
        /// <param name="number">The number to truncate.</param>
        /// <returns>A numeric value, truncated to the closest integer value.</returns>
        private static JToken TRUNC(JToken number)
        {
            return ExecuteOneArgumentNumberFunction(Math.Truncate, number);
        }

        /// <summary>
        /// Returns the string version of the JSON value.
        /// </summary>
        /// <param name="value">The value to get the string version of.</param>
        /// <returns>The string version of the JSON value.</returns>
        private static JToken TOSTRING(JToken value)
        {
            // Backend: TOSTRING(2049537175849502700) = "2.0495371758495027e+018"
            // Newtonsoft: TOSTRING(2049537175849502700) = 2.0495371758495027E+18
            throw new NotSupportedException("This function is too hard to match up");
        }

        /// <summary>
        /// Returns a string expression after converting lowercase character data to uppercase.
        /// </summary>
        /// <param name="str">The string to take the upper case of.</param>
        /// <returns>A string expression after converting lowercase character data to uppercase.</returns>
        private static JToken UPPER(JToken str)
        {
            return ExecuteOneArgumentStringFunction((value) => value.ToUpper(CultureInfo.InvariantCulture), str);
        }

        private static JToken ExecuteZeroArgumentFunction(Func<JToken> function, BuiltinFunctionName builtin, IReadOnlyList<JToken> arguments)
        {
            if (arguments.Count != 0)
            {
                throw new ArgumentException($"{builtin} takes no argument.");
            }

            return function();
        }

        private static JToken ExecuteOneArgumentFunction(Func<JToken, JToken> function, BuiltinFunctionName builtin, IReadOnlyList<JToken> arguments)
        {
            if (arguments.Count != 1)
            {
                throw new ArgumentException($"{builtin} takes exactly one argument.");
            }

            return function(arguments[0]);
        }

        private static JToken ExecuteTwoArgumentFunction(Func<JToken, JToken, JToken> function, BuiltinFunctionName builtin, IReadOnlyList<JToken> arguments)
        {
            if (arguments.Count != 2)
            {
                throw new ArgumentException($"{builtin} takes exactly two argument.");
            }

            return function(arguments[0], arguments[1]);
        }

        private static JToken ExecuteThreeArgumentFunction(Func<JToken, JToken, JToken, JToken> function, BuiltinFunctionName builtin, IReadOnlyList<JToken> arguments)
        {
            if (arguments.Count != 3)
            {
                throw new ArgumentException($"{builtin} takes exactly three argument.");
            }

            return function(arguments[0], arguments[1], arguments[2]);
        }

        private static JToken ExecuteOneOrTwoArgumentFunction(Func<JToken, JToken, JToken> function, BuiltinFunctionName builtin, IReadOnlyList<JToken> arguments)
        {
            if (arguments.Count != 1 && arguments.Count != 2)
            {
                throw new ArgumentException($"{builtin} takes exactly one or two arguments.");
            }

            JToken result;
            if (arguments.Count == 1)
            {
                result = function(arguments[0], null);
            }
            else
            {
                result = function(arguments[0], arguments[1]);
            }

            return result;
        }

        private static JToken ExecuteTwoOrThreeArgumentFunction(Func<JToken, JToken, JToken, JToken> function, BuiltinFunctionName builtin, IReadOnlyList<JToken> arguments)
        {
            if (arguments.Count != 2 && arguments.Count != 3)
            {
                throw new ArgumentException($"{builtin} takes exactly one or two arguments.");
            }

            JToken result;
            if (arguments.Count == 2)
            {
                result = function(arguments[0], arguments[1], null);
            }
            else
            {
                result = function(arguments[0], arguments[1], arguments[2]);
            }

            return result;
        }

        private static JToken ExecuteAtleastTwoArgumentFunction(Func<JToken, JToken, JToken[], JToken> function, BuiltinFunctionName builtin, IReadOnlyList<JToken> arguments)
        {
            if (arguments.Count < 2)
            {
                throw new ArgumentException($"{builtin} takes atleast two arguments.");
            }

            JToken result;
            if (arguments.Count == 2)
            {
                result = function(arguments[0], arguments[1], null);
            }
            else
            {
                result = function(arguments[0], arguments[1], arguments.Skip(2).ToArray());
            }

            return result;
        }

        private static JToken ExecuteOneArgumentNumberFunction(Func<double, double> numberFunction, JToken argument)
        {
            JToken result;
            if (Utils.TryConvertToNumber(argument, out double number))
            {
                result = numberFunction(number);
            }
            else
            {
                result = Undefined;
            }

            return result;
        }

        private static JToken ExecuteTwoArgumentNumberFunction(Func<double, double, double> numberFunction, JToken argument1, JToken argument2)
        {
            JToken result;
            bool argumentIsNumber1 = Utils.TryConvertToNumber(argument1, out double number1);
            bool argumentIsNumber2 = Utils.TryConvertToNumber(argument2, out double number2);

            if (argumentIsNumber1 && argumentIsNumber2)
            {
                result = numberFunction(number1, number2);
            }
            else
            {
                result = Undefined;
            }

            return result;
        }

        private static JToken ExecuteOneArgumentStringFunction(Func<string, JToken> stringFunction, JToken argument)
        {
            JToken result;
            if (Utils.TryConvertToString(argument, out string value))
            {
                result = stringFunction(value);
            }
            else
            {
                result = Undefined;
            }

            return result;
        }

        private static JToken ExecuteTwoArgumentStringFunction(Func<string, string, JToken> stringFunction, JToken argument1, JToken argument2)
        {
            JToken result;
            bool argumentIsString1 = Utils.TryConvertToString(argument1, out string string1);
            bool argumentIsString2 = Utils.TryConvertToString(argument2, out string string2);

            if (argumentIsString1 && argumentIsString2)
            {
                result = stringFunction(string1, string2);
            }
            else
            {
                result = Undefined;
            }

            return result;
        }

        private static JToken ExecuteSubstring(JToken stringArgument, JToken startIndex, JToken length)
        {
            JToken result = Undefined;

            bool argumentIsString = Utils.TryConvertToString(stringArgument, out string stringValue);
            bool startIndexIsNumber = Utils.TryConvertToNumber(startIndex, out double startIndexValue);
            bool lengthIsNumber = Utils.TryConvertToNumber(length, out double lengthValue);

            if (argumentIsString && startIndexIsNumber && lengthIsNumber)
            {
                if (stringValue.Length == 0)
                {
                    return string.Empty;
                }

                if (!Utils.TryConvertToInteger(startIndexValue, out long startIndexAsInteger))
                {
                    return Undefined;
                }

                int safeStartIndexAsInteger = (int)Math.Min(startIndexAsInteger, int.MaxValue);

                if (!Utils.TryConvertToInteger(lengthValue, out long lengthAsInteger))
                {
                    return Undefined;
                }

                if (startIndexAsInteger == 0 && stringValue.Length <= lengthAsInteger)
                {
                    return stringValue;
                }

                if (startIndexAsInteger >= stringValue.Length || startIndexAsInteger < 0 || lengthAsInteger <= 0)
                {
                    return string.Empty;
                }

                long maxLength = stringValue.Length - startIndexAsInteger;
                int safeMaxLength = (int)Math.Min(Math.Min(maxLength, lengthAsInteger), int.MaxValue);
                return stringValue.Substring(safeStartIndexAsInteger, safeMaxLength);
            }

            return result;
        }
    }
}
