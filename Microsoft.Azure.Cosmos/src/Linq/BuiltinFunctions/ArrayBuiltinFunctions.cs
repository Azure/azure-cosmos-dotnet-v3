//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq.Expressions;
    using Microsoft.Azure.Cosmos.Sql;

    internal static class ArrayBuiltinFunctions
    {
        private static Dictionary<string, BuiltinFunctionVisitor> ArrayBuiltinFunctionDefinitions { get; set; }

        private class ArrayConcatVisitor : SqlBuiltinFunctionVisitor
        {
            public ArrayConcatVisitor()
                : base("ARRAY_CONCAT", true, null)
            {
            }

            protected override SqlScalarExpression VisitImplicit(MethodCallExpression methodCallExpression, TranslationContext context)
            {
                if (methodCallExpression.Arguments.Count == 2)
                {
                    SqlScalarExpression array1 = ExpressionToSql.VisitScalarExpression(methodCallExpression.Arguments[0], context);
                    SqlScalarExpression array2 = ExpressionToSql.VisitScalarExpression(methodCallExpression.Arguments[1], context);
                    return SqlFunctionCallScalarExpression.CreateBuiltin("ARRAY_CONCAT", array1, array2);
                }

                return null;
            }
        }

        private class ArrayContainsVisitor : SqlBuiltinFunctionVisitor
        {
            public ArrayContainsVisitor()
                : base("ARRAY_CONTAINS", true, null)
            {
            }

            protected override SqlScalarExpression VisitImplicit(MethodCallExpression methodCallExpression, TranslationContext context)
            {
                Expression searchList = null;
                Expression searchExpression = null;

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

                if (searchList == null || searchExpression == null)
                {
                    return null;
                }

                if (searchList.NodeType == ExpressionType.Constant)
                {
                    return this.VisitIN(searchExpression, (ConstantExpression)searchList, context);
                }

                SqlScalarExpression array = ExpressionToSql.VisitScalarExpression(searchList, context);
                SqlScalarExpression expression = ExpressionToSql.VisitScalarExpression(searchExpression, context);
                return SqlFunctionCallScalarExpression.CreateBuiltin("ARRAY_CONTAINS", array, expression);
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
                return SqlInScalarExpression.Create(scalarExpression, false, items);
            }
        }

        private class ArrayCountVisitor : SqlBuiltinFunctionVisitor
        {
            public ArrayCountVisitor()
                : base("ARRAY_LENGTH", true, null)
            {
            }

            protected override SqlScalarExpression VisitImplicit(MethodCallExpression methodCallExpression, TranslationContext context)
            {
                if (methodCallExpression.Arguments.Count == 1)
                {
                    List<SqlScalarExpression> arguments = new List<SqlScalarExpression>();
                    SqlScalarExpression array = ExpressionToSql.VisitScalarExpression(methodCallExpression.Arguments[0], context);

                    return SqlFunctionCallScalarExpression.CreateBuiltin("ARRAY_LENGTH", array);
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

        static ArrayBuiltinFunctions()
        {
            ArrayBuiltinFunctionDefinitions = new Dictionary<string, BuiltinFunctionVisitor>();

            ArrayBuiltinFunctionDefinitions.Add("Concat",
                new ArrayConcatVisitor());

            ArrayBuiltinFunctionDefinitions.Add("Contains",
                new ArrayContainsVisitor());

            ArrayBuiltinFunctionDefinitions.Add("Count",
                new ArrayCountVisitor());

            ArrayBuiltinFunctionDefinitions.Add("get_Item",
                new ArrayGetItemVisitor());

            ArrayBuiltinFunctionDefinitions.Add("ToArray",
                new ArrayToArrayVisitor());

            ArrayBuiltinFunctionDefinitions.Add("ToList",
                new ArrayToArrayVisitor());
        }

        public static SqlScalarExpression Visit(MethodCallExpression methodCallExpression, TranslationContext context)
        {
            if (ArrayBuiltinFunctionDefinitions.TryGetValue(methodCallExpression.Method.Name, out BuiltinFunctionVisitor visitor))
            {
                return visitor.Visit(methodCallExpression, context);
            }

            throw new DocumentQueryException(string.Format(CultureInfo.CurrentCulture, ClientResources.MethodNotSupported, methodCallExpression.Method.Name));
        }
    }
}
