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
    using System.Linq;
    using System.Linq.Expressions;
    using Microsoft.Azure.Cosmos.Sql;

    internal static class StringBuiltinFunctions
    {
        private static readonly ImmutableDictionary<string, BuiltinFunctionVisitor> StringBuiltinFunctionDefinitions = new Dictionary<string, BuiltinFunctionVisitor>
        {
            {
                nameof(string.Concat),
                new StringVisitConcat()
            },
            {
                nameof(string.Contains),
                new StringVisitContains()
            },
            {
                nameof(string.EndsWith),
                new CosmosBuiltinFunctionVisitor(
                    SqlFunctionCallScalarExpression.Names.Endswith,
                    new List<ImmutableArray<Type>>
                    {
                        new Type[]{typeof(string)}.ToImmutableArray()
                    }.ToImmutableArray())
            },
            {
                nameof(string.IndexOf),
                new CosmosBuiltinFunctionVisitor(
                    SqlFunctionCallScalarExpression.Names.IndexOf,
                    new List<ImmutableArray<Type>>
                    {
                        new Type[]{typeof(char)}.ToImmutableArray(),
                        new Type[]{typeof(string)}.ToImmutableArray(),
                        new Type[]{typeof(char), typeof(int)}.ToImmutableArray(),
                        new Type[]{typeof(string), typeof(int)}.ToImmutableArray(),
                    }.ToImmutableArray())
            },
            {
                nameof(Enumerable.Count),
                new StringVisitCount()
            },
            {
                nameof(string.ToLower),
                new CosmosBuiltinFunctionVisitor(
                    SqlFunctionCallScalarExpression.Names.Lower,
                    new List<ImmutableArray<Type>>
                    {
                        new Type[]{}.ToImmutableArray(),
                    }.ToImmutableArray())
            },
            {
                nameof(string.TrimStart),
                new StringVisitTrimStart()
            },
            {
                nameof(string.Replace),
                new CosmosBuiltinFunctionVisitor(
                    SqlFunctionCallScalarExpression.Names.Replace,
                    new List<ImmutableArray<Type>>
                    {
                        new Type[]{typeof(char), typeof(char)}.ToImmutableArray(),
                        new Type[]{typeof(string), typeof(string)}.ToImmutableArray(),
                    }.ToImmutableArray())
            },
            {
                nameof(Enumerable.Reverse),
                new StringVisitReverse()
            },
            {
                nameof(string.TrimEnd),
                new StringVisitTrimEnd()
            },
            {
                nameof(string.StartsWith),
                new CosmosBuiltinFunctionVisitor(
                    SqlFunctionCallScalarExpression.Names.Startswith,
                    new List<ImmutableArray<Type>>
                    {
                        new Type[]{typeof(string)}.ToImmutableArray()
                    }.ToImmutableArray())
            },
            {
                nameof(string.Substring),
                new CosmosBuiltinFunctionVisitor(
                    SqlFunctionCallScalarExpression.Names.Substring,
                    new List<ImmutableArray<Type>>
                    {
                        new Type[]{typeof(int), typeof(int)}.ToImmutableArray(),
                    }.ToImmutableArray())
            },
            {
                nameof(string.ToUpper),
                new CosmosBuiltinFunctionVisitor(
                    SqlFunctionCallScalarExpression.Names.Upper,
                    new List<ImmutableArray<Type>>
                    {
                        new Type[]{}.ToImmutableArray(),
                    }.ToImmutableArray())
            },
            {
                "get_Chars",
                new StringGetCharsVisitor()
            },
            {
                nameof(string.Equals),
                new StringEqualsVisitor()
            },
        }.ToImmutableDictionary();

        private class StringVisitConcat : CosmosBuiltinFunctionVisitor
        {
            public StringVisitConcat()
                : base(
                      SqlFunctionCallScalarExpression.Names.Concat,
                      new List<ImmutableArray<Type>>()
                      {
                            new Type[]{typeof(string), typeof(string)}.ToImmutableArray(),
                            new Type[]{typeof(string), typeof(string), typeof(string)}.ToImmutableArray(),
                            new Type[]{typeof(string), typeof(string), typeof(string), typeof(string)}.ToImmutableArray(),
                      }.ToImmutableArray())
            {
            }

            protected override SqlScalarExpression VisitImplicit(MethodCallExpression methodCallExpression, TranslationContext context)
            {
                if (methodCallExpression.Arguments.Count == 1
                    && methodCallExpression.Arguments[0] is NewArrayExpression newArrayExpression)
                {
                    ReadOnlyCollection<Expression> argumentsExpressions = newArrayExpression.Expressions;
                    List<SqlScalarExpression> arguments = new List<SqlScalarExpression>();
                    foreach (Expression argument in argumentsExpressions)
                    {
                        arguments.Add(ExpressionToSql.VisitScalarExpression(argument, context));
                    }

                    return SqlFunctionCallScalarExpression.CreateBuiltin(
                        SqlFunctionCallScalarExpression.Identifiers.Concat,
                        arguments);
                }

                return null;
            }
        }

        private class StringVisitContains : CosmosBuiltinFunctionVisitor
        {
            public StringVisitContains()
                : base(
                      SqlFunctionCallScalarExpression.Names.Contains,
                      new List<ImmutableArray<Type>>()
                      {
                            new Type[]{typeof(string)}.ToImmutableArray(),
                      }.ToImmutableArray())
            {
            }

            protected override SqlScalarExpression VisitImplicit(MethodCallExpression methodCallExpression, TranslationContext context)
            {
                if (methodCallExpression.Arguments.Count == 2)
                {
                    SqlScalarExpression haystack = ExpressionToSql.VisitScalarExpression(methodCallExpression.Arguments[0], context);
                    SqlScalarExpression needle = ExpressionToSql.VisitScalarExpression(methodCallExpression.Arguments[1], context);
                    return SqlFunctionCallScalarExpression.CreateBuiltin(
                        SqlFunctionCallScalarExpression.Names.Contains,
                        haystack,
                        needle);

                }

                return null;
            }
        }

        private class StringVisitCount : CosmosBuiltinFunctionVisitor
        {
            public StringVisitCount()
                : base(SqlFunctionCallScalarExpression.Names.Length, argumentLists: null)
            {
            }

            protected override SqlScalarExpression VisitImplicit(MethodCallExpression methodCallExpression, TranslationContext context)
            {
                if (methodCallExpression.Arguments.Count == 1)
                {
                    SqlScalarExpression str = ExpressionToSql.VisitScalarExpression(methodCallExpression.Arguments[0], context);
                    return SqlFunctionCallScalarExpression.CreateBuiltin(SqlFunctionCallScalarExpression.Identifiers.Length, str);
                }

                return null;
            }
        }

        private class StringVisitTrimStart : CosmosBuiltinFunctionVisitor
        {
            public StringVisitTrimStart()
                : base(SqlFunctionCallScalarExpression.Names.Ltrim, argumentLists: null)
            {
            }

            protected override SqlScalarExpression VisitImplicit(MethodCallExpression methodCallExpression, TranslationContext context)
            {
                bool validInNet;
                bool validInNetCore;

                if (methodCallExpression.Arguments.Count == 1 &&
                    methodCallExpression.Arguments[0].NodeType == ExpressionType.Constant &&
                    methodCallExpression.Arguments[0].Type == typeof(char[]))
                {
                    char[] argumentsExpressions = (char[])((ConstantExpression)methodCallExpression.Arguments[0]).Value;
                    if (argumentsExpressions.Length == 0)
                    {
                        validInNet = true;
                        validInNetCore = false;
                    }
                    else
                    {
                        validInNet = false;
                        validInNetCore = false;
                    }
                }
                else if (methodCallExpression.Arguments.Count == 0)
                {
                    validInNet = false;
                    validInNetCore = true;
                }
                else
                {
                    validInNet = false;
                    validInNetCore = false;
                }

                if (validInNet || validInNetCore)
                {
                    SqlScalarExpression str = ExpressionToSql.VisitScalarExpression(methodCallExpression.Object, context);
                    return SqlFunctionCallScalarExpression.CreateBuiltin(SqlFunctionCallScalarExpression.Identifiers.Ltrim, str);
                }

                return null;
            }
        }

        private class StringVisitReverse : CosmosBuiltinFunctionVisitor
        {
            public StringVisitReverse()
                : base(SqlFunctionCallScalarExpression.Names.Reverse, argumentLists: null)
            {
            }

            protected override SqlScalarExpression VisitImplicit(MethodCallExpression methodCallExpression, TranslationContext context)
            {
                if (methodCallExpression.Arguments.Count == 1)
                {
                    SqlScalarExpression str = ExpressionToSql.VisitNonSubqueryScalarExpression(methodCallExpression.Arguments[0], context);
                    return SqlFunctionCallScalarExpression.CreateBuiltin(SqlFunctionCallScalarExpression.Identifiers.Reverse, str);
                }

                return null;
            }
        }

        private class StringVisitTrimEnd : CosmosBuiltinFunctionVisitor
        {
            public StringVisitTrimEnd()
                : base(SqlFunctionCallScalarExpression.Names.Rtrim, argumentLists: null)
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
                    return SqlFunctionCallScalarExpression.CreateBuiltin(SqlFunctionCallScalarExpression.Identifiers.Rtrim, str);
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

                    return SqlFunctionCallScalarExpression.CreateBuiltin(SqlFunctionCallScalarExpression.Identifiers.Substring, arguments);
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

        public static SqlScalarExpression Visit(MethodCallExpression methodCallExpression, TranslationContext context)
        {
            if (StringBuiltinFunctionDefinitions.TryGetValue(methodCallExpression.Method.Name, out BuiltinFunctionVisitor visitor))
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
