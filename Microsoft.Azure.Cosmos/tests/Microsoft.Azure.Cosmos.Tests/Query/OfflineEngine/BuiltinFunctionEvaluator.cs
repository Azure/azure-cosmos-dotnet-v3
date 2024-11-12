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
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Newtonsoft.Json.Linq;

    internal static class BuiltinFunctionEvaluator
    {
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

        public static CosmosElement EvaluateFunctionCall(string name, IReadOnlyList<CosmosElement> arguments)
        {
            if (!Enum.TryParse(value: name, ignoreCase: true, result: out BuiltinFunctionName builtinFunction))
            {
                throw new ArgumentException($"Unknown builtin function name: {name}");
            }

            // TODO: make the nullable function check based on the function signature and parameters.
            if (arguments.Any((arg) => arg is CosmosUndefined) && !NullableFunctions.Contains(builtinFunction))
            {
                return CosmosUndefined.Create();
            }

            CosmosElement result = builtinFunction switch
            {
                BuiltinFunctionName.ABS => ExecuteOneArgumentFunction(
                                        BuiltinFunctionEvaluator.ABS,
                                        builtinFunction,
                                        arguments),
                BuiltinFunctionName.ACOS => ExecuteOneArgumentFunction(
                                        BuiltinFunctionEvaluator.ACOS,
                                        builtinFunction,
                                        arguments),
                BuiltinFunctionName.ARRAY_CONCAT => ExecuteAtleastTwoArgumentFunction(
                                        BuiltinFunctionEvaluator.ARRAY_CONCAT,
                                        builtinFunction,
                                        arguments),
                BuiltinFunctionName.ARRAY_CONTAINS => ExecuteTwoOrThreeArgumentFunction(
                                        BuiltinFunctionEvaluator.ARRAY_CONTAINS,
                                        builtinFunction,
                                        arguments),
                BuiltinFunctionName.ARRAY_LENGTH => ExecuteOneArgumentFunction(
                                        BuiltinFunctionEvaluator.ARRAY_LENGTH,
                                        builtinFunction,
                                        arguments),
                BuiltinFunctionName.ARRAY_SLICE => ExecuteTwoOrThreeArgumentFunction(
                                        BuiltinFunctionEvaluator.ARRAY_SLICE,
                                        builtinFunction,
                                        arguments),
                BuiltinFunctionName.ASIN => ExecuteOneArgumentFunction(
                                        BuiltinFunctionEvaluator.ASIN,
                                        builtinFunction,
                                        arguments),
                BuiltinFunctionName.ATAN => ExecuteOneArgumentFunction(
                                        BuiltinFunctionEvaluator.ATAN,
                                        builtinFunction,
                                        arguments),
                BuiltinFunctionName.ATN2 => ExecuteTwoArgumentFunction(
                                        BuiltinFunctionEvaluator.ATN2,
                                        builtinFunction,
                                        arguments),
                BuiltinFunctionName.CEILING => ExecuteOneArgumentFunction(
                                        BuiltinFunctionEvaluator.CEILING,
                                        builtinFunction,
                                        arguments),
                BuiltinFunctionName.CONCAT => ExecuteAtleastTwoArgumentFunction(
                                        BuiltinFunctionEvaluator.CONCAT,
                                        builtinFunction,
                                        arguments),
                BuiltinFunctionName.CONTAINS => ExecuteTwoArgumentFunction(
                                        BuiltinFunctionEvaluator.CONTAINS,
                                        builtinFunction,
                                        arguments),
                BuiltinFunctionName.COS => ExecuteOneArgumentFunction(
                                        BuiltinFunctionEvaluator.COS,
                                        builtinFunction,
                                        arguments),
                BuiltinFunctionName.COT => ExecuteOneArgumentFunction(
                                        BuiltinFunctionEvaluator.COT,
                                        builtinFunction,
                                        arguments),
                BuiltinFunctionName.DEGREES => ExecuteOneArgumentFunction(
                                        BuiltinFunctionEvaluator.DEGREES,
                                        builtinFunction,
                                        arguments),
                BuiltinFunctionName.ENDSWITH => ExecuteTwoArgumentFunction(
                                        BuiltinFunctionEvaluator.ENDSWITH,
                                        builtinFunction,
                                        arguments),
                BuiltinFunctionName.EXP => ExecuteOneArgumentFunction(
                                        BuiltinFunctionEvaluator.EXP,
                                        builtinFunction,
                                        arguments),
                BuiltinFunctionName.FLOOR => ExecuteOneArgumentFunction(
                                        BuiltinFunctionEvaluator.FLOOR,
                                        builtinFunction,
                                        arguments),
                BuiltinFunctionName.INDEX_OF => ExecuteTwoArgumentFunction(
                                        BuiltinFunctionEvaluator.INDEX_OF,
                                        builtinFunction,
                                        arguments),
                BuiltinFunctionName.IS_ARRAY => ExecuteOneArgumentFunction(
                                        BuiltinFunctionEvaluator.IS_ARRAY,
                                        builtinFunction,
                                        arguments),
                BuiltinFunctionName.IS_BOOL => ExecuteOneArgumentFunction(
                                        BuiltinFunctionEvaluator.IS_BOOL,
                                        builtinFunction,
                                        arguments),
                BuiltinFunctionName.IS_DEFINED => ExecuteOneArgumentFunction(
                                        BuiltinFunctionEvaluator.IS_DEFINED,
                                        builtinFunction,
                                        arguments),
                BuiltinFunctionName.IS_NULL => ExecuteOneArgumentFunction(
                                        BuiltinFunctionEvaluator.IS_NULL,
                                        builtinFunction,
                                        arguments),
                BuiltinFunctionName.IS_NUMBER => ExecuteOneArgumentFunction(
                                        BuiltinFunctionEvaluator.IS_NUMBER,
                                        builtinFunction,
                                        arguments),
                BuiltinFunctionName.IS_OBJECT => ExecuteOneArgumentFunction(
                                        BuiltinFunctionEvaluator.IS_OBJECT,
                                        builtinFunction,
                                        arguments),
                BuiltinFunctionName.IS_PRIMITIVE => ExecuteOneArgumentFunction(
                                        BuiltinFunctionEvaluator.IS_PRIMITIVE,
                                        builtinFunction,
                                        arguments),
                BuiltinFunctionName.IS_STRING => ExecuteOneArgumentFunction(
                                        BuiltinFunctionEvaluator.IS_STRING,
                                        builtinFunction,
                                        arguments),
                BuiltinFunctionName.LEFT => ExecuteTwoArgumentFunction(
                                        BuiltinFunctionEvaluator.LEFT,
                                        builtinFunction,
                                        arguments),
                BuiltinFunctionName.LENGTH => ExecuteOneArgumentFunction(
                                        BuiltinFunctionEvaluator.LENGTH,
                                        builtinFunction,
                                        arguments),
                BuiltinFunctionName.LOG => ExecuteOneOrTwoArgumentFunction(
                                        BuiltinFunctionEvaluator.LOG,
                                        builtinFunction,
                                        arguments),
                BuiltinFunctionName.LOG10 => ExecuteOneArgumentFunction(
                                        BuiltinFunctionEvaluator.LOG10,
                                        builtinFunction,
                                        arguments),
                BuiltinFunctionName.LOWER => ExecuteOneArgumentFunction(
                                        BuiltinFunctionEvaluator.LOWER,
                                        builtinFunction,
                                        arguments),
                BuiltinFunctionName.LTRIM => ExecuteOneArgumentFunction(
                                        BuiltinFunctionEvaluator.LTRIM,
                                        builtinFunction,
                                        arguments),
                BuiltinFunctionName.PI => ExecuteZeroArgumentFunction(
                                        BuiltinFunctionEvaluator.PI,
                                        builtinFunction,
                                        arguments),
                BuiltinFunctionName.POWER => ExecuteTwoArgumentFunction(
                                        BuiltinFunctionEvaluator.POWER,
                                        builtinFunction,
                                        arguments),
                BuiltinFunctionName.RADIANS => ExecuteOneArgumentFunction(
                                        BuiltinFunctionEvaluator.RADIANS,
                                        builtinFunction,
                                        arguments),
                BuiltinFunctionName.REPLACE => ExecuteThreeArgumentFunction(
                                        BuiltinFunctionEvaluator.REPLACE,
                                        builtinFunction,
                                        arguments),
                BuiltinFunctionName.REPLICATE => ExecuteTwoArgumentFunction(
                                        BuiltinFunctionEvaluator.REPLICATE,
                                        builtinFunction,
                                        arguments),
                BuiltinFunctionName.REVERSE => ExecuteOneArgumentFunction(
                                        BuiltinFunctionEvaluator.REVERSE,
                                        builtinFunction,
                                        arguments),
                BuiltinFunctionName.RIGHT => ExecuteTwoArgumentFunction(
                                        BuiltinFunctionEvaluator.RIGHT,
                                        builtinFunction,
                                        arguments),
                BuiltinFunctionName.ROUND => ExecuteOneArgumentFunction(
                                        BuiltinFunctionEvaluator.ROUND,
                                        builtinFunction,
                                        arguments),
                BuiltinFunctionName.RTRIM => ExecuteOneArgumentFunction(
                                        BuiltinFunctionEvaluator.RTRIM,
                                        builtinFunction,
                                        arguments),
                BuiltinFunctionName.SIGN => ExecuteOneArgumentFunction(
                                        BuiltinFunctionEvaluator.SIGN,
                                        builtinFunction,
                                        arguments),
                BuiltinFunctionName.SIN => ExecuteOneArgumentFunction(
                                        BuiltinFunctionEvaluator.SIN,
                                        builtinFunction,
                                        arguments),
                BuiltinFunctionName.SQRT => ExecuteOneArgumentFunction(
                                        BuiltinFunctionEvaluator.SQRT,
                                        builtinFunction,
                                        arguments),
                BuiltinFunctionName.SQUARE => ExecuteOneArgumentFunction(
                                        BuiltinFunctionEvaluator.SQUARE,
                                        builtinFunction,
                                        arguments),
                BuiltinFunctionName.STARTSWITH => ExecuteTwoArgumentFunction(
                                        BuiltinFunctionEvaluator.STARTSWITH,
                                        builtinFunction,
                                        arguments),
                BuiltinFunctionName.SUBSTRING => ExecuteThreeArgumentFunction(
                                        BuiltinFunctionEvaluator.SUBSTRING,
                                        builtinFunction,
                                        arguments),
                BuiltinFunctionName.TAN => ExecuteOneArgumentFunction(
                                        BuiltinFunctionEvaluator.TAN,
                                        builtinFunction,
                                        arguments),
                BuiltinFunctionName.TRUNC => ExecuteOneArgumentFunction(
                                        BuiltinFunctionEvaluator.TRUNC,
                                        builtinFunction,
                                        arguments),
                BuiltinFunctionName.TOSTRING => ExecuteOneArgumentFunction(
                                        BuiltinFunctionEvaluator.TOSTRING,
                                        builtinFunction,
                                        arguments),
                BuiltinFunctionName.UPPER => ExecuteOneArgumentFunction(
                                        BuiltinFunctionEvaluator.UPPER,
                                        builtinFunction,
                                        arguments),
                _ => throw new ArgumentException($"Unknown {nameof(BuiltinFunctionName)}: {builtinFunction}"),
            };
            return result;
        }

        /// <summary>
        /// Returns the absolute (positive) value of the specified numeric expression.
        /// </summary>
        /// <param name="number">The numeric expression to take the absolute value of.</param>
        /// <returns>The absolute (positive) value of the specified numeric expression.</returns>
        private static CosmosElement ABS(CosmosElement number)
        {
            return ExecuteOneArgumentNumberFunction(Math.Abs, number);
        }

        /// <summary>
        /// Returns the angle, in radians, whose cosine is the specified numeric expression; also called arccosine.
        /// </summary>
        /// <param name="number">The numeric expression to take the arccosine of.</param>
        /// <returns>The angle, in radians, whose cosine is the specified numeric expression; also called arccosine.</returns>
        private static CosmosElement ACOS(CosmosElement number)
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
        private static CosmosElement ARRAY_CONCAT(
            CosmosElement first,
            CosmosElement second,
            params CosmosElement[] arrays)
        {
            if (!(first is CosmosArray firstArray))
            {
                return CosmosUndefined.Create();
            }

            if (!(second is CosmosArray secondArray))
            {
                return CosmosUndefined.Create();
            }

            if (arrays != null)
            {
                foreach (CosmosElement array in arrays)
                {
                    if (!(array is CosmosArray))
                    {
                        return CosmosUndefined.Create();
                    }
                }
            }

            List<CosmosElement> concatenatedArray = new List<CosmosElement>();
            foreach (CosmosElement arrayItem in firstArray)
            {
                concatenatedArray.Add(arrayItem);
            }

            foreach (CosmosElement arrayItem in secondArray)
            {
                concatenatedArray.Add(arrayItem);
            }

            if (arrays != null)
            {
                foreach (CosmosArray array in arrays)
                {
                    foreach (CosmosElement arrayItem in array)
                    {
                        concatenatedArray.Add(arrayItem);
                    }
                }
            }

            return CosmosArray.Create(concatenatedArray);
        }

        /// <summary>
        /// Returns a Boolean indicating whether the array contains the specified value. Can specify if the match is full or partial.
        /// </summary>
        /// <param name="haystack">The array to look in.</param>
        /// <param name="needle">The value to look for.</param>
        /// <param name="partialMatchToken">If the match is full or partial.</param>
        /// <returns>A Boolean indicating whether the array contains the specified value.</returns>
        private static CosmosElement ARRAY_CONTAINS(
            CosmosElement haystack,
            CosmosElement needle,
            CosmosElement partialMatchToken = null)
        {
            if (partialMatchToken == null || partialMatchToken is CosmosUndefined)
            {
                partialMatchToken = CosmosBoolean.Create(false);
            }

            if (!(partialMatchToken is CosmosBoolean partialMatchAsBoolean))
            {
                return CosmosUndefined.Create();
            }

            if (!(haystack is CosmosArray haystackAsArray))
            {
                return CosmosUndefined.Create();
            }

            if (needle is CosmosUndefined)
            {
                return CosmosUndefined.Create();
            }

            bool contains = false;
            foreach (CosmosElement hay in haystackAsArray)
            {
                if (partialMatchAsBoolean.Value)
                {
                    bool partialMatch;
                    if (needle is CosmosObject needleAsObject)
                    {
                        partialMatch = true;
                        foreach (KeyValuePair<string, CosmosElement> kvp in needleAsObject)
                        {
                            string name = kvp.Key;
                            CosmosElement needleValue = kvp.Value;

                            if ((hay is CosmosObject hayAsObject) && hayAsObject.TryGetValue(name, out CosmosElement hayValue))
                            {
                                partialMatch &= needleValue == hayValue;
                            }
                            else
                            {
                                partialMatch = false;
                            }
                        }
                    }
                    else
                    {
                        partialMatch = hay == needle;
                    }

                    contains |= partialMatch;
                }
                else
                {
                    contains |= hay == needle;
                }
            }

            return CosmosBoolean.Create(contains);
        }

        /// <summary>
        /// Returns the number of elements of the specified array expression.
        /// </summary>
        /// <param name="value">The array expression to find the length of.</param>
        /// <returns>The number of elements of the specified array expression.</returns>
        private static CosmosElement ARRAY_LENGTH(CosmosElement value)
        {
            if (!(value is CosmosArray cosmosArray))
            {
                return CosmosUndefined.Create();
            }

            return CosmosNumber64.Create(cosmosArray.Count);
        }

        /// <summary>
        /// Returns part of an array expression.
        /// </summary>
        /// <param name="value">The array expression to slice.</param>
        /// <param name="startIndex">The start index to slice from.</param>
        /// <param name="count">The end index to slice to.</param>
        /// <returns>Part of an array expression.</returns>
        private static CosmosElement ARRAY_SLICE(
            CosmosElement value,
            CosmosElement startIndex,
            CosmosElement count = null)
        {
            if (count == null)
            {
                count = CosmosNumber64.Create(long.MaxValue);
            }

            if (!(value is CosmosArray valueAsArray))
            {
                return CosmosUndefined.Create();
            }

            if (!(startIndex is CosmosNumber startIndexAsNumber))
            {
                return CosmosUndefined.Create();
            }

            if (!(count is CosmosNumber countAsNumber))
            {
                return CosmosUndefined.Create();
            }

            if (!startIndexAsNumber.Value.IsInteger)
            {
                return CosmosUndefined.Create();
            }

            long startIndexAsInteger = Number64.ToLong(startIndexAsNumber.Value);

            if (startIndexAsInteger < 0)
            {
                startIndexAsInteger += valueAsArray.Count;
                if (startIndexAsInteger < 0)
                {
                    startIndexAsInteger = 0;
                }
            }

            if (!countAsNumber.Value.IsInteger)
            {
                return CosmosUndefined.Create();
            }

            long countAsInteger = Number64.ToLong(countAsNumber.Value);
            countAsInteger = Math.Min(valueAsArray.Count - startIndexAsInteger, countAsInteger);

            if (valueAsArray.Count <= 0)
            {
                return valueAsArray;
            }

            if ((startIndexAsInteger <= 0) && (countAsInteger >= valueAsArray.Count))
            {
                return valueAsArray;
            }

            if (startIndexAsInteger >= valueAsArray.Count)
            {
                return CosmosArray.Empty;
            }

            if (countAsInteger <= 0)
            {
                return CosmosArray.Empty;
            }

            return CosmosArray.Create(valueAsArray
                .Skip((int)Math.Min(startIndexAsInteger, int.MaxValue))
                .Take((int)Math.Min(countAsInteger, int.MaxValue)));
        }

        /// <summary>
        /// Returns the angle, in radians, whose sine is the specified numeric expression. This is also called arcsine.
        /// </summary>
        /// <param name="number">The numeric expression to take the arcsine of.</param>
        /// <returns>The angle, in radians, whose sine is the specified numeric expression. This is also called arcsine.</returns>
        private static CosmosElement ASIN(CosmosElement number)
        {
            return ExecuteOneArgumentNumberFunction(Math.Asin, number);
        }

        /// <summary>
        /// Returns the angle, in radians, whose tangent is the specified numeric expression. This is also called arctangent.
        /// </summary>
        /// <param name="number">The numeric expression.</param>
        /// <returns>The angle, in radians, whose tangent is the specified numeric expression. This is also called arctangent.</returns>
        private static CosmosElement ATAN(CosmosElement number)
        {
            return ExecuteOneArgumentNumberFunction(Math.Atan, number);
        }

        /// <summary>
        /// Returns the angle, in radians, between the positive x-axis and the ray from the origin to the point (y, x), where x and y are the values of the two specified float expressions.
        /// </summary>
        /// <param name="x">x coordinate of the point.</param>
        /// <param name="y">y coordinate of the point.</param>
        /// <returns>the angle, in radians, between the positive x-axis and the ray from the origin to the point (y, x), where x and y are the values of the two specified float expressions.</returns>
        private static CosmosElement ATN2(CosmosElement x, CosmosElement y)
        {
            return ExecuteTwoArgumentNumberFunction(Math.Atan2, x, y);
        }

        /// <summary>
        /// Returns the smallest integer value greater than, or equal to, the specified numeric expression.
        /// </summary>
        /// <param name="number">The numeric expression.</param>
        /// <returns>The smallest integer value greater than, or equal to, the specified numeric expression.</returns>
        private static CosmosElement CEILING(CosmosElement number)
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
        private static CosmosElement CONCAT(
            CosmosElement string1,
            CosmosElement string2,
            params CosmosElement[] otherStrings)
        {
            if (!(string1 is CosmosString cosmosString1))
            {
                return CosmosUndefined.Create();
            }

            if (!(string2 is CosmosString cosmosString2))
            {
                return CosmosUndefined.Create();
            }

            if (otherStrings != null)
            {
                foreach (CosmosElement otherString in otherStrings)
                {
                    if (!(otherString is CosmosString))
                    {
                        return CosmosUndefined.Create();
                    }
                }
            }

            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(cosmosString1.Value);
            stringBuilder.Append(cosmosString2.Value);
            if (otherStrings != null)
            {
                foreach (CosmosString otherString in otherStrings)
                {
                    stringBuilder.Append(otherString.Value);
                }
            }

            return CosmosString.Create(stringBuilder.ToString());
        }

        /// <summary>
        /// Returns a Boolean indicating whether the first string expression contains the second.
        /// </summary>
        /// <param name="haystack">The string expression to search in.</param>
        /// <param name="needle">The string expression to look search for.</param>
        /// <returns>A Boolean indicating whether the first string expression contains the second.</returns>
        private static CosmosElement CONTAINS(
            CosmosElement haystack,
            CosmosElement needle)
        {
            return ExecuteTwoArgumentStringFunction(
                (value1, value2) => CosmosBoolean.Create(value1.Contains(value2)),
                haystack,
                needle);
        }

        /// <summary>
        /// Returns the trigonometric cosine of the specified angle, in radians, in the specified expression.
        /// </summary>
        /// <param name="number">The numeric expression to take the cosine of.</param>
        /// <returns>The trigonometric cosine of the specified angle, in radians, in the specified expression.</returns>
        private static CosmosElement COS(CosmosElement number)
        {
            return ExecuteOneArgumentNumberFunction(
            Math.Cos,
            number);
        }

        /// <summary>
        /// Returns the trigonometric cotangent of the specified angle, in radians, in the specified numeric expression.
        /// </summary>
        /// <param name="number">The numeric expression to take the cotangent of.</param>
        /// <returns>The trigonometric cotangent of the specified angle, in radians, in the specified numeric expression.</returns>
        private static CosmosElement COT(CosmosElement number)
        {
            return ExecuteOneArgumentNumberFunction(
            (value) => 1.0 / Math.Tan(value),
            number);
        }

        /// <summary>
        /// Returns the corresponding angle in degrees for an angle specified in radians.
        /// </summary>
        /// <param name="number">The angle specified in radians to convert to degrees.</param>
        /// <returns>The corresponding angle in degrees for an angle specified in radians.</returns>
        private static CosmosElement DEGREES(CosmosElement number)
        {
            return ExecuteOneArgumentNumberFunction(
            (radians) => 180 / Math.PI * radians,
            number);
        }

        /// <summary>
        /// Returns a Boolean indicating whether the first string expression ends with the second.
        /// </summary>
        /// <param name="str">The string expression to check.</param>
        /// <param name="suffix">The suffix to look for.</param>
        /// <returns>a Boolean indicating whether the first string expression ends with the second.</returns>
        private static CosmosElement ENDSWITH(CosmosElement str, CosmosElement suffix)
        {
            return ExecuteTwoArgumentStringFunction(
            (value1, value2) => CosmosBoolean.Create(value1.EndsWith(value2, StringComparison.Ordinal)),
            str,
            suffix);
        }

        /// <summary>
        /// Returns the exponent of the specified numeric expression.
        /// </summary>
        /// <param name="number">The numeric expression to take the exponent of.</param>
        /// <returns>The exponent of the specified numeric expression.</returns>
        private static CosmosElement EXP(CosmosElement number)
        {
            return ExecuteOneArgumentNumberFunction(Math.Exp, number);
        }

        /// <summary>
        /// Returns the largest integer less than or equal to the specified numeric expression.
        /// </summary>
        /// <param name="number">The number to take the floor of.</param>
        /// <returns>The largest integer less than or equal to the specified numeric expression.</returns>
        private static CosmosElement FLOOR(CosmosElement number)
        {
            return ExecuteOneArgumentNumberFunction(Math.Floor, number);
        }

        /// <summary>
        /// Returns the starting position of the first occurrence of the second string expression within the first specified string expression, or -1 if the string is not found.
        /// </summary>
        /// <param name="str">The string expression to look in.</param>
        /// <param name="substring">The string expression to look for.</param>
        /// <returns>The starting position of the first occurrence of the second string expression within the first specified string expression, or -1 if the string is not found.</returns>
        private static CosmosElement INDEX_OF(CosmosElement str, CosmosElement substring)
        {
            return ExecuteTwoArgumentStringFunction(
            (value1, value2) => CosmosNumber64.Create(value1.IndexOf(value2, StringComparison.Ordinal)),
            str,
            substring);
        }

        /// <summary>
        /// Returns a Boolean indicating if the type of the value is an array.
        /// </summary>
        /// <param name="value">The expression to check if it's an array.</param>
        /// <returns>A Boolean indicating if the type of the value is an array.</returns>
        private static CosmosElement IS_ARRAY(CosmosElement value)
        {
            return CosmosBoolean.Create(value is CosmosArray);
        }

        /// <summary>
        /// Returns a Boolean indicating if the type of the value is a Boolean.
        /// </summary>
        /// <param name="value">The expression to check if it is a boolean.</param>
        /// <returns>A Boolean indicating if the type of the value is a Boolean.</returns>
        private static CosmosElement IS_BOOL(CosmosElement value)
        {
            return CosmosBoolean.Create(value is CosmosBoolean);
        }

        /// <summary>
        /// Returns a Boolean indicating if the property has been assigned a value.
        /// </summary>
        /// <param name="value">The expression to check if it is defined.</param>
        /// <returns>A Boolean indicating if the property has been assigned a value.</returns>
        private static CosmosElement IS_DEFINED(CosmosElement value)
        {
            return CosmosBoolean.Create(value is not CosmosUndefined);
        }

        /// <summary>
        /// Returns a Boolean indicating if the type of the value is null.
        /// </summary>
        /// <param name="value">The expression to check if it is null.</param>
        /// <returns>A Boolean indicating if the type of the value is null.</returns>
        private static CosmosElement IS_NULL(CosmosElement value)
        {
            return CosmosBoolean.Create(value is CosmosNull);
        }

        /// <summary>
        /// Returns a Boolean indicating if the type of the value is a number.
        /// </summary>
        /// <param name="value">The expression to check if it is a number.</param>
        /// <returns>A Boolean indicating if the type of the value is a number.</returns>
        private static CosmosElement IS_NUMBER(CosmosElement value)
        {
            return CosmosBoolean.Create(value is CosmosNumber);
        }

        /// <summary>
        /// Returns a Boolean indicating if the type of the value is a JSON object.
        /// </summary>
        /// <param name="value">The expression to check if it is an object.</param>
        /// <returns>A Boolean indicating if the type of the value is a JSON object.</returns>
        private static CosmosElement IS_OBJECT(CosmosElement value)
        {
            return CosmosBoolean.Create(value is CosmosObject);
        }

        /// <summary>
        /// Returns a Boolean indicating if the type of the value is a string, number, Boolean or null.
        /// </summary>
        /// <param name="value">The expression to check if it is a primitive.</param>
        /// <returns>A Boolean indicating if the type of the value is a string, number, Boolean or null.</returns>
        private static CosmosElement IS_PRIMITIVE(CosmosElement value)
        {
            return CosmosBoolean.Create(Utils.IsPrimitive(value));
        }

        /// <summary>
        /// Returns a Boolean indicating if the type of the value is a string.
        /// </summary>
        /// <param name="value">The expression to check if it is a string.</param>
        /// <returns>A Boolean indicating if the type of the value is a string.</returns>
        private static CosmosElement IS_STRING(CosmosElement value)
        {
            return CosmosBoolean.Create(value is CosmosString);
        }

        /// <summary>
        /// Returns the left part of a string with the specified number of characters.
        /// </summary>
        /// <param name="str">The string to take the left part of.</param>
        /// <param name="length">The number of characters to take.</param>
        /// <returns>The left part of a string with the specified number of characters.</returns>
        private static CosmosElement LEFT(CosmosElement str, CosmosElement length)
        {
            return ExecuteSubstring(
            str,
            CosmosNumber64.Create(0),
            length);
        }

        /// <summary>
        /// Returns the number of characters of the specified string expression.
        /// </summary>
        /// <param name="str">The string expression to take the length of.</param>
        /// <returns>The number of characters of the specified string expression.</returns>
        private static CosmosElement LENGTH(CosmosElement str)
        {
            return ExecuteOneArgumentStringFunction(
            (value) => CosmosNumber64.Create(value.Length),
            str);
        }

        /// <summary>
        /// Returns the natural logarithm of the specified numeric expression, or the logarithm using the specified base.
        /// </summary>
        /// <param name="number">The number to take the log of.</param>
        /// <param name="numberBase">The (optional) base.</param>
        /// <returns>The natural logarithm of the specified numeric expression, or the logarithm using the specified base.</returns>
        private static CosmosElement LOG(CosmosElement number, CosmosElement numberBase = null)
        {
            return ExecuteOneArgumentNumberFunction(
            Math.Log,
            number);
        }

        /// <summary>
        /// Returns the base-10 logarithmic value of the specified numeric expression.
        /// </summary>
        /// <param name="number">The number to take the base-10 log of.</param>
        /// <returns>The base-10 logarithmic value of the specified numeric expression.</returns>
        private static CosmosElement LOG10(CosmosElement number)
        {
            return ExecuteOneArgumentNumberFunction(
            Math.Log10,
            number);
        }

        /// <summary>
        /// Returns a string expression after converting uppercase character data to lowercase.
        /// </summary>
        /// <param name="str">The string to lowercase.</param>
        /// <returns>A string expression after converting uppercase character data to lowercase.</returns>
        private static CosmosElement LOWER(CosmosElement str)
        {
            return ExecuteOneArgumentStringFunction(
            (value) => CosmosString.Create(value.ToLower(CultureInfo.InvariantCulture)),
            str);
        }

        /// <summary>
        /// Returns a string expression after it removes leading blanks.
        /// </summary>
        /// <param name="str">The string to remove leading blanks from.</param>
        /// <returns>A string expression after it removes leading blanks.</returns>
        private static CosmosElement LTRIM(CosmosElement str)
        {
            return ExecuteOneArgumentStringFunction(
            (value) => CosmosString.Create(value.TrimStart()),
            str);
        }

        /// <summary>
        /// Returns the constant value of PI.
        /// </summary>
        /// <returns>The constant value of PI.</returns>
        private static CosmosElement PI()
        {
            return CosmosNumber64.Create(Math.PI);
        }

        /// <summary>
        /// Returns the power of the specified numeric expression to the value specified.
        /// </summary>
        /// <param name="baseNumber">The base number to take the power of.</param>
        /// <param name="exponentNumber">The exponent value.</param>
        /// <returns>The power of the specified numeric expression to the value specified.</returns>
        private static CosmosElement POWER(
            CosmosElement baseNumber,
            CosmosElement exponentNumber)
        {
            return ExecuteTwoArgumentNumberFunction(
                Math.Pow,
                baseNumber,
                exponentNumber);
        }

        /// <summary>
        /// Returns radians when a numeric expression, in degrees, is entered.
        /// </summary>
        /// <param name="number">The number expression, in degrees, to take the power of.</param>
        /// <returns>Radians when a numeric expression, in degrees, is entered.</returns>
        private static CosmosElement RADIANS(CosmosElement number)
        {
            return ExecuteOneArgumentNumberFunction(
            (degrees) => Math.PI * degrees / 180.0,
            number);
        }

        /// <summary>
        /// Replaces all occurrences of a specified string value with another string value.
        /// </summary>
        /// <param name="stringValue">The string to have substrings replaced.</param>
        /// <param name="subString">The string to look for.</param>
        /// <param name="replacement">The string to replace with.</param>
        /// <returns>A string with all occurrences of a specified string value with another string value.</returns>
        private static CosmosElement REPLACE(
            CosmosElement stringValue,
            CosmosElement subString,
            CosmosElement replacement)
        {
            if (!(stringValue is CosmosString stringValueValue))
            {
                return CosmosUndefined.Create();
            }

            if (!(subString is CosmosString subStringValue))
            {
                return CosmosUndefined.Create();
            }

            if (subStringValue.Value.IsEmpty)
            {
                return CosmosUndefined.Create();
            }

            if (!(replacement is CosmosString replacementValue))
            {
                return CosmosUndefined.Create();
            }

            return CosmosString.Create(stringValueValue.Value.ToString().Replace(subStringValue.Value, replacementValue.Value));
        }

        /// <summary>
        /// Repeats a string value a specified number of times.
        /// </summary>
        /// <param name="str">The string to replicate.</param>
        /// <param name="repeatCount">The number of times to replicate the value.</param>
        /// <returns>The string value repeated a specified number of times.</returns>
        private static CosmosElement REPLICATE(
            CosmosElement str,
            CosmosElement repeatCount)
        {
            if (!(str is CosmosString strValue))
            {
                return CosmosUndefined.Create();
            }

            if (!(repeatCount is CosmosNumber repeatCountValue))
            {
                return CosmosUndefined.Create();
            }

            if (repeatCountValue.Value < 0)
            {
                return CosmosUndefined.Create();
            }

            long repeatCountAsLong = Number64.ToLong(repeatCountValue.Value);

            try
            {
                checked
                {
                    if ((strValue.Value.ToString().Length * repeatCountAsLong) > 10000)
                    {
                        return CosmosUndefined.Create();
                    }
                }
            }
            catch (ArithmeticException)
            {
                return CosmosUndefined.Create();
            }

            StringBuilder stringBuilder = new StringBuilder();
            for (long i = 0; i < repeatCountAsLong; i++)
            {
                stringBuilder.Append(strValue.Value);
            }

            return CosmosString.Create(stringBuilder.ToString());
        }

        /// <summary>
        /// Returns the reverse order of a string value.
        /// </summary>
        /// <param name="str">The string value to reverse.</param>
        /// <returns>The reverse order of a string value.</returns>
        private static CosmosElement REVERSE(CosmosElement str)
        {
            return ExecuteOneArgumentStringFunction(
            (value) => CosmosString.Create(new string(value.Reverse().ToArray())),
            str);
        }

        /// <summary>
        /// Returns the right part of a string with the specified number of characters.
        /// </summary>
        /// <param name="value">The string to take the right part of.</param>
        /// <param name="length">The number of characters to take.</param>
        /// <returns>The right part of a string with the specified number of characters.</returns>
        private static CosmosElement RIGHT(
            CosmosElement value,
            CosmosElement length)
        {
            if (!(value is CosmosString stringValue))
            {
                return CosmosUndefined.Create();
            }

            if (!(length is CosmosNumber lengthValue))
            {
                return CosmosUndefined.Create();
            }

            if (!lengthValue.Value.IsInteger)
            {
                return CosmosUndefined.Create();
            }

            CosmosElement offset = CosmosNumber64.Create(Math.Max(stringValue.Value.ToString().Length - Number64.ToLong(lengthValue.Value), 0));
            return ExecuteSubstring(value, offset, CosmosNumber64.Create(stringValue.Value.ToString().Length));
        }

        /// <summary>
        /// Returns a numeric value, rounded to the closest integer value.
        /// </summary>
        /// <param name="number">The numeric value to round.</param>
        /// <returns>A numeric value, rounded to the closest integer value.</returns>
        private static CosmosElement ROUND(CosmosElement number)
        {
            return ExecuteOneArgumentNumberFunction(
            (value) => Math.Round(value, MidpointRounding.AwayFromZero),
            number);
        }

        /// <summary>
        /// Returns a string expression after truncating all trailing blanks.
        /// </summary>
        /// <param name="str">The string to remove trailing blanks from.</param>
        /// <returns>A string expression after truncating all trailing blanks.</returns>
        private static CosmosElement RTRIM(CosmosElement str)
        {
            return ExecuteOneArgumentStringFunction(
            (value) => CosmosString.Create(value.TrimEnd()),
            str);
        }

        /// <summary>
        /// Returns the sign value (-1, 0, 1) of the specified numeric expression.
        /// </summary>
        /// <param name="number">The value to take the sign of.</param>
        /// <returns>The sign value (-1, 0, 1) of the specified numeric expression.</returns>
        private static CosmosElement SIGN(CosmosElement number)
        {
            return ExecuteOneArgumentNumberFunction(
            (value) => Math.Sign(value),
            number);
        }

        /// <summary>
        /// Returns the trigonometric sine of the specified angle, in radians, in the specified expression.
        /// </summary>
        /// <param name="number">The number to take the sine of.</param>
        /// <returns>The trigonometric sine of the specified angle, in radians, in the specified expression.</returns>
        private static CosmosElement SIN(CosmosElement number)
        {
            return ExecuteOneArgumentNumberFunction(
            Math.Sin,
            number);
        }

        /// <summary>
        /// Returns the square root of the specified numeric expression.
        /// </summary>
        /// <param name="number">The number to take the square root of.</param>
        /// <returns>The square root of the specified numeric expression.</returns>
        private static CosmosElement SQRT(CosmosElement number)
        {
            return ExecuteOneArgumentNumberFunction(
            Math.Sqrt,
            number);
        }

        /// <summary>
        /// Returns the square of the specified numeric expression.
        /// </summary>
        /// <param name="number">The number to square.</param>
        /// <returns>The square of the specified numeric expression.</returns>
        private static CosmosElement SQUARE(CosmosElement number)
        {
            return ExecuteOneArgumentNumberFunction(
            (value) => value * value,
            number);
        }

        /// <summary>
        /// Returns a Boolean indicating whether the first string expression starts with the second.
        /// </summary>
        /// <param name="str">The string to search.</param>
        /// <param name="prefix">The string to search for.</param>
        /// <returns>A Boolean indicating whether the first string expression starts with the second.</returns>
        private static CosmosElement STARTSWITH(
            CosmosElement str,
            CosmosElement prefix)
        {
            return ExecuteTwoArgumentStringFunction(
                (value1, value2) => CosmosBoolean.Create(value1.StartsWith(value2, StringComparison.Ordinal)),
                str,
                prefix);
        }

        /// <summary>
        /// Returns part of a string expression.
        /// </summary>
        /// <param name="value">The string to take the substring of.</param>
        /// <param name="startIndex">The start index of the string.</param>
        /// <param name="length">The length of the string.</param>
        /// <returns>Part of a string expression.</returns>
        private static CosmosElement SUBSTRING(CosmosElement value, CosmosElement startIndex, CosmosElement length)
        {
            return ExecuteSubstring(value, startIndex, length);
        }

        /// <summary>
        /// Returns the tangent of the input expression, in the specified expression.
        /// </summary>
        /// <param name="number">The number to take the tangent of.</param>
        /// <returns>The tangent of the input expression, in the specified expression.</returns>
        private static CosmosElement TAN(CosmosElement number)
        {
            return ExecuteOneArgumentNumberFunction(Math.Tan, number);
        }

        /// <summary>
        /// Returns a numeric value, truncated to the closest integer value.
        /// </summary>
        /// <param name="number">The number to truncate.</param>
        /// <returns>A numeric value, truncated to the closest integer value.</returns>
        private static CosmosElement TRUNC(CosmosElement number)
        {
            return ExecuteOneArgumentNumberFunction(Math.Truncate, number);
        }

        /// <summary>
        /// Returns the string version of the JSON value.
        /// </summary>
        /// <param name="value">The value to get the string version of.</param>
        /// <returns>The string version of the JSON value.</returns>
        private static CosmosElement TOSTRING(CosmosElement value)
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
        private static CosmosElement UPPER(CosmosElement str)
        {
            return ExecuteOneArgumentStringFunction(
            (value) => CosmosString.Create(value.ToUpper(CultureInfo.InvariantCulture)),
            str);
        }

        private static CosmosElement ExecuteZeroArgumentFunction(
            Func<CosmosElement> function,
            BuiltinFunctionName builtin,
            IReadOnlyList<CosmosElement> arguments)
        {
            if (arguments.Count != 0)
            {
                throw new ArgumentException($"{builtin} takes no argument.");
            }

            return function();
        }

        private static CosmosElement ExecuteOneArgumentFunction(
            Func<CosmosElement, CosmosElement> function,
            BuiltinFunctionName builtin,
            IReadOnlyList<CosmosElement> arguments)
        {
            if (arguments.Count != 1)
            {
                throw new ArgumentException($"{builtin} takes exactly one argument.");
            }

            return function(arguments[0]);
        }

        private static CosmosElement ExecuteTwoArgumentFunction(
            Func<CosmosElement, CosmosElement, CosmosElement> function,
            BuiltinFunctionName builtin,
            IReadOnlyList<CosmosElement> arguments)
        {
            if (arguments.Count != 2)
            {
                throw new ArgumentException($"{builtin} takes exactly two argument.");
            }

            return function(arguments[0], arguments[1]);
        }

        private static CosmosElement ExecuteThreeArgumentFunction(
            Func<CosmosElement, CosmosElement, CosmosElement, CosmosElement> function,
            BuiltinFunctionName builtin,
            IReadOnlyList<CosmosElement> arguments)
        {
            if (arguments.Count != 3)
            {
                throw new ArgumentException($"{builtin} takes exactly three argument.");
            }

            return function(arguments[0], arguments[1], arguments[2]);
        }

        private static CosmosElement ExecuteOneOrTwoArgumentFunction(
            Func<CosmosElement, CosmosElement, CosmosElement> function,
            BuiltinFunctionName builtin,
            IReadOnlyList<CosmosElement> arguments)
        {
            if (arguments.Count != 1 && arguments.Count != 2)
            {
                throw new ArgumentException($"{builtin} takes exactly one or two arguments.");
            }

            CosmosElement result = arguments.Count == 1 ? function(arguments[0], null) : function(arguments[0], arguments[1]);
            return result;
        }

        private static CosmosElement ExecuteTwoOrThreeArgumentFunction(
            Func<CosmosElement, CosmosElement, CosmosElement, CosmosElement> function,
            BuiltinFunctionName builtin,
            IReadOnlyList<CosmosElement> arguments)
        {
            if (arguments.Count != 2 && arguments.Count != 3)
            {
                throw new ArgumentException($"{builtin} takes exactly one or two arguments.");
            }

            CosmosElement result = arguments.Count == 2 ? function(arguments[0], arguments[1], null) : function(arguments[0], arguments[1], arguments[2]);
            return result;
        }

        private static CosmosElement ExecuteAtleastTwoArgumentFunction(
            Func<CosmosElement, CosmosElement, CosmosElement[], CosmosElement> function,
            BuiltinFunctionName builtin,
            IReadOnlyList<CosmosElement> arguments)
        {
            if (arguments.Count < 2)
            {
                throw new ArgumentException($"{builtin} takes atleast two arguments.");
            }

            CosmosElement result = arguments.Count == 2
                ? function(arguments[0], arguments[1], null)
                : function(arguments[0], arguments[1], arguments.Skip(2).ToArray());
            return result;
        }

        private static CosmosElement ExecuteOneArgumentNumberFunction(
            Func<double, double> numberFunction,
            CosmosElement argument)
        {
            if (!(argument is CosmosNumber argumentAsNumber))
            {
                return CosmosUndefined.Create();
            }

            return CosmosNumber64.Create(numberFunction(Number64.ToDouble(argumentAsNumber.Value)));
        }

        private static CosmosElement ExecuteTwoArgumentNumberFunction(
            Func<double, double, double> numberFunction,
            CosmosElement argument1,
            CosmosElement argument2)
        {
            if (!(argument1 is CosmosNumber argumentAsNumber1))
            {
                return CosmosUndefined.Create();
            }

            if (!(argument2 is CosmosNumber argumentAsNumber2))
            {
                return CosmosUndefined.Create();
            }

            return CosmosNumber64.Create(numberFunction(
                Number64.ToDouble(argumentAsNumber1.Value),
                Number64.ToDouble(argumentAsNumber2.Value)));
        }

        private static CosmosElement ExecuteOneArgumentStringFunction(
            Func<string, CosmosElement> stringFunction,
            CosmosElement argument)
        {
            if (!(argument is CosmosString argumentAsString))
            {
                return CosmosUndefined.Create();
            }

            return stringFunction(argumentAsString.Value);
        }

        private static CosmosElement ExecuteTwoArgumentStringFunction(
            Func<string, string, CosmosElement> stringFunction,
            CosmosElement argument1,
            CosmosElement argument2)
        {
            if (!(argument1 is CosmosString argumentAsString1))
            {
                return CosmosUndefined.Create();
            }

            if (!(argument2 is CosmosString argumentAsString2))
            {
                return CosmosUndefined.Create();
            }

            return stringFunction(argumentAsString1.Value, argumentAsString2.Value);
        }

        private static CosmosElement ExecuteSubstring(
            CosmosElement stringArgument,
            CosmosElement startIndex,
            CosmosElement length)
        {
            if (!(stringArgument is CosmosString stringArgumentValue))
            {
                return CosmosUndefined.Create();
            }

            if (!(startIndex is CosmosNumber startIndexAsNumber))
            {
                return CosmosUndefined.Create();
            }

            if (!(length is CosmosNumber lengthAsNumber))
            {
                return CosmosUndefined.Create();
            }

            if (stringArgumentValue.Value.IsEmpty)
            {
                return CosmosString.Empty;
            }

            if (!startIndexAsNumber.Value.IsInteger)
            {
                return CosmosUndefined.Create();
            }

            if (!lengthAsNumber.Value.IsInteger)
            {
                return CosmosUndefined.Create();
            }

            long startIndexAsInteger = Number64.ToLong(startIndexAsNumber.Value);
            long lengthAsInteger = Number64.ToLong(lengthAsNumber.Value);

            int safeStartIndexAsInteger = (int)Math.Min(startIndexAsInteger, int.MaxValue);

            if ((startIndexAsInteger == 0) && (stringArgumentValue.Value.ToString().Length <= lengthAsInteger))
            {
                return stringArgumentValue;
            }

            if ((startIndexAsInteger >= stringArgumentValue.Value.ToString().Length) || (startIndexAsInteger < 0) || (lengthAsInteger <= 0))
            {
                return CosmosString.Empty;
            }

            long maxLength = stringArgumentValue.Value.ToString().Length - startIndexAsInteger;
            int safeMaxLength = (int)Math.Min(Math.Min(maxLength, lengthAsInteger), int.MaxValue);
            return CosmosString.Create(stringArgumentValue.Value.ToString().Substring(safeStartIndexAsInteger, safeMaxLength));
        }
    }
}