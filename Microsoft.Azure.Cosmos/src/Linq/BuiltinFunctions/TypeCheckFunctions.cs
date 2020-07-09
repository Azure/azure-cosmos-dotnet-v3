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
    using Microsoft.Azure.Cosmos.SqlObjects;

    internal static class TypeCheckFunctions
    {
        private static readonly ImmutableDictionary<string, BuiltinFunctionVisitor> TypeCheckFunctionsDefinitions = new Dictionary<string, BuiltinFunctionVisitor>
        {
            {
                nameof(CosmosLinqExtensions.IsDefined),
                new CosmosBuiltinFunctionVisitor(
                    SqlFunctionCallScalarExpression.Names.IsDefined,
                    new List<ImmutableArray<Type>>()
                    {
                        new Type[]{typeof(object)}.ToImmutableArray(),
                    }.ToImmutableArray())
            },
            {
                nameof(CosmosLinqExtensions.IsNull),
                new CosmosBuiltinFunctionVisitor(
                    SqlFunctionCallScalarExpression.Names.IsNull,
                    new List<ImmutableArray<Type>>()
                    {
                        new Type[]{typeof(object)}.ToImmutableArray(),
                    }.ToImmutableArray())
            },
            {
                nameof(CosmosLinqExtensions.IsPrimitive),
                new CosmosBuiltinFunctionVisitor(
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
