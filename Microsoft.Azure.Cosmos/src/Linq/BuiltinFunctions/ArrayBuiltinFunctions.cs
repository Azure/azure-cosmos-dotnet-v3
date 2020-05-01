//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Globalization;
    using System.Linq;
    using System.Linq.Expressions;
    using Microsoft.Azure.Cosmos.Sql;

    internal static class ArrayBuiltinFunctions
    {
        private static readonly ImmutableDictionary<string, BuiltinFunctionVisitor> ArrayBuiltinFunctionDefinitions = new Dictionary<string, BuiltinFunctionVisitor>
        {
            {
                nameof(Enumerable.Concat),
                new ArrayConcatVisitor()
            },
            {
                nameof(Enumerable.Contains),
                new ArrayContainsVisitor()
            },
            {
                nameof(Enumerable.Count),
                new ArrayCountVisitor()
            },
            {
                "get_Item",
                new ArrayGetItemVisitor()
            },
            {
                nameof(Enumerable.ToArray),
                new ArrayToArrayVisitor()
            },
            {
                nameof(Enumerable.ToList),
                new ArrayToArrayVisitor()
            }
        }.ToImmutableDictionary();

        private sealed class ArrayConcatVisitor : SqlBuiltinFunctionVisitor
        {
            public ArrayConcatVisitor()
                : base(
                      name: SqlFunctionCallScalarExpression.Names.ArrayConcat,
                      argumentLists: null)
            {
            }

            protected override SqlScalarExpression VisitImplicit(
                MethodCallExpression methodCallExpression,
                TranslationContext context)
            {
                if (methodCallExpression.Arguments.Count == 2)
                {
                    SqlScalarExpression array1 = ExpressionToSql.VisitScalarExpression(methodCallExpression.Arguments[0], context);
                    SqlScalarExpression array2 = ExpressionToSql.VisitScalarExpression(methodCallExpression.Arguments[1], context);
                    return SqlFunctionCallScalarExpression.CreateBuiltin(
                        SqlFunctionCallScalarExpression.Identifiers.ArrayConcat,
                        array1,
                        array2);
                }

                return null;
            }
        }

        private sealed class ArrayContainsVisitor : SqlBuiltinFunctionVisitor
        {
            public ArrayContainsVisitor()
                : base(
                      name: SqlFunctionCallScalarExpression.Names.ArrayContains,
                      argumentLists: null)
            {
            }

            protected override SqlScalarExpression VisitImplicit(
                MethodCallExpression methodCallExpression,
                TranslationContext context)
            {
                Expression searchList;
                Expression searchExpression;

                // If non static Contains
                if (methodCallExpression.Arguments.Count == 1)
                {
                    searchList = methodCallExpression.Object;
                    searchExpression = methodCallExpression.Arguments[0];
                }
                // if extension method (static) Contains
                else if (methodCallExpression.Arguments.Count == 2)
                {
                    searchList = methodCallExpression.Arguments[0];
                    searchExpression = methodCallExpression.Arguments[1];
                }
                else
                {
                    searchList = null;
                    searchExpression = null;
                }

                if ((searchList == null) || (searchExpression == null))
                {
                    return null;
                }

                if (searchList.NodeType == ExpressionType.Constant)
                {
                    return this.VisitIN(searchExpression, (ConstantExpression)searchList, context);
                }

                SqlScalarExpression array = ExpressionToSql.VisitScalarExpression(searchList, context);
                SqlScalarExpression expression = ExpressionToSql.VisitScalarExpression(searchExpression, context);
                return SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.ArrayContains,
                    array,
                    expression);
            }

            private SqlScalarExpression VisitIN(Expression expression, ConstantExpression constantExpressionList, TranslationContext context)
            {
                List<SqlScalarExpression> items = new List<SqlScalarExpression>();
                foreach (object item in (IEnumerable)constantExpressionList.Value)
                {
                    items.Add(ExpressionToSql.VisitConstant(Expression.Constant(item), context));
                }

                // if the items list empty, then just return false expression
                if (items.Count == 0)
                {
                    return SqlLiteralScalarExpression.SqlFalseLiteralScalarExpression;
                }

                SqlScalarExpression scalarExpression = ExpressionToSql.VisitNonSubqueryScalarExpression(expression, context);
                return SqlInScalarExpression.Create(scalarExpression, not: false, items);
            }
        }

        private class ArrayCountVisitor : SqlBuiltinFunctionVisitor
        {
            public ArrayCountVisitor()
                : base(
                      name: SqlFunctionCallScalarExpression.Names.ArrayLength,
                      argumentLists: null)
            {
            }

            protected override SqlScalarExpression VisitImplicit(MethodCallExpression methodCallExpression, TranslationContext context)
            {
                if (methodCallExpression.Arguments.Count == 1)
                {
                    List<SqlScalarExpression> arguments = new List<SqlScalarExpression>();
                    SqlScalarExpression array = ExpressionToSql.VisitScalarExpression(methodCallExpression.Arguments[0], context);

                    return SqlFunctionCallScalarExpression.CreateBuiltin(SqlFunctionCallScalarExpression.Identifiers.ArrayLength, array);
                }

                return null;
            }
        }

        private class ArrayGetItemVisitor : BuiltinFunctionVisitor
        {
            protected override SqlScalarExpression VisitImplicit(MethodCallExpression methodCallExpression, TranslationContext context)
            {
                if (methodCallExpression.Object != null && methodCallExpression.Arguments.Count == 1)
                {
                    SqlScalarExpression memberExpression = ExpressionToSql.VisitScalarExpression(methodCallExpression.Object, context);
                    SqlScalarExpression indexExpression = ExpressionToSql.VisitScalarExpression(methodCallExpression.Arguments[0], context);

                    return SqlMemberIndexerScalarExpression.Create(memberExpression, indexExpression);
                }

                return null;
            }

            protected override SqlScalarExpression VisitExplicit(MethodCallExpression methodCallExpression, TranslationContext context)
            {
                return null;
            }
        }

        private class ArrayToArrayVisitor : BuiltinFunctionVisitor
        {
            protected override SqlScalarExpression VisitImplicit(MethodCallExpression methodCallExpression, TranslationContext context)
            {
                if (methodCallExpression.Arguments.Count == 1)
                {
                    return ExpressionToSql.VisitScalarExpression(methodCallExpression.Arguments[0], context);
                }

                return null;
            }

            protected override SqlScalarExpression VisitExplicit(MethodCallExpression methodCallExpression, TranslationContext context)
            {
                return null;
            }
        }

        public static SqlScalarExpression Visit(MethodCallExpression methodCallExpression, TranslationContext context)
        {
            if (!ArrayBuiltinFunctionDefinitions.TryGetValue(methodCallExpression.Method.Name, out BuiltinFunctionVisitor visitor))
            {
                throw new DocumentQueryException(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        ClientResources.MethodNotSupported,
                        methodCallExpression.Method.Name));

            }

            return visitor.Visit(methodCallExpression, context);
        }
    }
}
