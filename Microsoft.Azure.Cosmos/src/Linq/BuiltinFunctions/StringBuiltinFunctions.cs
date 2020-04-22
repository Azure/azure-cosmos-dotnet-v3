//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.Linq.Expressions;
    using Microsoft.Azure.Cosmos.Sql;

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

                    return SqlFunctionCallScalarExpression.CreateBuiltin("CONCAT", arguments);
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
                    SqlScalarExpression haystack = ExpressionToSql.VisitScalarExpression(methodCallExpression.Arguments[0], context);
                    SqlScalarExpression needle = ExpressionToSql.VisitScalarExpression(methodCallExpression.Arguments[1], context);
                    return SqlFunctionCallScalarExpression.CreateBuiltin("CONTAINS", haystack, needle);

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
                    SqlScalarExpression str = ExpressionToSql.VisitScalarExpression(methodCallExpression.Arguments[0], context);
                    return SqlFunctionCallScalarExpression.CreateBuiltin("LENGTH", str);
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
                bool validInNet = false;
                bool validInNetCore = false;

                if (methodCallExpression.Arguments.Count == 1 &&
                    methodCallExpression.Arguments[0].NodeType == ExpressionType.Constant &&
                    methodCallExpression.Arguments[0].Type == typeof(char[]))
                {
                    char[] argumentsExpressions = (char[])((ConstantExpression)methodCallExpression.Arguments[0]).Value;
                    if (argumentsExpressions.Length == 0)
                    {
                        validInNet = true;
                    }
                }
                else if (methodCallExpression.Arguments.Count == 0)
                {
                    validInNetCore = true;
                }

                if (validInNet || validInNetCore)
                {
                    SqlScalarExpression str = ExpressionToSql.VisitScalarExpression(methodCallExpression.Object, context);
                    return SqlFunctionCallScalarExpression.CreateBuiltin("LTRIM", str);
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
                    SqlScalarExpression str = ExpressionToSql.VisitNonSubqueryScalarExpression(methodCallExpression.Arguments[0], context);
                    return SqlFunctionCallScalarExpression.CreateBuiltin("REVERSE", str);
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
                bool validInNet = false;
                bool validInNetCore = false;

                if (methodCallExpression.Arguments.Count == 1 &&
                    methodCallExpression.Arguments[0].NodeType == ExpressionType.Constant &&
                    methodCallExpression.Arguments[0].Type == typeof(char[]))
                {
                    char[] argumentsExpressions = (char[])((ConstantExpression)methodCallExpression.Arguments[0]).Value;
                    if (argumentsExpressions.Length == 0)
                    {
                        validInNet = true;
                    }
                }
                else if (methodCallExpression.Arguments.Count == 0)
                {
                    validInNetCore = true;

                }

                if (validInNet || validInNetCore)
                {
                    SqlScalarExpression str = ExpressionToSql.VisitScalarExpression(methodCallExpression.Object, context);
                    return SqlFunctionCallScalarExpression.CreateBuiltin("RTRIM", str);
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
                    SqlScalarExpression[] arguments = new SqlScalarExpression[]
                    {
                        memberExpression,
                        indexExpression,
                        ExpressionToSql.VisitScalarExpression(Expression.Constant(1), context)
                    };

                    return SqlFunctionCallScalarExpression.CreateBuiltin("SUBSTRING", arguments);
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

                    return SqlBinaryScalarExpression.Create(SqlBinaryScalarOperatorKind.Equal, left, right);
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
            if (StringBuiltinFunctionDefinitions.TryGetValue(methodCallExpression.Method.Name, out BuiltinFunctionVisitor visitor))
            {
                return visitor.Visit(methodCallExpression, context);
            }

            throw new DocumentQueryException(string.Format(CultureInfo.CurrentCulture, ClientResources.MethodNotSupported, methodCallExpression.Method.Name));
        }
    }
}
