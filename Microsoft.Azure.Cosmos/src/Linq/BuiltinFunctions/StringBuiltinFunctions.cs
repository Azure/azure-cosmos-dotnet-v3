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
                    && methodCallExpression.Arguments[0] is NewArrayExpression newArrayExpression)
                {
                    ReadOnlyCollection<Expression> argumentsExpressions = newArrayExpression.Expressions;
                    List<SqlScalarExpression> arguments = new List<SqlScalarExpression>();
                    foreach (Expression argument in argumentsExpressions)
                    {
                        arguments.Add(ExpressionToSql.VisitScalarExpression(argument, context));
                    }

                    return SqlFunctionCallScalarExpression.CreateBuiltin("CONCAT", arguments.ToImmutableArray());
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
                        new Type[]{typeof(string)},
                        new Type[]{typeof(char)}
                    })
            {
            }

            protected override SqlScalarExpression VisitImplicit(MethodCallExpression methodCallExpression, TranslationContext context)
            {
                if (methodCallExpression.Arguments.Count == 2)
                {
                    SqlScalarExpression haystack = ExpressionToSql.VisitScalarExpression(methodCallExpression.Object, context);
                    SqlScalarExpression needle = ExpressionToSql.VisitScalarExpression(methodCallExpression.Arguments[0], context);
                    SqlScalarExpression caseInsensitive = SqlStringWithComparisonVisitor.GetCaseInsensitiveExpression(methodCallExpression.Arguments[1]);
                    return SqlFunctionCallScalarExpression.CreateBuiltin("CONTAINS", haystack, needle, caseInsensitive);
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

        private sealed class SqlStringWithComparisonVisitor : BuiltinFunctionVisitor
        {
            private static readonly HashSet<StringComparison> IgnoreCaseComparisons = new HashSet<StringComparison>(new[]
            {
                StringComparison.CurrentCultureIgnoreCase,
                StringComparison.InvariantCultureIgnoreCase,
                StringComparison.OrdinalIgnoreCase
            });

            public string SqlName { get; }

            public SqlStringWithComparisonVisitor(string sqlName)
            {
                this.SqlName = sqlName ?? throw new ArgumentNullException(nameof(sqlName));
            }

            public static SqlScalarExpression GetCaseInsensitiveExpression(Expression expression)
            {
                if (expression is ConstantExpression inputExpression
                    && inputExpression.Value is StringComparison comparisonValue
                    && IgnoreCaseComparisons.Contains(comparisonValue))
                {
                    SqlBooleanLiteral literal = SqlBooleanLiteral.Create(true);
                    return SqlLiteralScalarExpression.Create(literal);
                }

                return null;
            }

            protected override SqlScalarExpression VisitImplicit(MethodCallExpression methodCallExpression, TranslationContext context)
            {
                int argumentCount = methodCallExpression.Arguments.Count;
                if (argumentCount == 0 || argumentCount > 2)
                {
                    return null;
                }

                List<SqlScalarExpression> arguments = new List<SqlScalarExpression>
                {
                    ExpressionToSql.VisitNonSubqueryScalarExpression(methodCallExpression.Object, context),
                    ExpressionToSql.VisitNonSubqueryScalarExpression(methodCallExpression.Arguments[0], context)
                };

                if (argumentCount > 1)
                {
                    arguments.Add(GetCaseInsensitiveExpression(methodCallExpression.Arguments[1]));
                }

                return SqlFunctionCallScalarExpression.CreateBuiltin(this.SqlName, arguments.ToArray());
            }

            protected override SqlScalarExpression VisitExplicit(MethodCallExpression methodCallExpression, TranslationContext context)
            {
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

                if (methodCallExpression.Arguments.Count == 2)
                {
                    SqlScalarExpression left = ExpressionToSql.VisitScalarExpression(methodCallExpression.Object, context);
                    SqlScalarExpression right = ExpressionToSql.VisitScalarExpression(methodCallExpression.Arguments[0], context);
                    SqlScalarExpression caseInsensitive = SqlStringWithComparisonVisitor.GetCaseInsensitiveExpression(methodCallExpression.Arguments[1]);

                    return SqlFunctionCallScalarExpression.CreateBuiltin(
                        SqlFunctionCallScalarExpression.Names.StringEquals,
                        left,
                        right,
                        caseInsensitive);
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
            StringBuiltinFunctionDefinitions = new Dictionary<string, BuiltinFunctionVisitor>
            {
                {
                    "Concat",
                    new StringVisitConcat()
                },
                {
                    "Contains",
                    new StringVisitContains()
                },
                {
                    "EndsWith",
                    new SqlStringWithComparisonVisitor("ENDSWITH")
                },
                {
                    "IndexOf",
                    new SqlBuiltinFunctionVisitor("INDEX_OF",
                    false,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(char)},
                        new Type[]{typeof(string)},
                        new Type[]{typeof(char), typeof(int)},
                        new Type[]{typeof(string), typeof(int)},
                    })
                },
                {
                    "Count",
                    new StringVisitCount()
                },
                {
                    "ToLower",
                    new SqlBuiltinFunctionVisitor("LOWER",
                    false,
                    new List<Type[]>()
                    {
                        new Type[]{}
                    })
                },
                {
                    "TrimStart",
                    new StringVisitTrimStart()
                },
                {
                    "Replace",
                    new SqlBuiltinFunctionVisitor("REPLACE",
                    false,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(char), typeof(char)},
                        new Type[]{typeof(string), typeof(string)}
                    })
                },
                {
                    "Reverse",
                    new StringVisitReverse()
                },
                {
                    "TrimEnd",
                    new StringVisitTrimEnd()
                },
                {
                    "StartsWith",
                    new SqlStringWithComparisonVisitor("STARTSWITH")
                },
                {
                    "Substring",
                    new SqlBuiltinFunctionVisitor("SUBSTRING",
                    false,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(int), typeof(int)}
                    })
                },
                {
                    "ToUpper",
                    new SqlBuiltinFunctionVisitor("UPPER",
                    false,
                    new List<Type[]>()
                    {
                        new Type[]{}
                    })
                },
                {
                    "get_Chars",
                    new StringGetCharsVisitor()
                },
                {
                    "Equals",
                    new StringEqualsVisitor()
                }
            };
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
