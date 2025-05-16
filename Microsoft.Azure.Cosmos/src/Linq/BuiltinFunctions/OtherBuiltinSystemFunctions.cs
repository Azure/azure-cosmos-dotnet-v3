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
        private class RRFVisit : SqlBuiltinFunctionVisitor
        {
            public RRFVisit()
                : base("RRF",
                    true,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(double[])}
                    })
            {
            }

            protected override SqlScalarExpression VisitImplicit(MethodCallExpression methodCallExpression, TranslationContext context)
            {
                if (methodCallExpression.Arguments.Count == 1
                    && methodCallExpression.Arguments[0] is NewArrayExpression argumentsExpressions)
                {
                    // For RRF, We don't need to care about the first argument, it is the object itself and have no relevance to the computation
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

                    return SqlFunctionCallScalarExpression.CreateBuiltin(SqlFunctionCallScalarExpression.Names.RRF, arguments.ToImmutableArray());
                }

                return null;
            }

            protected override SqlScalarExpression VisitExplicit(MethodCallExpression methodCallExpression, TranslationContext context)
            {
                return null;
            }
        }

        private class FullTextScoreVisit : SqlBuiltinFunctionVisitor
        {
            public FullTextScoreVisit()
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

        private class VectorDistanceVisit : SqlBuiltinFunctionVisitor
        {
            public VectorDistanceVisit()
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

        private static Dictionary<string, BuiltinFunctionVisitor> FunctionsDefinitions { get; set; }

        static OtherBuiltinSystemFunctions()
        {
            FunctionsDefinitions = new Dictionary<string, BuiltinFunctionVisitor>
            {
                [nameof(CosmosLinqExtensions.DocumentId)] = new SqlBuiltinFunctionVisitor(
                    sqlName: "DOCUMENTID",
                    isStatic: true,
                    argumentLists: new List<Type[]>()
                    {
                        new Type[]{typeof(object)},
                    }),
                [nameof(CosmosLinqExtensions.RRF)] = new RRFVisit(),
                [nameof(CosmosLinqExtensions.FullTextScore)] = new FullTextScoreVisit(),
                [nameof(CosmosLinqExtensions.VectorDistance)] = new VectorDistanceVisit(),
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
