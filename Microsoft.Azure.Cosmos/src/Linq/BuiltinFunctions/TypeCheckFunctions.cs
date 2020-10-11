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

    internal static class TypeCheckFunctions
    {
        private static Dictionary<string, BuiltinFunctionVisitor> TypeCheckFunctionsDefinitions { get; set; }

        static TypeCheckFunctions()
        {
            TypeCheckFunctionsDefinitions = new Dictionary<string, BuiltinFunctionVisitor>
            {
                {
                    "IsDefined",
                    new SqlBuiltinFunctionVisitor("IS_DEFINED",
                    true,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(object)},
                    })
                },
                {
                    "IsNull",
                    new SqlBuiltinFunctionVisitor("IS_NULL",
                    true,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(object)},
                    })
                },
                {
                    "IsPrimitive",
                    new SqlBuiltinFunctionVisitor("IS_PRIMITIVE",
                    true,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(object)},
                    })
                }
            };
        }

        public static SqlScalarExpression Visit(MethodCallExpression methodCallExpression, TranslationContext context)
        {
            return TypeCheckFunctionsDefinitions.TryGetValue(methodCallExpression.Method.Name, out BuiltinFunctionVisitor visitor)
                ? visitor.Visit(methodCallExpression, context)
                : throw new DocumentQueryException(string.Format(CultureInfo.CurrentCulture, ClientResources.MethodNotSupported, methodCallExpression.Method.Name));
        }
    }
}
