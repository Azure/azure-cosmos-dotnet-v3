//-----------------------------------------------------------------------
// <copyright file="ScalarExpressionEvaluator.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Tests.Query.OfflineEngine
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Cosmos.SqlObjects;
    using Microsoft.Azure.Cosmos.SqlObjects.Visitors;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Given a scalar expression and document this class evaluates the result of that scalar expression.
    /// </summary>
    internal sealed class ScalarExpressionEvaluator : SqlScalarExpressionVisitor<JToken, JToken>
    {
        private static readonly JToken Undefined = null;

        private readonly CollectionConfigurations collectionConfigurations;

        private ScalarExpressionEvaluator(CollectionConfigurations collectionConfigurations)
        {
            this.collectionConfigurations = collectionConfigurations;
        }

        public override JToken Visit(SqlArrayCreateScalarExpression scalarExpression, JToken document)
        {
            JArray result = new JArray();
            foreach (SqlScalarExpression item in scalarExpression.Items)
            {
                JToken value = item.Accept(this, document);
                if (value != Undefined)
                {
                    result.Add(value);
                }
            }

            return result;
        }

        public override JToken Visit(SqlArrayScalarExpression scalarExpression, JToken document)
        {
            // Only run on the current document since the subquery is always correlated.
            IEnumerable<JToken> subqueryResults = SqlInterpreter.ExecuteQuery(
                new JToken[] { document },
                scalarExpression.SqlQuery,
                this.collectionConfigurations);
            JArray arrayScalarResult = new JArray();
            foreach (JToken subQueryResult in subqueryResults)
            {
                arrayScalarResult.Add(subqueryResults);
            }

            return arrayScalarResult;
        }

        public override JToken Visit(SqlBetweenScalarExpression scalarExpression, JToken document)
        {
            // expression <not> BETWEEN left AND right === <not>(expression >= left && expression <= right);
            SqlBinaryScalarExpression expressionGTELeft = SqlBinaryScalarExpression.Create(
                SqlBinaryScalarOperatorKind.GreaterThanOrEqual,
                scalarExpression.Expression,
                scalarExpression.Left);

            SqlBinaryScalarExpression expressionLTERight = SqlBinaryScalarExpression.Create(
                SqlBinaryScalarOperatorKind.LessThanOrEqual,
                scalarExpression.Expression,
                scalarExpression.Right);

            SqlScalarExpression logicalBetween = SqlBinaryScalarExpression.Create(
                SqlBinaryScalarOperatorKind.And,
                expressionGTELeft,
                expressionLTERight);

            if (scalarExpression.IsNot)
            {
                logicalBetween = SqlUnaryScalarExpression.Create(SqlUnaryScalarOperatorKind.Not, logicalBetween);
            }

            return logicalBetween.Accept(this, document);
        }

        public override JToken Visit(SqlBinaryScalarExpression scalarExpression, JToken document)
        {
            JToken left = scalarExpression.Left.Accept(this, document);
            JToken right = scalarExpression.Right.Accept(this, document);

            JToken result;
            switch (scalarExpression.OperatorKind)
            {
                case SqlBinaryScalarOperatorKind.Add:
                    result = PerformBinaryNumberOperation((number1, number2) => number1 + number2, left, right);
                    break;

                case SqlBinaryScalarOperatorKind.And:
                    result = PerformLogicalAnd(left, right);
                    break;

                case SqlBinaryScalarOperatorKind.BitwiseAnd:
                    result = PerformBinaryNumberOperation((number1, number2) => DoubleToInt32Bitwise(number1) & DoubleToInt32Bitwise(number2), left, right);
                    break;

                case SqlBinaryScalarOperatorKind.BitwiseOr:
                    result = PerformBinaryNumberOperation((number1, number2) => DoubleToInt32Bitwise(number1) | DoubleToInt32Bitwise(number2), left, right);
                    break;

                case SqlBinaryScalarOperatorKind.BitwiseXor:
                    result = PerformBinaryNumberOperation((number1, number2) => DoubleToInt32Bitwise(number1) ^ DoubleToInt32Bitwise(number2), left, right);
                    break;

                case SqlBinaryScalarOperatorKind.Coalesce:
                    if (left != Undefined)
                    {
                        result = left;
                    }
                    else
                    {
                        result = right;
                    }

                    break;

                case SqlBinaryScalarOperatorKind.Divide:
                    result = PerformBinaryNumberOperation((number1, number2) => number1 / number2, left, right);
                    break;

                case SqlBinaryScalarOperatorKind.Equal:
                    result = PerformBinaryEquality(equals => equals, left, right);
                    break;

                case SqlBinaryScalarOperatorKind.GreaterThan:
                    result = PerformBinaryInequality((comparison) => comparison > 0, left, right);
                    break;

                case SqlBinaryScalarOperatorKind.GreaterThanOrEqual:
                    result = PerformBinaryInequality((comparison) => comparison >= 0, left, right);
                    break;

                case SqlBinaryScalarOperatorKind.LessThan:
                    result = PerformBinaryInequality((comparison) => comparison < 0, left, right);
                    break;

                case SqlBinaryScalarOperatorKind.LessThanOrEqual:
                    result = PerformBinaryInequality((comparison) => comparison <= 0, left, right);
                    break;

                case SqlBinaryScalarOperatorKind.Modulo:
                    result = PerformBinaryNumberOperation((number1, number2) => number1 % number2, left, right);
                    break;

                case SqlBinaryScalarOperatorKind.Multiply:
                    result = PerformBinaryNumberOperation((number1, number2) => number1 * number2, left, right);
                    break;

                case SqlBinaryScalarOperatorKind.NotEqual:
                    result = PerformBinaryEquality(equals => !equals, left, right);
                    break;

                case SqlBinaryScalarOperatorKind.Or:
                    result = PerformLogicalOr(left, right);
                    break;

                case SqlBinaryScalarOperatorKind.StringConcat:
                    result = PerformBinaryStringOperation((string1, string2) => string1 + string2, left, right);
                    break;

                case SqlBinaryScalarOperatorKind.Subtract:
                    result = PerformBinaryNumberOperation((number1, number2) => number1 - number2, left, right);
                    break;

                default:
                    throw new ArgumentException($"Unknown {nameof(SqlBinaryScalarOperatorKind)}: {scalarExpression.OperatorKind}");
            }

            return result;
        }

        public override JToken Visit(SqlCoalesceScalarExpression scalarExpression, JToken document)
        {
            JToken left = scalarExpression.Left.Accept(this, document);
            JToken right = scalarExpression.Right.Accept(this, document);

            return left != Undefined ? left : right;
        }

        public override JToken Visit(SqlConditionalScalarExpression scalarExpression, JToken document)
        {
            JToken condition = scalarExpression.Condition.Accept(this, document);
            JToken first = scalarExpression.Consequent.Accept(this, document);
            JToken second = scalarExpression.Alternative.Accept(this, document);

            return Utils.IsTrue(condition) ? first : second;
        }

        public override JToken Visit(SqlExistsScalarExpression scalarExpression, JToken document)
        {
            // Only run on the current document since the subquery is always correlated.
            IEnumerable<JToken> subqueryResults = SqlInterpreter.ExecuteQuery(
                new JToken[] { document },
                scalarExpression.Subquery,
                this.collectionConfigurations);
            return subqueryResults.Any();
        }

        public override JToken Visit(SqlFunctionCallScalarExpression scalarExpression, JToken document)
        {
            List<JToken> arguments = new List<JToken>();
            foreach (SqlScalarExpression argument in scalarExpression.Arguments)
            {
                JToken evaluatedArgument = argument.Accept(this, document);
                arguments.Add(evaluatedArgument);
            }

            if (scalarExpression.IsUdf)
            {
                throw new NotSupportedException("Udfs are not supported.");
            }

            if (scalarExpression.Name.Value.StartsWith("ST_", StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException("Spatial functions are not supported.");
            }

            return BuiltinFunctionEvaluator.EvaluateFunctionCall(
                scalarExpression.Name.Value,
                arguments);
        }

        public override JToken Visit(SqlInScalarExpression scalarExpression, JToken document)
        {
            JToken expression = scalarExpression.Needle.Accept(this, document);
            if (expression == Undefined)
            {
                return Undefined;
            }

            HashSet<JToken> items = new HashSet<JToken>(JToken.EqualityComparer);
            foreach (SqlScalarExpression item in scalarExpression.Haystack)
            {
                items.Add(item.Accept(this, document));
            }

            bool contains = items.Contains(expression);
            if (scalarExpression.Not)
            {
                contains = !contains;
            }

            return contains;
        }

        public override JToken Visit(SqlLiteralScalarExpression scalarExpression, JToken document)
        {
            SqlLiteral sqlLiteral = scalarExpression.Literal;
            return sqlLiteral.Accept(SqlLiteralToJToken.Singleton);
        }

        public override JToken Visit(SqlMemberIndexerScalarExpression scalarExpression, JToken document)
        {
            JToken member = scalarExpression.Member.Accept(this, document);
            JToken index = scalarExpression.Indexer.Accept(this, document);
            return IndexIntoMember(member, index);
        }

        public override JToken Visit(SqlObjectCreateScalarExpression scalarExpression, JToken document)
        {
            JObject result = new JObject();
            foreach (SqlObjectProperty sqlObjectProperty in scalarExpression.Properties)
            {
                string key = sqlObjectProperty.Name.Value;
                JToken value = sqlObjectProperty.Value.Accept(this, document);
                if (value != Undefined)
                {
                    result[key] = value;
                }
            }

            return result;
        }

        public override JToken Visit(SqlPropertyRefScalarExpression scalarExpression, JToken document)
        {
            JToken result;
            if (scalarExpression.Member == null)
            {
                // just an identifier
                result = document[scalarExpression.Identifier.Value];
            }
            else
            {
                JToken member = scalarExpression.Member.Accept(this, document);
                JToken index = scalarExpression.Identifier.Value;
                result = IndexIntoMember(member, index);
            }

            return result;
        }

        public override JToken Visit(SqlSubqueryScalarExpression scalarExpression, JToken document)
        {
            // Only run on the current document since the subquery is always correlated.
            IEnumerable<JToken> subqueryResults = SqlInterpreter.ExecuteQuery(
                new JToken[] { document },
                scalarExpression.Query,
                this.collectionConfigurations);

            JToken result;
            int cardinality = subqueryResults.Count();
            if (cardinality > 1)
            {
                throw new ArgumentException("The cardinality of a subquery can not exceed 1.");
            }
            else if (cardinality == 1)
            {
                result = subqueryResults.First();
            }
            else
            {
                // cardinality = 0
                result = Undefined;
            }

            return result;
        }

        public override JToken Visit(SqlUnaryScalarExpression scalarExpression, JToken document)
        {
            JToken expression = scalarExpression.Expression.Accept(this, document);

            JToken result;
            switch (scalarExpression.OperatorKind)
            {
                case SqlUnaryScalarOperatorKind.BitwiseNot:
                    result = PerformUnaryNumberOperation((number) => ~DoubleToInt32Bitwise(number), expression);
                    break;

                case SqlUnaryScalarOperatorKind.Not:
                    result = PerformUnaryBooleanOperation((boolean) => !boolean, expression);
                    break;

                case SqlUnaryScalarOperatorKind.Minus:
                    result = PerformUnaryNumberOperation((number) => -number, expression);
                    break;

                case SqlUnaryScalarOperatorKind.Plus:
                    result = PerformUnaryNumberOperation((number) => +number, expression);
                    break;

                default:
                    throw new ArgumentException($"Unknown {nameof(SqlUnaryScalarOperatorKind)}: {scalarExpression.OperatorKind}");
            }

            return result;
        }

        public static ScalarExpressionEvaluator Create(CollectionConfigurations collectionConfigurations)
        {
            return new ScalarExpressionEvaluator(collectionConfigurations);
        }

        private static JToken PerformBinaryNumberOperation(Func<double, double, JToken> operation, JToken left, JToken right)
        {
            bool leftIsNumber = Utils.TryConvertToNumber(left, out double leftNumber);
            bool rightIsNumber = Utils.TryConvertToNumber(right, out double rightNumber);

            JToken result;
            if (leftIsNumber && rightIsNumber)
            {
                result = operation(leftNumber, rightNumber);
            }
            else
            {
                result = Undefined;
            }

            return result;
        }

        private static JToken PerformLogicalAnd(JToken left, JToken right)
        {
            bool leftIsBoolean = Utils.TryConvertToBoolean(left, out bool leftBoolean);
            bool rightIsBoolean = Utils.TryConvertToBoolean(right, out bool rightBoolean);

            // If the expression is false && <anything>, then the result is false
            if (leftIsBoolean && !leftBoolean)
            {
                return false;
            }

            if (rightIsBoolean && !rightBoolean)
            {
                return false;
            }

            JToken result;
            if (leftIsBoolean && rightIsBoolean)
            {
                result = leftBoolean && rightBoolean;
            }
            else
            {
                // If either argument is not a boolean then the result is undefined
                result = Undefined;
            }

            return result;
        }

        private static JToken PerformLogicalOr(JToken left, JToken right)
        {
            bool leftIsBoolean = Utils.TryConvertToBoolean(left, out bool leftBoolean);
            bool rightIsBoolean = Utils.TryConvertToBoolean(right, out bool rightBoolean);

            // If the expression is true || <anything>, then the result is true
            if (leftIsBoolean && leftBoolean)
            {
                return true;
            }

            if (rightIsBoolean && rightBoolean)
            {
                return true;
            }

            JToken result;
            if (leftIsBoolean && rightIsBoolean)
            {
                result = leftBoolean || rightBoolean;
            }
            else
            {
                // If either argument is not a boolean then the result is undefined
                result = Undefined;
            }

            return result;
        }

        private static JToken PerformBinaryStringOperation(Func<string, string, JToken> operation, JToken left, JToken right)
        {
            bool leftIsString = Utils.TryConvertToString(left, out string leftString);
            bool rightIsString = Utils.TryConvertToString(right, out string rightString);

            JToken result;
            if (leftIsString && rightIsString)
            {
                result = operation(leftString, rightString);
            }
            else
            {
                result = Undefined;
            }

            return result;
        }

        private static JToken PerformBinaryInequality(Func<int, JToken> inequalityFunction, JToken left, JToken right)
        {
            JToken result;
            if (Utils.TryCompare(left, right, out int comparison))
            {
                result = inequalityFunction(comparison);
            }
            else
            {
                result = Undefined;
            }

            return result;
        }

        private static JToken PerformBinaryEquality(Func<bool, JToken> equalityFunction, JToken left, JToken right)
        {
            if (left == Undefined || right == Undefined)
            {
                return Undefined;
            }

            return equalityFunction(JsonTokenEqualityComparer.Value.Equals(left, right));
        }

        private static JToken PerformUnaryNumberOperation(Func<double, JToken> unaryOperation, JToken operand)
        {
            JToken result;
            if (Utils.TryConvertToNumber(operand, out double number))
            {
                result = unaryOperation(number);
            }
            else
            {
                result = Undefined;
            }

            return result;
        }

        private static JToken PerformUnaryBooleanOperation(Func<bool, JToken> unaryOperation, JToken operand)
        {
            JToken result;
            bool boolean;
            if (Utils.TryConvertToBoolean(operand, out boolean))
            {
                result = unaryOperation(boolean);
            }
            else
            {
                result = Undefined;
            }

            return result;
        }

        /// <summary>
        /// DoubleToInt32Bitwise performs a conversion from double to INT32 for purpose of bitwise operations like Not, BitwiseAnd, BitwiseOr.
        /// The conversion is compatible with JavaScript:
        /// -   double is interpreted as a large integer value (Truncate semantic).
        /// -   large integer represented by value is casted to INT32 by preserving it's 32 least significant bits (like when casting LONG LONG to INT).
        /// </summary>
        /// <param name="doubleValue">The value.</param>
        /// <returns>Converts a double to int32 for js bitwise operations.</returns>
        /// <example>
        /// DoubleToInt32Bitwise(100) = 100
        /// DoubleToInt32Bitwise(-100) = 100
        /// DoubleToInt32Bitwise(2147483647) = 2147483647
        /// DoubleToInt32Bitwise(-2147483647.987) = 2147483647
        /// DoubleToInt32Bitwise(2147483648) = -2147483648 
        /// DoubleToInt32Bitwise(2147483649) = -2147483647 
        /// DoubleToInt32Bitwise(6442450944) = -2147483648.
        /// </example>
        private static int DoubleToInt32Bitwise(double doubleValue)
        {

            // Optimistic cast to INT32
            if (doubleValue <= int.MaxValue)
            {
                return (int)doubleValue;
            }

            // Nan, +Infinity, -Infinity are casted as 0.
            if (double.IsInfinity(doubleValue) || double.IsNaN(doubleValue))
            {
                return 0;
            }

            uint ToUInt32(double value)
            {
                long ToInteger(double valueToConvertToInteger)
                {
                    return valueToConvertToInteger < 0 ? (long)Math.Ceiling(valueToConvertToInteger) : (long)Math.Floor(valueToConvertToInteger);
                }

                return (uint)(ToInteger(value) % (long)Math.Pow(2, 32));
            }

            uint unsignedInt32 = ToUInt32(doubleValue);
            if (unsignedInt32 >= Math.Pow(2, 31))
            {
                return (int)((long)unsignedInt32 - (long)Math.Pow(2, 32));
            }
            else
            {
                return (int)unsignedInt32;
            }
        }

        private static JToken IndexIntoMember(JToken member, JToken index)
        {
            if (member == Undefined || index == Undefined)
            {
                return Undefined;
            }

            JToken result;
            JsonType jsonType = JsonTypeUtils.JTokenTypeToJsonType(index.Type);
            switch (jsonType)
            {
                case JsonType.Number:
                    if (JsonTypeUtils.JTokenTypeToJsonType(member.Type) != JsonType.Array)
                    {
                        result = Undefined;
                        break;
                    }

                    double numberIndex = index.Value<double>();
                    if ((int)numberIndex != numberIndex || numberIndex < 0)
                    {
                        throw new ArgumentException("Number index must be a non negative integer.");
                    }

                    JArray arrayMember = (JArray)member;
                    if (numberIndex >= arrayMember.Count)
                    {
                        result = Undefined;
                    }
                    else
                    {
                        result = arrayMember[(int)numberIndex];
                    }

                    break;

                case JsonType.String:
                    if (JsonTypeUtils.JTokenTypeToJsonType(member.Type) != JsonType.Object)
                    {
                        result = Undefined;
                        break;
                    }

                    string stringIndex = index.Value<string>();
                    JObject objectMember = (JObject)member;

                    result = objectMember[stringIndex];
                    break;

                case JsonType.Array:
                    throw new ArgumentException("Can not index using an array.");
                case JsonType.Boolean:
                    throw new ArgumentException("Can not index using a boolean.");
                case JsonType.Null:
                    throw new ArgumentException("Can not index using a null.");
                case JsonType.Object:
                    throw new ArgumentException("Can not index using an object.");
                default:
                    throw new ArgumentException($"Unknown {nameof(JsonType)}: {jsonType}");
            }

            return result;
        }

        private sealed class SqlLiteralToJToken : SqlLiteralVisitor<JToken>
        {
            public static readonly SqlLiteralToJToken Singleton = new SqlLiteralToJToken();

            private static readonly JToken NullJToken = JValue.CreateNull();
            private static readonly JToken TrueJToken = true;
            private static readonly JToken FalseJToken = false;

            public override JToken Visit(SqlNumberLiteral literal)
            {
                return Number64.ToDouble(literal.Value);
            }

            public override JToken Visit(SqlStringLiteral literal)
            {
                return literal.Value;
            }

            public override JToken Visit(SqlUndefinedLiteral literal)
            {
                return null;
            }

            public override JToken Visit(SqlNullLiteral literal)
            {
                return NullJToken;
            }

            public override JToken Visit(SqlBooleanLiteral literal)
            {
                return literal.Value ? TrueJToken : FalseJToken;
            }
        }
    }
}
