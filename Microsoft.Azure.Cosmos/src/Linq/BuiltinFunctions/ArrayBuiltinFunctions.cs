//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using Microsoft.Azure.Cosmos.Sql;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;

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
                    List<SqlScalarExpression> arguments = new List<SqlScalarExpression>();
                    arguments.Add(ExpressionToSql.VisitScalarExpression(methodCallExpression.Arguments[0], context));
                    arguments.Add(ExpressionToSql.VisitScalarExpression(methodCallExpression.Arguments[1], context));

                    return new SqlFunctionCallScalarExpression(new SqlIdentifier(this.SqlName), arguments.ToArray(), false);
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

                if(searchList == null || searchExpression == null)
                {
                    return null;
                }

                if(searchList.NodeType == ExpressionType.Constant)
                {
                    return VisitIN(searchExpression, (ConstantExpression)searchList, context);
                }

                List<SqlScalarExpression> arguments = new List<SqlScalarExpression>();

                arguments.Add(ExpressionToSql.VisitScalarExpression(searchList, context));
                arguments.Add(ExpressionToSql.VisitScalarExpression(searchExpression, context));

                return new SqlFunctionCallScalarExpression(new SqlIdentifier(this.SqlName), arguments.ToArray(), false);
            }

            private SqlScalarExpression VisitIN(Expression expression, ConstantExpression constantExpressionList, TranslationContext context)
            {
                List<SqlScalarExpression> items = new List<SqlScalarExpression>();
                foreach (var item in ((IEnumerable)(constantExpressionList.Value)))
                {
                    items.Add(ExpressionToSql.VisitConstant(Expression.Constant(item)));
                }

                // if the items list empty, then just return false expression
                if(items.Count == 0)
                {
                    return new SqlLiteralScalarExpression(new SqlBooleanLiteral(false));
                }

                SqlScalarExpression scalarExpression = ExpressionToSql.VisitScalarExpression(expression, context);
                return new SqlInScalarExpression(scalarExpression, items.ToArray(), false);
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
                    arguments.Add(ExpressionToSql.VisitScalarExpression(methodCallExpression.Arguments[0], context));

                    return new SqlFunctionCallScalarExpression(new SqlIdentifier(this.SqlName), arguments.ToArray(), false);
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

                    return new SqlMemberIndexerScalarExpression(memberExpression, indexExpression);
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
        }

        public static SqlScalarExpression Visit(MethodCallExpression methodCallExpression, TranslationContext context)
        {
            BuiltinFunctionVisitor visitor = null;
            if (ArrayBuiltinFunctionDefinitions.TryGetValue(methodCallExpression.Method.Name, out visitor))
            {
                return visitor.Visit(methodCallExpression, context);
            }

            throw new DocumentQueryException(string.Format(CultureInfo.CurrentCulture, ClientResources.MethodNotSupported, methodCallExpression.Method.Name));
        }
    }
}
