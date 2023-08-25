//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq.Expressions;
    using Microsoft.Azure.Cosmos.SqlObjects;

    internal static class MathBuiltinFunctions
    {
        private static Dictionary<string, BuiltinFunctionVisitor> MathBuiltinFunctionDefinitions { get; set; }

        static MathBuiltinFunctions()
        {
            MathBuiltinFunctionDefinitions = new Dictionary<string, BuiltinFunctionVisitor>
            {
                {
                    "Abs",
                    new SqlBuiltinFunctionVisitor("ABS",
                    true,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(decimal)},
                        new Type[]{typeof(double)},
                        new Type[]{typeof(float)},
                        new Type[]{typeof(int)},
                        new Type[]{typeof(long)},
                        new Type[]{typeof(sbyte)},
                        new Type[]{typeof(short)},
                    })
                },
                {
                    "Acos",
                    new SqlBuiltinFunctionVisitor("ACOS",
                    true,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(double)}
                    })
                },
                {
                    "Asin",
                    new SqlBuiltinFunctionVisitor("ASIN",
                    true,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(double)}
                    })
                },
                {
                    "Atan",
                    new SqlBuiltinFunctionVisitor("ATAN",
                    true,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(double)}
                    })
                },
                {
                    "Atan2",
                    new SqlBuiltinFunctionVisitor("ATN2",
                    true,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(double), typeof(double)}
                    })
                },
                {
                    "Ceiling",
                    new SqlBuiltinFunctionVisitor("CEILING",
                    true,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(decimal)},
                        new Type[]{typeof(double)}
                    })
                },
                {
                    "Cos",
                    new SqlBuiltinFunctionVisitor("COS",
                    true,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(double)}
                    })
                },
                {
                    "Exp",
                    new SqlBuiltinFunctionVisitor("EXP",
                    true,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(double)}
                    })
                },
                {
                    "Floor",
                    new SqlBuiltinFunctionVisitor("FLOOR",
                    true,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(decimal)},
                        new Type[]{typeof(double)}
                    })
                },
                {
                    "Log",
                    new SqlBuiltinFunctionVisitor("LOG",
                    true,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(double)},
                        new Type[]{typeof(double), typeof(double)}
                    })
                },
                {
                    "Log10",
                    new SqlBuiltinFunctionVisitor("LOG10",
                    true,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(double)}
                    })
                },
                {
                    "Pow",
                    new SqlBuiltinFunctionVisitor("POWER",
                    true,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(double), typeof(double)}
                    })
                },
                {
                    "Round",
                    new SqlBuiltinFunctionVisitor("ROUND",
                    true,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(decimal)},
                        new Type[]{typeof(double)}
                    })
                },
                {
                    "Sign",
                    new SqlBuiltinFunctionVisitor("SIGN",
                    true,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(decimal)},
                        new Type[]{typeof(double)},
                        new Type[]{typeof(float)},
                        new Type[]{typeof(int)},
                        new Type[]{typeof(long)},
                        new Type[]{typeof(sbyte)},
                        new Type[]{typeof(short)},
                    })
                },
                {
                    "Sin",
                    new SqlBuiltinFunctionVisitor("SIN",
                    true,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(double)}
                    })
                },
                {
                    "Sqrt",
                    new SqlBuiltinFunctionVisitor("SQRT",
                    true,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(double)}
                    })
                },
                {
                    "Tan",
                    new SqlBuiltinFunctionVisitor("TAN",
                    true,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(double)}
                    })
                },
                {
                    "Truncate",
                    new SqlBuiltinFunctionVisitor("TRUNC",
                    true,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(decimal)},
                        new Type[]{typeof(double)}
                    })
                }
            };
        }

        public static SqlScalarExpression Visit(MethodCallExpression methodCallExpression, TranslationContext context)
        {
            return MathBuiltinFunctionDefinitions.TryGetValue(methodCallExpression.Method.Name, out BuiltinFunctionVisitor visitor)
                ? visitor.Visit(methodCallExpression, context)
                : throw new DocumentQueryException(string.Format(CultureInfo.CurrentCulture, ClientResources.MethodNotSupported, methodCallExpression.Method.Name));
        }
    }
}
