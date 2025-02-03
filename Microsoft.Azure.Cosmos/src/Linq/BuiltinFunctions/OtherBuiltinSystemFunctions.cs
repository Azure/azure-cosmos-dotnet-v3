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
                        new Type[]{typeof(object), typeof(Func<object, object>[])}
                    })
            {
            }

            protected override SqlScalarExpression VisitImplicit(MethodCallExpression methodCallExpression, TranslationContext context)
            {
                if (methodCallExpression.Arguments.Count == 2
                    && methodCallExpression.Arguments[1] is NewArrayExpression argumentsExpressions)
                {
                    // For RRF, We don't need to care about the first argument, it is the object itself and have no relevance to the computation
                    ReadOnlyCollection<Expression> functionListExpression = argumentsExpressions.Expressions;
                    List<SqlScalarExpression> arguments = new List<SqlScalarExpression>();
                    foreach (Expression argument in functionListExpression)
                    {
                        arguments.Add(ExpressionToSql.VisitScalarExpression(argument, context));
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

            //protected override SqlScalarExpression VisitImplicit(MethodCallExpression methodCallExpression, TranslationContext context)
            //{
            //    if (methodCallExpression.Arguments.Count == 2
            //        && methodCallExpression.Arguments[1] is NewArrayExpression argumentsExpressions)
            //    {
            //        // For RRF, We don't need to care about the first argument, it is the object itself and have no relevance to the computation
            //        ReadOnlyCollection<Expression> functionListExpression = argumentsExpressions.Expressions;
            //        List<SqlScalarExpression> arguments = new List<SqlScalarExpression>();
            //        foreach (Expression argument in functionListExpression)
            //        {
            //            arguments.Add(ExpressionToSql.VisitScalarExpression(argument, context));
            //        }

            //        return SqlFunctionCallScalarExpression.CreateBuiltin(SqlFunctionCallScalarExpression.Names.RRF, arguments.ToImmutableArray());
            //    }

            //    return null;
            //}

            //protected override SqlScalarExpression VisitExplicit(MethodCallExpression methodCallExpression, TranslationContext context)
            //{
            //    return null;
            //}
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
