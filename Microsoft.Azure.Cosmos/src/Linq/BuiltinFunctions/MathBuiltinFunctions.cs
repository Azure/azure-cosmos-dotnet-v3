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
            MathBuiltinFunctionDefinitions = new Dictionary<string, BuiltinFunctionVisitor>();

            MathBuiltinFunctionDefinitions.Add("Abs",
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
                    }));

            MathBuiltinFunctionDefinitions.Add("Acos",
                new SqlBuiltinFunctionVisitor("ACOS",
                    true,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(double)}
                    }));

            MathBuiltinFunctionDefinitions.Add("Asin",
                new SqlBuiltinFunctionVisitor("ASIN",
                    true,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(double)}
                    }));

            MathBuiltinFunctionDefinitions.Add("Atan",
                new SqlBuiltinFunctionVisitor("ATAN",
                    true,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(double)}
                    }));

            MathBuiltinFunctionDefinitions.Add("Atan2",
                new SqlBuiltinFunctionVisitor("ATN2",
                    true,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(double), typeof(double)}
                    }));

            MathBuiltinFunctionDefinitions.Add("Ceiling",
                new SqlBuiltinFunctionVisitor("CEILING",
                    true,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(decimal)},
                        new Type[]{typeof(double)}
                    }));

            MathBuiltinFunctionDefinitions.Add("Cos",
                new SqlBuiltinFunctionVisitor("COS",
                    true,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(double)}
                    }));

            MathBuiltinFunctionDefinitions.Add("Exp",
                new SqlBuiltinFunctionVisitor("EXP",
                    true,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(double)}
                    }));

            MathBuiltinFunctionDefinitions.Add("Floor",
                new SqlBuiltinFunctionVisitor("FLOOR",
                    true,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(decimal)},
                        new Type[]{typeof(double)}
                    }));

            MathBuiltinFunctionDefinitions.Add("Log",
                new SqlBuiltinFunctionVisitor("LOG",
                    true,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(double)},
                        new Type[]{typeof(double), typeof(double)}
                    }));

            MathBuiltinFunctionDefinitions.Add("Log10",
                new SqlBuiltinFunctionVisitor("LOG10",
                    true,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(double)}
                    }));

            MathBuiltinFunctionDefinitions.Add("Pow",
                new SqlBuiltinFunctionVisitor("POWER",
                    true,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(double), typeof(double)}
                    }));

            MathBuiltinFunctionDefinitions.Add("Round",
                new SqlBuiltinFunctionVisitor("ROUND",
                    true,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(decimal)},
                        new Type[]{typeof(double)}
                    }));

            MathBuiltinFunctionDefinitions.Add("Sign",
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
                    }));

            MathBuiltinFunctionDefinitions.Add("Sin",
                new SqlBuiltinFunctionVisitor("SIN",
                    true,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(double)}
                    }));

            MathBuiltinFunctionDefinitions.Add("Sqrt",
                new SqlBuiltinFunctionVisitor("SQRT",
                    true,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(double)}
                    }));

            MathBuiltinFunctionDefinitions.Add("Tan",
                new SqlBuiltinFunctionVisitor("TAN",
                    true,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(double)}
                    }));

            MathBuiltinFunctionDefinitions.Add("Truncate",
                new SqlBuiltinFunctionVisitor("TRUNC",
                    true,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(decimal)},
                        new Type[]{typeof(double)}
                    }));
        }

        public static SqlScalarExpression Visit(MethodCallExpression methodCallExpression, TranslationContext context)
        {
            if (MathBuiltinFunctionDefinitions.TryGetValue(methodCallExpression.Method.Name, out BuiltinFunctionVisitor visitor))
            {
                return visitor.Visit(methodCallExpression, context);
            }

            throw new DocumentQueryException(string.Format(CultureInfo.CurrentCulture, ClientResources.MethodNotSupported, methodCallExpression.Method.Name));
        }
    }
}
