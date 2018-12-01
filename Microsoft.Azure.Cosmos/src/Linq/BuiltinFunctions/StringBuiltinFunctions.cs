//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using Microsoft.Azure.Cosmos.Sql;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    
    internal static class StringBuiltinFunctions
    {
        private static Dictionary<string, BuiltinFunctionVisitor> StringBuiltinFunctionDefinitions { get; set; }

        private class StringVisitConcat : SqlBuiltinFunctionVisitor
        {
            public StringVisitConcat()
                : base("CONCAT",
                    true,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(string), typeof(string)},
                        new Type[]{typeof(string), typeof(string), typeof(string)},
                        new Type[]{typeof(string), typeof(string), typeof(string), typeof(string)},
                    })
            {
            }

            protected override SqlScalarExpression VisitImplicit(MethodCallExpression methodCallExpression, TranslationContext context)
            {
                if (methodCallExpression.Arguments.Count == 1
                    && methodCallExpression.Arguments[0] is NewArrayExpression)
                {
                    ReadOnlyCollection<Expression> argumentsExpressions = ((NewArrayExpression)methodCallExpression.Arguments[0]).Expressions;
                    List<SqlScalarExpression> arguments = new List<SqlScalarExpression>();
                    foreach (Expression argument in argumentsExpressions)
                    {
                        arguments.Add(ExpressionToSql.VisitScalarExpression(argument, context));
                    }

                    return new SqlFunctionCallScalarExpression(new SqlIdentifier(this.SqlName), arguments.ToArray(), false);
                }

                return null;
            }
        }

        private class StringVisitContains : SqlBuiltinFunctionVisitor
        {
            public StringVisitContains()
                : base("CONTAINS",
                    false,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(string)}
                    })
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

        private class StringVisitCount : SqlBuiltinFunctionVisitor
        {
            public StringVisitCount()
                : base("LENGTH",
                    true,
                    null)
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

        private class StringVisitTrimStart : SqlBuiltinFunctionVisitor
        {
            public StringVisitTrimStart()
                : base("LTRIM",
                    false,
                    null)
            {
            }

            protected override SqlScalarExpression VisitImplicit(MethodCallExpression methodCallExpression, TranslationContext context)
            {
                if (methodCallExpression.Arguments.Count == 1 &&
                    methodCallExpression.Arguments[0].NodeType == ExpressionType.Constant &&
                    methodCallExpression.Arguments[0].Type == typeof(char[]))
                {
                    char[] argumentsExpressions = (char[])((ConstantExpression)methodCallExpression.Arguments[0]).Value;
                    if (argumentsExpressions.Length == 0)
                    {
                        List<SqlScalarExpression> arguments = new List<SqlScalarExpression>();
                        arguments.Add(ExpressionToSql.VisitScalarExpression(methodCallExpression.Object, context));

                        return new SqlFunctionCallScalarExpression(new SqlIdentifier(this.SqlName), arguments.ToArray(), false);
                    }
                }

                return null;
            }
        }

        private class StringVisitReverse : SqlBuiltinFunctionVisitor
        {
            public StringVisitReverse()
                : base("REVERSE",
                    true,
                    null)
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

        private class StringVisitTrimEnd : SqlBuiltinFunctionVisitor
        {
            public StringVisitTrimEnd()
                : base("RTRIM",
                    false,
                    null)
            {
            }

            protected override SqlScalarExpression VisitImplicit(MethodCallExpression methodCallExpression, TranslationContext context)
            {
                if (methodCallExpression.Arguments.Count == 1 &&
                    methodCallExpression.Arguments[0].NodeType == ExpressionType.Constant &&
                    methodCallExpression.Arguments[0].Type == typeof(char[]))
                {
                    char[] argumentsExpressions = (char[])((ConstantExpression)methodCallExpression.Arguments[0]).Value;
                    if (argumentsExpressions.Length == 0)
                    {
                        List<SqlScalarExpression> arguments = new List<SqlScalarExpression>();
                        arguments.Add(ExpressionToSql.VisitScalarExpression(methodCallExpression.Object, context));

                        return new SqlFunctionCallScalarExpression(new SqlIdentifier(this.SqlName), arguments.ToArray(), false);
                    }
                }

                return null;
            }
        }

        private class StringGetCharsVisitor : BuiltinFunctionVisitor
        {
            protected override SqlScalarExpression VisitImplicit(MethodCallExpression methodCallExpression, TranslationContext context)
            {
                if (methodCallExpression.Arguments.Count == 1)
                {
                    SqlScalarExpression memberExpression = ExpressionToSql.VisitScalarExpression(methodCallExpression.Object, context);
                    SqlScalarExpression indexExpression = ExpressionToSql.VisitScalarExpression(methodCallExpression.Arguments[0], context);
                    var arguments = new SqlScalarExpression[] {
                        memberExpression,
                        indexExpression,
                        ExpressionToSql.VisitScalarExpression(Expression.Constant(1), context)
                    };

                    return new SqlFunctionCallScalarExpression(new SqlIdentifier("SUBSTRING"), arguments, false);
                }

                return null;
            }

            protected override SqlScalarExpression VisitExplicit(MethodCallExpression methodCallExpression, TranslationContext context)
            {
                return null;
            }
        }

        private class StringEqualsVisitor : BuiltinFunctionVisitor
        {
            protected override SqlScalarExpression VisitImplicit(MethodCallExpression methodCallExpression, TranslationContext context)
            {
                if (methodCallExpression.Arguments.Count == 1)
                {
                    SqlScalarExpression left = ExpressionToSql.VisitScalarExpression(methodCallExpression.Object, context);
                    SqlScalarExpression right = ExpressionToSql.VisitScalarExpression(methodCallExpression.Arguments[0], context);

                    return new SqlBinaryScalarExpression(SqlBinaryScalarOperatorKind.Equal, left, right);
                }

                return null;
            }

            protected override SqlScalarExpression VisitExplicit(MethodCallExpression methodCallExpression, TranslationContext context)
            {
                return null;
            }
        }

        static StringBuiltinFunctions()
        {
            StringBuiltinFunctionDefinitions = new Dictionary<string, BuiltinFunctionVisitor>();

            StringBuiltinFunctionDefinitions.Add("Concat",
                new StringVisitConcat());

            StringBuiltinFunctionDefinitions.Add("Contains",
                new StringVisitContains());

            StringBuiltinFunctionDefinitions.Add("EndsWith",
                new SqlBuiltinFunctionVisitor("ENDSWITH",
                    false,
                    new List<Type[]>
                    {
                        new Type[]{typeof(string)}
                    }));

            StringBuiltinFunctionDefinitions.Add("IndexOf",
                new SqlBuiltinFunctionVisitor("INDEX_OF",
                    false,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(char)},
                        new Type[]{typeof(string)},
                        new Type[]{typeof(char), typeof(int)},
                        new Type[]{typeof(string), typeof(int)},
                    }));

            StringBuiltinFunctionDefinitions.Add("Count",
                new StringVisitCount());

            StringBuiltinFunctionDefinitions.Add("ToLower",
                new SqlBuiltinFunctionVisitor("LOWER",
                    false,
                    new List<Type[]>()
                    {
                        new Type[]{}
                    }));

            StringBuiltinFunctionDefinitions.Add("TrimStart",
                new StringVisitTrimStart());

            StringBuiltinFunctionDefinitions.Add("Replace",
                new SqlBuiltinFunctionVisitor("REPLACE",
                    false,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(char), typeof(char)},
                        new Type[]{typeof(string), typeof(string)}
                    }));

            StringBuiltinFunctionDefinitions.Add("Reverse",
                new StringVisitReverse());

            StringBuiltinFunctionDefinitions.Add("TrimEnd",
                new StringVisitTrimEnd());

            StringBuiltinFunctionDefinitions.Add("StartsWith",
                new SqlBuiltinFunctionVisitor("STARTSWITH",
                    false,
                    new List<Type[]>
                    {
                        new Type[]{typeof(string)}
                    }));

            StringBuiltinFunctionDefinitions.Add("Substring",
                new SqlBuiltinFunctionVisitor("SUBSTRING",
                    false,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(int), typeof(int)}
                    }));

            StringBuiltinFunctionDefinitions.Add("ToUpper",
                new SqlBuiltinFunctionVisitor("UPPER",
                    false,
                    new List<Type[]>()
                    {
                        new Type[]{}
                    }));

            StringBuiltinFunctionDefinitions.Add("get_Chars",
                new StringGetCharsVisitor());

            StringBuiltinFunctionDefinitions.Add("Equals",
                new StringEqualsVisitor());
        }

        public static SqlScalarExpression Visit(MethodCallExpression methodCallExpression, TranslationContext context)
        {
            BuiltinFunctionVisitor visitor = null;
            if (StringBuiltinFunctionDefinitions.TryGetValue(methodCallExpression.Method.Name, out visitor))
            {
                return visitor.Visit(methodCallExpression, context);
            }

            throw new DocumentQueryException(string.Format(CultureInfo.CurrentCulture, ClientResources.MethodNotSupported, methodCallExpression.Method.Name));
        }
    }
}
