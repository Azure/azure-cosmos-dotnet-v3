//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.Linq.Expressions;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.SqlObjects;

    internal static class OtherBuiltinSystemFunctions
    {
        private class RRFVisitor : SqlBuiltinFunctionVisitor
        {
            public RRFVisitor()
                : base("RRF",
                    true,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(double[])},
                        new Type[]{typeof(double[]), typeof(double[])}
                    })
            {
            }

            protected override SqlScalarExpression VisitImplicit(MethodCallExpression methodCallExpression, TranslationContext context)
            {
                if (methodCallExpression.Arguments.Count != 1 && methodCallExpression.Arguments.Count != 2)
                {
                    throw new DocumentQueryException("Invalid Argument Count.");
                }

                if (methodCallExpression.Arguments[0] is NewArrayExpression argumentsExpressions)
                {
                    ReadOnlyCollection<Expression> functionListExpression = argumentsExpressions.Expressions;
                    List<SqlScalarExpression> arguments = new List<SqlScalarExpression>();
                    foreach (Expression argument in functionListExpression)
                    {
                        if (!(argument is MethodCallExpression functionCallExpression))
                        {
                            throw new ArgumentException(
                                string.Format(
                                    CultureInfo.CurrentCulture,
                                    "Expressions of type {0} is not supported as an argument to CosmosLinqExtensions.RRF. Supported expressions are method calls to {1}, {2}.",
                                    argument.Type,
                                    nameof(CosmosLinqExtensions.FullTextScore),
                                    nameof(CosmosLinqExtensions.VectorDistance)));
                        }
                        
                        if (functionCallExpression.Method.Name != nameof(CosmosLinqExtensions.FullTextScore) &&
                            functionCallExpression.Method.Name != nameof(CosmosLinqExtensions.VectorDistance))
                        {
                            throw new ArgumentException(
                                string.Format(
                                    CultureInfo.CurrentCulture,
                                    "Method {0} is not supported as an argument to CosmosLinqExtensions.RRF. Supported methods are {1}, {2}.",
                                    functionCallExpression.Method.Name,
                                    nameof(CosmosLinqExtensions.FullTextScore),
                                    nameof(CosmosLinqExtensions.VectorDistance)));
                        }

                        arguments.Add(ExpressionToSql.VisitNonSubqueryScalarExpression(argument, context));
                    }

                    // Append the weight if exists
                    if (methodCallExpression.Arguments.Count == 2)
                    {
                        arguments.Add(ExpressionToSql.VisitNonSubqueryScalarExpression(methodCallExpression.Arguments[1], context));
                    }

                    return SqlFunctionCallScalarExpression.CreateBuiltin(SqlFunctionCallScalarExpression.Names.RRF, arguments.ToImmutableArray());
                }

                throw new DocumentQueryException(string.Format(CultureInfo.CurrentCulture, "Method {0} is not supported with the given argument list.", methodCallExpression.Method.Name));
            }

            protected override SqlScalarExpression VisitExplicit(MethodCallExpression methodCallExpression, TranslationContext context)
            {
                return null;
            }
        }

        private class FullTextScoreVisitor : SqlBuiltinFunctionVisitor
        {
            public FullTextScoreVisitor()
                : base("FullTextScore",
                    true,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(object), typeof(string[])}
                    })
            {
            }

            protected override SqlScalarExpression VisitImplicit(MethodCallExpression methodCallExpression, TranslationContext context)
            {
                if (methodCallExpression.Arguments.Count == 2
                    && methodCallExpression.Arguments[1] is ConstantExpression stringListArgumentExpression
                    && ExpressionToSql.VisitConstant(stringListArgumentExpression, context) is SqlArrayCreateScalarExpression arrayScalarExpressions)
                {
                    List<SqlScalarExpression> arguments = new List<SqlScalarExpression>
                    {
                        ExpressionToSql.VisitNonSubqueryScalarExpression(methodCallExpression.Arguments[0], context)
                    };

                    arguments.AddRange(arrayScalarExpressions.Items);

                    return SqlFunctionCallScalarExpression.CreateBuiltin(SqlFunctionCallScalarExpression.Names.FullTextScore, arguments.ToImmutableArray());
                }

                return null;
            }

            protected override SqlScalarExpression VisitExplicit(MethodCallExpression methodCallExpression, TranslationContext context)
            {
                return null;
            }
        }

        private class VectorDistanceVisitor : SqlBuiltinFunctionVisitor
        {
            public VectorDistanceVisitor()
                : base("VectorDistance",
                    true,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(float[]), typeof(float[]), typeof(bool), typeof(CosmosLinqExtensions.VectorDistanceOptions)},
                        new Type[]{typeof(sbyte[]), typeof(sbyte[]), typeof(bool), typeof(CosmosLinqExtensions.VectorDistanceOptions)},
                        new Type[]{typeof(byte[]), typeof(byte[]), typeof(bool), typeof(CosmosLinqExtensions.VectorDistanceOptions)},
                    })
            {
            }

            protected override SqlScalarExpression VisitImplicit(MethodCallExpression methodCallExpression, TranslationContext context)
            {
                if (methodCallExpression.Arguments.Count != 4) throw new ArgumentException();

                List<SqlScalarExpression> arguments = new List<SqlScalarExpression>
                {
                    ExpressionToSql.VisitNonSubqueryScalarExpression(methodCallExpression.Arguments[0], context),
                    ExpressionToSql.VisitNonSubqueryScalarExpression(methodCallExpression.Arguments[1], context),
                    ExpressionToSql.VisitNonSubqueryScalarExpression(methodCallExpression.Arguments[2], context)
                };

                if (methodCallExpression.Arguments[3] is ConstantExpression optionExpression && optionExpression.Value != null)
                {
                    JsonSerializerOptions options = new JsonSerializerOptions
                    {
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                    };
                    options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

                    string serializedConstant = JsonSerializer.Serialize(
                        optionExpression.Value, 
                        options);

                    arguments.Add(CosmosElement.Parse(serializedConstant).Accept(CosmosElementToSqlScalarExpressionVisitor.Singleton));
                }

                return SqlFunctionCallScalarExpression.CreateBuiltin(SqlFunctionCallScalarExpression.Names.VectorDistance, arguments.ToImmutableArray());
            }

            protected override SqlScalarExpression VisitExplicit(MethodCallExpression methodCallExpression, TranslationContext context)
            {
                return null;
            }
        }

        private class ArrayContainsAllAnyVisitor : SqlBuiltinFunctionVisitor
        {
            public ArrayContainsAllAnyVisitor(string sqlName)
                : base(sqlName,
                    true,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(object), typeof(object[])}
                    })
            {
            }

            protected override SqlScalarExpression VisitImplicit(MethodCallExpression methodCallExpression, TranslationContext context)
            {
                if (methodCallExpression.Arguments.Count != 2)
                {
                    return null;
                }

                List<SqlScalarExpression> arguments = new List<SqlScalarExpression>
                {
                    // First argument: the array to search in
                    ExpressionToSql.VisitNonSubqueryScalarExpression(methodCallExpression.Arguments[0], context)
                };

                // Unwrap the second argument based on its type
                Expression secondArgument = methodCallExpression.Arguments[1];

                switch (secondArgument)
                {
                    case NewArrayExpression arrayExpression:
                        // Unwrap inline array initialization (e.g., new[] { 1, 2, 3 })
                        foreach (Expression element in arrayExpression.Expressions)
                        {
                            arguments.Add(ExpressionToSql.VisitNonSubqueryScalarExpression(element, context));
                        }
                        break;

                    case ConstantExpression constantExpression when constantExpression.Value is Array constantArray:
                        // Unwrap constant array
                        foreach (object element in constantArray)
                        {
                            Expression constantElementExpression = Expression.Constant(element, element?.GetType() ?? typeof(object));
                            arguments.Add(ExpressionToSql.VisitNonSubqueryScalarExpression(constantElementExpression, context));
                        }
                        break;

                    default:
                        return null;
                }

                return SqlFunctionCallScalarExpression.CreateBuiltin(this.SqlName, arguments.ToArray());
            }

            protected override SqlScalarExpression VisitExplicit(MethodCallExpression methodCallExpression, TranslationContext context)
            {
                return null;
            }
        }

        private static Dictionary<string, BuiltinFunctionVisitor> FunctionsDefinitions { get; set; }

        static OtherBuiltinSystemFunctions()
        {
            FunctionsDefinitions = new Dictionary<string, BuiltinFunctionVisitor>
            {
                [nameof(CosmosLinqExtensions.ArrayContainsAll)] = new ArrayContainsAllAnyVisitor(sqlName: "ARRAY_CONTAINS_ALL"),
                [nameof(CosmosLinqExtensions.ArrayContainsAny)] = new ArrayContainsAllAnyVisitor(sqlName: "ARRAY_CONTAINS_ANY"),
                [nameof(CosmosLinqExtensions.DocumentId)] = new SqlBuiltinFunctionVisitor(
                    sqlName: "DOCUMENTID",
                    isStatic: true,
                    argumentLists: new List<Type[]>()
                    {
                        new Type[]{typeof(object)},
                    }),
                [nameof(CosmosLinqExtensions.FullTextScore)] = new FullTextScoreVisitor(),
                [nameof(CosmosLinqExtensions.RRF)] = new RRFVisitor(),
                [nameof(CosmosLinqExtensions.VectorDistance)] = new VectorDistanceVisitor(),
            };
        }

        public static SqlScalarExpression Visit(MethodCallExpression methodCallExpression, TranslationContext context)
        {
            return FunctionsDefinitions.TryGetValue(methodCallExpression.Method.Name, out BuiltinFunctionVisitor visitor)
                ? visitor.Visit(methodCallExpression, context)
                : throw new DocumentQueryException(string.Format(CultureInfo.CurrentCulture, ClientResources.MethodNotSupported, methodCallExpression.Method.Name));
        }
    }
}
