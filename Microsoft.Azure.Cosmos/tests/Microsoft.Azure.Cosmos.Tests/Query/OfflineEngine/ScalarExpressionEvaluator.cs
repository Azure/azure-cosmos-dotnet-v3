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
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.SqlObjects;
    using Microsoft.Azure.Cosmos.SqlObjects.Visitors;

    /// <summary>
    /// Given a scalar expression and document this class evaluates the result of that scalar expression.
    /// </summary>
    internal sealed class ScalarExpressionEvaluator : SqlScalarExpressionVisitor<CosmosElement, CosmosElement>
    {
        public static readonly ScalarExpressionEvaluator Singleton = new ScalarExpressionEvaluator();

        private ScalarExpressionEvaluator()
        {
        }

        public override CosmosElement Visit(
            SqlAllScalarExpression scalarExpression,
            CosmosElement document)
        {
            // We evaluate the ALL expression by constructing an equivalent EXISTS and evaluating that.
            // ALL ( Filter Expression ) ==> NOT EXISTS ( NOT Filter Expression )

            // If there is is no filter expression, then an equivalent filter expression of just true is created.
            SqlScalarExpression filterExpression;
            if (scalarExpression.Subquery.WhereClause == null)
            {
                SqlLiteral trueLiteral = SqlBooleanLiteral.Create(true);
                filterExpression = SqlLiteralScalarExpression.Create(trueLiteral);
            }
            else
            {
                filterExpression = scalarExpression.Subquery.WhereClause.FilterExpression;
            }

            // Create a NOT unary with filter expression.
            SqlUnaryScalarExpression negatedFilterExpression = SqlUnaryScalarExpression.Create(
                SqlUnaryScalarOperatorKind.Not,
                filterExpression);

            // Create new where clause with negated filter expression.
            SqlWhereClause newWhereClause = SqlWhereClause.Create(negatedFilterExpression);

            // create new subquery with new where clause. 
            SqlQuery newSqlQuery = SqlQuery.Create(
                scalarExpression.Subquery.SelectClause,
                scalarExpression.Subquery.FromClause,
                newWhereClause,
                scalarExpression.Subquery.GroupByClause,
                scalarExpression.Subquery.OrderByClause,
                scalarExpression.Subquery.OffsetLimitClause);

            // Create an exists expression with new subquery.
            SqlExistsScalarExpression newExistsScalarExpression = SqlExistsScalarExpression.Create(newSqlQuery);

            // Create a not unary with the exists expression.
            SqlUnaryScalarExpression negatedExistsExpression = SqlUnaryScalarExpression.Create(
                SqlUnaryScalarOperatorKind.Not,
                newExistsScalarExpression);

            // Visit the equivalent NOT EXISTS expression.
            return this.Visit(negatedExistsExpression, document);
        }

        public override CosmosElement Visit(
            SqlArrayCreateScalarExpression scalarExpression,
            CosmosElement document)
        {
            List<CosmosElement> arrayItems = new List<CosmosElement>();
            foreach (SqlScalarExpression item in scalarExpression.Items)
            {
                CosmosElement value = item.Accept(this, document);
                if (value is not CosmosUndefined)
                {
                    arrayItems.Add(value);
                }
            }

            return CosmosArray.Create(arrayItems);
        }

        public override CosmosElement Visit(
            SqlArrayScalarExpression scalarExpression,
            CosmosElement document)
        {
            // Only run on the current document since the subquery is always correlated.
            IEnumerable<CosmosElement> subqueryResults = SqlInterpreter.ExecuteQuery(
                new CosmosElement[] { document },
                scalarExpression.SqlQuery);
            List<CosmosElement> arrayScalarResult = new List<CosmosElement>();
            foreach (CosmosElement subQueryResult in subqueryResults)
            {
                arrayScalarResult.Add(subQueryResult);
            }

            return CosmosArray.Create(subqueryResults);
        }

        public override CosmosElement Visit(
            SqlBetweenScalarExpression scalarExpression,
            CosmosElement document)
        {
            // expression <not> BETWEEN left AND right === <not>(expression >= left && expression <= right);
            SqlBinaryScalarExpression expressionGTELeft = SqlBinaryScalarExpression.Create(
                SqlBinaryScalarOperatorKind.GreaterThanOrEqual,
                scalarExpression.Expression,
                scalarExpression.StartInclusive);

            SqlBinaryScalarExpression expressionLTERight = SqlBinaryScalarExpression.Create(
                SqlBinaryScalarOperatorKind.LessThanOrEqual,
                scalarExpression.Expression,
                scalarExpression.EndInclusive);

            SqlScalarExpression logicalBetween = SqlBinaryScalarExpression.Create(
                SqlBinaryScalarOperatorKind.And,
                expressionGTELeft,
                expressionLTERight);

            if (scalarExpression.Not)
            {
                logicalBetween = SqlUnaryScalarExpression.Create(SqlUnaryScalarOperatorKind.Not, logicalBetween);
            }

            return logicalBetween.Accept(this, document);
        }

        public override CosmosElement Visit(
            SqlBinaryScalarExpression scalarExpression,
            CosmosElement document)
        {
            CosmosElement left = scalarExpression.LeftExpression.Accept(this, document);
            CosmosElement right = scalarExpression.RightExpression.Accept(this, document);

            CosmosElement result;
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
                    result = left is not CosmosUndefined ? left : right;

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

        public override CosmosElement Visit(SqlCoalesceScalarExpression scalarExpression, CosmosElement document)
        {
            CosmosElement left = scalarExpression.Left.Accept(this, document);
            CosmosElement right = scalarExpression.Right.Accept(this, document);

            return left is not CosmosUndefined ? left : right;
        }

        public override CosmosElement Visit(SqlConditionalScalarExpression scalarExpression, CosmosElement document)
        {
            CosmosElement condition = scalarExpression.Condition.Accept(this, document);
            CosmosElement first = scalarExpression.Consequent.Accept(this, document);
            CosmosElement second = scalarExpression.Alternative.Accept(this, document);

            return (condition is CosmosBoolean cosmosBoolean && cosmosBoolean.Value) ? first : second;
        }

        public override CosmosElement Visit(SqlExistsScalarExpression scalarExpression, CosmosElement document)
        {
            // Only run on the current document since the subquery is always correlated.
            IEnumerable<CosmosElement> subqueryResults = SqlInterpreter.ExecuteQuery(
                new CosmosElement[] { document },
                scalarExpression.Subquery);
            return CosmosBoolean.Create(subqueryResults.Any());
        }

        public override CosmosElement Visit(SqlFirstScalarExpression scalarExpression, CosmosElement document)
        {
            // Only run on the current document since the subquery is always correlated.
            IEnumerable<CosmosElement> subqueryResults = SqlInterpreter.ExecuteQuery(
                new CosmosElement[] { document },
                scalarExpression.Subquery);

            return subqueryResults.FirstOrDefault(CosmosUndefined.Create());
        }

        public override CosmosElement Visit(SqlFunctionCallScalarExpression scalarExpression, CosmosElement document)
        {
            List<CosmosElement> arguments = new List<CosmosElement>();
            foreach (SqlScalarExpression argument in scalarExpression.Arguments)
            {
                CosmosElement evaluatedArgument = argument.Accept(this, document);
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

        public override CosmosElement Visit(SqlInScalarExpression scalarExpression, CosmosElement document)
        {
            CosmosElement expression = scalarExpression.Needle.Accept(this, document);
            if (expression is CosmosUndefined)
            {
                return expression;
            }

            HashSet<CosmosElement> items = new HashSet<CosmosElement>();
            foreach (SqlScalarExpression item in scalarExpression.Haystack)
            {
                items.Add(item.Accept(this, document));
            }

            bool contains = items.Contains(expression);
            if (scalarExpression.Not)
            {
                contains = !contains;
            }

            return CosmosBoolean.Create(contains);
        }

        public override CosmosElement Visit(SqlLastScalarExpression scalarExpression, CosmosElement document)
        {
            // Only run on the current document since the subquery is always correlated.
            IEnumerable<CosmosElement> subqueryResults = SqlInterpreter.ExecuteQuery(
                new CosmosElement[] { document },
                scalarExpression.Subquery);

            return subqueryResults.LastOrDefault(CosmosUndefined.Create());
        }

        public override CosmosElement Visit(SqlLikeScalarExpression scalarExpression, CosmosElement document)
        {
            // Consider the necessity of having v3 offline engine. Should we remove this altogether?
            throw new NotImplementedException();
        }

        public override CosmosElement Visit(SqlLiteralScalarExpression scalarExpression, CosmosElement document)
        {
            SqlLiteral sqlLiteral = scalarExpression.Literal;
            return sqlLiteral.Accept(SqlLiteralToCosmosElement.Singleton);
        }

        public override CosmosElement Visit(SqlMemberIndexerScalarExpression scalarExpression, CosmosElement document)
        {
            CosmosElement member = scalarExpression.Member.Accept(this, document);
            CosmosElement index = scalarExpression.Indexer.Accept(this, document);
            return IndexIntoMember(member, index);
        }

        public override CosmosElement Visit(SqlObjectCreateScalarExpression scalarExpression, CosmosElement document)
        {
            Dictionary<string, CosmosElement> properties = new Dictionary<string, CosmosElement>();
            foreach (SqlObjectProperty sqlObjectProperty in scalarExpression.Properties)
            {
                string key = sqlObjectProperty.Name.Value;
                CosmosElement value = sqlObjectProperty.Value.Accept(this, document);
                if (value is not CosmosUndefined)
                {
                    properties[key] = value;
                }
            }

            return CosmosObject.Create(properties);
        }

        public override CosmosElement Visit(SqlParameterRefScalarExpression scalarExpression, CosmosElement document)
        {
            return CosmosString.Create(scalarExpression.Parameter.Name);
        }

        public override CosmosElement Visit(SqlPropertyRefScalarExpression scalarExpression, CosmosElement document)
        {
            CosmosObject documentAsObject = (CosmosObject)document;
            CosmosElement result;
            if (scalarExpression.Member == null)
            {
                // just an identifier
                result = documentAsObject[scalarExpression.Identifier.Value];
            }
            else
            {
                CosmosElement member = scalarExpression.Member.Accept(this, document);
                CosmosElement index = CosmosString.Create(scalarExpression.Identifier.Value);
                result = IndexIntoMember(member, index);
            }

            return result;
        }

        public override CosmosElement Visit(SqlSubqueryScalarExpression scalarExpression, CosmosElement document)
        {
            // Only run on the current document since the subquery is always correlated.
            IEnumerable<CosmosElement> subqueryResults = SqlInterpreter.ExecuteQuery(
                new CosmosElement[] { document },
                scalarExpression.Query);

            CosmosElement result;
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
                result = CosmosUndefined.Create();
            }

            return result;
        }

        public override CosmosElement Visit(SqlUnaryScalarExpression scalarExpression, CosmosElement document)
        {
            CosmosElement expression = scalarExpression.Expression.Accept(this, document);
            CosmosElement result = scalarExpression.OperatorKind switch
            {
                SqlUnaryScalarOperatorKind.BitwiseNot => PerformUnaryNumberOperation((number) => ~DoubleToInt32Bitwise(number), expression),
                SqlUnaryScalarOperatorKind.Not => PerformUnaryBooleanOperation((boolean) => !boolean, expression),
                SqlUnaryScalarOperatorKind.Minus => PerformUnaryNumberOperation((number) => -number, expression),
                SqlUnaryScalarOperatorKind.Plus => PerformUnaryNumberOperation((number) => +number, expression),
                _ => throw new ArgumentException($"Unknown {nameof(SqlUnaryScalarOperatorKind)}: {scalarExpression.OperatorKind}"),
            };
            return result;
        }

        private static CosmosElement PerformBinaryNumberOperation(
            Func<double, double, double> operation,
            CosmosElement left,
            CosmosElement right)
        {
            if (!(left is CosmosNumber leftAsNumber))
            {
                return CosmosUndefined.Create();
            }

            if (!(right is CosmosNumber rightAsNumber))
            {
                return CosmosUndefined.Create();
            }

            double result = operation(Number64.ToDouble(leftAsNumber.Value), Number64.ToDouble(rightAsNumber.Value));
            return CosmosNumber64.Create(result);
        }

        private static CosmosElement PerformLogicalAnd(CosmosElement left, CosmosElement right)
        {
            bool leftIsBoolean = left is CosmosBoolean;
            bool rightIsBoolean = right is CosmosBoolean;

            // If the expression is false && <anything>, then the result is false
            if (leftIsBoolean && !(left as CosmosBoolean).Value)
            {
                return CosmosBoolean.Create(false);
            }

            if (rightIsBoolean && !(right as CosmosBoolean).Value)
            {
                return CosmosBoolean.Create(false);
            }

            if (!leftIsBoolean)
            {
                return CosmosUndefined.Create();
            }

            if (!rightIsBoolean)
            {
                return CosmosUndefined.Create();
            }

            bool result = (left as CosmosBoolean).Value && (right as CosmosBoolean).Value;
            return CosmosBoolean.Create(result);
        }

        private static CosmosElement PerformLogicalOr(CosmosElement left, CosmosElement right)
        {
            bool leftIsBoolean = left is CosmosBoolean;
            bool rightIsBoolean = right is CosmosBoolean;

            // If the expression is true || <anything>, then the result is true
            if (leftIsBoolean && (left as CosmosBoolean).Value)
            {
                return CosmosBoolean.Create(true);
            }

            if (rightIsBoolean && (right as CosmosBoolean).Value)
            {
                return CosmosBoolean.Create(true);
            }

            if (!leftIsBoolean)
            {
                return CosmosUndefined.Create();
            }

            if (!rightIsBoolean)
            {
                return CosmosUndefined.Create();
            }

            bool result = (left as CosmosBoolean).Value || (right as CosmosBoolean).Value;
            return CosmosBoolean.Create(result);
        }

        private static CosmosElement PerformBinaryStringOperation(
            Func<string, string, string> operation,
            CosmosElement left,
            CosmosElement right)
        {
            if (!(left is CosmosString leftAsString))
            {
                return CosmosUndefined.Create();
            }

            if (!(right is CosmosString rightAsString))
            {
                return CosmosUndefined.Create();
            }

            string result = operation(leftAsString.Value, rightAsString.Value);
            return CosmosString.Create(result);
        }

        private static CosmosElement PerformBinaryInequality(
            Func<int, bool> inequalityFunction,
            CosmosElement left,
            CosmosElement right)
        {
            if (!Utils.TryCompare(left, right, out int comparison))
            {
                return CosmosUndefined.Create();
            }

            bool result = inequalityFunction(comparison);
            return CosmosBoolean.Create(result);
        }

        private static CosmosElement PerformBinaryEquality(
            Func<bool, bool> equalityFunction,
            CosmosElement left,
            CosmosElement right)
        {
            if ((left is CosmosUndefined) || (right is CosmosUndefined))
            {
                return CosmosUndefined.Create();
            }

            return CosmosBoolean.Create(equalityFunction(left == right));
        }

        private static CosmosElement PerformUnaryNumberOperation(
            Func<double, double> unaryOperation,
            CosmosElement operand)
        {
            if (!(operand is CosmosNumber operandAsNumber))
            {
                return CosmosUndefined.Create();
            }

            double result = unaryOperation(Number64.ToDouble(operandAsNumber.Value));
            return CosmosNumber64.Create(result);
        }

        private static CosmosElement PerformUnaryBooleanOperation(
            Func<bool, bool> unaryOperation,
            CosmosElement operand)
        {
            if (!(operand is CosmosBoolean operandAsBoolean))
            {
                return CosmosUndefined.Create();
            }

            bool result = unaryOperation(operandAsBoolean.Value);
            return CosmosBoolean.Create(result);
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

            static uint ToUInt32(double value)
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

        private static CosmosElement IndexIntoMember(CosmosElement member, CosmosElement indexer)
        {
            if ((member is CosmosUndefined) || (indexer is CosmosUndefined))
            {
                return CosmosUndefined.Create();
            }

            return member.Accept(MemberIndexerVisitor.Singleton, indexer);
        }

        private sealed class SqlLiteralToCosmosElement : SqlLiteralVisitor<CosmosElement>
        {
            public static readonly SqlLiteralToCosmosElement Singleton = new SqlLiteralToCosmosElement();

            public override CosmosElement Visit(SqlNumberLiteral literal)
            {
                return CosmosNumber64.Create(literal.Value);
            }

            public override CosmosElement Visit(SqlStringLiteral literal)
            {
                return CosmosString.Create(literal.Value);
            }

            public override CosmosElement Visit(SqlUndefinedLiteral literal)
            {
                return CosmosUndefined.Create();
            }

            public override CosmosElement Visit(SqlNullLiteral literal)
            {
                return CosmosNull.Create();
            }

            public override CosmosElement Visit(SqlBooleanLiteral literal)
            {
                return CosmosBoolean.Create(literal.Value);
            }
        }

        private sealed class MemberIndexerVisitor : ICosmosElementVisitor<CosmosElement, CosmosElement>
        {
            public static readonly MemberIndexerVisitor Singleton = new MemberIndexerVisitor();

            private MemberIndexerVisitor()
            {
            }

            public CosmosElement Visit(CosmosArray cosmosArray, CosmosElement indexer)
            {
                if (!(indexer is CosmosNumber indexerAsNumber))
                {
                    return CosmosUndefined.Create();
                }

                if (!indexerAsNumber.Value.IsInteger)
                {
                    throw new ArgumentException("Number index must be a non negative integer.");
                }

                long numberIndexValue = Number64.ToLong(indexerAsNumber.Value);
                if (numberIndexValue < 0)
                {
                    throw new ArgumentException("Number index must be a non negative integer.");
                }

                if (numberIndexValue >= cosmosArray.Count)
                {
                    return CosmosUndefined.Create();
                }

                return cosmosArray[(int)numberIndexValue];
            }

            public CosmosElement Visit(CosmosObject cosmosObject, CosmosElement indexer)
            {
                if (!(indexer is CosmosString indexerAsString))
                {
                    return CosmosUndefined.Create();
                }

                string stringIndexValue = indexerAsString.Value;
                if (!cosmosObject.TryGetValue(stringIndexValue, out CosmosElement propertyValue))
                {
                    return CosmosUndefined.Create();
                }

                return propertyValue;
            }

            public CosmosElement Visit(CosmosBinary cosmosBinary, CosmosElement indexer)
            {
                return CosmosUndefined.Create();
            }

            public CosmosElement Visit(CosmosBoolean cosmosBoolean, CosmosElement indexer)
            {
                return CosmosUndefined.Create();
            }

            public CosmosElement Visit(CosmosGuid cosmosGuid, CosmosElement indexer)
            {
                return CosmosUndefined.Create();
            }

            public CosmosElement Visit(CosmosNull cosmosNull, CosmosElement indexer)
            {
                return CosmosUndefined.Create();
            }

            public CosmosElement Visit(CosmosNumber cosmosNumber, CosmosElement indexer)
            {
                return CosmosUndefined.Create();
            }

            public CosmosElement Visit(CosmosString cosmosString, CosmosElement indexer)
            {
                return CosmosUndefined.Create();
            }

            public CosmosElement Visit(CosmosUndefined cosmosUndefined, CosmosElement input)
            {
                return CosmosUndefined.Create();
            }
        }
    }
}