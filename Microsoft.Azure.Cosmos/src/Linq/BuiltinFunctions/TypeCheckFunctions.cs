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

    internal static class TypeCheckFunctions
    {
        private static readonly ImmutableDictionary<string, BuiltinFunctionVisitor> TypeCheckFunctionsDefinitions = new Dictionary<string, BuiltinFunctionVisitor>
        {
            {
                nameof(CosmosLinqExtensions.IsDefined),
                new SqlBuiltinFunctionVisitor(
                    SqlFunctionCallScalarExpression.Names.IsDefined,
                    new List<ImmutableArray<Type>>()
                    {
                        new Type[]{typeof(object)}.ToImmutableArray(),
                    }.ToImmutableArray())
            },
            {
                nameof(CosmosLinqExtensions.IsNull),
                new SqlBuiltinFunctionVisitor(
                    SqlFunctionCallScalarExpression.Names.IsNull,
                    new List<ImmutableArray<Type>>()
                    {
                        new Type[]{typeof(object)}.ToImmutableArray(),
                    }.ToImmutableArray())
            },
            {
                nameof(CosmosLinqExtensions.IsPrimitive),
                new SqlBuiltinFunctionVisitor(
                    SqlFunctionCallScalarExpression.Names.IsPrimitive,
                    new List<ImmutableArray<Type>>()
                    {
                        new Type[]{typeof(object)}.ToImmutableArray(),
                    }.ToImmutableArray())
            }
        }.ToImmutableDictionary();

        public static SqlScalarExpression Visit(MethodCallExpression methodCallExpression, TranslationContext context)
        {
            if (TypeCheckFunctionsDefinitions.TryGetValue(methodCallExpression.Method.Name, out BuiltinFunctionVisitor visitor))
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
