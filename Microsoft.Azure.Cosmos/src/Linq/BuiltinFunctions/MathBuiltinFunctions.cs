//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Globalization;
    using System.Linq.Expressions;
    using Microsoft.Azure.Cosmos.Sql;

    internal static class MathBuiltinFunctions
    {
        private static readonly ImmutableDictionary<string, BuiltinFunctionVisitor> MathBuiltinFunctionDefinitions = new Dictionary<string, BuiltinFunctionVisitor>
        {
            {
                nameof(Math.Abs),
                new CosmosBuiltinFunctionVisitor(
                    SqlFunctionCallScalarExpression.Names.Abs,
                    new ImmutableArray<Type>[]
                    {
                        new Type[]{typeof(decimal)}.ToImmutableArray(),
                        new Type[]{typeof(double)}.ToImmutableArray(),
                        new Type[]{typeof(float)}.ToImmutableArray(),
                        new Type[]{typeof(int)}.ToImmutableArray(),
                        new Type[]{typeof(long)}.ToImmutableArray(),
                        new Type[]{typeof(sbyte)}.ToImmutableArray(),
                        new Type[]{typeof(short)}.ToImmutableArray(),
                    }.ToImmutableArray())
            },
            {
                nameof(Math.Acos),
                new CosmosBuiltinFunctionVisitor(
                    SqlFunctionCallScalarExpression.Names.Acos,
                    new ImmutableArray<Type>[]
                    {
                        new Type[]{typeof(double)}.ToImmutableArray(),
                    }.ToImmutableArray())
            },
            {
                nameof(Math.Asin),
                new CosmosBuiltinFunctionVisitor(
                    SqlFunctionCallScalarExpression.Names.Asin,
                    new ImmutableArray<Type>[]
                    {
                        new Type[]{typeof(double)}.ToImmutableArray(),
                    }.ToImmutableArray())
            },
            {
                nameof(Math.Atan),
                new CosmosBuiltinFunctionVisitor(
                    SqlFunctionCallScalarExpression.Names.Atan,
                    new ImmutableArray<Type>[]
                    {
                        new Type[]{typeof(double)}.ToImmutableArray(),
                    }.ToImmutableArray())
            },
            {
                nameof(Math.Atan2),
                new CosmosBuiltinFunctionVisitor(
                    SqlFunctionCallScalarExpression.Names.Atn2,
                    new ImmutableArray<Type>[]
                    {
                        new Type[]{typeof(double)}.ToImmutableArray(),
                    }.ToImmutableArray())
            },
            {
                nameof(Math.Ceiling),
                new CosmosBuiltinFunctionVisitor(
                    SqlFunctionCallScalarExpression.Names.Ceiling,
                    new ImmutableArray<Type>[]
                    {
                        new Type[]{typeof(double)}.ToImmutableArray(),
                    }.ToImmutableArray())
            },
            {
                nameof(Math.Cos),
                new CosmosBuiltinFunctionVisitor(
                    SqlFunctionCallScalarExpression.Names.Cos,
                    new ImmutableArray<Type>[]
                    {
                        new Type[]{typeof(double)}.ToImmutableArray(),
                    }.ToImmutableArray())
            },
            {
                nameof(Math.Exp),
                new CosmosBuiltinFunctionVisitor(
                    SqlFunctionCallScalarExpression.Names.Exp,
                    new ImmutableArray<Type>[]
                    {
                        new Type[]{typeof(double)}.ToImmutableArray(),
                    }.ToImmutableArray())
            },
            {
                nameof(Math.Floor),
                new CosmosBuiltinFunctionVisitor(
                    SqlFunctionCallScalarExpression.Names.Floor,
                    new ImmutableArray<Type>[]
                    {
                        new Type[]{typeof(decimal)}.ToImmutableArray(),
                        new Type[]{typeof(double)}.ToImmutableArray(),
                    }.ToImmutableArray())
            },
            {
                nameof(Math.Log),
                new CosmosBuiltinFunctionVisitor(
                    SqlFunctionCallScalarExpression.Names.Log,
                    new ImmutableArray<Type>[]
                    {
                        new Type[]{typeof(double)}.ToImmutableArray(),
                        new Type[]{typeof(double), typeof(double)}.ToImmutableArray(),
                    }.ToImmutableArray())
            },
            {
                nameof(Math.Log10),
                new CosmosBuiltinFunctionVisitor(
                    SqlFunctionCallScalarExpression.Names.Log10,
                    new ImmutableArray<Type>[]
                    {
                        new Type[]{typeof(double)}.ToImmutableArray(),
                    }.ToImmutableArray())
            },
            {
                nameof(Math.Pow),
                new CosmosBuiltinFunctionVisitor(
                    SqlFunctionCallScalarExpression.Names.Power,
                    new ImmutableArray<Type>[]
                    {
                        new Type[]{typeof(double), typeof(double)}.ToImmutableArray(),
                    }.ToImmutableArray())
            },
            {
                nameof(Math.Round),
                new CosmosBuiltinFunctionVisitor(
                    SqlFunctionCallScalarExpression.Names.Round,
                    new ImmutableArray<Type>[]
                    {
                        new Type[]{typeof(decimal)}.ToImmutableArray(),
                        new Type[]{typeof(double)}.ToImmutableArray(),
                    }.ToImmutableArray())
            },
            {
                nameof(Math.Sign),
                new CosmosBuiltinFunctionVisitor(
                    SqlFunctionCallScalarExpression.Names.Sign,
                    new ImmutableArray<Type>[]
                    {
                        new Type[]{typeof(decimal)}.ToImmutableArray(),
                        new Type[]{typeof(double)}.ToImmutableArray(),
                        new Type[]{typeof(float)}.ToImmutableArray(),
                        new Type[]{typeof(int)}.ToImmutableArray(),
                        new Type[]{typeof(long)}.ToImmutableArray(),
                        new Type[]{typeof(sbyte)}.ToImmutableArray(),
                        new Type[]{typeof(short)}.ToImmutableArray(),
                    }.ToImmutableArray())
            },
            {
                nameof(Math.Sin),
                new CosmosBuiltinFunctionVisitor(
                    SqlFunctionCallScalarExpression.Names.Sin,
                    new ImmutableArray<Type>[]
                    {
                        new Type[]{typeof(double)}.ToImmutableArray(),
                    }.ToImmutableArray())
            },
            {
                nameof(Math.Sqrt),
                new CosmosBuiltinFunctionVisitor(
                    SqlFunctionCallScalarExpression.Names.Sin,
                    new ImmutableArray<Type>[]
                    {
                        new Type[]{typeof(double)}.ToImmutableArray(),
                    }.ToImmutableArray())
            },
            {
                nameof(Math.Tan),
                new CosmosBuiltinFunctionVisitor(
                    SqlFunctionCallScalarExpression.Names.Tan,
                    new ImmutableArray<Type>[]
                    {
                        new Type[]{typeof(double)}.ToImmutableArray(),
                    }.ToImmutableArray())
            },
            {
                nameof(Math.Truncate),
                new CosmosBuiltinFunctionVisitor(
                    SqlFunctionCallScalarExpression.Names.Trunc,
                    new ImmutableArray<Type>[]
                    {
                        new Type[]{typeof(decimal)}.ToImmutableArray(),
                        new Type[]{typeof(double)}.ToImmutableArray(),
                    }.ToImmutableArray())
            },
        }.ToImmutableDictionary();

        public static SqlScalarExpression Visit(MethodCallExpression methodCallExpression, TranslationContext context)
        {
            if (!MathBuiltinFunctionDefinitions.TryGetValue(methodCallExpression.Method.Name, out BuiltinFunctionVisitor visitor))
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
