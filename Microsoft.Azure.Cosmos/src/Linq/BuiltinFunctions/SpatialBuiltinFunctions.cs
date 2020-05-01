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
    using Microsoft.Azure.Cosmos.Spatial;
    using Microsoft.Azure.Cosmos.Sql;

    internal static class SpatialBuiltinFunctions
    {
        private static readonly ImmutableDictionary<string, BuiltinFunctionVisitor> SpatialBuiltinFunctionDefinitions = new Dictionary<string, BuiltinFunctionVisitor>
        {
            {
                nameof(Geometry.Distance),
                new SqlBuiltinFunctionVisitor(
                    SqlFunctionCallScalarExpression.Names.StDistance,
                    new ImmutableArray<Type>[]
                    {
                        new Type[]{typeof(Geometry)}.ToImmutableArray(),
                    }.ToImmutableArray())
            },
            {
                nameof(Geometry.Within),
                new SqlBuiltinFunctionVisitor(
                    SqlFunctionCallScalarExpression.Names.StWithin,
                    new ImmutableArray<Type>[]
                    {
                        new Type[]{typeof(Geometry)}.ToImmutableArray(),
                    }.ToImmutableArray())
            },
            {
                nameof(Geometry.IsValidDetailed),
                new SqlBuiltinFunctionVisitor(
                    SqlFunctionCallScalarExpression.Names.StIsvaliddetailed,
                    new ImmutableArray<Type>[]{}.ToImmutableArray())
            },
            {
                nameof(Geometry.IsValid),
                new SqlBuiltinFunctionVisitor(
                    SqlFunctionCallScalarExpression.Names.StIsvalid,
                    new ImmutableArray<Type>[]{}.ToImmutableArray())
            },
            {
                nameof(Geometry.Intersects),
                new SqlBuiltinFunctionVisitor(
                    SqlFunctionCallScalarExpression.Names.StIntersects,
                    new ImmutableArray<Type>[]
                    {
                        new Type[]{typeof(Geometry)}.ToImmutableArray(),
                    }.ToImmutableArray())
            }
        }.ToImmutableDictionary();

        public static SqlScalarExpression Visit(MethodCallExpression methodCallExpression, TranslationContext context)
        {
            if (!SpatialBuiltinFunctionDefinitions.TryGetValue(methodCallExpression.Method.Name, out BuiltinFunctionVisitor visitor))
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
